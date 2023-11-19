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
using System.Net;
#endregion

// PENDING      - functions saved for a happy day
// ABANDONED    - obsolete functions 

namespace Gingerbread
{
    class BatchGeometry
    {
        static Properties.Settings sets = Properties.Settings.Default;

        // Private class, levelPack for convenient
        private class levelPack
        {
            public ElementId id;
            public string name;
            public double elevation;
            public double projElevation;
            public double height;
            // the levelPack caches data in Imperial not Metric
            public levelPack(ElementId id, string name, double elevation, double projElevation)
            { this.id = id; this.name = name; this.elevation = elevation; 
                this.projElevation = projElevation;  this.height = 0; }
        };

        public static void Execute(Document doc, 
            out Dictionary<int, Tuple<string, double>> dictElevation,
            out Dictionary<int, List<gbSeg>> dictWall,
            out Dictionary<int, List<gbSeg>> dictWallPatch,
            out Dictionary<int, List<gbSeg>> dictCurtain,
            out Dictionary<int, List<gbSeg>> dictCurtaSystem, 
            out Dictionary<int, List<Tuple<string, string, List<gbXYZ>, gbSeg>>> dictColumn,
            out Dictionary<int, List<Tuple<string, string, List<gbXYZ>, gbSeg>>> dictBeam,
            out Dictionary<int, List<Tuple<gbXYZ, string>>> dictWindow,
            out Dictionary<int, List<Tuple<gbXYZ, string>>> dictDoor,
            out Dictionary<int, List<List<List<gbXYZ>>>> dictFloor,
            out Dictionary<int, List<List<gbXYZ>>> dictShade, 
            out Dictionary<int, List<gbSeg>> dictSeparationline,
            // dictFirewall records customized partitions for fire zone definition
            out Dictionary<int, List<gbSeg>> dictFirewall,
            out Dictionary<int, List<gbSeg>> dictGrid,
            out Dictionary<int, List<Tuple<List<List<gbXYZ>>, string>>> dictRoom,
            out Dictionary<string, List<Tuple<string, double>>> dictWindowplus,
            out Dictionary<string, List<Tuple<string, double>>> dictDoorplus,
            out string checkInfo)
        {
            // initiate variables for output
            dictElevation = new Dictionary<int, Tuple<string, double>>();
            dictWall = new Dictionary<int, List<gbSeg>>();
            dictWallPatch = new Dictionary<int, List<gbSeg>>();
            dictCurtain = new Dictionary<int, List<gbSeg>>();
            dictCurtaSystem = new Dictionary<int, List<gbSeg>>();
            dictColumn = new Dictionary<int, List<Tuple<string, string, List<gbXYZ>, gbSeg>>>();   // use sweep for this type of geometry
            dictBeam = new Dictionary<int, List<Tuple<string, string, List<gbXYZ>, gbSeg>>>();     // use sweep for this type of geometry
            dictWindow = new Dictionary<int, List<Tuple<gbXYZ, string>>>();
            dictDoor = new Dictionary<int, List<Tuple<gbXYZ, string>>>();
            dictFloor = new Dictionary<int, List<List<List<gbXYZ>>>>();
            dictShade = new Dictionary<int, List<List<gbXYZ>>>();
            dictSeparationline = new Dictionary<int, List<gbSeg>>();
            // dictFirewall records customized partitions for fir zone definition
            dictFirewall = new Dictionary<int, List<gbSeg>>();
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
                    levelPack l = new levelPack(e.LevelId, level.Name, level.Elevation, level.ProjectElevation);
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
                    levelPack l = new levelPack(e.LevelId, level.Name, level.Elevation, level.ProjectElevation);
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
                    levels.Add(new levelPack(level.Id, level.Name, level.Elevation, level.ProjectElevation));
                    break;
                }
            }

            // calculat the offset from base level to internal origin (usually 0, but not always)
            // the coordinate retrieved through API is the project elevation
            // the coordinate you see in the Revit UI is the elevation
            // to convert make you retrieved in line with what you see you need to deduct the _bias
            Properties.Settings.Default.offsetZ = levels[0].projElevation - levels[0].elevation;
            XYZ _bias = new XYZ(0, 0, Properties.Settings.Default.offsetZ);

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
                dictWallPatch.Add(z, new List<gbSeg>());
                dictFirewall.Add(z, new List<gbSeg>());
                dictColumn.Add(z, new List<Tuple<string, string, List<gbXYZ>, gbSeg>>());
                dictBeam.Add(z, new List<Tuple<string, string, List<gbXYZ>, gbSeg>>());
                dictCurtain.Add(z, new List<gbSeg>());
                dictWindow.Add(z, new List<Tuple<gbXYZ, string>>());
                dictShade.Add(z, new List<List<gbXYZ>>());
                dictFloor.Add(z, new List<List<List<gbXYZ>>>());
                dictGrid.Add(z, new List<gbSeg>());
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
                        doorLocs.Add(new Tuple<gbXYZ, string>(Util.gbXYZConvert(lp - _bias), $"{width:F0} x {height:F0}"));
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
                        XYZ p1 = gridLine.GetEndPoint(0) - _bias;
                        XYZ p2 = gridLine.GetEndPoint(1) - _bias;
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
                                XYZ pt = bc.GetEndPoint(0) - _bias;
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
                        XYZ gridStart = grid.GetEndPoint(0) - _bias;
                        XYZ gridEnd = grid.GetEndPoint(1) - _bias;
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

                // 20230329 
                // a sample to check user defined attribute
                bool isFirewall = false;
                foreach (Parameter par in wall.Parameters)
                {
                    if (par.Definition.Name == "是否是防火分区边界墙")
                    {
                        if (par.HasValue == true)
                        {
                            isFirewall = true;
                            // then center lines of this wall will be append to dictFirewall
                        }
                    }
                }

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
                double summit = ge.GetBoundingBox().Max.Z - Properties.Settings.Default.offsetZ;
                double bottom = ge.GetBoundingBox().Min.Z - Properties.Settings.Default.offsetZ;

                // access footprint (refer to CmdSketchFootprint.cs)
                // for a wall with door openings, its footprint can be several rectangulars
                List<List<gbSeg>> footprints = new List<List<gbSeg>>();
                if (wall.WallType.Kind == WallKind.Curtain)
                {
                    // TASK do not support curtain wall (not necessary for now)
                    //CurtainGrid cg = wall.CurtainGrid;
                    //footprints.AddRange(GetPanelPlaneIntersectionCurve(cg, activeViewElevation + tol));
                    //footprints.AddRange(GetMullionPlaneIntersectionCurve(cg, activeViewElevation + tol));
                }
                else
                {
                    // not using boolean intersection
                    // a test module for the GetSolidBottomFace() method
                    foreach (GeometryObject obj in ge)
                    {
                        Solid solid = obj as Solid;
                        if (null != solid)
                        {
                            var bottomFace = GetSolidBottomFace(solid);
                            if (bottomFace != null)
                            {
                                foreach (CurveLoop loop in bottomFace.GetEdgesAsCurveLoops())
                                {
                                    List<gbSeg> edgeLoop = new List<gbSeg>() { };
                                    foreach (Curve edge in loop)
                                    {
                                        if (edge is Line)
                                        {
                                            edgeLoop.Add(Util.gbSegConvert(edge as Line));
                                        }
                                        else
                                        {
                                            List<XYZ> ptsTessellated = new List<XYZ>(edge.Tessellate());
                                            for (int i = 0; i < ptsTessellated.Count - 1; i++)
                                                edgeLoop.Add(new gbSeg(
                                                    Util.gbXYZConvert(ptsTessellated[i]), 
                                                    Util.gbXYZConvert(ptsTessellated[i + 1])
                                                    ));
                                        }
                                    }
                                    footprints.Add(edgeLoop);
                                }
                            }
                        }
                    }
                }

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
                    bool isSpanMid = Util.IsSpanMid(bottom, summit, levels[i].elevation,
                        levels[i].elevation + levels[i].height);
                    if (spanCheck > 0.5 * levels[i].height || 
                        (spanCheck > 0.3 * levels[i].height && isSpanMid))
                    {
                        // if the WallType is CurtainWall, append it to dictCurtain
                        if (wall.WallType.Kind == WallKind.Curtain)
                        {
                            // check if the curtain acts like a window
                            // the height of the curtain wall should be almost equal
                            // or over the height of this level
                            // 0.2m gap ensures the strength of the structure, practical value
                            // PENDING  - this value may vary with projects
                            if (summit - bottom <= levels[i].height + Util.MToFoot(0.2))
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
                                // the intersection between curtian grid and floor plane fails on ROOMVENT and Georgia test
                                // Revit component "curtain" may not have grid or the grid may not be complete, so do not trust it
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
                        {
                            dictWall[i].AddRange(temps);
                            // additionally, add them to dictFirewall if it is a firewall
                            if (isFirewall)
                                dictFirewall[i].AddRange(temps);

                            // do endpoint patch? if so...
                            // find out the element jointed with this wall
                            // check if their centerlines are intersected, if not
                            // then append foorprints intersected by centerlines of both walls
                            if (Properties.Settings.Default.patchWall)
                            {
                                List<gbSeg> flattenFootprints = Util.FlattenList(footprints);
                                
                                // first extend the location line to the footprint
                                // then consider the start point and end point
                                // below is the common situation
                                // ┌---------┐     ┌--------------------┐ -> footprint
                                // ├---------┤-----├----------------    │ -> location curve
                                // └---------┘     └--------------------┘
                                // try to extend the location line to the footprint boundary
                                foreach (gbSeg seg in flattenFootprints)
                                    temps[0] = GBMethod.SegExtensionToSeg(temps[0], seg, sets.tolAlignment * 2);

                                double thickness = Util.FootToM(wall.WallType.Width);

                                // 20231116 thought the wall e may not be in the current document
                                // if external files are linked, you may need to look for joined elements in other documents
                                // current function can only search for the joined element within the same doc
                                ICollection<ElementId> eGeneralIds = JoinGeometryUtils.GetJoinedElements(e.Document, e);
                                ElementArray eAtStart = lc.get_ElementsAtJoin(0);
                                ElementArray eAtEnd = lc.get_ElementsAtJoin(1);

                                // list for potential joined elements
                                List<Element> eAtJoint = new List<Element>() { };
                                List<ElementId> eAtJointIds = new List<ElementId>() { };

                                foreach (Element eJoined in eAtStart)
                                {
                                    if (eJoined.Id == e.Id || eAtJointIds.Contains(eJoined.Id)) continue;
                                    else { eAtJointIds.Add(eJoined.Id); eAtJoint.Add(eJoined); }
                                }
                                foreach (Element eJoined in eAtStart)
                                {
                                    if (eJoined.Id == e.Id || eAtJointIds.Contains(eJoined.Id)) continue;
                                    else { eAtJointIds.Add(eJoined.Id); eAtJoint.Add(eJoined); }
                                }
                                foreach (ElementId eId in eGeneralIds)
                                {
                                    if (eId == e.Id || eAtJointIds.Contains(eId)) continue;
                                    else { eAtJointIds.Add(eId); eAtJoint.Add(doc.GetElement(eId)); }
                                }

                                foreach (Element eJoined in eAtJoint)
                                {
                                    if (eJoined is Wall)
                                    {
                                        Wall jointWall = eJoined as Wall;
                                        LocationCurve jlc = jointWall.Location as LocationCurve;
                                        if (jlc.Curve is Line)
                                        {
                                            gbSeg jlc_check = Util.gbSegConvert(jlc.Curve as Line);
                                            //var llx = GBMethod.SegIntersection(temps[0], jlc_check, sets.tolDouble, sets.tolDouble,
                                            //    out gbXYZ sect, out double t1, out double t2);
                                            var angle_delta = GBMethod.VectorAnglePI_2(temps[0].Direction, jlc_check.Direction);
                                            var para_gap = GBMethod.SegProjectToSeg(jlc_check, temps[0], 
                                                out _, out _, out gbSeg union);
                                            if (angle_delta < sets.tolTheta && para_gap > sets.tolAlignment / 5)
                                            {
                                                double jlc_width = Util.FootToM(jointWall.WallType.Width);
                                                if (thickness < jlc_width) thickness = jlc_width;
                                                // create a patch perpendicular to temps[0] with length = thickness, at the middle of union
                                                gbXYZ basePt = union.PointAt(0.5);
                                                gbXYZ startPt = basePt + thickness / 2 * GBMethod.GetPendicularVec(temps[0].Direction, true);
                                                gbXYZ endPt = basePt + thickness / 2 * GBMethod.GetPendicularVec(temps[0].Direction, false);
                                                dictWallPatch[i].Add(new gbSeg(startPt, endPt));
                                            }
                                        }
                                    }
                                }
                                // also, if the wall footprint intersects with the separation line
                                // append the same endpoint patch to dictWall
                            }
                        }
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
                                        XYZ ptStart = edge.AsCurve().GetEndPoint(0) - _bias;
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
                                        XYZ ptStart = edge.AsCurve().GetEndPoint(0) - _bias;
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
                                        XYZ ptStart = edge.AsCurve().GetEndPoint(0) - _bias;
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

                // FamilyInstance.Location.LocationPoint may not be XYZ
                // note that this method allows null as a return value
                // 20231118 if it is null, you cannot perform calculatons on it
                XYZ lp = Util.GetFamilyInstanceLocation(w);
                if (lp == null)
                    continue;
                
                double height = Util.FootToMm(ws.get_Parameter(BuiltInParameter.WINDOW_HEIGHT).AsDouble());
                double width = Util.FootToMm(ws.get_Parameter(BuiltInParameter.WINDOW_WIDTH).AsDouble());

                Options op = w.Document.Application.Create.NewGeometryOptions();
                GeometryElement ge = w.get_Geometry(op);
                double summit = ge.GetBoundingBox().Max.Z - Properties.Settings.Default.offsetZ;
                double bottom = ge.GetBoundingBox().Min.Z - Properties.Settings.Default.offsetZ;

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
                        dictWindow[i].Add(new Tuple<gbXYZ, string>(Util.gbXYZConvert(lp - _bias), $"{width:F0} x {height:F0}"));
                }
            }   

            // ######################### Global Grid System ############################

            if (sets.followGrid)
            {
                IList<Element> _eGrids = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .OfCategory(BuiltInCategory.OST_Grids)
                .ToElements();

                foreach (Element eGrid in _eGrids)
                {
                    if (eGrid is Autodesk.Revit.DB.Grid)
                    {
                        var grid = eGrid as Autodesk.Revit.DB.Grid;
                        if (!grid.IsCurved)
                        {
                            dictGrid[0].Add(Util.gbSegConvert(grid.Curve as Line));
                        }
                    }
                }
            }

            // ######################### USER DEFINED SHADE ############################
            // DEUBG you should dump all geometry cache from previous project when user opens another one
            if (Properties.Settings.Default.shadeIds != "")
            {
                string[] serializedRefs = Properties.Settings.Default.shadeIds.Split('#');
                foreach (string serializedRef in serializedRefs)
                {
                    Autodesk.Revit.DB.Reference faceRef = Autodesk.Revit.DB.Reference
                        .ParseFromStableRepresentation(doc, serializedRef);
                    if (faceRef == null) continue;

                    Element eleRef = doc.GetElement(faceRef);
                    if (eleRef is null) continue;
                    GeometryObject geoObject = eleRef.GetGeometryObjectFromReference(faceRef);
                    if (geoObject is null) continue;
                    PlanarFace planarFace = geoObject as PlanarFace;
                    // a planar face may have multiple loops representation, like MCR
                    List<List<gbXYZ>> vertexLoops = new List<List<gbXYZ>>() { };
                    foreach (CurveLoop loop in planarFace.GetEdgesAsCurveLoops())
                    {
                        List<gbXYZ> vertexLoop = new List<gbXYZ>() { };
                        foreach (Curve edge in loop)
                        {
                            if (edge is Line)
                            {
                                vertexLoop.Add(Util.gbXYZConvert(edge.GetEndPoint(0)));
                            }
                            else
                            {
                                List<XYZ> ptsTessellated = new List<XYZ>(edge.Tessellate());
                                // remove the end point so there will be no duplicate
                                ptsTessellated.RemoveAt(ptsTessellated.Count - 1);
                                vertexLoop.AddRange(Util.gbXYZsConvert(ptsTessellated));
                            }
                        }
                        vertexLoops.Add(vertexLoop);
                    }
                    // currently, these shades will not be serialized
                    // so add -1 key to mark them as absolete ones
                    dictShade.Add(-1, vertexLoops);
                }
            }



            // ######################### STRUCTURE SECTION #############################
            if (Properties.Settings.Default.exportStruct)
            {
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
                    // get the height of the column by retrieving its geometry element
                    Options op = fi.Document.Application.Create.NewGeometryOptions();
                    GeometryElement ge = fi.get_Geometry(op);

                    double summit = ge.GetBoundingBox().Max.Z - Properties.Settings.Default.offsetZ;
                    double bottom = ge.GetBoundingBox().Min.Z - Properties.Settings.Default.offsetZ;

                    // prepare the geometry first
                    List<CurveLoop> colCrvLoops = GetFootprintOfColumn(fi);
                    if (colCrvLoops.Count == 0)
                        continue;
                    List<gbXYZ> colPoly = new List<gbXYZ>();
                    // only cache the outer boundary without holes
                    foreach (Curve crv in colCrvLoops[0])
                    {
                        if (crv is Line)
                        {
                            // 20231118 this may not follow the looping sequence
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

                    // get bounding box of the colPoly
                    List<gbXYZ> colPolyBox = GBMethod.ElevatePts(
                        OrthoHull.GetRectHull(colPoly), colPoly[0].Z);
                    colPolyBox.RemoveAt(4);

                    // what to do with the slant column?
                    // OrthoHull.GetRectHull() returns a closed polyline on XY plane
                    gbXYZ centroid = GBMethod.GetRectCentroid(OrthoHull.GetRectHull(colPoly));
                    gbSeg colAxis = new gbSeg(
                        new gbXYZ(centroid.X, centroid.Y, Util.FootToM(bottom)),
                        new gbXYZ(centroid.X, centroid.Y, Util.FootToM(summit))
                        );

                    // or: (this may not work for columns)
                    //LocationCurve lc = fi.Location as LocationCurve;
                    //gbSeg colAxis = Util.gbSegConvert(lc.Curve as Line);

                    for (int i = 0; i < levels.Count; i++)
                    {
                        // add location point if the column lies within the range of this level
                        // this is irrelevant to its host level
                        // sometimes the levels from linked file are not corresponding to the current model
                        if (//fi.LevelId == levels[i].id ||
                           summit >= (levels[i].elevation + 0.5 * levels[i].height) &&
                           bottom <= (levels[i].elevation + 0.5 * levels[i].height))
                        {

                            // make it a closed polygon
                            if (colPoly.Count > 0)
                            {
                                colPoly.Add(colPoly[0]);
                                dictColumn[i].Add(new Tuple<string, string, List<gbXYZ>, gbSeg>(
                                    fi.Id.ToString(), fi.Name, colPolyBox, colAxis));
                            }
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
                    double summit = ge.GetBoundingBox().Max.Z - Properties.Settings.Default.offsetZ;
                    double bottom = ge.GetBoundingBox().Min.Z - Properties.Settings.Default.offsetZ;
                    //Debug.Print("Beam upper limit: " + Util.FootToM(summit).ToString());

                    // prepare the geometry
                    List<CurveLoop> beamCrvLoops = GetFootprintOfBeam(fi);
                    if (beamCrvLoops.Count == 0)
                        continue;
                    List<gbXYZ> beamPoly = new List<gbXYZ>();
                    foreach (Curve crv in beamCrvLoops[0])
                    {
                        if (crv is Line)
                        {
                            beamPoly.Add(Util.gbXYZConvert(crv.GetEndPoint(0)));
                        }
                        else
                        {
                            List<XYZ> ptsTessellated = new List<XYZ>(crv.Tessellate());
                            ptsTessellated.RemoveAt(ptsTessellated.Count - 1);
                            beamPoly.AddRange(Util.gbXYZsConvert(ptsTessellated));
                        }
                    }

                    if (beamPoly[0].Z > 20)
                        Debug.Print($"error at iteration {fiBeams.IndexOf(fi)}");

                    for (int i = 0; i < levels.Count; i++)
                    {
                        //Debug.Print("Level height: " + Util.FootToM(levels[i].elevation + levels[i].height).ToString());
                        // compare the upper limit and the level elevation
                        // assume a tolerance of 0.2m
                        if (Math.Abs(summit - (levels[i].elevation + levels[i].height)) < Util.MToFoot(0.2))
                        {
                            //Debug.Print("Beam location: " + Util.gbSegConvert(lc.Curve as Line).ToString());
                            dictBeam[i].Add(new Tuple<string, string, List<gbXYZ>, gbSeg>(
                                fi.Id.ToString(), fi.Name, beamPoly, Util.gbSegConvert(lc.Curve as Line)));
                            break;
                        }
                    }
                }
            }

            // ABANDONED. now
            // add the roof level at last (almost with no info)
            //dictElevation.Add(dictElevation.Count, new Tuple<string, double>("Roof",
            //    Util.FootToM(levels.Last().elevation + levels.Last().height)));

            // update the coordinates according to project rotation angle (if exists)
            // considering to embed it in each loop
            if (sets.originTheta != 0)
            {
                double theta = sets.originTheta / 180 * Math.PI;
                for (int i = 0; i < dictWall.Count; i++)
                {
                    dictWall[i] = GBMethod.transCoords(dictWall[i], theta);
                    dictWallPatch[i] = GBMethod.transCoords(dictWallPatch[i], theta);
                    dictCurtain[i] = GBMethod.transCoords(dictCurtain[i], theta);
                    dictCurtaSystem[i] = GBMethod.transCoords(dictCurtaSystem[i], theta);
                    dictSeparationline[i] = GBMethod.transCoords(dictSeparationline[i], theta);
                    dictFirewall[i] = GBMethod.transCoords(dictFirewall[i], theta);
                    dictGrid[i] = GBMethod.transCoords(dictGrid[i], theta);

                    for (int j = 0; j < dictColumn[i].Count; j++)
                    {
                        var transCol = new Tuple<string, string, List<gbXYZ>, gbSeg>(
                            dictColumn[i][j].Item1, dictColumn[i][j].Item2, 
                            GBMethod.transCoords(dictColumn[i][j].Item3, theta),
                            GBMethod.transCoords(dictColumn[i][j].Item4, theta));
                        dictColumn[i][j] = transCol;
                    }
                    for (int j = 0; j < dictBeam[i].Count; j++)
                    {
                        var transBeam = new Tuple<string, string, List<gbXYZ>, gbSeg>(
                            dictBeam[i][j].Item1, dictBeam[i][j].Item2,
                            GBMethod.transCoords(dictBeam[i][j].Item3, theta),
                            GBMethod.transCoords(dictBeam[i][j].Item4, theta));
                        dictColumn[i][j] = transBeam;
                    }
                    for (int j = 0; j < dictWindow[i].Count; j++)
                    {
                        var transWin = new Tuple<gbXYZ, string>(
                            GBMethod.transCoords(dictWindow[i][j].Item1, theta), dictWindow[i][j].Item2);
                        dictWindow[i][j] = transWin;
                    }
                    for (int j = 0; j < dictDoor[i].Count; j++)
                    {
                        var transDoor = new Tuple<gbXYZ, string>(
                            GBMethod.transCoords(dictDoor[i][j].Item1, theta), dictDoor[i][j].Item2);
                        dictDoor[i][j] = transDoor;
                    }

                    for (int j = 0; j < dictFloor[i].Count; j++)
                    {
                        for (int k = 0; k < dictFloor[i][j].Count; k++)
                        {
                            dictFloor[i][j][k] = GBMethod.transCoords(dictFloor[i][j][k], theta);
                        }
                    }
                    for (int j = 0; j < dictFloor[i].Count; j++)
                    {
                        dictShade[i][j] = GBMethod.transCoords(dictShade[i][j], theta);
                    }
                    for (int j = 0; j < dictRoom[i].Count; j++)
                    {
                        var newLoop = new List<List<gbXYZ>>() { };
                        for (int k = 0; k < dictRoom[i][j].Item1.Count; k++)
                            newLoop.Add(GBMethod.transCoords(dictRoom[i][j].Item1[k], theta));
                        var transRoom = new Tuple<List<List<gbXYZ>>, string>(newLoop, dictRoom[i][j].Item2);
                        dictRoom[i][j] = transRoom;
                    }
                }
            }

            // filter out null values?
            for (int i = 0; i < dictWall.Count; i++)
            {
                dictWall[i].RemoveAll(item => item == null);
                dictWallPatch[i].RemoveAll(item => item == null);
                dictCurtain[i].RemoveAll(item => item == null);
                dictCurtaSystem[i].RemoveAll(item => item == null);
                dictSeparationline[i].RemoveAll(item => item == null);
                dictColumn[i].RemoveAll(item => item.Item1 == null);
                dictBeam[i].RemoveAll(item => item.Item1 == null);
            }

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

        static List<CurveLoop> GetFootprintOfBeam(FamilyInstance fi)
        {
            List<CurveLoop> footprints = new List<CurveLoop>();
            LocationCurve lc = fi.Location as LocationCurve;
            if (!(lc.Curve is Line))
                return footprints;

            Line lc_line = lc.Curve as Line;
            gbXYZ axis_dir = Util.gbXYZConvert(lc_line.Direction);

            //// skip if it has a vertical axis /no use
            if (axis_dir.Z > 0.000001)
                return footprints;

            //// skip for invalid axis /no use
            if (axis_dir.Norm() < 0.1)
                return footprints;

            Options opt = new Options();
            opt.ComputeReferences = true;
            opt.DetailLevel = ViewDetailLevel.Medium;
            GeometryElement ge = fi.get_Geometry(opt);

            foreach (GeometryObject obj in ge)
            {
                if (obj is Solid)
                {
                    Solid so = obj as Solid;
                    Face bottomFace = null;
                    foreach (Face f in so.Faces)
                    {
                        gbXYZ normal = Util.gbXYZConvert(f.ComputeNormal(new UV(0, 0)));
                        // note that axis.Direction is on XY plane
                        // the bottom face has to be vertical to the XY plane
                        if (Math.Abs(GBMethod.VectorAngle2PI(normal, axis_dir) - Math.PI)< 0.017)
                        {
                            bottomFace = f;
                        }
                    }

                    if (null != bottomFace)
                    {
                        // skip beam with too small section (100*100mm as minimum)
                        if (bottomFace.Area > 11 || bottomFace.Area < 0.11)
                            return footprints;

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
                            Solid so2 = obj2 as Solid;
                            Face bottomFace = null;
                            foreach (Face f in so2.Faces)
                            {
                                gbXYZ normal = Util.gbXYZConvert(f.ComputeNormal(new UV(0, 0)));
                                if (Math.Abs(GBMethod.VectorAngle2PI(normal, axis_dir) - Math.PI) < 0.017)
                                {
                                    bottomFace = f;
                                }
                            }
                            if (null != bottomFace)
                            {
                                // skip beam with too small section (100*100mm as minimum, 1m2 as maximum)
                                if (bottomFace.Area > 11 || bottomFace.Area < 0.11)
                                    return footprints;

                                foreach (CurveLoop edge in bottomFace.GetEdgesAsCurveLoops())
                                {
                                    footprints.Add(edge);
                                }
                            }
                        }
                    }
                }
            }

            return footprints;
        }

        // Private method
        // Iterate to get the bottom face of a solid
        static Face GetSolidBottomFace(Solid solid)
        {
            List<Face> faces = new List<Face>() { };
            List<double> elevations = new List<double>() { };
            double min = double.PositiveInfinity;
            PlanarFace pf = null;
            foreach (Face face in solid.Faces)
            {
                pf = face as PlanarFace;
                if (null != pf)
                {
                    if (Core.Basic.IsVertical(pf.FaceNormal, Properties.Settings.Default.tolDouble)
                        && pf.FaceNormal.Z < 0)
                    {
                        faces.Add(pf);
                        elevations.Add(pf.Origin.Z);
                        if (elevations.Last() < min) min = elevations.Last();
                    }
                }
            }
            if (faces.Count == 0) return null;
            return faces[elevations.IndexOf(min)];
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