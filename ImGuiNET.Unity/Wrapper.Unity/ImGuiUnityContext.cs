#if IMGUI_DEBUG || UNITY_EDITOR
using System;
using UnityEngine;

namespace ImGuiNET.Unity
{
    sealed class ImGuiUnityContext
    {
        public IntPtr state;            // ImGui internal state
        public TextureManager textures; // texture / font state
    }

    public static unsafe partial class ImGuiUn
    {
        public static event Action Before; 
        public static event Action Layout; 
        public static event Action After;  
        internal static void DoLayout()
        {
            try
            {
                Before?.Invoke();
                Layout?.Invoke();
                After?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        internal static void Reset()
        {
            Before = default;
            Layout = default;
            After = default;
        }
        
        // textures
        public static int GetTextureId(Texture texture) => s_currentUnityContext?.textures.GetTextureId(texture) ?? -1;
        internal static SpriteInfo GetSpriteInfo(Sprite sprite) => s_currentUnityContext?.textures.GetSpriteInfo(sprite) ?? null;

        internal static ImGuiUnityContext s_currentUnityContext;

        internal static ImGuiUnityContext CreateUnityContext()
        {
            return new ImGuiUnityContext 
            {
                state = ImGui.CreateContext(),
                textures = new TextureManager(),
            };
        }

        internal static void DestroyUnityContext(ImGuiUnityContext context)
        {
            s_currentUnityContext = default;
            ImGui.DestroyContext(context.state);
        }

        internal static void SetUnityContext(ImGuiUnityContext context)
        {
            s_currentUnityContext = context;
            ImGui.SetCurrentContext(context?.state ?? IntPtr.Zero);
        }
    }
}
#endif
