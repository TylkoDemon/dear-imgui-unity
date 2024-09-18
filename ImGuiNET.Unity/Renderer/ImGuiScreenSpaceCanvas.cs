#if IMGUI_DEBUG || UNITY_EDITOR

using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.UI;

#if USING_URP
using UnityEngine.Rendering.Universal;
#endif

namespace ImGuiNET.Unity
{
    internal sealed class ImGuiScreenSpaceCanvas : MonoBehaviour
    {
        private Camera myCamera;
        private RenderTexture renderTexture;

        private Canvas canvas;
        
        private void Start()
        {
            var myCameraObject = new GameObject("Screen-Space ImGui Camera")
            {
                hideFlags = HideFlags.DontSave
            };
            myCameraObject.transform.SetParent(transform);
            
            myCamera = myCameraObject.AddComponent<Camera>();
            myCamera.enabled = false;
            myCamera.clearFlags = CameraClearFlags.SolidColor;
            myCamera.backgroundColor = Color.clear;
            myCamera.cullingMask = 0;
            myCamera.depth = float.MaxValue;
            myCamera.orthographic = true;
            myCamera.orthographicSize = 1f;
            myCamera.nearClipPlane = 0.3f;
            myCamera.farClipPlane = 1000f;
            myCamera.useOcclusionCulling = false;

            var srpType = RenderUtils.GetSRP();
            switch (srpType)
            {
                case SRPType.BuiltIn:
                    break;
                case SRPType.URP:
#if USING_URP
                    // Initialize the camera for URP
                    var data = myCamera.GetUniversalAdditionalCameraData();
                    data.SetRenderer(DearImGui.Instance.toTextureRendererIndex);
#endif
                    break;
                case SRPType.HDRP:
#if USING_HDRP
                    // Initialize the camera for HDRP
                    // TODO
#endif
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            // Create the render texture
            myCamera.targetTexture = renderTexture = new RenderTexture(Screen.width, Screen.height, 0, GraphicsFormat.R16G16B16A16_SFloat)
            {
                name = "ImGui Screen-Space Canvas",
                hideFlags = HideFlags.DontSave
            };
            
            var rt = RenderTexture.active;
            RenderTexture.active = renderTexture;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = rt;

            var buffer = DearImGui.Buffer;
            Assert.IsNotNull(buffer, "buffer != null");
            myCamera.AddCommandBuffer(CameraEvent.AfterEverything, buffer);
            
            // Create canvas.
            var canvasObject = new GameObject("Screen-Space ImGui Canvas")
            {
                hideFlags = HideFlags.DontSave
            };
            canvasObject.transform.SetParent(transform);
            
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1;
            canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.None;
            
            var canvasScaler = canvasObject.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            
            // Create RawImage.
            var rawImageObject = new GameObject("Screen-Space ImGui RawImage")
            {
                hideFlags = HideFlags.DontSave
            };
            rawImageObject.transform.SetParent(canvasObject.transform);
            rawImageObject.transform.localPosition = Vector3.zero;
            rawImageObject.transform.localScale = Vector3.one;
            
            // Stretch the RawImage to fit the screen.
            var rectTransform = rawImageObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
            
            var rawImage = rawImageObject.AddComponent<RawImage>();
            rawImage.texture = renderTexture;
            rawImage.color = Color.white;
            rawImage.raycastTarget = false;
        }

        private void OnDestroy()
        {
            if (myCamera != null)
                Destroy(myCamera.gameObject);
            
            if (renderTexture != null)
                Destroy(renderTexture);

            myCamera = default;
            renderTexture = default;
        }

        private void Update()
        {
            if (!DearImGui.Render)
            {
                canvas.enabled = false;
                return;
            }
            
            Assert.IsNotNull(myCamera, "myCamera != null");
            Assert.IsNotNull(renderTexture, "renderTexture != null");
            
            // Validate if render texture size is valid, if not, resize it
            if (renderTexture.width != Screen.width || renderTexture.height != Screen.height)
            {
                renderTexture.Release();
                renderTexture.width = Screen.width;
                renderTexture.height = Screen.height;
            }
            else
            {
                // myCamera.Render();
                FixedRateRender();
            }
        }

        // NOTE: Imgui does not require high refresh rate experience, so we can save some performance on this.
        // TODO: Expose option to change between 30/60/90/120/144 and unlimited.
        private float lastTime;
        private void FixedRateRender()
        {
            const int targetFps = 60;
            const float targetDeltaTime = 1f / targetFps;
            
            var time = Time.unscaledTime;
            var delta = time - lastTime;
            if (delta < targetDeltaTime)
                return;
            
            lastTime = time;
            myCamera.Render();
            canvas.enabled = true;
        }

        public Camera GetCamera()
        {
            return myCamera;
        }
    }
}
#endif