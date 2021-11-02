#region Namespaces
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Gingerbread.Core;
#endregion

namespace Gingerbread
{
    class BatchGeometry
    {
        // declare gbLevel class for private use
        private class gbLevel
        {
            public ElementId id;
            public string name;
            public double elevation;
            public double height;
            public gbLevel(ElementId id, string name, double elevation)
            { this.id = id; this.name = name; this.elevation = elevation; this.height = 0; }
        };

        public static void Execute(Document doc, 
            out Dictionary<int, Tuple<string, double>> dictElevation,
            out Dictionary<int, List<gbSeg>> dictWall,
            out Dictionary<int, List<gbSeg>> dictCurtain, 
            out Dictionary<int, List<Tuple<gbXYZ, string>>> dictColumn,
            out Dictionary<int, List<Tuple<gbXYZ, string>>> dictWindow,
            out Dictionary<int, List<Tuple<gbXYZ, string>>> dictDoor)
        {
            // initiate variables for output
            dictElevation = new Dictionary<int, Tuple<string, double>>();
            dictWall = new Dictionary<int, List<gbSeg>>();
            dictCurtain = new Dictionary<int, List<gbSeg>>();
            dictColumn = new Dictionary<int, List<Tuple<gbXYZ, string>>>();
            dictWindow = new Dictionary<int, List<Tuple<gbXYZ, string>>>();
            dictDoor = new Dictionary<int, List<Tuple<gbXYZ, string>>>();


            // batch levels for iteration
            List<gbLevel> levels = new List<gbLevel>();

            // prefix the variables that are elements with e-. same rule to the rest
            // get all floors
            IList<Element> eFloors = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WhereElementIsNotElementType()
                .ToElements();
            foreach (Element e in eFloors)
            {
                Level level = doc.GetElement(e.LevelId) as Level;
                gbLevel l = new gbLevel(e.LevelId, level.Name, level.Elevation);
                if (!levels.Contains(l))
                    levels.Add(l);
            }

            // get all roofbases
            IList<Element> eRoofs = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Roofs)
                .WhereElementIsNotElementType()
                .ToElements();
            foreach (Element e in eRoofs)
            {
                Level level = doc.GetElement(e.LevelId) as Level;
                gbLevel l = new gbLevel(e.LevelId, level.Name, level.Elevation);
                if (!levels.Contains(l))
                    levels.Add(l);
            }
            levels = levels.OrderBy(z => z.elevation).ToList(); //升序
            // assign height to each level (only > 2500 mm )
            // note that the embeded unit of Revit is foot, so you must do the conversion
            for (int i = 0; i < levels.Count - 1; i++)
            {
                double deltaZ = levels[i + 1].elevation - levels[i].elevation;
                //if (deltaZ > Util.MmToFoot(2500))
                levels[i].height = deltaZ;
            }
            // skim out levels with 0 height
            for (int i = levels.Count - 1; i >= 0; i--)
                if (levels[i].height == 0)
                    levels.RemoveAt(i);

            

            // iterate each floor to append familyinstance information to the dictionary
            for (int z = 0; z < levels.Count; z++)
            {
                // reusable filters are declared here (level filter for example)
                ElementLevelFilter levelFilter = new ElementLevelFilter(levels[z].id);

                // append to dictElevation
                dictElevation.Add(z, new Tuple<string, double>(
                    levels[z].name, 
                    Math.Round(Util.FootToM(levels[z].elevation), 3)
                    ));
                dictWall.Add(z, new List<gbSeg>());
                dictCurtain.Add(z, new List<gbSeg>());

                // append to dictColumn
                List<Tuple<gbXYZ, string>> columnLocs = new List<Tuple<gbXYZ, string>>();
                ElementMulticategoryFilter bothColumnFilter = new ElementMulticategoryFilter(
                    new List<BuiltInCategory> { BuiltInCategory.OST_Columns, BuiltInCategory.OST_StructuralColumns });
                IList<Element> eColumns = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .WherePasses(levelFilter)
                    .WherePasses(bothColumnFilter)
                    .ToElements();
                foreach (Element e in eColumns)
                {
                    FamilyInstance c = e as FamilyInstance;
                    XYZ lp = Util.GetFamilyInstanceLocation(c);
                    columnLocs.Add(new Tuple<gbXYZ, string>(Util.gbXYZConvert(lp), c.Name));
                }
                dictColumn.Add(z, columnLocs);

                // append to dictDoor
                List<Tuple<gbXYZ, string>> doorLocs = new List<Tuple<gbXYZ, string>>();
                IList<Element> eDoors = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .WherePasses(levelFilter)
                    .ToElements();
                foreach (Element e in eDoors)
                {
                    FamilyInstance d = e as FamilyInstance;
                    Wall wall = d.Host as Wall;
                    if (wall.WallType.Kind != WallKind.Curtain)
                    {
                        XYZ lp = Util.GetFamilyInstanceLocation(d);
                        doorLocs.Add(new Tuple<gbXYZ, string>(Util.gbXYZConvert(lp), d.Name));
                    }
                }
                dictDoor.Add(z, doorLocs);

                // append to dictWindow
                List<Tuple<gbXYZ, string>> windowLocs = new List<Tuple<gbXYZ, string>>();
                IList<Element> eWindows = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .OfCategory(BuiltInCategory.OST_Windows)
                    .WherePasses(levelFilter)
                    .ToElements();
                foreach (Element e in eWindows)
                {
                    FamilyInstance w = e as FamilyInstance;
                    XYZ lp = Util.GetFamilyInstanceLocation(w);
                    windowLocs.Add(new Tuple<gbXYZ, string>(Util.gbXYZConvert(lp), w.Name));
                }
                dictWindow.Add(z, windowLocs);
            }


            // allocate wall information to each floor
            IList<Element> eWalls = new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .OfCategory(BuiltInCategory.OST_Walls)
                .ToElements();

            foreach (Element e in eWalls)
            {
                List<gbSeg> temps = new List<gbSeg>();

                Wall wall = e as Wall;
                LocationCurve lc = wall.Location as LocationCurve;
                // convert Revit.DB.Line to Gingerbread.gbSeg
                if (lc.Curve is Line)
                    temps.Add(Util.gbSegConvert(lc.Curve as Line));
                // tessellate and simplify the curve to polyline by Douglas-Peucker algorithm
                else
                {
                    List<XYZ> pts = new List<XYZ>(lc.Curve.Tessellate());
                    List<XYZ> midPts = CurveSimplify.DouglasPeuckerReduction(pts, Util.MmToFoot(1000));
                    for (int i = 0; i < midPts.Count - 1; i++)
                        temps.Add(
                            new gbSeg(Util.gbXYZConvert(midPts[i]), Util.gbXYZConvert(midPts[i + 1])));
                }

                // get the height of the wall by retrieving its geometry element
                Options op = wall.Document.Application.Create.NewGeometryOptions();
                GeometryElement ge = wall.get_Geometry(op);
                double summit = ge.GetBoundingBox().Max.Z;
                double bottom = ge.GetBoundingBox().Min.Z;
                for (int i = 0; i < levels.Count; i++)
                {
                    // add location lines if the wall lies within the range of this level
                    if (wall.LevelId == levels[i].id || 
                       summit >= (levels[i].elevation + 0.5 * levels[i].height) &&
                       bottom <= (levels[i].elevation + 0.5 * levels[i].height))
                    {
                        dictWall[i].AddRange(temps);
                        // additionally, if the walltype is curtainwall, append it to dictCurtain
                        if (wall.WallType.Kind == WallKind.Curtain)
                            dictCurtain[i].AddRange(temps);
                    }
                }
            }

            // DEBUG
            string outputInfo = "";
            for (int i = 0; i < levels.Count; i++)
            {
                outputInfo += $"#{i} '{dictElevation[i].Item1}' elevation-{dictElevation[i].Item2} \n";
                outputInfo += $"    numCol-{dictColumn[i].Count} numWin-{dictWindow[i].Count} numDoor-{dictDoor[i].Count} \n";
                outputInfo += $"    numWall-{dictWall[i].Count} including curtianwall-{dictCurtain[i].Count} \n";
            }
            outputInfo += "Done model check.";
            Properties.Settings.Default.checkInfo = outputInfo;

            // feedback
            //System.Windows.MessageBox.Show("Information pre-processed.", "Info");
            return;
        }
    }
}