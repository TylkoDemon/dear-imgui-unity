#if IMGUI_DEBUG || UNITY_EDITOR
using UnityEngine;

namespace ImGuiNET.Unity
{
    [System.Serializable]
    struct FontDefinition
    {
        [Tooltip("Font file imported as a TextAsset (e.g. a .bytes file). Loaded from memory; takes precedence over FontPath.")]
        public TextAsset FontData;
        [Tooltip("Path to a font file under StreamingAssets. Used only when FontData is not set.")]
        public string FontPath;
        public FontConfig Config;
    }
}
#endif