﻿//
// Project under MIT License https://github.com/TylkoDemon/dear-imgui-unity
// 
// A DearImgui implementation for Unity for URP, HDRP and Builtin that requires minimal setup.
// Based on (forked from)https://github.com/realgamessoftware/dear-imgui-unity
//  with uses ImGuiNET(https://github.com/ImGuiNET/ImGui.NET) and cimgui (https://github.com/cimgui/cimgui)
//
// For HDRP support, this package utilizes HDRP-UI-Camera-Stacking(https://github.com/alelievr/HDRP-UI-Camera-Stacking)
//  to draw DearImgui on top of Unity UI.
// HDRP support is limited for UI to be drawn in Camera or World Space.
// Screen-Space Canvas is not yet supported.
//

#if IMGUI_DEBUG || UNITY_EDITOR
using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace ImGuiNET.Unity
{
    // This component is responsible for setting up ImGui for use in Unity.
    // It holds the necessary context and sets it up before any operation is done to ImGui.
    // (e.g. set the context, texture and font managers before calling Layout)

    /// <summary>
    ///     Dear ImGui integration into Unity
    /// </summary>
    public sealed class DearImGui : MonoBehaviour
    {
        private ImGuiUnityContext _context;
        private IImGuiRenderer _renderer;
        private IImGuiPlatform _platform;
        private SRPType _srpType;
        
        public static CommandBuffer Buffer { get; private set; }
        
        [Header("Systep")]
        [FormerlySerializedAs("camera")]
        [SerializeField] public new Camera camera = default!;
        [FormerlySerializedAs("_renderFeature")] 
        [SerializeField] private RenderImGuiFeature renderFeature = default!;
        [FormerlySerializedAs("_rendererType")]
        [SerializeField] private RenderUtils.RenderType rendererType = RenderUtils.RenderType.Mesh;
        [FormerlySerializedAs("_platformType")]
        [SerializeField] private Platform.Type platformType = Platform.Type.InputManager;

        [Header("Configuration")]
        [FormerlySerializedAs("_initialConfiguration")]
        [SerializeField] private IOConfig initialConfiguration = default!;
        [FormerlySerializedAs("_fontAtlasConfiguration")]
        [SerializeField] private FontAtlasConfigAsset fontAtlasConfiguration = null!;
        [FormerlySerializedAs("_iniSettings")]
        [SerializeField] private IniSettingsAsset iniSettings = null!; // null: uses default imgui.ini file
        [FormerlySerializedAs("_enableDocking")] 
        [SerializeField] private bool enableDocking = true;
        
        [Header("Customization")]
        [FormerlySerializedAs("_shaders")]
        [SerializeField] private ShaderResourcesAsset shaders = null!;
        [FormerlySerializedAs("_style")] 
        [SerializeField] private StyleAsset style = null!;
        [FormerlySerializedAs("_cursorShapes")] 
        [SerializeField] private CursorShapesAsset cursorShapes = null!;

        private const string CommandBufferTag = "DearImGui";
        private static readonly ProfilerMarker s_prepareFramePerfMarker = new("DearImGui.PrepareFrame");
        private static readonly ProfilerMarker s_layoutPerfMarker = new("DearImGui.Layout");
        private static readonly ProfilerMarker s_drawListPerfMarker = new("DearImGui.RenderDrawLists");

        private void Awake()
        {
            if (Instance != null)
            {
                Debug.LogWarning($"A duplicate instance of {nameof(DearImGui)} was found.");
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            _context = ImGuiUn.CreateUnityContext();
            
            DontDestroyOnLoad(gameObject);

#if USING_HDRP
            // We use third-party library to inject our Dear Imgui content after rendering HDRP is done.
            // NOTE: The nature of Imgui is to draw it on top of anything else.
            //       In unity, drawing on top of built in unity UI is challenging.
            //       Current approach renders our Dear Imgui after UI is done rendering, but your canvases can't be Screen-Space.
            HDCameraUI.OnAfterUIRendering += OnAfterUI;
#endif
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            ImGuiUn.DestroyUnityContext(_context);
            Instance = null;

#if USING_HDRP
            HDCameraUI.OnAfterUIRendering -= OnAfterUI;
#endif
        }

#if USING_HDRP
        private void OnAfterUI(ScriptableRenderContext ctx)
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                return;
            }
            
            var cb = Buffer;
            if (cb == null)
                return;
            
            ctx.ExecuteCommandBuffer(cb);
            ctx.Submit();
        }
#endif

        private void OnEnable()
        {
            if (Instance != this)
                return;
            
            Debug.Log("Dear ImGui is enabled", this);
            
            // Discover SRP type.
            _srpType = RenderUtils.GetSRP();
            
            // Setup command buffer.
            Buffer = RenderUtils.GetCommandBuffer(CommandBufferTag);
            switch (_srpType)
            {
                case SRPType.BuiltIn:
                    Assert.IsNotNull(camera, "camera != null");
                    camera.AddCommandBuffer(CameraEvent.AfterEverything, Buffer);
                    break;
                case SRPType.URP:
                    Assert.IsNotNull(renderFeature, "renderFeature != null");
                    renderFeature.commandBuffer = Buffer;
                    break;
                case SRPType.HDRP:
                    // NOTE: HDRP consumes Buffer locally via OnAfterUI event. 
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            ImGuiUn.SetUnityContext(_context);
            ImGuiIOPtr io = ImGui.GetIO();

            // Enable docking.
            if (enableDocking)
                io.ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            
            initialConfiguration.ApplyTo(io);
            if (style != null)
                style.ApplyTo(ImGui.GetStyle());

            _context.textures.BuildFontAtlas(io, fontAtlasConfiguration);
            _context.textures.Initialize(io);

            SetPlatform(Platform.Create(platformType, cursorShapes, iniSettings), io);
            SetRenderer(RenderUtils.Create(rendererType, shaders, _context.textures), io);
            if (_platform == null) Fail(nameof(_platform));
            if (_renderer == null) Fail(nameof(_renderer));

            void Fail(string reason)
            {
                OnDisable();
                enabled = false;
                throw new Exception($"Failed to start: {reason}");
            }
        }

        private void OnDisable()
        {
            if (Instance != this)
                return;
            
            Debug.Log("Dear ImGui is disabled");
            
            ImGuiUn.SetUnityContext(_context);
            ImGuiIOPtr io = ImGui.GetIO();

            SetRenderer(null, io);
            SetPlatform(null, io);

            ImGuiUn.SetUnityContext(null);

            _context.textures.Shutdown();
            _context.textures.DestroyFontAtlas(io);

            switch (_srpType)
            {
                case SRPType.BuiltIn:
                    if (camera != null)
                        camera.RemoveCommandBuffer(CameraEvent.AfterEverything, Buffer);
                    break;
                case SRPType.URP:
                    if (renderFeature != null)
                        renderFeature.commandBuffer = null;
                    break;
                case SRPType.HDRP:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            if (Buffer != null)
                RenderUtils.ReleaseCommandBuffer(Buffer);
            Buffer = null;
        }

        private void OnApplicationQuit()
        {
            ImGuiUn.Reset();
        }

        private void Reset()
        {
            initialConfiguration.SetDefaults();
        }

        public void Reload()
        {
            OnDisable();
            OnEnable();
        }
        
        private void Update()
        {
            if (Instance != this)
                return;
            
            ImGuiUn.SetUnityContext(_context);
            ImGuiIOPtr io = ImGui.GetIO();
            
            s_prepareFramePerfMarker.Begin(this);
            _context.textures.PrepareFrame(io);
            _platform.PrepareFrame(io, camera.pixelRect);
            ImGui.NewFrame();
            s_prepareFramePerfMarker.End();

            s_layoutPerfMarker.Begin(this);
            try
            {
                ImGuiUn.DoLayout();
            }
            finally
            {
                ImGui.Render();
                s_layoutPerfMarker.End();
            }

            s_drawListPerfMarker.Begin(this);
            Buffer.Clear();
            _renderer.RenderDrawLists(Buffer, ImGui.GetDrawData());
            s_drawListPerfMarker.End();
        }

        private void SetRenderer(IImGuiRenderer renderer, ImGuiIOPtr io)
        {
            _renderer?.Shutdown(io);
            _renderer = renderer;
            _renderer?.Initialize(io);
        }

        private void SetPlatform(IImGuiPlatform platform, ImGuiIOPtr io)
        {
            _platform?.Shutdown(io);
            _platform = platform;
            _platform?.Initialize(io);
        }
        
        public static DearImGui Instance { get; private set; }
    }
}
#endif