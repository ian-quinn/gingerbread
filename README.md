# gingerbread :rice_cracker:

![Revit API](https://img.shields.io/badge/Revit%20API-2020-red.svg)
![.NET](https://img.shields.io/badge/.NET-4.7-red.svg)

A lightweight gbXML export module for Revit, WIP. Sometimes we find the Revit model much too complex for an accurate and lightweight gbXML export. There may be broken space boundaries, shattered surfaces, or tiny twisted patches, thus leading to failures in the building energy simulation. Here in this toy project we try to create a simple BREP-like space crafting addin with fuzzy space detection and simplification, aside from the native gbXML export module either based on room/space definition and energy analysis model. However, we've just started and there won't come the first release very soon. Hope this toy yield some practical algorithms and don't fall back to some duplication of efforts

```
gingerbread
├ /Demo
│ ├ *.rvt  - RVT file for testing
│ └ *.gif  - Example movie
└ /Gingerbread
  ├ /Properties  - Assembly info
  ├ /Resources
  │ ├ /ico  - Button icon files
  ├ /Core  - Core algorithms
  ├ App.cs  - Main entry
  ├ Util*.cs  - Utility methods
  └ Gingerbread.addin  - Application manifest
```

<img src="/Demo/Screenshot.png?raw=true">