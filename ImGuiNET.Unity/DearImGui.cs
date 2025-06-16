//
// Project under MIT License https://github.com/TylkoDemon/dear-imgui-unity
// 
// A DearImgui implementation for Unity for URP that requires minimal setup.
// Based on https://github.com/realgamessoftware/dear-imgui-unity
// Uses ImGuiNET(https://github.com/ImGuiNET/ImGui.NET) and cimgui (https://github.com/cimgui/cimgui)
//

#if IMGUI_DEBUG || UNITY_EDITOR
using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
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
        
        [Header("System")] 
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
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            if (_context != null)
                ImGuiUn.DestroyUnityContext(_context);
            Instance = null;
        }
        
        private void OnEnable()
        {
            if (Instance != this)
                return;
            
            // Discover an SRP type.
            _srpType = RenderUtils.GetSRP();
            Debug.Log($"Dear ImGui is enabled. SRP: {_srpType}", this);
            
            // Setup canvas to draw our ImGui on top of Unity UI.
            SetupCanvas();
            
            // Configure ImGui context.
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

        private void SetupCanvas()
        {
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
                _myScreenSpaceCanvas.Setup();
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
            
            if (_myScreenSpaceCanvas != null)
                _myScreenSpaceCanvas.gameObject.SetActive(false);
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
        
        public static bool ShouldRender()
        {
            bool anyRequest = false;
            for (int index0 = 0; index0 < _drawRequests.Count; index0++)
            {
                if (_drawRequests[index0]())
                {
                    anyRequest = true;
                    break;
                }
            }
            return anyRequest;
        }
        
        public static void DisposeStaticContext()
        {
            ImGuiUn.Reset();
            OnImguiUpdate = null;
        }
        
        internal static bool IsReadyToDraw()
        {
            if (Instance == null)
                return false;
            return Instance._context != null && Instance._renderer != null && Instance._platform != null && Instance.enabled;
        }
        
        internal static ImGuiUnityContext GetContext()
        {
            if (Instance == null)
                throw new Exception("DearImGui instance is null");
            return Instance._context;
        }
        
        internal static IImGuiPlatform GetPlatform()
        {
            if (Instance == null)
                throw new Exception("DearImGui instance is null");
            return Instance._platform;
        }
        
        internal static IImGuiRenderer GetRenderer()
        {
            if (Instance == null)
                throw new Exception("DearImGui instance is null");
            return Instance._renderer;
        }
        
        /// <summary>
        ///     Always invokes before the ImGui layout is done, even if <see cref="Render"/> is false.
        /// </summary>
        public static event Action OnImguiUpdate;

        public delegate bool DrawRequest();
        private static readonly List<DrawRequest> _drawRequests = new List<DrawRequest>();
        public static void AddDrawRequest([NotNull] DrawRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request)); 
            _drawRequests.Add(request);
        }
        
        public static void RemoveDrawRequest([NotNull] DrawRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request)); 
            _drawRequests.Remove(request);
        }
        
        public static DearImGui Instance { get; private set; }
    }
}
#endif