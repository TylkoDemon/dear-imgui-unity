# Dear ImGui for Unity, dockable version

UPM package for the immediate mode GUI library, Dear ImGui (https://github.com/ocornut/imgui).

![image](https://github.com/TylkoDemon/dear-imgui-unity/assets/6078922/537000db-57f2-4886-bf64-3fccbff37666)

### Features

Full Dear Imgui implementation (based on [ImGui.NET](https://github.com/ImGuiNET/ImGui.NET)), version 1.89.1 with Docking features.

### See Also

This package uses Dear ImGui C bindings by [cimgui](https://github.com/cimgui/cimgui) and the C# wrapper by [ImGui.NET](https://github.com/mellinoe/ImGui.NET).

The development project for the package can be found at [https://github.com/TylkoDemon/dear-imgui-unity](https://github.com/TylkoDemon/dear-imgui-unity).

### Limitation

To draw Dear Imgui after Screen-Space Overlay UI, you need to use RenderingMode.IntoRenderTexture mode, which actually uses another Screen-Space Overlay canvas set to maximum render order, and draws our Imgui by using Raw Image.

**While using this mode, please make sure to disable RenderImGuiFeature on your Render Feature (do not remove, just disable).**

**If you don't do that, your ImGui will be rendered twice, once by the SRP and the second time by dedicated Camera for Screen-Space UI.**

# Setting Up

Full setup with example (draw imgui demo) of this package can be found at [https://github.com/TylkoDemon/Dear-Imgui-For-Unity](https://github.com/TylkoDemon/Dear-Imgui-For-Unity)

# Unity Compatibility

This fork of dear-imgui-unity is currently developed by using `Unity 6000.1.1f1`

Supports Render Graph and Compatibility Mode

# SRPs

- URP Support Only

# Platforms

- Windows Support Only
