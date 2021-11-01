# gingerbread :rice_cracker:

![Revit API](https://img.shields.io/badge/Revit%20API-2020-red.svg)
![.NET](https://img.shields.io/badge/.NET-4.7-red.svg)

A lightweight gbXML export module for Revit, WIP. Sometimes we find the Revit model much too complex for an accurate and lightweight gbXML export. There may be broken space boundaries, shattered surfaces, or tiny twisted patches, thus leading to failures in the building energy simulation. Here in this toy project we try to create a simple BREP-like space crafting addin with fuzzy space detection and simplification, aside from the native gbXML export module either based on room/space definition or energy analysis model. However, we've just started and the algorithm needs many improvements.

```
gingerbread
└ /Gingerbread
  ├ /Properties   - Assembly info
  ├ /Resources
  │ ├ /Icon       - Icons
  │ ├ /lib        - Clipper.cs
  │ └ /spider     - spider gbXML viewer
  ├ /Views        - WPF
  ├ /Core         - reserved mehtod
  ├ App.cs        - App entry
  ├ Cmd*.cs       - Button entry
  ├ Ext*.cs       - External command for WPF
  ├ Util*.cs      - Utility method
  └ Gingerbread.addin  - App manifest
```

**Dependencies** in use
[Clipper](http://www.angusj.com/delphi/clipper.php) 6.1.3  
[CefSharp](https://github.com/cefsharp/CefSharp) 65.0.1  
[spider-gbXML-tools](https://github.com/ladybug-tools/spider-gbxml-tools) basic V7  
Currently the spider is under test thus not fully embedded  

**Compile** the code
- Revit 2020 - ChefSharp 65.0.1 - Visual Studio 2019
- Make sure the Build/Debug platform is switched to either x64 or x86
- Post-build event settings
```bash
if exist "$(AppData)\Autodesk\REVIT\Addins\2020" copy "$(ProjectDir)*.addin" "$(AppData)\Autodesk\REVIT\Addins\2020"
if exist "$(AppData)\Autodesk\REVIT\Addins\2020" mkdir "$(AppData)\Autodesk\REVIT\Addins\2020\Gingerbread"
copy "$(ProjectDir)$(OutputPath)*.dll" "$(AppData)\Autodesk\REVIT\Addins\2020\Gingerbread"
copy "$(ProjectDir)$(OutputPath)\Resources\spider\*.*" "$(AppData)\Autodesk\REVIT\Addins\2020\Gingerbread\Spider"
```

**Demo**
Still buggy right now but it works with the Revit sample file `Technical_school-current_m.rvt`  
[![Snapshot](https://i.postimg.cc/505qQ1n5/Artboard-1.jpg)](https://postimg.cc/0MQJLggr)