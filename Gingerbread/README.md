# Code Covenant

**Class** Algorithms all cached in Core folder. Use CamelCase. Manipulation verb after the subject none, or just use standard shorty of the algorithm such as HertelMelhorn, StraightSkeleton. Use `Cmd` to prefix Revit command entrance, `Ext` to prefix external command called by XAML. All classes prefixed with `gb` is reserved by GingerBread (also as GreenBuilding). For now I use `gbXYZ` `gbSeg` `gbRegion` to help with geometric calculation, and `gbLevel` `gbFloor` `gbZone` `gbSurface` `gbOpening` for gbXML serialization.

**Geometry id** Classify the geometry by its floor-group-zone and label it accordingly with `::` as separation (same convention with Honeybee), such as "F0::G1::Z1::Wall_3"  

**Debug.** Try to prefix the message printed to the console with the name of current function or class. Same rule goes with the `LogPrint` method.  