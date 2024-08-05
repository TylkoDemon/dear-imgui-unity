#if IMGUI_DEBUG || UNITY_EDITOR
using System;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

#if USING_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

#if USING_URP
using UnityEngine.Rendering.Universal;
#endif

namespace ImGuiNET.Unity
{
    public enum SRPType
    {
        BuiltIn = 0,
        URP = 1,
        HDRP = 2,
    }
    
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
        
        public static SRPType GetSRP()
        {
            var currentRP = GraphicsSettings.currentRenderPipeline;
#if USING_URP
            if (currentRP is UniversalRenderPipelineAsset)
                return SRPType.URP;
#endif
#if USING_HDRP
            if (currentRP is HDRenderPipelineAsset)
                return SRPType.HDRP;
#endif

            if (currentRP != null)
                throw new Exception("Unknown and unsupported SRP detected.");
            return SRPType.BuiltIn;
        }

        public static CommandBuffer GetCommandBuffer(string name)
        {
#if USING_URP
              return CommandBufferPool.Get(name);
#else
            return new CommandBuffer { name = name };
#endif
        }

        public static void ReleaseCommandBuffer(CommandBuffer cmd)
        {
#if USING_URP
            CommandBufferPool.Release(cmd);
#else
            cmd.Release();
#endif
        }
    }
}
#endif