#if IMGUI_DEBUG || UNITY_EDITOR
using UnityEngine;
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

        private class PassData
        {
            internal TextureHandle source;
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
                    
                    passData.source = resourceData.cameraColor;
                    passData.pixelRect = cameraData.camera.pixelRect;
                    
                    builder.UseTexture(resourceData.cameraColor, AccessFlags.ReadWrite);

                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecutePass(data, context));
                }
            }
            
            private void ExecutePass(PassData data, UnsafeGraphContext unsafeContext)
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
                
                renderer.RenderDrawLists(buffer, ImGui.GetDrawData());
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