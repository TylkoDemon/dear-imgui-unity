# Dear ImGui for Unity, dockable version

UPM package for the immediate mode GUI library, Dear ImGui (https://github.com/ocornut/imgui).

![image](https://github.com/TylkoDemon/dear-imgui-unity/assets/6078922/537000db-57f2-4886-bf64-3fccbff37666)

### Usage

- [Add package](https://docs.unity3d.com/Manual/upm-ui-giturl.html) from git URL: [https://github.com/TylkoDemon/dear-imgui-unity.git](https://github.com/TylkoDemon/dear-imgui-unity.git) .
- Drag an `ImGui Pass` prefab that can be found at `Packages/Dear ImGui/Resources/ImGui Pass.prefab` into your Bootstrap/Entry scene (it invokes DontDestroyOnLoad on itself).
- When using the Universal Render Pipeline, add a `Render Im Gui Feature` render feature to the renderer asset. Assign it to the `render feature` field of the DearImGui component.
- When using the HDRP, no extra setup is needed. Prefab is preconfigured to work just fine.
- When using the Inbuilt Renderer, no extra setup is needed.
- Subscribe to the `ImGuiUn.Layout` event and use ImGui functions.
- Example script:
  ```cs
  using UnityEngine;
  using ImGuiNET;

  public class DearImGuiDemo : MonoBehaviour
  {
      void OnEnable()
      {
          ImGuiUn.Layout += OnLayout;
      }

      void OnDisable()
      {
          ImGuiUn.Layout -= OnLayout;
      }

      void OnLayout()
      {
          ImGui.ShowDemoWindow();
      }
  }
  ```

### Features

Full Dear Imgui implementation (based on [ImGui.NET](https://github.com/ImGuiNET/ImGui.NET)), version 1.76 with Docking features.

### See Also

This package uses Dear ImGui C bindings by [cimgui](https://github.com/cimgui/cimgui) and the C# wrapper by [ImGui.NET](https://github.com/mellinoe/ImGui.NET).

The development project for the package can be found at [https://github.com/TylkoDemon/dear-imgui-unity](https://github.com/TylkoDemon/dear-imgui-unity).

Full setup with examples of this package can be found at [https://github.com/TylkoDemon/Dear-Imgui-For-Unity](https://github.com/TylkoDemon/Dear-Imgui-For-Unity)

To draw IMGUI after UI in Camera-Space, this package utilizes HDRP-UI-Camera-Stacking which can be found at [https://github.com/alelievr/HDRP-UI-Camera-Stacking](https://github.com/alelievr/HDRP-UI-Camera-Stacking)

### Limitation

To draw Dear Imgui after Screen-Space Overlay UI, you need to use RenderingMode.IntoRenderTexture mode, which actually uses another Screen-Space Overlay canvas set to maximum render order, and draws our Imgui by using Raw Image.
  This feature has been tested only on URP.

# Unity Compatibility
This fork of dear-imgui-unity is currently developed by using `Unity 2022.3.10f1`

