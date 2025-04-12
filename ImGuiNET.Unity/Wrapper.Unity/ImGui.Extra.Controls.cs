#if IMGUI_DEBUG || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;

namespace ImGuiNET.Unity
{
    // ImGui extra functionality related with Images
    public static partial class ImGuiUn
    {
        private static readonly Dictionary<Type, string[]> EnumNames = new Dictionary<Type, string[]>();
        private static readonly Dictionary<Type, int[]> EnumValues = new Dictionary<Type, int[]>();

        public static T EnumPopup<T>(string label, T value) where T : Enum
        {
            EnumPopup(label, ref value);
            return value;
        }
        public static void EnumPopup<T>(string label, ref T value) where T : Enum
        {
            if (!EnumNames.TryGetValue(typeof(T), out var names))
            {
                names = Enum.GetNames(typeof(T));
                EnumNames[typeof(T)] = names;
            }
            
            if (!EnumValues.TryGetValue(typeof(T), out int[] values))
            {
                values = (int[])Enum.GetValues(typeof(T));
                EnumValues[typeof(T)] = values;
            }

            int index = Array.IndexOf(values, Convert.ToInt32(value));
            if (index == -1)
                index = 0;
            if (ImGui.Combo(label, ref index, names, names.Length))
                value = (T)Enum.ToObject(typeof(T), values[index]);
        }
        
        public static float FloatSlider(string label, float value, int min, int max)
        {
            float v = value;
            if (ImGui.SliderFloat(label, ref v, min, max))
            {
                value = v;
                return value;
            }
            return value;
        }
        
        public static int IntSlider(string label, int value, int min, int max)
        {
            int v = value;
            if (ImGui.SliderInt(label, ref v, min, max))
            {
                value = v;
                return value;
            }
            return value;
        }

        public static bool Toggle(string label, bool value)
        {
            bool v = value;
            if (ImGui.Checkbox(label, ref v))
            {
                value = v;
                return value;
            }
            return value;
        }

        public static float FloatField(string label, float value)
        {
            float v = value;
            if (ImGui.InputFloat(label, ref v))
            {
                value = v;
                return value;
            }
            return value;
        }

        public static int IntField(string label, int value)
        {
            int v = value;
            if (ImGui.InputInt(label, ref v))
            {
                value = v;
                return value;
            }
            return value;
        }

        public static string TextField(string label, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            uint size = (uint)bytes.Length + 1u;
            byte[] buffer = new byte[size];
            Array.Copy(bytes, buffer, bytes.Length);
            if (ImGui.InputText(label, buffer, size))
            {
                value = Encoding.UTF8.GetString(buffer, 0, (int)size - 1);
                return value;
            }
            return value;
        }
    }
}
#endif