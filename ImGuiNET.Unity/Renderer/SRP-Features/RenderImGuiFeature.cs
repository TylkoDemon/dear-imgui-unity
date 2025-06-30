#if IMGUI_DEBUG || UNITY_EDITOR
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

#if USING_URP
using UnityEngine.Rendering.Universal;
#endif

#if USING_URP
namespace ImGuiNET.Unity
{
    public class RenderImGuiFeature : ScriptableRendererFeature
    {
        private const string profilerTag = "[Dear ImGui]";
        private const string bufferPoolId = "[Dear ImGui]";
        
        private static readonly int ImguiTextureId = Shader.PropertyToID("_ImGuiTexture");
        
        private class PassData
        {
            internal TextureHandle source;
            internal TextureHandle outputTexture;
            internal Rect pixelRect;
        }
        
        class ExecuteCommandBufferPass : ScriptableRenderPass
        {
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                using (var builder = renderGraph.AddUnsafePass<PassData>(profilerTag, out var passData))
                {
                    UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                    UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                    
                    // Create a render texture for ImGui output
                    var textureDesc = new TextureDesc(cameraData.camera.pixelWidth, cameraData.camera.pixelHeight)
                    {
                        colorFormat = GraphicsFormat.R32G32B32A32_SFloat,
                        name = "ImGuiTexture",
                        clearBuffer = true
                    };
                    var imguiTexture = renderGraph.CreateTexture(textureDesc);
                    
                    passData.source = resourceData.cameraColor;
                    passData.pixelRect = cameraData.camera.pixelRect;
                    passData.outputTexture = imguiTexture;
                    
                    builder.UseTexture(resourceData.cameraColor, AccessFlags.ReadWrite);
                    builder.UseTexture(imguiTexture, AccessFlags.Write);
                    
                    builder.SetGlobalTextureAfterPass(imguiTexture, ImguiTextureId);

                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
                }
            }
            
            private static void ExecutePass(PassData data, UnsafeGraphContext unsafeContext)
            {
                CommandBuffer buffer = CommandBufferHelpers.GetNativeCommandBuffer(unsafeContext.cmd);
                var context = DearImGui.GetContext();
                var platform = DearImGui.GetPlatform();
                var renderer = DearImGui.GetRenderer();
                
                ImGuiUn.SetUnityContext(context);
                ImGuiIOPtr io = ImGui.GetIO();
                
                context.textures.PrepareFrame(io);
                platform.PrepareFrame(io, data.pixelRect);
                ImGui.NewFrame();
                
                try
                {
                    ImGuiUn.DoLayout();
                }
                finally
                {
                    ImGui.Render();
                }
                
                buffer.SetRenderTarget(data.outputTexture);
                renderer.RenderDrawLists(buffer, ImGui.GetDrawData());
            }

            public override void Execute(ScriptableRenderContext srp, ref RenderingData renderingData)
            {
                var camera = renderingData.cameraData.camera;
                var desc = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight, RenderTextureFormat.ARGB32, 0)
                {
                    sRGB = true
                };
                RenderTexture imguiTexture = RenderTexture.GetTemporary(desc);
                CommandBuffer buffer = CommandBufferPool.Get(bufferPoolId);
                
                buffer.SetRenderTarget(imguiTexture);
                buffer.ClearRenderTarget(true, true, Color.clear);
                
                var context = DearImGui.GetContext();
                var platform = DearImGui.GetPlatform();
                var renderer = DearImGui.GetRenderer();
    
                ImGuiUn.SetUnityContext(context);
                ImGuiIOPtr io = ImGui.GetIO();
    
                context.textures.PrepareFrame(io);
                platform.PrepareFrame(io, renderingData.cameraData.camera.pixelRect);
                ImGui.NewFrame();
    
                try
                {
                    ImGuiUn.DoLayout();
                }
                finally
                {
                    ImGui.Render();
                }
    
                renderer.RenderDrawLists(buffer, ImGui.GetDrawData());
                buffer.SetGlobalTexture(ImguiTextureId, imguiTexture);
                
                srp.ExecuteCommandBuffer(buffer);
                buffer.Clear();
                CommandBufferPool.Release(buffer);
                RenderTexture.ReleaseTemporary(imguiTexture);
            }
        }

        ExecuteCommandBufferPass _executeCommandBufferPass;
        
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;

        public override void Create()
        {
            _executeCommandBufferPass = new ExecuteCommandBufferPass()
            {
                renderPassEvent = renderPassEvent,
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!Application.isPlaying)
                return;
            if (!DearImGui.IsReadyToDraw())
                return;
            if (renderingData.cameraData.cameraType != CameraType.Game)
                return;
            
            _executeCommandBufferPass.renderPassEvent = renderPassEvent;
            renderer.EnqueuePass(_executeCommandBufferPass);
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            _executeCommandBufferPass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
        }
    }
}
#else
namespace ImGuiNET.Unity
{
    public class RenderImGuiFeature : UnityEngine.ScriptableObject
    {
        public CommandBuffer commandBuffer;
    }
}
#endif
#endif