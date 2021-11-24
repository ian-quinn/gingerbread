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
            out Dictionary<int, List<Tuple<gbXYZ, string>>> dictDoor,
            out Dictionary<int, List<gbSeg>> dictSeparationline,
            out Dictionary<int, List<gbSeg>> dictGrid,
            out Dictionary<int, List<gbXYZ>> dictRoom,
            out Dictionary<int, List<List<List<gbSeg>>>> dictFloor,
            out Dictionary<string, List<Tuple<string, double>>> dictWindowplus,
            out Dictionary<string, List<Tuple<string, double>>> dictDoorplus)
        {
            // initiate variables for output
            dictElevation = new Dictionary<int, Tuple<string, double>>();
            dictWall = new Dictionary<int, List<gbSeg>>();
            dictCurtain = new Dictionary<int, List<gbSeg>>();
            dictColumn = new Dictionary<int, List<Tuple<gbXYZ, string>>>();
            dictWindow = new Dictionary<int, List<Tuple<gbXYZ, string>>>();
            dictDoor = new Dictionary<int, List<Tuple<gbXYZ, string>>>();
            dictSeparationline = new Dictionary<int, List<gbSeg>>();
            dictGrid = new Dictionary<int, List<gbSeg>>();
            dictRoom = new Dictionary<int, List<gbXYZ>>();
            dictWindowplus = new Dictionary<string, List<Tuple<string, double>>>();
            dictDoorplus = new Dictionary<string, List<Tuple<string, double>>>();
            //List<List<List<gbSeg>>> nestedFloor = new List<List<List<gbSeg>>>();
            dictFloor = new Dictionary<int, List<List<List<gbSeg>>>>();


            // batch levels for iteration
            List<gbLevel> levels = new List<gbLevel>();

            // prefix the variables that are elements with e-. same rule to the rest
            // get all floors

            ProjectInfo projectInfo = doc.ProjectInformation;
            Dictionary<string, string> dictproinfo = new Dictionary<string, string>();
            dictproinfo.Add("OrganizationDescription", projectInfo.OrganizationDescription);
            dictproinfo.Add("OrganizationName", projectInfo.OrganizationName);
            dictproinfo.Add("BuildingName", projectInfo.BuildingName);
            dictproinfo.Add("Author", projectInfo.Author);
            dictproinfo.Add("Number", projectInfo.Number);
            dictproinfo.Add("Name", projectInfo.Name);
            dictproinfo.Add("Address", projectInfo.Address);
            dictproinfo.Add("ClientName", projectInfo.ClientName);
            dictproinfo.Add("Status", projectInfo.Status);
            dictproinfo.Add("IssueDate", projectInfo.IssueDate);

            Properties.Settings.Default.OrganizationDescription = projectInfo.OrganizationDescription;
            Properties.Settings.Default.OrganizationName = projectInfo.OrganizationName;
            Properties.Settings.Default.BuildingName = projectInfo.BuildingName;
            Properties.Settings.Default.Author = projectInfo.Author;
            Properties.Settings.Default.Number = projectInfo.Number;
            Properties.Settings.Default.Name = projectInfo.Name;
            Properties.Settings.Default.Address = projectInfo.Address;
            Properties.Settings.Default.ClientName = projectInfo.ClientName;
            Properties.Settings.Default.Status = projectInfo.Status;
            Properties.Settings.Default.IssueDate = projectInfo.IssueDate;
            Properties.Settings.Default.Save();

            IList<Element> eWindowplus = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .OfCategory(BuiltInCategory.OST_Windows)
                    .ToElements();
            foreach (Element e in eWindowplus)
            {
                List<Tuple<string, double>> properties = new List<Tuple<string, double>>();
                FamilyInstance f = e as FamilyInstance;
                string name = f.Name;
                FamilySymbol fs = f.Symbol;
                double height = fs.get_Parameter(BuiltInParameter.WINDOW_HEIGHT).AsDouble();
                properties.Add(new Tuple<string, double>("heitht", height));
                double width = fs.get_Parameter(BuiltInParameter.WINDOW_WIDTH).AsDouble();
                properties.Add(new Tuple<string, double>("width", width));
                FamilyThermalProperties ft = fs.GetThermalProperties();
                double value_r = ft.ThermalResistance;
                properties.Add(new Tuple<string, double>("value_r", value_r));
                double value_k = ft.HeatTransferCoefficient;
                properties.Add(new Tuple<string, double>("value_k", value_k));
                if (!dictWindowplus.Keys.Contains(name))
                    dictWindowplus.Add(name, properties);
            }

            IList<Element> eDoorplus = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .ToElements();
            foreach (Element e in eDoorplus)
            {
                List<Tuple<string, double>> properties = new List<Tuple<string, double>>();
                FamilyInstance f = e as FamilyInstance;
                string name = f.Name;
                FamilySymbol fs = f.Symbol;
                double height = fs.get_Parameter(BuiltInParameter.DOOR_HEIGHT).AsDouble();
                properties.Add(new Tuple<string, double>("heitht", height));
                double width = fs.get_Parameter(BuiltInParameter.DOOR_WIDTH).AsDouble();
                properties.Add(new Tuple<string, double>("width", width));
                FamilyThermalProperties ft = fs.GetThermalProperties();
                double value_r = ft.ThermalResistance;
                properties.Add(new Tuple<string, double>("value_r", value_r));
                double value_k = ft.HeatTransferCoefficient;
                properties.Add(new Tuple<string, double>("value_k", value_k));
                if (!dictDoorplus.Keys.Contains(name))
                    dictDoorplus.Add(name, properties);
            }

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
                if (deltaZ > Util.MmToFoot(2500))
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
                dictColumn.Add(z, new List<Tuple<gbXYZ, string>>());
                dictWindow.Add(z, new List<Tuple<gbXYZ, string>>());
                dictCurtain.Add(z, new List<gbSeg>());

                List<List<List<gbSeg>>> nestedFloor = new List<List<List<gbSeg>>>();
                IList<Element> efloors = new FilteredElementCollector(doc)
                 .OfCategory(BuiltInCategory.OST_Floors)
                 .WherePasses(levelFilter)
                 .WhereElementIsNotElementType()
                 .ToElements();
                foreach (Element e in efloors)
                {
                    List<List<gbSeg>> floorloops = new List<List<gbSeg>>();
                    Options op = e.Document.Application.Create.NewGeometryOptions();
                    GeometryElement ge = e.get_Geometry(op);
                    foreach (GeometryObject geomObj in ge)
                    {
                        Solid geomSolid = geomObj as Solid;
                        if (null != geomObj)
                        {
                            foreach (Face geomFace in geomSolid.Faces)
                            {
                                PlanarFace planar = geomFace as PlanarFace;
                                Debug.Print($"({geomFace.Area})");
                                if (planar != null)
                                {
                                    Debug.Print("if");
                                    Debug.Print($"({planar.Origin.X})");
                                    Debug.Print($"({planar.FaceNormal.Z})");

                                    if (planar.FaceNormal.Z == 1)
                                    {
                                        foreach (EdgeArray edgeArray in geomFace.EdgeLoops)
                                        {
                                            List<gbSeg> floorloop = new List<gbSeg>();
                                            foreach (Edge edge in edgeArray)
                                            {
                                                XYZ ptStart = edge.AsCurve().GetEndPoint(0);
                                                XYZ ptEnd = edge.AsCurve().GetEndPoint(1);
                                                gbSeg newSeg = new gbSeg(Util.gbXYZConvert(ptStart), Util.gbXYZConvert(ptEnd));
                                                floorloop.Add(newSeg);
                                                Debug.Print($"Seg: {newSeg.PointAt(0)} - {newSeg.PointAt(1)}");
                                            }
                                            floorloops.Add(floorloop);
                                            Debug.Print("floorloops " + floorloop.Count);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    nestedFloor.Add(floorloops);
                    Debug.Print("nestedFloor");
                }
                dictFloor.Add(z, nestedFloor);


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


                List<gbSeg> separationlineLocs = new List<gbSeg>();
                IList<Element> eSeparationlines = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_RoomSeparationLines)
                    .WherePasses(levelFilter)
                    .ToElements();
                foreach (Element e in eSeparationlines)
                {
                    LocationCurve lc = e.Location as LocationCurve;
                    if (lc.Curve is Line)
                    {
                        Line gridLine = lc.Curve as Line;
                        XYZ p1 = gridLine.GetEndPoint(0);
                        XYZ p2 = gridLine.GetEndPoint(1);
                        gbXYZ gbP1 = new gbXYZ(Util.FootToM(p1.X), Util.FootToM(p1.Y), Util.FootToM(p1.Z));
                        gbXYZ gbP2 = new gbXYZ(Util.FootToM(p2.X), Util.FootToM(p2.Y), Util.FootToM(p2.Z));
                        separationlineLocs.Add(new gbSeg(gbP1, gbP2));
                    }

                }
                dictSeparationline.Add(z, separationlineLocs);

                List<gbXYZ> roomlocs = new List<gbXYZ>();
                IList<Element> eRooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WherePasses(levelFilter)
                    .ToElements();
                foreach (Element e in eRooms)
                {
                    Debug.Print("have eRooms");
                    LocationPoint lp = e.Location as LocationPoint;
                    XYZ pt = lp.Point;
                    roomlocs.Add(Util.gbXYZConvert(pt));
                }
                dictRoom.Add(z, roomlocs);
            }

            List<gbSeg> gridLocs = new List<gbSeg>();
            IList<Element> eGrids = new FilteredElementCollector(doc)
                .OfClass(typeof(Grid))
                .OfCategory(BuiltInCategory.OST_Grids)
                .ToElements();
            foreach (Element e in eGrids)
            {
                Grid grid = e as Grid;
                if (!grid.IsCurved)
                {
                    Line gridLine = grid.Curve as Line;
                    XYZ p1 = gridLine.GetEndPoint(0);
                    XYZ p2 = gridLine.GetEndPoint(1);
                    gbXYZ gbP1 = new gbXYZ(Util.FootToM(p1.X), Util.FootToM(p1.Y), Util.FootToM(p1.Z));
                    gbXYZ gbP2 = new gbXYZ(Util.FootToM(p2.X), Util.FootToM(p2.Y), Util.FootToM(p2.Z));
                    gridLocs.Add(new gbSeg(gbP1, gbP2));
                }
            }
            dictGrid.Add(0, gridLocs);

            using (Transaction tx = new Transaction(doc, "Sketch locations"))
            {
                tx.Start();
                // Util.SketchSegs(doc, lineShatters);
                Debug.Print("dictGrid " + dictGrid[0].Count);
                Debug.Print("dictRoom " + dictRoom[0].Count);
                Debug.Print("dictSeparationline " + dictSeparationline[0].Count);
                foreach (gbXYZ lp in dictRoom[0])
                {
                    Util.SketchMarker(doc, new XYZ(lp.X, lp.Y, 0));
                    Debug.Print("LP sketched.");
                }
                foreach (gbSeg seg in dictGrid[0])
                {
                    Util.SketchSegs(doc, new List<gbSeg>() { seg });
                    Debug.Print("dictGrid sketched.");
                }
                foreach (gbSeg line in dictSeparationline[0])
                {
                    Curve zline = Line.CreateBound(ProjectZPoint(line.PointAt(0)), ProjectZPoint(line.PointAt(1))) as Curve;
                    Util.SketchCurves(doc, new List<Curve>() { zline });
                    Debug.Print("dictSeparationline sketched.");
                }
                tx.Commit();
            }

            XYZ ProjectZPoint(gbXYZ pt)
            {
                return new XYZ(pt.X, pt.Y, 0);
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


            ElementMulticategoryFilter bothColumnFilter = new ElementMulticategoryFilter(
                new List<BuiltInCategory> { BuiltInCategory.OST_Columns, BuiltInCategory.OST_StructuralColumns });
            IList<Element> eColumns = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .WherePasses(bothColumnFilter)
                .ToElements();
            foreach (Element e in eColumns)
            {
                FamilyInstance c = e as FamilyInstance;
                XYZ lp = Util.GetFamilyInstanceLocation(c);
                List<Tuple<gbXYZ, string>> columnLocs = new List<Tuple<gbXYZ, string>>();
                columnLocs.Add(new Tuple<gbXYZ, string>(Util.gbXYZConvert(lp), c.Name));

                Options op = c.Document.Application.Create.NewGeometryOptions();
                GeometryElement ge = c.get_Geometry(op);
                double summit = ge.GetBoundingBox().Max.Z;
                double bottom = ge.GetBoundingBox().Min.Z;
                for (int i = 0; i < levels.Count; i++)
                {
                    if (summit >= levels[i].elevation && bottom <= levels[i].elevation)
                        dictColumn[i].AddRange(columnLocs);

                    else if (i < levels.Count - 1 && bottom >= levels[i].elevation && bottom < levels[i + 1].elevation)
                        dictColumn[i].AddRange(columnLocs);

                    else if ((i == levels.Count - 1) && (bottom >= levels[i].elevation))
                        dictColumn[i].AddRange(columnLocs);
                }
            }


            IList<Element> eWindows = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_Windows)
                .ToElements();
            foreach (Element e in eWindows)
            {
                FamilyInstance w = e as FamilyInstance;
                XYZ lp = Util.GetFamilyInstanceLocation(w);
                List<Tuple<gbXYZ, string>> windowLocs = new List<Tuple<gbXYZ, string>>();
                windowLocs.Add(new Tuple<gbXYZ, string>(Util.gbXYZConvert(lp), w.Name));

                Options op = w.Document.Application.Create.NewGeometryOptions();
                GeometryElement ge = w.get_Geometry(op);
                double summit = ge.GetBoundingBox().Max.Z;
                double bottom = ge.GetBoundingBox().Min.Z;
                for (int i = 0; i < levels.Count; i++)
                {
                    if (summit >= levels[i].elevation && bottom <= levels[i].elevation)
                        dictWindow[i].AddRange(windowLocs);

                    else if (i < levels.Count - 1 && bottom >= levels[i].elevation && bottom < levels[i + 1].elevation)
                        dictWindow[i].AddRange(windowLocs);

                    else if ((i == levels.Count - 1) && (bottom >= levels[i].elevation))
                        dictWindow[i].AddRange(windowLocs);
                }
            }

            for (int i = 0; i < levels.Count; i++)
            {
                System.Windows.MessageBox.Show(i.ToString() + "\n" + "Column " + dictColumn[i].Count + "\n" + "Window" + dictWindow[i].Count, "Info");
            }

            // feedback
            System.Windows.MessageBox.Show("Information pre-processed.", "Info");
            return;
        }
    }
}