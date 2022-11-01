#region Namespaces
using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Gingerbread.Core;
#endregion

// PENDING      - functions saved for a happy day
// ABANDONED    - obsolete functions 

namespace Gingerbread
{
    class BatchGeometry
    {
        // Private class, levelPack for convenient
        private class levelPack
        {
            public ElementId id;
            public string name;
            public double elevation;
            public double height;
            // the levelPack caches data in Imperial not Metric
            public levelPack(ElementId id, string name, double elevation)
            { this.id = id; this.name = name; this.elevation = elevation; this.height = 0; }
        };

        public static void Execute(Document doc, 
            out Dictionary<int, Tuple<string, double>> dictElevation,
            out Dictionary<int, List<gbSeg>> dictWall,
            out Dictionary<int, List<gbSeg>> dictCurtain,
            out Dictionary<int, List<gbSeg>> dictCurtaSystem, 
            out Dictionary<int, List<Tuple<List<gbXYZ>, string>>> dictColumn,
            out Dictionary<int, List<Tuple<gbSeg, string>>> dictBeam,
            out Dictionary<int, List<Tuple<gbXYZ, string>>> dictWindow,
            out Dictionary<int, List<Tuple<gbXYZ, string>>> dictDoor,
            out Dictionary<int, List<List<List<gbXYZ>>>> dictFloor,
            out Dictionary<int, List<List<gbXYZ>>> dictShade, 
            out Dictionary<int, List<gbSeg>> dictSeparationline,
            out Dictionary<int, List<gbSeg>> dictGrid,
            out Dictionary<int, List<Tuple<List<List<gbXYZ>>, string>>> dictRoom,
            out Dictionary<string, List<Tuple<string, double>>> dictWindowplus,
            out Dictionary<string, List<Tuple<string, double>>> dictDoorplus,
            out string checkInfo)
        {
            // initiate variables for output
            dictElevation = new Dictionary<int, Tuple<string, double>>();
            dictWall = new Dictionary<int, List<gbSeg>>();
            dictCurtain = new Dictionary<int, List<gbSeg>>();
            dictCurtaSystem = new Dictionary<int, List<gbSeg>>();
            dictColumn = new Dictionary<int, List<Tuple<List<gbXYZ>, string>>>();
            dictBeam = new Dictionary<int, List<Tuple<gbSeg, string>>>();
            dictWindow = new Dictionary<int, List<Tuple<gbXYZ, string>>>();
            dictDoor = new Dictionary<int, List<Tuple<gbXYZ, string>>>();
            dictFloor = new Dictionary<int, List<List<List<gbXYZ>>>>();
            dictShade = new Dictionary<int, List<List<gbXYZ>>>();
            dictSeparationline = new Dictionary<int, List<gbSeg>>();
            dictGrid = new Dictionary<int, List<gbSeg>>();
            dictRoom = new Dictionary<int, List<Tuple<List<List<gbXYZ>>, string>>>();
            dictWindowplus = new Dictionary<string, List<Tuple<string, double>>>();
            dictDoorplus = new Dictionary<string, List<Tuple<string, double>>>();
            // retrieve all linked documents
            List<Document> refDocs = new List<Document>();
            if (Properties.Settings.Default.includeRef)
                refDocs = Util.GetLinkedDocuments(doc).ToList();
            List<Document> allDocs = new List<Document>() { doc };
            allDocs.AddRange(refDocs);

            // batch levels for iteration
            List<levelPack> levels = new List<levelPack>();


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
                properties.Add(new Tuple<string, double>("height", height));
                double width = fs.get_Parameter(BuiltInParameter.WINDOW_WIDTH).AsDouble();
                properties.Add(new Tuple<string, double>("width", width));
                if (fs.HasThermalProperties())
                {
                    FamilyThermalProperties ft = fs.GetThermalProperties();
                    if (ft == null)
                    {
                        double value_r = 0;
                        properties.Add(new Tuple<string, double>("value_r", value_r));
                        double value_k = 0;
                        properties.Add(new Tuple<string, double>("value_k", value_k));
                    }
                    else
                    {
                        double value_r = ft.ThermalResistance;
                        properties.Add(new Tuple<string, double>("value_r", value_r));
                        double value_k = ft.HeatTransferCoefficient;
                        properties.Add(new Tuple<string, double>("value_k", value_k));
                    }

                }
                else
                {
                    double value_r = 0;
                    properties.Add(new Tuple<string, double>("value_r", value_r));
                    double value_k = 0;
                    properties.Add(new Tuple<string, double>("value_k", value_k));
                }
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
                properties.Add(new Tuple<string, double>("height", height));
                double width = fs.get_Parameter(BuiltInParameter.DOOR_WIDTH).AsDouble();
                properties.Add(new Tuple<string, double>("width", width));
                if (fs.HasThermalProperties())
                {
                    FamilyThermalProperties ft = fs.GetThermalProperties();
                    if (ft == null)
                    {
                        double value_r = 0;
                        properties.Add(new Tuple<string, double>("value_r", value_r));
                        double value_k = 0;
                        properties.Add(new Tuple<string, double>("value_k", value_k));
                    }
                    else
                    {
                        double value_r = ft.ThermalResistance;
                        properties.Add(new Tuple<string, double>("value_r", value_r));
                        double value_k = ft.HeatTransferCoefficient;
                        properties.Add(new Tuple<string, double>("value_k", value_k));
                    }

                }
                else
                {
                    double value_r = 0;
                    properties.Add(new Tuple<string, double>("value_r", value_r));
                    double value_k = 0;
                    properties.Add(new Tuple<string, double>("value_k", value_k));
                }
                if (!dictDoorplus.Keys.Contains(name))
                    dictDoorplus.Add(name, properties);
            }


            // prefix the variables that are elements with e-. same rule to the rest
            // get all floors

            //IList<Element> _eFloors = new FilteredElementCollector(doc)
            //    .OfCategory(BuiltInCategory.OST_Floors)
            //    .WhereElementIsNotElementType()
            //    .ToElements();

            //foreach (Element e in _eFloors)
            //{
            //    Level level = doc.GetElement(e.LevelId) as Level;
            //    if (level == null)
            //        continue;
            //    levelPack l = new levelPack(e.LevelId, level.Name, level.Elevation);
            //    if (!levels.Contains(l))
            //        levels.Add(l);
            //}

            // get all roof bases

            //IList<Element> _eRoofs = new FilteredElementCollector(doc)
            //    .OfCategory(BuiltInCategory.OST_Roofs)
            //    .WhereElementIsNotElementType()
            //    .ToElements();

            //foreach (Element e in _eRoofs)
            //{
            //    Level level = doc.GetElement(e.LevelId) as Level;
            //    if (level == null)
            //        continue;
            //    levelPack l = new levelPack(e.LevelId, level.Name, level.Elevation);
            //    if (!levels.Contains(l))
            //        levels.Add(l);
            //}

            List<double> elevationCache = new List<double>() { };

            foreach (Document someDoc in allDocs)
            {
                IList<Element> _eFloors = new FilteredElementCollector(someDoc)
                    .OfCategory(BuiltInCategory.OST_Floors)
                    .WhereElementIsNotElementType()
                    .ToElements();
                foreach (Element e in _eFloors)
                {
                    Level level = someDoc.GetElement(e.LevelId) as Level;
                    if (level == null)
                        continue;
                    levelPack l = new levelPack(e.LevelId, level.Name, level.Elevation);
                    if (!levels.Contains(l) && CheckSimilarity(level.Elevation, Util.MmToFoot(100), elevationCache))
                    {
                        levels.Add(l);
                        elevationCache.Add(level.Elevation);
                    }
                }

                IList<Element> _eRoofs = new FilteredElementCollector(someDoc)
                    .OfCategory(BuiltInCategory.OST_Roofs)
                    .WhereElementIsNotElementType()
                    .ToElements();
                foreach (Element e in _eRoofs)
                {
                    Level level = someDoc.GetElement(e.LevelId) as Level;
                    if (level == null)
                        continue;
                    levelPack l = new levelPack(e.LevelId, level.Name, level.Elevation);
                    if (!levels.Contains(l) && CheckSimilarity(level.Elevation, Util.MmToFoot(100), elevationCache))
                    {
                        levels.Add(l);
                        elevationCache.Add(level.Elevation);
                    }
                }
            }

            
            levels = levels.OrderBy(z => z.elevation).ToList(); // ascending order
            // assign height to each level (only > 2000 mm )
            // note that the embedded unit of Revit is foot, so you must do the conversion
            for (int i = 0; i < levels.Count - 1; i++)
            {
                double deltaZ = levels[i + 1].elevation - levels[i].elevation;
                if (deltaZ > Util.MmToFoot(2000))
                    levels[i].height = deltaZ;
            }
            // skim out levels with 0 height
            for (int i = levels.Count - 1; i >= 0; i--)
                if (levels[i].height == 0)
                    levels.RemoveAt(i);


            IList<Element> eLevels = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.INVALID)
                .OfClass(typeof(Level))
                .ToElements();
            foreach (Element eLevel in eLevels)
            {
                Level level = eLevel as Level;
                if (Math.Abs(level.Elevation - levels.Last().elevation - levels.Last().height) < 0.01)
                {
                    levels.Add(new levelPack(level.Id, level.Name, level.Elevation));
                    break;
                }
            }


            IList<Element> eCurtaSys = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_CurtaSystem)
                .ToElements();
            //Debug.Print($"BatchGeometry:: We got {eCurtaSys.Count} curtain system elements");

            List<List<Line>> gridClusters = new List<List<Line>>();
            foreach (Element e in eCurtaSys)
            {
                if (e is null)
                    continue;
                CurtainSystem cs = e as CurtainSystem;
                if (cs is null)
                    continue;
                List<Line> gridCluster = new List<Line>();
                //Debug.Write($"BatchGeometry:: The curtainGridSet- ");
                foreach (CurtainGrid cg in cs.CurtainGrids)
                {
                    //Debug.Write($" X ");
                    //List<ElementId> vIds = cg.GetVGridLineIds().ToList();
                    //for (int v = 0; v < vIds.Count; v++)
                    //{
                    //    CurtainGridLine cgLine = doc.GetElement(vIds[v]) as CurtainGridLine;
                    //    Curve gl = cgLine.FullCurve.Clone();
                    //    gridCluster.Add(gl);
                    //}
                    gridCluster.AddRange(Util.GetCurtainGridVerticalLattice(doc, cg));
                }
                //Debug.Write($"\n");
                gridClusters.Add(gridCluster);
            }
            //Debug.Print($"BatchGeometry:: We got {gridClusters.Count} curtainGrids");

            // get all roomtags for room extraction
            IList<Element> eRoomTags = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_RoomTags)
                .ToElements();

            // iterate each floor to append the FamilyInstance information to the dictionary
            // targeting: dictElevation, dictDoor, dictSeparationline, dictRoom, dictCurtaSystem
            for (int z = 0; z < levels.Count; z++)
            {
                // reusable filters are declared here (level filter for example)
                ElementLevelFilter levelFilter = new ElementLevelFilter(levels[z].id);

                // populate the dictElevation
                dictElevation.Add(z, new Tuple<string, double>(
                    levels[z].name,
                    Math.Round(Util.FootToM(levels[z].elevation), 3)
                    ));
                // initiate other dictionaries
                dictWall.Add(z, new List<gbSeg>());
                dictColumn.Add(z, new List<Tuple<List<gbXYZ>, string>>());
                dictBeam.Add(z, new List<Tuple<gbSeg, string>>());
                dictCurtain.Add(z, new List<gbSeg>());
                dictWindow.Add(z, new List<Tuple<gbXYZ, string>>());
                dictShade.Add(z, new List<List<gbXYZ>>());
                dictFloor.Add(z, new List<List<List<gbXYZ>>>());
                // initiation of the dictCurtaSystem is at the end of the loop


                // ABANDONED
                // populate the dictWindow
                //List<Tuple<gbXYZ, string>> windowLocs = new List<Tuple<gbXYZ, string>>();
                //IList<Element> eWindows = new FilteredElementCollector(doc)
                //    .OfClass(typeof(FamilyInstance))
                //    .OfCategory(BuiltInCategory.OST_Windows)
                //    .WherePasses(levelFilter)
                //    .ToElements();
                //foreach (Element e in eWindows)
                //{
                //    FamilyInstance w = e as FamilyInstance;
                //    FamilySymbol ws = w.Symbol;
                //    double height = Util.FootToMm(ws.get_Parameter(BuiltInParameter.WINDOW_HEIGHT).AsDouble());
                //    double width = Util.FootToMm(ws.get_Parameter(BuiltInParameter.WINDOW_WIDTH).AsDouble());
                //    XYZ lp = Util.GetFamilyInstanceLocation(w);
                //    if (lp is null)
                //        continue;
                //    windowLocs.Add(new Tuple<gbXYZ, string>(Util.gbXYZConvert(lp), $"{width:F0} x {height:F0}"));
                //}
                //dictWindow.Add(z, windowLocs);


                // populate the dictDoor
                // doors spanning multiple levels are not allowed
                List<Tuple<gbXYZ, string>> doorLocs = new List<Tuple<gbXYZ, string>>();
                IList<Element> eDoors = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilyInstance))
                    .OfCategory(BuiltInCategory.OST_Doors)
                    .WherePasses(levelFilter)
                    .ToElements();
                foreach (Element e in eDoors)
                {
                    FamilyInstance d = e as FamilyInstance;
                    FamilySymbol ds = d.Symbol;
                    double height = Util.FootToMm(ds.get_Parameter(BuiltInParameter.WINDOW_HEIGHT).AsDouble());
                    double width = Util.FootToMm(ds.get_Parameter(BuiltInParameter.WINDOW_WIDTH).AsDouble());
                    Wall wall = d.Host as Wall;
                    if (wall.WallType.Kind != WallKind.Curtain)
                    {
                        XYZ lp = Util.GetFamilyInstanceLocation(d);
                        if (lp is null)
                            continue;
                        doorLocs.Add(new Tuple<gbXYZ, string>(Util.gbXYZConvert(lp), $"{width:F0} x {height:F0}"));
                        //Debug.Print($"BatchGeometry:: F{z}: Got door at " + lp.ToString());
                    }
                }
                dictDoor.Add(z, doorLocs);


                // populate the room separation lines
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


                // add room location and label
                // better use the polygon boundary to represent the room
                // there can be risks using the visual centroid of a polygon
                List<Tuple<List<List<gbXYZ>>, string>> roomlocs = new List<Tuple<List<List<gbXYZ>>, string>>();
                List<Element> eRooms = new List<Element>();
                foreach (Element eTag in eRoomTags)
                {
                    RoomTag roomTag = eTag as RoomTag;
                    if (roomTag == null)
                        continue;
                    string tagName = roomTag.TagText;
                    Room room = roomTag.Room;
                    if (room.LevelId == levels[z].id)
                    {
                        List<List<gbXYZ>> boundaryLoops = new List<List<gbXYZ>>();
                        var roomBoundaries = room.GetBoundarySegments(new SpatialElementBoundaryOptions());
                        if (roomBoundaries.Count == 0)
                            continue;
                        // the boundary of a room is a nested list of points
                        // representing a simply connected region (List.count == 1) or
                        // multiply connected region (List.count > 1)
                        // considering the situation of corridor, it is better to 
                        // cache multiple loops inside one list, as representation of a room
                        foreach (var nestedSegments in roomBoundaries)
                        {
                            List<gbXYZ> boundaryLoop = new List<gbXYZ>();
                            foreach (BoundarySegment bs in nestedSegments)
                            {
                                Curve bc = bs.GetCurve();
                                XYZ pt = bc.GetEndPoint(0);
                                boundaryLoop.Add(Util.gbXYZConvert(pt));
                            }
                            boundaryLoop.Add(boundaryLoop[0]);
                            boundaryLoops.Add(boundaryLoop);
                        }
                        roomlocs.Add(new Tuple<List<List<gbXYZ>>, string>(boundaryLoops, tagName));
                    }
                }
                // ABANDONED
                /*IList<Element> eRooms = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WherePasses(levelFilter)
                    .ToElements();
                foreach (Element e in eRooms)
                {
                    LocationPoint lp = e.Location as LocationPoint;
                    XYZ pt = lp.Point;
                    roomlocs.Add(new Tuple<gbXYZ, string>(Util.gbXYZConvert(pt), "office"));
                }*/
                dictRoom.Add(z, roomlocs);


                // mark intersection point of curtain grid lines with each floor plane
                // add curtain system boundary to the dictionary
                Plane floorPlane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, levels[z].elevation));
                
                List<gbSeg> lcCrvs = new List<gbSeg>();
                foreach (List<Line> gridCluster in gridClusters)
                {
                    List<gbXYZ> lcPts = new List<gbXYZ>();
                    foreach (Line grid in gridCluster)
                    {
                        if (grid is null)
                            continue;
                        XYZ gridStart = grid.GetEndPoint(0);
                        XYZ gridEnd = grid.GetEndPoint(1);
                        //Debug.Print($"BatchGeometry:: girdType- {grid.GetType()}");

                        // by default, the grid line starts from the bottom and ends at the top
                        // note that the gridStart.Z may be slightly larger than levels[z].elevation due to double precision
                        // you have to round it to 0 then do the comparison
                        if (gridEnd.Z > levels[z].elevation + levels[z].height / 2 &&
                            gridStart.Z < levels[z].elevation || Util.IsZero(gridStart.Z - levels[z].elevation))
                        {
                            // when it comes to NURBS, you may need the following method to calculate
                            // the intersection between the plane and the curve. as robust as possible
                            //IntersectionResultArray ir = Basic.PlaneCurveIntersection(floorPlane, grid);
                            XYZ intersectPt = Basic.LineIntersectPlane(gridStart, gridEnd, levels[z].elevation);
                            if (intersectPt != null)
                                lcPts.Add(Util.gbXYZConvert(intersectPt));
                            //Debug.Print($"BatchGeometry:: Point ({intersectPt.X},{intersectPt.Y},{intersectPt.Z}) added");
                        }
                    }
                    // PENDING the simplify polyline method is buggy
                    //RegionTessellate.SimplifyPoly(lcPts);

                    if (lcPts.Count > 1)
                    {
                        // when it comes to NURBS, you need a polyline connecting each intersection point
                        //for (int i = 0; i < lcPts.Count - 1; i++)
                        //    lcCrvs.Add(new gbSeg(lcPts[i], lcPts[i + 1]));

                        lcCrvs.Add(new gbSeg(lcPts[0], lcPts[lcPts.Count - 1]));
                    }
                    //Debug.Print($"BatchGeometry:: Gen {lcCrvs.Count} segs from {gridCluster.Count} grids");
                }
                dictCurtaSystem.Add(z, lcCrvs);
            }

            // others are sorted by their actual geometry
            // not by the level attribute assigned to

            // find the corresponding level for each wall
            IList<Element> eWalls = new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .OfCategory(BuiltInCategory.OST_Walls)
                .ToElements();
            Debug.Print($"original wall number: {eWalls.Count}");

            List<Element> eWallsPlus = eWalls.ToList();

            foreach (Document refDoc in refDocs)
            {
                IList<Element> _eWalls = new FilteredElementCollector(refDoc)
                    .OfClass(typeof(Wall))
                    .OfCategory(BuiltInCategory.OST_Walls)
                    .ToElements();
                eWallsPlus.AddRange(_eWalls);
            }

            foreach (Element e in eWallsPlus)
            {
                List<gbSeg> temps = new List<gbSeg>();
                Wall wall = e as Wall;

                // access baseline by LocationCurve
                LocationCurve lc = wall.Location as LocationCurve;

                // convert Revit.DB.Line to Gingerbread.gbSeg
                if (lc.Curve is Line)
                    temps.Add(Util.gbSegConvert(lc.Curve as Line));
                // tessellate and simplify the curve to polyline by Douglas-Peucker algorithm
                else
                {
                    List<XYZ> pts = new List<XYZ>(lc.Curve.Tessellate());
                    // or more easier you may tessellate it manually
                    /*List<XYZ> pts = new List<XYZ>();
                    for (int i = 0; i < 3; i++)
                        pts.Add(lc.Curve.Evaluate(0.5 * i, true));
                    List<XYZ> midPts = pts;*/
                    List<XYZ> midPts = CurveSimplify.DouglasPeuckerReduction(pts, Util.MmToFoot(500));
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
                    //Debug.Print($"summit {summit} bottom {bottom} vs. lv^ " +
                    //    $"{levels[i].elevation + 0.8 * levels[i].height} lv_ {levels[i].elevation + 0.2 * levels[i].height}");

                    // mark the hosting level of a wall only by its geometry regardless of its level attribute
                    // PENDING  - this could be dangerous
                    // previous statement: (revised on 2022-?)
                    //wall.LevelId == levels[i].id || blahblah

                    // previous statement: (revised on 2022-08-10)
                    //summit >= levels[i].elevation + 0.5 * levels[i].height &&
                    //bottom <= levels[i].elevation + 0.1 * levels[i].height)
                    double spanCheck = Util.SpanOverlap(bottom, summit, levels[i].elevation,
                        levels[i].elevation + levels[i].height);
                    if (spanCheck > 0.5 * levels[i].height)
                    {
                            // if the WallType is CurtainWall, append it to dictCurtain
                            if (wall.WallType.Kind == WallKind.Curtain)
                        {
                            // check if the curtain acts like a window
                            // the height of the curtain wall should be almost equal
                            // or over the height of this level
                            // 0.2m gap ensures the strength of the structure, practical value
                            // PENDING  - this value may vary with projects
                            if (summit - bottom < levels[i].height - Util.MToFoot(0.2))
                            {
                                Debug.Print($"BatchGeometry:: Panel Level{i}-{levels[i].height - Util.MToFoot(0.1)} u:{summit} b:{bottom}");
                                foreach (gbSeg segment in temps)
                                {
                                    gbXYZ locationPt = segment.PointAt(0.5);
                                    double width = Math.Round(segment.Length, 3) * 1000;
                                    double height = Math.Round(Util.FootToM(summit - bottom), 3) * 1000;
                                    var winPanel = new Tuple<gbXYZ, string>(
                                        locationPt, width.ToString() + " x " + height.ToString());
                                    dictWindow[i].Add(winPanel);
                                }
                                // overload the centerlines
                                dictCurtain[i].AddRange(temps);
                            }
                            // if the curtain wall does surpass the height of level
                            // it may function like a normal one
                            else
                            {
                                // --------------------comment out the lines in between when testing ROOMVENT model----------------
                                CurtainGrid cg = wall.CurtainGrid;

                                if (cg != null)
                                {
                                    List<XYZ> boundaryPts = new List<XYZ>();
                                    Options cwOpt = wall.Document.Application.Create.NewGeometryOptions();
                                    cwOpt.IncludeNonVisibleObjects = true;
                                    GeometryElement geomElem = wall.get_Geometry(cwOpt);

                                    foreach (GeometryObject go in geomElem)
                                    {
                                        Curve anyCrv = go as Curve;
                                        if (anyCrv != null)
                                        {
                                            XYZ start = anyCrv.GetEndPoint(0);
                                            XYZ end = anyCrv.GetEndPoint(1);
                                            // the curtain wall my span over multiple levels
                                            // only taking the bottom line is not an ideal choice
                                            if (Math.Abs(start.Z - end.Z) < 0.00001 && Math.Abs(start.Z - levels[i].elevation) < 0.5)
                                            {
                                                boundaryPts.Add(anyCrv.GetEndPoint(0));
                                                boundaryPts.Add(anyCrv.GetEndPoint(1));
                                            }
                                        }
                                    }

                                    //Debug.Print($"BatchGeometry:: Piling altogether {panelPts.Count} points");
                                    if (boundaryPts.Count >= 2)
                                    {
                                        boundaryPts = boundaryPts.OrderBy(p => p.X).ToList();
                                        boundaryPts = boundaryPts.OrderBy(p => p.Y).ToList();
                                        temps = new List<gbSeg>() { new gbSeg(Util.gbXYZConvert(boundaryPts[0]), Util.gbXYZConvert(boundaryPts.Last())) };
                                        //Debug.Print($"BatchGeometry:: new baseline updated");
                                    }
                                    else
                                    {
                                        //Debug.Print("BatchGeometry:: empty panelPts");
                                        temps = new List<gbSeg>();
                                    }
                                }
                                // --------------------comment out the lines in between when testing ROOMVENT model----------------

                                dictCurtain[i].AddRange(temps);
                            }
                        }

                        // if the WallType is something else, Basic, Stacked, Unknown, append it to dictWall
                        else
                            dictWall[i].AddRange(temps);
                    }
                    // if a wall belongs to no level, make it a shading surface
                    else
                    {
                        int levelMark = -1;
                        if (spanCheck > 0.1 * levels[i].height)
                            levelMark = i;
                        else if (summit > levels[levels.Count - 1].elevation)
                            levelMark = levels.Count - 1;
                        if (levelMark >= 0)
                        {
                            foreach (gbSeg temp in temps)
                            {
                                List<gbXYZ> vertice = new List<gbXYZ>();
                                vertice.Add(temp.Start);
                                vertice.Add(temp.End);
                                vertice.Add(temp.End + new gbXYZ(0, 0, Util.FootToM(summit - bottom)));
                                vertice.Add(temp.Start + new gbXYZ(0, 0, Util.FootToM(summit - bottom)));
                                dictShade[levelMark].Add(vertice);
                            }
                        }
                    }
                }
            }

            // allocate the floor to each level
            IList<Element> eFloors = new FilteredElementCollector(doc)
                 .OfCategory(BuiltInCategory.OST_Floors)
                 .WhereElementIsNotElementType()
                 .ToElements();
            List<Element> eFloorsPlus = eFloors.ToList();
            foreach (Document refDoc in refDocs)
            {
                IList<Element> eFloorsRef = new FilteredElementCollector(refDoc)
                    .OfCategory(BuiltInCategory.OST_Floors)
                    .WhereElementIsNotElementType()
                    .ToElements();
                eFloorsPlus.AddRange(eFloorsRef);
            }

            foreach (Element e in eFloorsPlus)
            {
                int levelMark = -1;
                List<List<gbXYZ>> floorSlab = new List<List<gbXYZ>>();
                List<List<gbXYZ>> shadeSlabs = new List<List<gbXYZ>>();

                Floor floor = e as Floor;
                if (floor == null)
                    continue;

                IList<Autodesk.Revit.DB.Reference> references = HostObjectUtils.GetTopFaces(floor);
                if (references.Count > 0)
                {
                    var reference = references[0];
                    GeometryObject topFaceGeo = floor.GetGeometryObjectFromReference(reference);
                    PlanarFace topFace = topFaceGeo as PlanarFace;
                    // assuming that the floor slab clings to the level plane, a mandatory rule of BIM
                    // slabs not within the ±0.2m range of the level elevation will be cast as shadings
                    if (topFace != null)
                    {
                        for (int i = 0; i < levels.Count; i++)
                        {
                            double deltaZ = topFace.Origin.Z - levels[i].elevation;
                            if (Math.Abs(deltaZ) < Util.MToFoot(0.2))
                            {
                                foreach (EdgeArray edgeArray in topFace.EdgeLoops)
                                {
                                    List<gbXYZ> boundaryLoop = new List<gbXYZ>();
                                    foreach (Edge edge in edgeArray)
                                    {
                                        XYZ ptStart = edge.AsCurve().GetEndPoint(0);
                                        boundaryLoop.Add(Util.gbXYZConvert(ptStart));
                                    }
                                    boundaryLoop.Add(boundaryLoop[0]);
                                    // the boundary loop can be clockwise or counter-clockwise
                                    // the clockwise loop always represents the boundary of a single slab
                                    // the counter-clockwise loop represents the boundary of inner holes
                                    // 20220812 note: may not be true. the hole can be clockwise represented
                                    floorSlab.Add(boundaryLoop);
                                    levelMark = i;
                                }
                                // if marked as a Floor, it cannot be a Shade
                                break;
                            }
                            // check if the slab is within the span of the previous level
                            else if (i > 0 && deltaZ < 0 && Math.Abs(deltaZ) < levels[i - 1].height)
                            {
                                double maxArea = -1;
                                int maxIndex = -1;
                                int loopCounter = 0;
                                List<List<gbXYZ>> tempLoops = new List<List<gbXYZ>>();
                                foreach (EdgeArray edgeArray in topFace.EdgeLoops)
                                {
                                    List<gbXYZ> boundaryLoop = new List<gbXYZ>();
                                    foreach (Edge edge in edgeArray)
                                    {
                                        XYZ ptStart = edge.AsCurve().GetEndPoint(0);
                                        boundaryLoop.Add(Util.gbXYZConvert(ptStart));
                                    }
                                    boundaryLoop.Add(boundaryLoop[0]);
                                    tempLoops.Add(boundaryLoop);
                                    double area = Math.Abs(GBMethod.GetPolyArea(boundaryLoop));
                                    if (area > maxArea)
                                    {
                                        maxArea = area;
                                        maxIndex = loopCounter;
                                    }
                                    loopCounter++;
                                    // as to shading surface, only the outer, counter-clockwise loop is needed
                                    //if (!GBMethod.IsClockwise(boundaryLoop))
                                    //    shadeSlabs.Add(boundaryLoop);
                                }
                                // the loop with the maximum unsigned area will be the outer loop
                                shadeSlabs.Add(tempLoops[maxIndex]);
                                levelMark = i - 1;
                            }
                        }
                    }
                }
                // here, a floorSlab may include multiple solids
                // but they are all bundled within one mass
                if (levelMark > -1)
                {
                    dictFloor[levelMark].Add(floorSlab);
                    dictShade[levelMark].AddRange(shadeSlabs);
                }
            }

            IList<Element> eRoofs = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Roofs)
                .WhereElementIsNotElementType()
                .ToElements();

            List<Element> eRoofsPlus = eRoofs.ToList();
            foreach (Document refDoc in refDocs)
            {
                IList<Element> eRoofsRef = new FilteredElementCollector(refDoc)
                    .OfCategory(BuiltInCategory.OST_Roofs)
                    .WhereElementIsNotElementType()
                    .ToElements();
                eRoofsPlus.AddRange(eRoofsRef);
            }

            List<List<List<gbXYZ>>> roofSlabs = new List<List<List<gbXYZ>>>();
            foreach (Element e in eRoofsPlus)
            {
                int levelMark = -1;
                List<List<gbXYZ>> roofSlab = new List<List<gbXYZ>>();

                Floor floor = e as Floor;
                if (floor == null)
                    continue;

                IList<Autodesk.Revit.DB.Reference> references = HostObjectUtils.GetBottomFaces(floor);
                if (references.Count > 0)
                {
                    var reference = references[0];
                    GeometryObject bottomFaceGeo = floor.GetGeometryObjectFromReference(reference);
                    PlanarFace bottomFace = bottomFaceGeo as PlanarFace;
                    if (bottomFace != null)
                    {
                        for (int i = 0; i < levels.Count; i++)
                        {
                            if (Math.Abs(bottomFace.Origin.Z - levels[i].elevation) < Util.MToFoot(0.2))
                            {
                                foreach (EdgeArray edgeArray in bottomFace.EdgeLoops)
                                {
                                    List<gbXYZ> boundaryLoop = new List<gbXYZ>();
                                    foreach (Edge edge in edgeArray)
                                    {
                                        XYZ ptStart = edge.AsCurve().GetEndPoint(0);
                                        boundaryLoop.Add(Util.gbXYZConvert(ptStart));
                                    }
                                    boundaryLoop.Add(boundaryLoop[0]);
                                    // reverse all loops when dealing a down-facing surface
                                    boundaryLoop.Reverse();
                                    roofSlab.Add(boundaryLoop);
                                    levelMark = i;
                                }
                            }
                        }
                    }
                }
                roofSlabs.Add(roofSlab);
                if (roofSlab.Count > 0)
                dictFloor[levelMark].Add(roofSlab);
            }

            // allocate window information to each level
            // note that the window may span over multiple levels
            IList<Element> eWindows = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_Windows)
                .ToElements();
            foreach (Element e in eWindows)
            {
                FamilyInstance w = e as FamilyInstance;
                FamilySymbol ws = w.Symbol;
                XYZ lp = Util.GetFamilyInstanceLocation(w);
                if (lp == null)
                    continue;

                double height = Util.FootToMm(ws.get_Parameter(BuiltInParameter.WINDOW_HEIGHT).AsDouble());
                double width = Util.FootToMm(ws.get_Parameter(BuiltInParameter.WINDOW_WIDTH).AsDouble());

                Options op = w.Document.Application.Create.NewGeometryOptions();
                GeometryElement ge = w.get_Geometry(op);
                double summit = ge.GetBoundingBox().Max.Z;
                double bottom = ge.GetBoundingBox().Min.Z;

                if (summit < bottom)
                    continue;
                // the window may be misplaced to other levels
                // set a tolerance to check whether the window is misplaced by modeling precision issue
                for (int i = 0; i < levels.Count; i++)
                {
                    if (levels[i].elevation - bottom >= 0 && 
                        summit - levels[i].elevation >= Util.MToFoot(0.1) ||
                        levels[i].elevation + levels[i].height - bottom >= Util.MToFoot(0.1) &&
                        summit - levels[i].elevation - levels[i].height >= 0 || 
                        levels[i].elevation < bottom && 
                        levels[i].elevation + levels[i].height > summit)
                        dictWindow[i].Add(new Tuple<gbXYZ, string>(Util.gbXYZConvert(lp), $"{width:F0} x {height:F0}"));
                }
            }


            // ######################### STRUCTURE SECTION #############################
            // allocate column information to each floor
            // also read data from linked Revit model if necessary
            ElementMulticategoryFilter bothColumnFilter = new ElementMulticategoryFilter(
                new List<BuiltInCategory> { BuiltInCategory.OST_Columns, BuiltInCategory.OST_StructuralColumns });

            IList<Element> eColumns = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .WherePasses(bothColumnFilter)
                .ToElements();
            //Debug.Print($"Type of the element {eColumns[0].GetType()}");

            List<FamilyInstance> fiColumns = new List<FamilyInstance>();

            foreach (Element e in eColumns)
            {
                if (e is null)
                    continue;
                fiColumns.Add(e as FamilyInstance);
            }

            foreach (Document refDoc in refDocs)
            {
                IList<Element> _eColumns = new FilteredElementCollector(refDoc)
                    .OfClass(typeof(FamilyInstance))
                    .WherePasses(bothColumnFilter)
                    .ToElements();
                //Debug.Print($"All together elements {_eColumns.Count}");
                foreach (Element e in _eColumns)
                {
                    if (e is null)
                        continue;
                    FamilyInstance fi = e as FamilyInstance;
                    //Debug.Print($"Trying to convert... {fi.GetType()}");
                    fiColumns.Add(fi);
                }
            }
            foreach (FamilyInstance fi in fiColumns)
            {
                // FamilyInstance.Location.LocationPoint may not be XYZ
                // PENDING come back to this later
                // note that this method allows null as a return value
                XYZ lp = Util.GetFamilyInstanceLocationPoint(fi);
                if (null == lp)
                    continue;
                
                // get the height of the column by retrieving its geometry element
                Options op = fi.Document.Application.Create.NewGeometryOptions();
                GeometryElement ge = fi.get_Geometry(op);

                double summit = ge.GetBoundingBox().Max.Z;
                double bottom = ge.GetBoundingBox().Min.Z;
                for (int i = 0; i < levels.Count; i++)
                {
                    // add location point if the column lies within the range of this level
                    // this is irrelevant to its host level
                    // sometimes the levels from linked file are not corresponding to the current model
                    if (//fi.LevelId == levels[i].id ||
                       summit >= (levels[i].elevation + 0.5 * levels[i].height) &&
                       bottom <= (levels[i].elevation + 0.5 * levels[i].height))
                    {
                        List<CurveLoop> colCrvLoops = GetFootprintOfColumn(fi);
                        List<gbXYZ> colPoly = new List<gbXYZ>();
                        // only cache the outer boundary without holes
                        foreach (Curve crv in colCrvLoops[0])
                        {
                            if (crv is Line)
                            {
                                colPoly.Add(Util.gbXYZConvert(crv.GetEndPoint(0)));
                            }
                            // we are expecting rectangular columns but
                            // there will always be special-shaped or cylindrical ones
                            else
                            {
                                List<XYZ> ptsTessellated = new List<XYZ>(crv.Tessellate());
                                // remove the end point so there will be no duplicate
                                ptsTessellated.RemoveAt(ptsTessellated.Count - 1);
                                colPoly.AddRange(Util.gbXYZsConvert(ptsTessellated));
                            }
                        }
                        // make it a closed polygon
                        colPoly.Add(colPoly[0]);
                        if (colPoly.Count > 0)
                            dictColumn[i].Add(new Tuple<List<gbXYZ>, string>(colPoly, fi.Name));
                    }
                }
            }


            // allocate beam information to each floor
            // read data from linked Revit model if necessary
            List<Tuple<gbSeg, string>> beamLocs = new List<Tuple<gbSeg, string>>();
            IList<Element> eBeams = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .ToElements();

            List<FamilyInstance> fiBeams = new List<FamilyInstance>();
            foreach (Element e in eBeams)
            {
                if (e is null)
                    continue;
                fiBeams.Add(e as FamilyInstance);
            }

            foreach (Document refDoc in refDocs)
            {
                IList<Element> _eBeams = new FilteredElementCollector(refDoc)
                    .OfClass(typeof(FamilyInstance))
                    .OfCategory(BuiltInCategory.OST_StructuralFraming)
                    .ToElements();
                foreach (Element e in _eBeams)
                {
                    if (e is null)
                        continue;
                    fiBeams.Add(e as FamilyInstance);
                }
            }

            //Debug.Print($"BatchGeometry:: beams in total - {fiBeams.Count}");
            foreach (FamilyInstance fi in fiBeams)
            {
                // is it dangerous not considering the curve might not be a line?
                LocationCurve lc = fi.Location as LocationCurve;
                if (null == lc)
                    continue;

                Options op = fi.Document.Application.Create.NewGeometryOptions();
                GeometryElement ge = fi.get_Geometry(op);
                double summit = ge.GetBoundingBox().Max.Z;
                double bottom = ge.GetBoundingBox().Min.Z;
                //Debug.Print("Beam upper limit: " + Util.FootToM(summit).ToString());

                for (int i = 0; i < levels.Count; i++)
                {
                    //Debug.Print("Level height: " + Util.FootToM(levels[i].elevation + levels[i].height).ToString());
                    // compare the upper limit and the level elevation
                    // assume a tolerance of 0.2m
                    if (Math.Abs(summit - (levels[i].elevation + levels[i].height)) < Util.MToFoot(0.2))
                    {
                        //Debug.Print("Beam location: " + Util.gbSegConvert(lc.Curve as Line).ToString());
                        dictBeam[i].Add(new Tuple<gbSeg, string>(Util.gbSegConvert(lc.Curve as Line), fi.Name));
                        break;
                    }
                }
            }

            // ABANDONED. now
            // add the roof level at last (almost with no info)
            //dictElevation.Add(dictElevation.Count, new Tuple<string, double>("Roof",
            //    Util.FootToM(levels.Last().elevation + levels.Last().height)));

            // DEBUG
            checkInfo = "";
            
            for (int i = 0; i < levels.Count; i++)
            {
                checkInfo += $"#{i} {dictElevation[i].Item2}m <{dictElevation[i].Item1}> geometry summary\n";
                checkInfo += $"    Wall-{dictWall[i].Count} \tFloorSlab-{dictFloor[i].Count} \tWindow-{dictWindow[i].Count} \tColumn-{dictColumn[i].Count}\n";
                checkInfo += $"    Curtain-{dictCurtain[i].Count} \tRoom-{dictRoom[i].Count}  \tDoor-{dictDoor[i].Count}  \tBeam-{dictBeam[i].Count}\n";
                checkInfo += $"    CurtaSys-{dictCurtaSystem[i].Count} \tSeparation-{dictSeparationline[i].Count} \tSkylight-? \tShade-{dictShade[i].Count}\n";

                string logInfo = "";
                logInfo += $"Level-{i} {dictElevation[i].Item1} {dictElevation[i].Item2}m with components:\n";
                logInfo += $"           Wall-{dictWall[i].Count} Curtain-{dictCurtain[i].Count} CurtaSys-{dictCurtaSystem[i].Count}\n";
                logInfo += $"           Window-{dictWindow[i].Count} Door-{dictDoor[i].Count} Column-{dictColumn[i].Count} Beam-{dictBeam[i].Count}\n";
                int floorSlabCount = 0;
                int floorHoleCount = 0;
                foreach (List<List<gbXYZ>> floorSlab in dictFloor[i])
                {
                    foreach (List<gbXYZ> loop in floorSlab)
                    {
                        if (GBMethod.IsClockwise(loop))
                            floorHoleCount++;
                        else
                            floorSlabCount++;
                    }
                }
                logInfo += $"           FloorMass-{dictFloor[i].Count} FloorSlab-{floorSlabCount} FloorHole-{floorHoleCount} FloorShade-{dictShade[i].Count}\n";
                logInfo += $"           Room-{dictRoom[i].Count} Separation-{dictSeparationline[i].Count}\n";
                Util.LogPrint(logInfo);
            }
            checkInfo += "\nDone model check.";
            

            return;
        }

        // Private method
        // Iterate to get the bottom face of a solid
        static Face GetBottomFace(Solid solid)
        {
            PlanarFace pf = null;
            foreach (Face face in solid.Faces)
            {
                pf = face as PlanarFace;
                if (null != pf)
                {
                    if (Core.Basic.IsVertical(pf.FaceNormal, Properties.Settings.Default.tolDouble)
                        && pf.FaceNormal.Z < 0)
                    {
                        break;
                    }
                }
            }
            return pf;
        }

        // Private method
        // Generate the CurveLoop of the footprint
        static List<CurveLoop> GetFootprintOfColumn(FamilyInstance fi)
        {
            List<CurveLoop> footprints = new List<CurveLoop>();

            Options opt = new Options();
            opt.ComputeReferences = true;
            opt.DetailLevel = ViewDetailLevel.Medium;
            GeometryElement ge = fi.get_Geometry(opt);

            foreach (GeometryObject obj in ge)
            {
                if (obj is Solid)
                {
                    Face bottomFace = GetBottomFace(obj as Solid);
                    if (null != bottomFace)
                    {
                        foreach (CurveLoop edge in bottomFace.GetEdgesAsCurveLoops())
                        {
                            footprints.Add(edge);
                        }
                    }
                }
                else if (obj is GeometryInstance)
                {
                    GeometryInstance geoInstance = obj as GeometryInstance;
                    GeometryElement geoElement = geoInstance.GetInstanceGeometry();
                    foreach (GeometryObject obj2 in geoElement)
                    {
                        if (obj2 is Solid)
                        {
                            Solid solid2 = obj2 as Solid;
                            if (solid2.Faces.Size > 0)
                            {
                                Face bottomFace = GetBottomFace(solid2);
                                foreach (CurveLoop edge in bottomFace.GetEdgesAsCurveLoops())
                                {
                                    footprints.Add(edge);
                                }
                            }
                        }
                    }
                }
            }
            // else
            // doing nothing and return the empty list
            return footprints;
        }

        // compare to a list of values to check if there are similar ones
        static bool CheckSimilarity(double value, double threshold, List<double> nums)
        {
            if (nums.Count == 0)
                return true;
            foreach (double num in nums)
            {
                if (Math.Abs(value - num) < threshold)
                {
                    return false;
                }
            }
            return true;
        }
    }
}