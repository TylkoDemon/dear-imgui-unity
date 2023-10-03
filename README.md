# Dear ImGui for Unity, dockable version

UPM package for the immediate mode GUI library, Dear ImGui (https://github.com/ocornut/imgui).

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

### See Also

This package uses Dear ImGui C bindings by [cimgui](https://github.com/cimgui/cimgui) and the C# wrapper by [ImGui.NET](https://github.com/mellinoe/ImGui.NET).

The development project for the package can be found at [https://github.com/TylkoDemon/dear-imgui-unity](https://github.com/TylkoDemon/dear-imgui-unity).

Full setup with examples of this package can be found at [https://github.com/TylkoDemon/Dear-Imgui-For-Unity](https://github.com/TylkoDemon/Dear-Imgui-For-Unity)

To draw IMGUI after UI in Camera-Space, this package utilizes HDRP-UI-Camera-Stacking which can be found at [https://github.com/alelievr/HDRP-UI-Camera-Stacking](https://github.com/alelievr/HDRP-UI-Camera-Stacking)

### Limitation

Dear Imgui renders behind Screen-Space UI, which means that you should rely on Camera-Space canvases if you want to use this package right now.

# Unity Compatibility
This fork of dear-imgui-unity is currently developed by using `Unity 2022.3.10f1`

