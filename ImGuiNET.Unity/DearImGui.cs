//
// Project under MIT License https://github.com/TylkoDemon/dear-imgui-unity
// 
// A DearImgui implementation for Unity for URP, HDRP and Builtin that requires minimal setup.
// Based on (forked from)https://github.com/realgamessoftware/dear-imgui-unity
//  with uses ImGuiNET(https://github.com/ImGuiNET/ImGui.NET) and cimgui (https://github.com/cimgui/cimgui)
//
// For HDRP support, this package utilizes HDRP-UI-Camera-Stacking(https://github.com/alelievr/HDRP-UI-Camera-Stacking)
//  to draw DearImgui on top of Unity UI.
// HDRP support is limited for UI to be drawn in Camera or World Space.
// On HDRP Screen-Space Canvas is not yet supported.
//
// In URP and Builtin, to draw DearImgui on top of Unity UI, you need to use IntoRenderTexture rendering mode.
//

#if IMGUI_DEBUG || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

#if USING_URP
using UnityEngine.Rendering.Universal;
#endif

namespace ImGuiNET.Unity
{
    // This component is responsible for setting up ImGui for use in Unity.
    // It holds the necessary context and sets it up before any operation is done to ImGui.
    // (e.g. set the context, texture and font managers before calling Layout)

    public enum RenderingMode
    {
        /// <summary>
        ///     Standard mode where Dear ImGui is rendered directly to the camera.
        /// </summary>
        /// <remarks>
        ///     In this mode, if you're using Screen-Space Canvas, Dear ImGui will be drawn behind it.
        /// </remarks>
        DirectlyToCamera = 0,
        
        /// <summary>
        ///     Experimental mode where Dear ImGui is rendered into a RenderTexture that is then drawn by
        ///      Screen-Space Canvas to combat issues with Screen-Space Canvas.
        /// </summary>
        /// <remarks>
        ///     As this process involves few extra steps and finishing the render with unity's Canvas system,
        ///      it is overall more performance heavy.
        /// </remarks>
        IntoRenderTexture = 1
    }
    
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

        [Header("System")] 
        [SerializeField] private Camera defaultCamera = default!;
        [FormerlySerializedAs("camera")]
        [SerializeField] private Camera hdrpCamera = default!;
        [FormerlySerializedAs("_renderFeature")] 
        [SerializeField] private RenderImGuiFeature renderFeature = default!;
        [FormerlySerializedAs("_rendererType")]
        [SerializeField] private RenderUtils.RenderType rendererType = RenderUtils.RenderType.Mesh;
        [FormerlySerializedAs("_platformType")]
        [SerializeField] private Platform.Type platformType = Platform.Type.InputManager;

        [Header("Configuration")] 
        [SerializeField] private RenderingMode renderingMode = RenderingMode.DirectlyToCamera;
        [SerializeField] internal int toTextureRendererIndex = 0;
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

        private bool _myCameraIsDirty;
        
        private Camera _myPreviousCamera;
        private Camera _myCamera;
        public Camera GetCamera()
        {
            if (_myScreenSpaceCanvas != null)
                return _myScreenSpaceCanvas.GetCamera();
            return _myCamera;
        }

        public void SetCamera([NotNull] Camera newCamera)
        {
            if (newCamera == null) throw new ArgumentNullException(nameof(newCamera));
            if (_myCamera == newCamera)
                return;
            
            _myPreviousCamera = _myCamera;
            _myCamera = newCamera;
            _myCameraIsDirty = true;
        }
        
        private ImGuiScreenSpaceCanvas _myScreenSpaceCanvas;
        
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
            
            gameObject.transform.SetParent(null);
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

        /// <summary>
        ///     Note attempts to find our render feature on currently active camera.
        /// </summary>
        public void DiscoverRenderFeature(Camera cam = null)
        {
            var srpType = RenderUtils.GetSRP();
            if (srpType != SRPType.URP)
                return;

            if (cam == null)
                cam = GetCamera();
            Assert.IsNotNull(cam, "Failed to discover render feature: Camera reference is missing!");

#if USING_URP
            var urp = cam.GetUniversalAdditionalCameraData();
            if (urp == null)
                return;

            var myRenderer = urp.scriptableRenderer;
            if (myRenderer == null)
                return;

            DiscoverRenderFeature(myRenderer);
#endif
        }
        
#if USING_URP
        public void DiscoverRenderFeature(ScriptableRenderer myRenderer)
        {
            // our List<ScriptableRendererFeature> m_RendererFeatures field is private, so we need to use reflection to access it
            var rendererFeaturesField = typeof(ScriptableRenderer).GetField("m_RendererFeatures", BindingFlags.NonPublic | BindingFlags.Instance);
            if (rendererFeaturesField == null)
                return;
            
            var rendererFeatures = (List<ScriptableRendererFeature>)rendererFeaturesField.GetValue(myRenderer);
            if (rendererFeatures == null)
                return;

            for (var index0 = 0; index0 < rendererFeatures.Count; index0++)
            {
                var feature = rendererFeatures[index0];
                if (feature is RenderImGuiFeature guiFeature)
                {
                    Debug.Log($"Found render feature: {guiFeature.name}", this);
                    renderFeature = guiFeature;
                    return;
                }
            }

            Debug.LogError("Failed to discover render feature: RenderImGuiFeature is missing!", this);
        }
#endif
        
        private void OnEnable()
        {
            if (Instance != this)
                return;
            
            Debug.Log("Dear ImGui is enabled", this);

            // Discover SRP type.
            _srpType = RenderUtils.GetSRP();
            
            // Discover render feature.
            if (renderFeature == null)
                DiscoverRenderFeature();

            switch (renderingMode)
            {
                case RenderingMode.DirectlyToCamera:
                    SetupDirectCamera();
                    break;
                case RenderingMode.IntoRenderTexture:
                    SetupCanvas();
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

        private void SetupDirectCamera()
        {
            // Validate if camera reference is present.
            var cam = GetCamera();
            if (cam == null)
            {
                // Camera is missing, try to discover it.
                // In HDRP we want to use our exclusive camera that is part of our IMGUI Pass.
                if (_srpType == SRPType.HDRP)
                {
                    Assert.IsNotNull(hdrpCamera, "hdrpCamera != null");
                    cam = hdrpCamera;
                    // NOTE: Reference to HDRPCamera is different due to HDRP-UI-Camera-Stacking and it's configuration.
                    //       Unlike URP and BuiltIn, where you need to point at your camera, in HDRP, we point at exclusive camera.
                    //       I've done it this way to avoid configuration issues when switching between SRP types.
                }
                else if (defaultCamera == null)
                {
                    cam = Camera.main;
                    if (cam == null)
                    {
                        cam = FindObjectOfType<Camera>();
                        if (cam == null)
                        {
                            Debug.LogError("No camera found, please assign a camera to the DearImGui component.");
                            enabled = false;
                            return;
                        }
                    }
                }
                else cam = defaultCamera;
                SetCamera(cam);
                _myCameraIsDirty = false;
            }
            
            // Setup command buffer.
            Buffer = RenderUtils.GetCommandBuffer(CommandBufferTag);
            switch (_srpType)
            {
                case SRPType.BuiltIn:
                    Assert.IsNotNull(cam, "camera != null");
                    cam.AddCommandBuffer(CameraEvent.AfterEverything, Buffer);
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
        }

        private void SetupCanvas()
        {
            // Setup command buffer.
            Buffer = RenderUtils.GetCommandBuffer(CommandBufferTag);

            if (_myScreenSpaceCanvas != null)
                _myScreenSpaceCanvas.gameObject.SetActive(true);
            else
            {
                // Spawn canvas.
                var obj = new GameObject("ImGui Screen-Space Canvas")
                {
                    hideFlags = HideFlags.NotEditable | HideFlags.DontSave
                };
                obj.transform.SetParent(transform);
                _myScreenSpaceCanvas = obj.AddComponent<ImGuiScreenSpaceCanvas>();
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

            var cam = GetCamera();
            switch (_srpType)
            {
                case SRPType.BuiltIn:
                    if (_myPreviousCamera != null)
                        _myPreviousCamera.RemoveCommandBuffer(CameraEvent.AfterEverything, Buffer);
                    if (cam != null)
                        cam.RemoveCommandBuffer(CameraEvent.AfterEverything, Buffer);
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
            
            if (_myScreenSpaceCanvas != null)
                _myScreenSpaceCanvas.gameObject.SetActive(false);
            
            if (Buffer != null)
                RenderUtils.ReleaseCommandBuffer(Buffer);
            Buffer = null;

            _myPreviousCamera = defaultCamera;
            _myCameraIsDirty = false;
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
            OnImguiUpdate?.Invoke();
            if (!Render)
                return;
            
            if (Instance != this)
                return;

            if (_myCameraIsDirty)
            {
                _myCameraIsDirty = false;
                Reload();
                return;
            }

            var cam = GetCamera();
            if (cam == null)
            {
                Debug.LogError("Camera reference is missing, please assign a camera to the DearImGui component.", this);
                enabled = false;
                return;
            }
            
            ImGuiUn.SetUnityContext(_context);
            ImGuiIOPtr io = ImGui.GetIO();
            
            s_prepareFramePerfMarker.Begin(this);
            _context.textures.PrepareFrame(io);
            _platform.PrepareFrame(io, cam.pixelRect);
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

        public static event Action OnImguiUpdate;

        /// <summary>
        ///     A flag that tell us if imgui should currently render.
        /// </summary>
        /// <remarks>
        ///     You can use this flag to disable rendering in cases where your debuting tools are hidden anyway.
        /// </remarks>
        public static bool Render { get; set; } = true;
        
        public static DearImGui Instance { get; private set; }
    }
}
#endif