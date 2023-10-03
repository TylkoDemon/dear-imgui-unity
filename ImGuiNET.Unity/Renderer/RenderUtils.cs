#if IMGUI_DEBUG || UNITY_EDITOR
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace ImGuiNET.Unity
{
    static class RenderUtils
    {
        public enum RenderType
        {
            Mesh = 0,
            Procedural = 1,
        }

        public static IImGuiRenderer Create(RenderType type, ShaderResourcesAsset shaders, TextureManager textures)
        {
            Assert.IsNotNull(shaders, "Shaders not assigned.");
            switch (type)
            {
                case RenderType.Mesh:       return new ImGuiRendererMesh(shaders, textures);
                case RenderType.Procedural: return new ImGuiRendererProcedural(shaders, textures);
                default:                    return null;
            }
        }
        
        public static CommandBuffer GetCommandBuffer(string name)
        {
            return new CommandBuffer { name = name };
        }

        public static void ReleaseCommandBuffer(CommandBuffer cmd)
        {
            cmd.Release();
        }
    }
}
#endif