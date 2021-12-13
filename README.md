# gingerbread :rice_cracker:

![Revit API](https://img.shields.io/badge/Revit%20API-2020-red.svg)
![.NET](https://img.shields.io/badge/.NET-4.7-red.svg)

A lightweight gbXML export module for Revit, WIP. Sometimes we find the Revit model much too complex for an accurate and lightweight gbXML export. There may be broken space boundaries, shattered surfaces, or tiny twisted patches, thus leading to failures in the building energy simulation. Here in this toy project we try to create a simple BREP-like space crafting addin with fuzzy space detection and simplification, aside from the native gbXML export module either based on room/space definition or energy analysis model. However, we've just started and the algorithm needs many improvements.

```
gingerbread
└ /Gingerbread
  ├ /Properties   - Assembly info
  ├ /Resources
  │ ├ /ico        - Icons
  │ ├ /lib        - Clipper.cs
  │ └ /spider     - Spider gbXML viewer
  ├ /Views        - WPF
  ├ /Core         - Reserved mehtod
  ├ App.cs        - App entry
  ├ Cmd*.cs       - Button entry
  ├ Ext*.cs       - External event
  ├ Util*.cs      - Utility method
  └ Gingerbread.addin  - App manifest
```

**Dependencies**  
- [Clipper](http://www.angusj.com/delphi/clipper.php) 6.1.3  
- [CefSharp](https://github.com/cefsharp/CefSharp) 65.0.1  
- [spider-gbXML-tools](https://github.com/ladybug-tools/spider-gbxml-tools) basic V7  

**Compile**  
- Revit 2020 - ChefSharp 65.0.1 - Visual Studio 2019
- Make sure the Build/Debug platform is switched to x64
- Post-build event settings:
```bash
if exist "$(AppData)\Autodesk\REVIT\Addins\2020" copy "$(ProjectDir)*.addin" "$(AppData)\Autodesk\REVIT\Addins\2020"
if exist "$(AppData)\Autodesk\REVIT\Addins\2020" mkdir "$(AppData)\Autodesk\REVIT\Addins\2020\Gingerbread" mkdir "$(AppData)\Autodesk\REVIT\Addins\2020\Gingerbread\Spider"
copy "$(ProjectDir)$(OutputPath)*.dll" "$(AppData)\Autodesk\REVIT\Addins\2020\Gingerbread"
copy "$(ProjectDir)$(OutputPath)\Resources\spider\*.*" "$(AppData)\Autodesk\REVIT\Addins\2020\Gingerbread\Spider"
```

**Demo**  
Still buggy right now but it works with the Revit sample file `Technical_school-current_m.rvt`  
![Snapshot](https://i.postimg.cc/KvZ04tGG/interface.jpg)

**Note**  
There is something I though might be useful for beginners with the Revit addin development. I'll leave the notes here.
- All WPF windows interit from one basewindow class where the border and title bar are customized. You may refer to `BaseWindow.cs` and `BaseWindowStyle.xaml` and see how that works.
- We use `Properties.Settings.Default` to cache some user settings and bind them in XAML textbox.
- External events are called by clicking the button on WPF window. Likely, with `Delegate` and `Dispatcher`, a progressbar is added to the main window monitoring the processing of an external event. `DoEvent` is not optimal. Here's a greate sample of this kind of progressbar: [engthiago/WPFMonitorRevitAPI](https://github.com/engthiago/WPFMonitorRevitAPI)
- Several ways to implement the embedded Web browser. 1. Using CefSharp in line with Revit and call local website dynamically (in .cs), but I don't know how to pass variable to JavaScript because there seems a conflict between addin and Revit when initializing a browser. 2. Still with CefSharp but initializing local website within XAML. This is the current solution. Without the data communication users have to load XML manually which is dumb. 3. Try other C# dependencies like DotNetBrowser that has no conflicts with Revit (Not free). 4. Open up another process for addin's CefSharp. Refer to [Kim Sivonen](https://forums.autodesk.com/t5/revit-api-forum/revit-2019-1-add-in-and-cefsharp-library/td-p/8205740)

If you got better solution please let me know Many thanks.  