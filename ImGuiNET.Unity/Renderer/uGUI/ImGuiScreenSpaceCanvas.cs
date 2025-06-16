#if IMGUI_DEBUG || UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

namespace ImGuiNET.Unity
{
    internal sealed class ImGuiScreenSpaceCanvas : MonoBehaviour
    {
        private Canvas canvas;
        private Material imguiMaterial;
        
        public void Setup()
        {
            imguiMaterial = new Material(Shader.Find("Hidden/UI/ImGuiUI"));
            imguiMaterial.hideFlags = HideFlags.DontSave;
            
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
            var imageObject = new GameObject("Screen-Space ImGui Image")
            {
                hideFlags = HideFlags.DontSave
            };
            imageObject.transform.SetParent(canvasObject.transform);
            imageObject.transform.localPosition = Vector3.zero;
            imageObject.transform.localScale = Vector3.one;
            
            // Stretch the RawImage to fit the screen.
            var rectTransform = imageObject.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;
            
            var image = imageObject.AddComponent<Image>();
            image.material = imguiMaterial;
            image.raycastTarget = false;
        }

        private void OnDestroy()
        {
            if (imguiMaterial != null)
                Destroy(imguiMaterial);
            imguiMaterial = default;
        }
    }
}
#endif