#if IMGUI_DEBUG || UNITY_EDITOR
using UnityEngine;

namespace ImGuiNET.Unity
{
    [CreateAssetMenu(menuName = "Tools/Dear ImGui/Font Atlas Configuration")]
    sealed class FontAtlasConfigAsset : ScriptableObject
    {
        public FontRasterizerType Rasterizer;
        public uint RasterizerFlags;
        public FontDefinition[] Fonts;
    }

    enum FontRasterizerType
    {
        StbTrueType,
        FreeType,
    }
}
#endif