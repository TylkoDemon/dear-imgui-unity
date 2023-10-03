#if IMGUI_DEBUG || UNITY_EDITOR
using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

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
        private bool _enableDocking = true;

        public Camera camera;
        [SerializeField] private RenderUtils.RenderType _rendererType = RenderUtils.RenderType.Mesh;
        [SerializeField] private Platform.Type _platformType = Platform.Type.InputManager;

        [Header("Configuration")]
        [SerializeField]
        private IOConfig _initialConfiguration = default;
        [SerializeField] private FontAtlasConfigAsset _fontAtlasConfiguration = null;
        [SerializeField] private IniSettingsAsset _iniSettings = null;  // null: uses default imgui.ini file

        [Header("Customization")]
        [SerializeField]
        private ShaderResourcesAsset _shaders = null;
        [SerializeField] private StyleAsset _style = null;
        [SerializeField] private CursorShapesAsset _cursorShapes = null;

        private const string CommandBufferTag = "DearImGui";
        private static readonly ProfilerMarker s_prepareFramePerfMarker = new ProfilerMarker("DearImGui.PrepareFrame");
        private static readonly ProfilerMarker s_layoutPerfMarker = new ProfilerMarker("DearImGui.Layout");
        private static readonly ProfilerMarker s_drawListPerfMarker = new ProfilerMarker("DearImGui.RenderDrawLists");

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            
            Instance = this;
            _context = ImGuiUn.CreateUnityContext();

            if (_enableDocking)
            {
                ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
            }
            
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
            
            Debug.Log("Dear ImGui is enabled");
            
            Buffer = RenderUtils.GetCommandBuffer(CommandBufferTag);
            ImGuiUn.SetUnityContext(_context);
            ImGuiIOPtr io = ImGui.GetIO();

            _initialConfiguration.ApplyTo(io);
            _style?.ApplyTo(ImGui.GetStyle());

            _context.textures.BuildFontAtlas(io, _fontAtlasConfiguration);
            _context.textures.Initialize(io);

            SetPlatform(Platform.Create(_platformType, _cursorShapes, _iniSettings), io);
            SetRenderer(RenderUtils.Create(_rendererType, _shaders, _context.textures), io);
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
            _initialConfiguration.SetDefaults();
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

        public static CommandBuffer Buffer { get; private set; }
        
        public static DearImGui Instance { get; private set; }
    }
}
#endif