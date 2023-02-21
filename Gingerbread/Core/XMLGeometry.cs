using System;
using System.Collections.Generic;
using ClipperLib;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Gingerbread.Core
{
    class XMLGeometry
    {
        public static void Generate(
            Dictionary<int, Tuple<string, double>> dictElevation,
            Dictionary<int, List<gbRegion>> dictRegion,
            //Dictionary<int, List<List<gbXYZ>>> dictLoop,
            Dictionary<int, List<List<gbXYZ>>> dictShell,
            //Dictionary<int, List<List<string>>> dictMatch,
            Dictionary<int, List<Tuple<gbXYZ, string>>> dictWindow,
            Dictionary<int, List<Tuple<gbXYZ, string>>> dictDoor,
            Dictionary<int, List<Tuple<List<gbXYZ>, string>>> dictColumn, 
            Dictionary<int, List<Tuple<gbSeg, string>>> dictBeam,
            Dictionary<int, List<gbSeg>> dictCurtain,
            Dictionary<int, List<gbSeg>> dictAirwall,
            Dictionary<int, List<List<List<gbXYZ>>>> dictFloor, 
            Dictionary<int, List<List<gbXYZ>>> dictShade,
            Dictionary<int, List<Tuple<List<List<gbXYZ>>, string>>> dictRoom, 
            out List<gbZone> zones,
            out List<gbLoop> floors,
            out List<gbSurface> surfaces,
            out List<gbLoop> columns, 
            out List<gbLoop> beams, 
            out List<gbLoop> shafts)
        {
            List<gbLevel> levels = new List<gbLevel>();
            int numLevels = dictElevation.Keys.Count;
            foreach (KeyValuePair<int, Tuple<string, double>> kvp in dictElevation)
                levels.Add(new gbLevel(kvp.Key, kvp.Value.Item1, kvp.Value.Item2, numLevels));
            for (int i = 0; i < levels.Count - 1; i++)
                levels[i].height = levels[i + 1].elevation - levels[i].elevation;

            
            //foreach (gbLevel level in levels)
                //Debug.Print("XMLGeometry:: " + $"On level {level.id} elevation {level.elevation} height {level.height}");

            // cached intermediate data
            zones = new List<gbZone>();
            surfaces = new List<gbSurface>();
            floors = new List<gbLoop>();
            // cached spaces by floor for surface matching across levels
            Dictionary<int, List<gbZone>> dictZone = new Dictionary<int, List<gbZone>>();


            //List<List<string>> srfIds = new List<List<string>>();
            //List<List<Line>> boundaryLines = new List<List<Line>>();

            // global opening size regex pattern
            // 0000 x 0000 is the default naming for all opening family types, for now
            string sizeMockup = @"\d+";

            // 1st loop to add spaces, walls, adjacency and adhering openings
            foreach (gbLevel level in levels)
            {
                Util.LogPrint($"----------------Shape the wall on Level-{level.id}----------------");
                // leave the roof level for special cares
                if (level.isTop) continue;
                // check if all the dictionary is positioned
                if (!dictRegion.ContainsKey(level.id) ||
                    !dictShell.ContainsKey(level.id) ||
                    !dictWindow.ContainsKey(level.id) ||
                    !dictDoor.ContainsKey(level.id) ||
                    !dictCurtain.ContainsKey(level.id) ||
                    !dictFloor.ContainsKey(level.id))
                    continue;

                // prepare the label list 
                List<Tuple<gbXYZ, double>> hollows = new List<Tuple<gbXYZ, double>>();
                foreach (List<List<gbXYZ>> panel in dictFloor[level.id])
                    for (int i = 1; i < panel.Count; i++)
                    {
                        double area = GBMethod.GetPolyArea(panel[i]);
                        gbXYZ centroid = GBMethod.GetPolyCentroid(panel[i]);
                        hollows.Add(new Tuple<gbXYZ, double>(centroid, area));
                    }

                // add new floors
                foreach (List<gbXYZ> blockShell in dictShell[level.id])
                    floors.Add(new gbLoop("F" + level.id, level, blockShell, 0));

                List<gbZone> thisZone = new List<gbZone>();
                List<gbSurface> thisSurface = new List<gbSurface>();
                int shaftCount = 0;
                int stairCount = 0;
                int voidCount = 0;
                foreach (gbRegion region in dictRegion[level.id])
                {
                    // skip the void region that is just a place holder
                    if (region.loop == null || region.loop.Count == 0)
                        continue;

                    //Debug.Print($"XMLGeometry:: Heading for {region.label}");
                    gbZone newZone = new gbZone(region.label, level, region);
                    //newZone.function = "Office";

                    if (dictRoom.ContainsKey(level.id))
                    {
                        foreach (Tuple<List<List<gbXYZ>>, string> roomLabel in dictRoom[level.id])
                        {
                            // do intersection clip between a MCR room boundary and the region
                            List<List<gbXYZ>> polyRegion = new List<List<gbXYZ>>() { region.loop };
                            if (region.innerLoops != null)
                                polyRegion.AddRange(region.innerLoops);
                            // ClipPoly has many overloads to handle simply/multiply connected regions
                            List<List<gbXYZ>> polyOverlap = GBMethod.ClipPoly(roomLabel.Item1,
                                polyRegion, ClipType.ctIntersection);
                            if (polyOverlap.Count > 0)
                            {
                                double areaOverlap = GBMethod.GetPolysArea(polyOverlap);
                                double areaRatio = areaOverlap / newZone.area;
                                // this range comes from real project experience
                                // by default, the thickness of interior wall, and the column
                                // all take part of the room area. The ratio fluctuates with the area of zone
                                Debug.Print($"XMLGeometry:: Assigning label {newZone.id} | {roomLabel.Item2} : {areaRatio}");
                                if (areaRatio > 0.5 && areaRatio <= 1)
                                {
                                    if (newZone.function == null)
                                    {
                                        newZone.function = roomLabel.Item2;
                                        if (roomLabel.Item2 == "Void")
                                            voidCount++;
                                    }
                                    else
                                    {
                                        Debug.Print($"XMLGeometry:: Space pre-filled with {newZone.function}");
                                    }
                                    //Debug.Print($"XMLGeometry:: Void space detected at {newZone.id}");
                                    break;
                                }
                            }
                        }

                        // as a substitute, if there is no labeling, Shaft/Stair will be assigned
                        // to the void region where the floor has a hole
                        if (newZone.function == null)
                            foreach (Tuple<gbXYZ, double> hollow in hollows)
                                if (GBMethod.IsPtInPoly(hollow.Item1, region.loop, false))
                                {
                                    double areaRatio = hollow.Item2 / newZone.area;
                                    if (areaRatio > 0.5 && areaRatio < 1)
                                    {
                                        if (hollow.Item2 < 10)
                                        {
                                            newZone.function = "Shaft"; shaftCount++;
                                        }

                                        else
                                        {
                                            newZone.function = "Stair"; stairCount++;
                                        }
                                    }
                                }
                    }

                    // check if this zone is exposed (with no ceiling above)
                    if (dictFloor.ContainsKey(level.nextId))
                    {
                        foreach (List<List<gbXYZ>> panel in dictFloor[level.nextId])
                        {
                            foreach (List<gbXYZ> loop in panel)
                            {
                                if (GBMethod.IsClockwise(loop))
                                {
                                    List<List<gbXYZ>> result = GBMethod.ClipPoly(
                                        GBMethod.ElevatePts(loop, 0), region.loop, ClipType.ctIntersection);
                                    double overlap = GBMethod.GetPolysArea(result);
                                    if (overlap / newZone.area > 0.8)
                                        newZone.isExposedAbove = true;
                                }
                            }
                        }
                    }

                    thisZone.Add(newZone);

                    

                    //List<string> srfId = new List<string>();
                    //List<Line> boundaryLine = new List<Line>();

                    //for (int k = 0; k < region.loop.Count - 1; k++)
                    //{
                    //    //srfId.Add(newZone.walls[k].id);
                    //    //boundaryLine.Add(new Line(dictLoop[levelLabel[i]][j][k], dictLoop[levelLabel[i]][j][k + 1]));
                    //    string adjacency = region.match[k];
                    //    newZone.walls[k].adjSrfId = adjacency;
                    //    if (adjacency.Contains("Outside"))
                    //        newZone.walls[k].type = surfaceTypeEnum.ExteriorWall;
                    //    else
                    //        newZone.walls[k].type = surfaceTypeEnum.InteriorWall;
                    //}



                    //srfIds.Add(srfId);
                    //boundaryLines.Add(boundaryLine);
                    thisSurface.AddRange(newZone.walls);
                }
                dictZone.Add(level.id, thisZone);
                surfaces.AddRange(thisSurface);

                Util.LogPrint($"Space: L{level.id} has {thisZone.Count} space, {stairCount} stair, {shaftCount} shaft, {voidCount} void");

                // adhere openings to the surface. Note that the overlapping openings are forbidden, 
                // so following this order we create windows first because they are least likely to be mis-drawn 
                // by people and of higher importance in building simulation. Then if the curtain wall has a 
                // collision with windows, cancel its generation. 
                //Debug.Print($"XMLGeometry:: this level-{level.id} has {dictWindow[level.id].Count} windows");
                foreach (Tuple<gbXYZ, string> opening in dictWindow[level.id])
                {
                    //Debug.Print($"XMLGeometry:: window {opening.Item1} {opening.Item2}");
                    // get the planar size of this component
                    // cancel generation if not valid
                    List<double> sizes = new List<double>();
                    foreach (Match match in Regex.Matches(opening.Item2, sizeMockup))
                    {
                        double size = Convert.ToInt32(match.Value) / 1000.0;
                        sizes.Add(size);
                    }
                    if (sizes.Count != 2)
                        continue;

                    double minDistance = double.PositiveInfinity;
                    double minParam = 0;
                    gbXYZ minPlummet = opening.Item1;
                    int hostId = 0;

                    for (int k = 0; k < thisSurface.Count; k++)
                    {
                        double distance = GBMethod.PtDistanceToSeg(opening.Item1, thisSurface[k].locationLine,
                            out gbXYZ plummet, out double sectParam);
                        distance = Math.Round(distance, 6);
                        if (distance < minDistance && sectParam > 0 && sectParam < 1)
                        {
                            minDistance = distance;
                            minPlummet = plummet;
                            minParam = sectParam;
                            hostId = k;
                        }
                    }
                    // if the projection distance surpass the lattice alignment threshold
                    // skip this component (its host wall may not spawn)
                    if (minDistance > 2 * Properties.Settings.Default.tolAlignment)
                        continue;

                    gbXYZ origin = thisSurface[hostId].locationLine.PointAt(0);
                    gbXYZ vecMove = opening.Item1 - origin; // prepare for opening adhering to srf

                    // these lines all within the relative plane of the host surface
                    List<gbXYZ> openingLoop2d = new List<gbXYZ>();
                    gbXYZ insertPt2d = new gbXYZ(
                        thisSurface[hostId].locationLine.Length * minParam, 
                        opening.Item1.Z - level.elevation, 0);
                    openingLoop2d.Add(insertPt2d + new gbXYZ(- sizes[0] / 2, 0, 0));
                    openingLoop2d.Add(insertPt2d + new gbXYZ(sizes[0] / 2, 0, 0));
                    openingLoop2d.Add(insertPt2d + new gbXYZ(sizes[0] / 2, sizes[1], 0));
                    openingLoop2d.Add(insertPt2d + new gbXYZ(-sizes[0] / 2, sizes[1], 0));

                    thisSurface[hostId].subLoops.Add(openingLoop2d);
                }
                // convert 2D openings hosted by a surface to 3D coords
                foreach (gbSurface srf in thisSurface)
                {
                    List<List<List<gbXYZ>>> loopClusters = GBMethod.PolyClusterByOverlap(srf.subLoops, true);
                    foreach (List<List<gbXYZ>> loopCluster in loopClusters)
                    {
                        if (loopCluster.Count > 1)
                        {
                            Util.LogPrint($"Opening: Overlapping windows detected on {srf.id}");
                        }
                        List<gbXYZ> scatterPts = Util.FlattenList(loopCluster);
                        List<gbXYZ> boundingBox = OrthoHull.GetRectHull(scatterPts);

                        // clip the opening by surface boundary. get the intersected region
                        List<gbXYZ> srfLoop2d = new List<gbXYZ>();
                        srfLoop2d.Add(new gbXYZ(0, 0, 0));
                        srfLoop2d.Add(new gbXYZ(srf.width, 0, 0));
                        srfLoop2d.Add(new gbXYZ(srf.width, srf.height, 0));
                        srfLoop2d.Add(new gbXYZ(0, srf.height, 0));
                        srfLoop2d.Add(srfLoop2d[0]);
                        //Debug.Write($"XMLGeometry:: Windowloop");
                        //foreach (gbXYZ pt in boundingBox)
                        //    Debug.Write($"{pt}-");
                        //Debug.Write($"\n");
                        //Debug.Write($"XMLGeometry:: Srfloop");
                        //foreach (gbXYZ pt in srfLoop2d)
                        //    Debug.Write($"{pt}-");
                        //Debug.Write($"\n");
                        List<gbXYZ> srfOffset = GBMethod.OffsetPoly(srfLoop2d, -0.05)[0];

                        List<gbXYZ> rectifiedOpening = new List<gbXYZ>();
                        if (GBMethod.IsPolyInPoly(boundingBox, srfLoop2d))
                            rectifiedOpening = boundingBox;
                        else
                        {
                            List<List<gbXYZ>> section = GBMethod.ClipPoly(boundingBox, srfOffset, ClipType.ctIntersection);
                            rectifiedOpening = section[0];
                            //Debug.Write($"XMLGeometry:: Rectified");
                            //foreach (gbXYZ pt in rectifiedOpening)
                            //    Debug.Write($"{pt}-");
                            //Debug.Write($"\n");
                            //Debug.Print("Did boolean operation");
                            Util.LogPrint($"Opening: Outbound windows detected on {srf.id}");
                        }
                        RegionTessellate.SimplifyPoly(rectifiedOpening);
                        double openingArea = GBMethod.GetPolyArea(rectifiedOpening);
                        if (openingArea < 0.001)
                        {
                            Util.LogPrint($"Opening: Window < 0.001 will not be added");
                            continue;
                        }

                        // prevent the sum of opening area surpassing the base surface area
                        if (srf.openingArea + openingArea > srf.area)
                        {
                            Util.LogPrint($"Opening: The base surface is too full to host another window");
                            continue;
                        }
                        srf.openingArea += openingArea;

                        //Debug.Print("XMLGeometry:: " + "Srf location: " + srf.locationLine.ToString());
                        gbXYZ srfVec = srf.locationLine.Direction;
                        gbXYZ srfOrigin = srf.locationLine.PointAt(0);
                        List<gbXYZ> openingLoop = new List<gbXYZ>();
                        foreach (gbXYZ pt in rectifiedOpening)
                        {
                            //Debug.Print("XMLGeometry:: " + "Pt before transformation: " + pt.ToString());
                            gbXYZ _pt = pt.SwapPlaneZY().RotateOnPlaneZ(srfVec).Move(srfOrigin);
                            openingLoop.Add(_pt);
                            //Debug.Print("XMLGeometry:: " + "Pt after transformation: " + _pt.ToString());
                        }

                        gbOpening newOpening = new gbOpening(srf.id + "::Opening_" +
                            srf.openings.Count, openingLoop);
                        newOpening.type = openingTypeEnum.FixedWindow;
                        srf.openings.Add(newOpening);
                        srf.subLoops = new List<List<gbXYZ>>();
                    }
                }


                // curtain wall is the most likely to go wild
                foreach (gbSeg opening in dictCurtain[level.id])
                {
                    gbSeg projection;

                    for (int k = 0; k < thisSurface.Count; k++)
                    {
                        // note that the second segment is the baseline
                        // the projection has the same direction as the second segment
                        projection = GBMethod.SegProjection(opening, thisSurface[k].locationLine, 
                            false, out double distance);
                        if (projection.Length > 0.5 && distance < Properties.Settings.Default.tolAlignment)
                        {
                            List<gbXYZ> openingLoop = new List<gbXYZ>();

                            double elevation = thisSurface[k].loop[0].Z;
                            double height = thisSurface[k].height;
                            openingLoop.Add(projection.PointAt(0) + new gbXYZ(0, 0, elevation));
                            openingLoop.Add(projection.PointAt(1) + new gbXYZ(0, 0, elevation));
                            openingLoop.Add(projection.PointAt(1) + new gbXYZ(0, 0, elevation + height));
                            openingLoop.Add(projection.PointAt(0) + new gbXYZ(0, 0, elevation + height));

                            gbOpening newOpening = new gbOpening(thisSurface[k].id + "::Opening_" +
                               thisSurface[k].openings.Count, GBMethod.PolyOffset(openingLoop, 0.05, true))
                            //thisSurface[k].openings.Count, GBMethod.OffsetPoly(openingLoop, 0.1)[0])
                            {
                                width = projection.Length,
                                height = height,
                                type = openingTypeEnum.FixedWindow
                            };
                            // PENDING for more general methods here
                            if (!IsOpeningOverlap(thisSurface[k].openings, newOpening))
                            {
                                thisSurface[k].openingArea += newOpening.area;
                                thisSurface[k].openings.Add(newOpening);
                            }
                        }
                        // record a successful match which is abandoned due to small width
                        else if (projection.Length < 0.5 && projection.Length > 0.000001 && distance < 0.1)
                        {
                            Util.LogPrint($"Glazing: {projection.Length:f4}m one is ignored at {{{projection}}}");
                        }
                        //else
                            //Debug.Print($"Glazing: No adherence found for this curtain wal");
                    }
                }

                // as to doors
                // such process is the same as the one creating windows
                // the door will be abandoned if the accumulated opening area surpasses the base surface area
                //Debug.Print($"XMLGeometry:: There are {dictDoor[level.id].Count} doors on level-{level.id}");
                foreach (Tuple<gbXYZ, string> opening in dictDoor[level.id])
                {
                    //Debug.Print($"XMLGeometry:: Door inserts at {opening.Item1} with dimension {opening.Item2}");
                    // get the planar size of this component 
                    // cancel generation if not valid
                    List<double> sizes = new List<double>();
                    foreach (Match match in Regex.Matches(opening.Item2, sizeMockup))
                    {
                        double size = Convert.ToInt32(match.Value) / 1000.0;
                        sizes.Add(size);
                    }
                    if (sizes.Count != 2)
                        continue;

                    double minDistance = double.PositiveInfinity;
                    double minParam = 0;
                    gbXYZ minPlummet = opening.Item1;
                    int hostId = 0;

                    for (int k = 0; k < thisSurface.Count; k++)
                    {
                        double distance = GBMethod.PtDistanceToSeg(opening.Item1, thisSurface[k].locationLine,
                            out gbXYZ plummet, out double sectParam);
                        // PENDING
                        distance = Math.Round(distance, 6);
                        if (distance < minDistance &&
                            // Math.Abs(distance - minDistance) > Properties.Settings.Default.tolDouble &&
                            sectParam > 0 && sectParam < 1)
                        {
                            minDistance = distance;
                            minPlummet = plummet;
                            minParam = sectParam;
                            hostId = k;
                        }
                    }
                    //Debug.Print($"XMLGeometry:: {thisSurface[hostId].id} got insert point {minPlummet} with distance {minDistance}");

                    // if the projection distance surpass the lattice alignment threshold
                    // skip this component (its host wall may not spawn)
                    if (minDistance > 2 * Properties.Settings.Default.tolAlignment)
                        continue;

                    gbXYZ origin = thisSurface[hostId].locationLine.PointAt(0);
                    gbXYZ vecMove = opening.Item1 - origin; // prepare for opening adhering to srf

                    // these lines all within the relative plane of the host surface
                    List<gbXYZ> openingLoop2d = new List<gbXYZ>();
                    gbXYZ insertPt2d = new gbXYZ(
                        thisSurface[hostId].locationLine.Length * minParam,
                        opening.Item1.Z - level.elevation, 0);
                    openingLoop2d.Add(insertPt2d + new gbXYZ(-sizes[0] / 2, 0, 0));
                    openingLoop2d.Add(insertPt2d + new gbXYZ(sizes[0] / 2, 0, 0));
                    openingLoop2d.Add(insertPt2d + new gbXYZ(sizes[0] / 2, sizes[1], 0));
                    openingLoop2d.Add(insertPt2d + new gbXYZ(-sizes[0] / 2, sizes[1], 0));

                    thisSurface[hostId].subLoops.Add(openingLoop2d);
                }
                // convert 2D openings hosted by a surface to 3D coords
                foreach (gbSurface srf in thisSurface)
                {
                    List<List<List<gbXYZ>>> loopClusters = GBMethod.PolyClusterByOverlap(srf.subLoops, true);

                    // what if the subLoops are isolated from the host surface?
                    // there might be mulfunctions in preprocessing 5/2/23

                    foreach (List<List<gbXYZ>> loopCluster in loopClusters)
                    {
                        if (loopCluster.Count > 1)
                        {
                            Util.LogPrint($"Opening: Overlapping doors detected on {srf.id}");
                        }

                        List<gbXYZ> scatterPts = Util.FlattenList(loopCluster);
                        List<gbXYZ> boundingBox = OrthoHull.GetRectHull(scatterPts);

                        // clip by surface boundary. take intersection
                        List<gbXYZ> srfLoop2d = new List<gbXYZ>();
                        srfLoop2d.Add(new gbXYZ(0, 0, 0));
                        srfLoop2d.Add(new gbXYZ(srf.width, 0, 0));
                        srfLoop2d.Add(new gbXYZ(srf.width, srf.height, 0));
                        srfLoop2d.Add(new gbXYZ(0, srf.height, 0));
                        srfLoop2d.Add(srfLoop2d[0]);
                        //Debug.Write($"XMLGeometry:: Doorloop");
                        //foreach (gbXYZ pt in boundingBox)
                        //    Debug.Write($"{pt}-");
                        //Debug.Write($"\n");
                        //Debug.Write($"XMLGeometry:: Srfloop");
                        //foreach (gbXYZ pt in srfLoop2d)
                        //    Debug.Write($"{pt}-");
                        //Debug.Write($"\n");
                        List<gbXYZ> srfOffset = GBMethod.OffsetPoly(srfLoop2d, -0.05)[0];

                        List<gbXYZ> rectifiedOpening = new List<gbXYZ>();
                        if (GBMethod.IsPolyInPoly(boundingBox, srfLoop2d))
                            rectifiedOpening = boundingBox;
                        else
                        {
                            List<List<gbXYZ>> section = GBMethod.ClipPoly(boundingBox, srfOffset, ClipType.ctIntersection);
                            // boolean intersect between boundingBox and srfOffset may yield nothing
                            if (section.Count == 0) // abandon boolean substraction
                                continue;

                            rectifiedOpening = section[0];
                            //Debug.Write($"XMLGeometry:: Rectified");
                            //foreach (gbXYZ pt in rectifiedOpening)
                            //    Debug.Write($"{pt}-");
                            //Debug.Write($"\n");
                            //Debug.Print("Did boolean operation");
                            Util.LogPrint($"Opening: Outbound doors detected on {srf.id}");
                        }
                        RegionTessellate.SimplifyPoly(rectifiedOpening);
                        double openingArea = GBMethod.GetPolyArea(rectifiedOpening);
                        if (openingArea < 0.001)
                        {
                            Util.LogPrint($"Opening: Door < 0.001 will not be added");
                            continue;
                        }
                            
                        // prevent the sum of opening area surpassing the base surface area
                        if (srf.openingArea + openingArea > srf.area)
                        {
                            Util.LogPrint($"Opening: The base surface is too full to host another door");
                            continue;
                        }
                        srf.openingArea += openingArea;

                        // the returning rectified Opening has already been an open loop
                        //rectifiedOpening.RemoveAt(rectifiedOpening.Count - 1); // transfer to open polyloop

                        //Debug.Print("XMLGeometry:: " + "Srf location: " + srf.locationLine.ToString());
                        gbXYZ srfVec = srf.locationLine.Direction;
                        gbXYZ srfOrigin = srf.locationLine.PointAt(0);
                        List<gbXYZ> openingLoop = new List<gbXYZ>();
                        foreach (gbXYZ pt in rectifiedOpening)
                        {
                            //Debug.Print("XMLGeometry:: " + "Pt before transformation: " + pt.ToString());
                            gbXYZ _pt = pt.SwapPlaneZY().RotateOnPlaneZ(srfVec).Move(srfOrigin);
                            openingLoop.Add(_pt);
                            //Debug.Print("XMLGeometry:: " + "Pt after transformation: " + _pt.ToString());
                        }
                        gbOpening newOpening = new gbOpening(srf.id + "::Opening_" +
                            srf.openings.Count, openingLoop);
                        newOpening.type = openingTypeEnum.NonSlidingDoor;
                        srf.openings.Add(newOpening);
                        srf.subLoops = new List<List<gbXYZ>>();
                    }
                }

                // airwall assignment to interior walls. air boundary as for ceiling or floor
                // will be left to the serialization process
                foreach (gbSeg airwall in dictAirwall[level.id])
                {
                    gbSeg projection;

                    for (int k = 0; k < thisSurface.Count; k++)
                    {
                        // the surface that already hosts an opening will not be the airwall
                        if (thisSurface[k].openings.Count > 0)
                        {
                            Util.LogPrint($"Airwall: type conversion forbidden because the surface already hosts an opening");
                            continue;
                        }
                            
                        // note that the second segment is the baseline
                        // the projection has the same direction as the second segment
                        projection = GBMethod.SegProjection(airwall, thisSurface[k].locationLine,
                            false, out double distance);
                        if (projection.Length > 0.5 && distance < Properties.Settings.Default.tolAlignment)
                        {
                            thisSurface[k].type = surfaceTypeEnum.Air;
                            Util.LogPrint($"Airwall: Space separation added to {thisSurface[k].id}");
                        }
                    }
                }
            }

            // 2nd loop solve the adjacency among floors
            // perform on already created zones
            // prepare a counter for shadowing level indexation
            int shadowingCounter = 0;

            foreach (gbLevel level in levels)
            {
                if (level.isTop) continue;

                Util.LogPrint($"----------------Shape the floor on Level-{level.id}----------------");

                // sum of area ratio to check if the next floor is shadowing the current one
                List<double> sumSimilarity = new List<double>();

                foreach (gbZone zone in dictZone[level.id])
                {
                    //Debug.Print($"XMLGeometry:: #{levels.IndexOf(level)} iteration to {dictZone[level.id].IndexOf(zone)}");
                    // ground slab or roof check
                    // translate zone tiles to surfaces
                    if (level.isBottom)
                    {
                        surfaceTypeEnum surfaceTypeDef = surfaceTypeEnum.SlabOnGrade;
                        if (level.isBasement)
                            surfaceTypeDef = surfaceTypeEnum.UndergroundSlab;
                        //if (zone.level.elevation - 0 > 0.1)
                        //    surfaceTypeDef = surfaceTypeEnum.RaisedFloor;

                        // to prevent empty tessellation
                        if (zone.tiles == null)
                        {
                            //Debug.Print("XMLGeometry:: WARNING No tiles due to MCR tessellation failure!");
                        }
                        else if (zone.tiles.Count == 1)
                        {
                            // the existence of zone.loop has been checked before
                            // consider to add another check here
                            List<gbXYZ> dupPoly = GBMethod.ReorderPoly(GBMethod.GetDuplicatePoly(zone.loop));
                            RegionTessellate.SimplifyPoly(dupPoly);
                            dupPoly.Reverse();

                            gbSurface floor = new gbSurface(zone.id + "::Floor_0", zone.id, dupPoly, 180);
                            floor.type = surfaceTypeDef;
                            floor.adjSrfId = "Outside";
                            zone.floors.Add(floor);
                        }
                        else
                        {
                            int counter = 0;
                            foreach (List<gbXYZ> tile in zone.tiles)
                            {
                                // to prevent empty tile loop
                                if (tile.Count == 0)
                                {
                                    //Debug.Print($"XMLGeometry:: WARNING This MCR zone tile is null!");
                                    continue;
                                }

                                List<gbXYZ> dupTile = GBMethod.ReorderPoly(GBMethod.GetDuplicatePoly(tile));
                                RegionTessellate.SimplifyPoly(dupTile);
                                dupTile.Reverse();

                                gbSurface floor = new gbSurface(zone.id + "::Floor_0", zone.id, dupTile, 180);
                                floor.type = surfaceTypeDef;
                                floor.adjSrfId = "Outside";
                                zone.floors.Add(floor);
                                counter++;
                            }
                        }
                    }
                    
                    if (level.id == levels.Count - 2)
                    {
                        //if (zone.tiles.Count == 1)
                        //{
                        //    gbSurface ceiling = new gbSurface(zone.id + "::Ceil_0", zone.id,
                        //    GBMethod.ElevatePtsLoop(zone.loop, level.height), 0);
                        //    ceiling.type = surfaceTypeEnum.Roof;
                        //    ceiling.adjSrfId = "Outside";
                        //    zone.ceilings.Add(ceiling);
                        //}
                        int counter = 0;
                        //List<List<gbXYZ>> skyLights = new List<List<gbXYZ>>();
                        //if (dictFloor.ContainsKey(level.id + 1))
                        //    foreach (List<List<gbXYZ>> slab in dictFloor[level.id + 1])
                        //        foreach (List<gbXYZ> loop in slab)
                        //            if (GBMethod.IsClockwise(loop))
                        //                skyLights.Add(loop);
                        foreach (List<gbXYZ> tile in zone.tiles)
                        {
                            RegionTessellate.SimplifyPoly(tile);
                            List<gbXYZ> tilePoly = GBMethod.ReorderPoly(GBMethod.ElevatePts(tile, level.height));
                            gbSurface ceilingTile = new gbSurface(zone.id + "::Ceil_" + counter, zone.id,
                                tilePoly, 0);
                            ceilingTile.type = surfaceTypeEnum.Roof;
                            ceilingTile.adjSrfId = "Outside";
                            //if (skyLights.Count > 0)
                            //{
                            //    Debug.Print("XMLGeometry:: Roof detected for skylights");
                            //    List<List<gbXYZ>> results = GBMethod.ClipPoly(new List<List<gbXYZ>>() { tile },
                            //        skyLights, ClipType.ctIntersection);
                            //    if (results.Count > 0)
                            //    {
                            //        for (int i = 0; i < results.Count; i++)
                            //        {
                            //            gbOpening panel = new gbOpening(ceilingTile.id + "::Opening_" + i,
                            //                GBMethod.ElevatePtsLoop(results[i], level.elevation + level.height));
                            //            panel.type = openingTypeEnum.FixedSkylight;
                            //            ceilingTile.openings.Add(panel);
                            //            Debug.Print("XMLGeometry:: skylight added");
                            //        }
                            //    }
                            //}
                            zone.ceilings.Add(ceilingTile);
                            counter++;
                        }
                    }

                    // offset roof check
                    // clip the zone tiles then translate them to surfaces
                    // note that a level may include multiple block shells  
                    if (level.id != levels.Count - 2 && dictShell.ContainsKey(level.nextId))
                    {
                        int containmentCounter = 0;
                        foreach (List<gbXYZ> blockShell in dictShell[level.nextId])
                            if (GBMethod.IsPolyInPoly(GBMethod.ElevatePts(zone.loop, 0), blockShell))
                                containmentCounter++;
                        if (containmentCounter == 0) // not in any blockShell
                        {
                            List<List<gbXYZ>> sectLoops = new List<List<gbXYZ>>();
                            foreach (List<gbXYZ> tile in zone.tiles)
                            {
                                List<List<gbXYZ>> result = GBMethod.ClipPoly(
                                    new List<List<gbXYZ>>() { GBMethod.ElevatePts(tile, 0) },
                                    dictShell[level.nextId], ClipType.ctDifference);
                                sectLoops.AddRange(result);
                                //Debug.Print($"XMLGeometry:: Interval roof {zone.id} section - {result.Count}");
                            }
                            if (sectLoops.Count != 0)
                            {
                                // duplicate sectLoops only for debugging
                                // if stable just modify sectLoops directly
                                List<List<gbXYZ>> rawLoops = new List<List<gbXYZ>>();
                                for (int j = sectLoops.Count - 1; j >= 0; j--)
                                {
                                    List<gbXYZ> rawLoop = sectLoops[j];
                                    RegionTessellate.SimplifyPoly(rawLoop);
                                    if (rawLoop.Count != 0)
                                    {
                                        // always keep the loop closed
                                        rawLoop.Add(rawLoop[0]);
                                        rawLoops.Add(rawLoop);
                                    }
                                }
                                // in case the the loops are nested, so do sort out mcrs first
                                List<List<List<gbXYZ>>> mcrs = SortMultiplyConnectedRegions(rawLoops);
                                for (int j = 0; j < mcrs.Count; j++)
                                {
                                    bool checker3 = GBMethod.IsConvex(mcrs[j][0]);
                                    if (mcrs[j].Count == 0) continue;
                                    if (mcrs[j].Count == 1 && mcrs[j][0].Count != 0)
                                    {
                                        // for a simply connected region
                                        // no big deal if it is convex or concave
                                        List<gbXYZ> tilePoly = GBMethod.ReorderPoly(
                                            GBMethod.ElevatePts(mcrs[j][0], level.elevation + level.height));
                                        gbSurface splitCeil = new gbSurface(zone.id + "::Ceil_" + zone.ceilings.Count, zone.id,
                                            tilePoly, 0);
                                        splitCeil.adjSrfId = "Outside";
                                        splitCeil.type = surfaceTypeEnum.Roof;
                                        zone.ceilings.Add(splitCeil);
                                    }
                                    else
                                    {
                                        // for a multiply connected region
                                        List<List<gbXYZ>> casters = RegionTessellate.Rectangle(mcrs[j]);
                                        foreach (List<gbXYZ> caster in casters)
                                        {
                                            List<gbXYZ> tilePoly = GBMethod.ReorderPoly(
                                                GBMethod.ElevatePts(caster, level.elevation + level.height));
                                            gbSurface splitCeil = new gbSurface(zone.id + "::Ceil_" + zone.ceilings.Count, zone.id,
                                                tilePoly, 0);
                                            splitCeil.adjSrfId = "Outside";
                                            splitCeil.type = surfaceTypeEnum.Roof;
                                            zone.floors.Add(splitCeil);
                                        }
                                    }
                                }
                            }
                        }
                    }


                    // exposed floor generation
                    if (!level.isBottom && dictShell.ContainsKey(level.prevId))
                    {
                        int containmentCounter = 0;
                        foreach (List<gbXYZ> blockShell in dictShell[level.prevId])
                            if (GBMethod.IsPolyInPoly(GBMethod.ElevatePts(zone.loop, 0), blockShell))
                                containmentCounter++;
                        if (containmentCounter == 0) // not in any blockShell
                        {
                            List<List<gbXYZ>> sectLoops = new List<List<gbXYZ>>();
                            foreach (List<gbXYZ> tile in zone.tiles)
                            {
                                List<List<gbXYZ>> result = GBMethod.ClipPoly(
                                    new List<List<gbXYZ>>() { GBMethod.ElevatePts(tile, 0) },
                                    dictShell[level.prevId], ClipType.ctDifference);
                                sectLoops.AddRange(result);
                                //Debug.Print($"XMLGeometry:: Exposed floor {zone.id} section - {result.Count}");
                            }

                            // if the previous shell is completely within the the tile
                            // in case that any MCR is generated within sectLoops
                            // you must check the CW/CCW of the loop and containment
                            if (sectLoops.Count != 0)
                            {
                                // duplicate sectLoops only for debugging
                                // if stable just modify sectLoops directly
                                List<List<gbXYZ>> rawLoops = new List<List<gbXYZ>>();
                                for (int j = sectLoops.Count - 1; j >= 0; j--)
                                {
                                    List<gbXYZ> rawLoop = sectLoops[j];
                                    RegionTessellate.SimplifyPoly(rawLoop);
                                    if (rawLoop.Count != 0)
                                    {
                                        // always keep the loop closed
                                        rawLoop.Add(rawLoop[0]);
                                        rawLoops.Add(rawLoop);
                                    }
                                }
                                // in case the the loops are nested, so do sort out mcrs first
                                List<List<List<gbXYZ>>> mcrs = SortMultiplyConnectedRegions(rawLoops);
                                for (int j = 0; j < mcrs.Count; j++)
                                {
                                    bool checker3 = GBMethod.IsConvex(mcrs[j][0]);
                                    if (mcrs[j].Count == 0) continue;
                                    if (mcrs[j].Count == 1 && GBMethod.IsConvex(mcrs[j][0]) && mcrs[j][0].Count != 0)
                                    {
                                        // for a convex AND simply connected region
                                        List<gbXYZ> revLoop = GBMethod.ReorderPoly(
                                            GBMethod.ElevatePts(mcrs[j][0], level.elevation));
                                        revLoop.Reverse();
                                        gbSurface splitFloor = new gbSurface(zone.id + "::Floor_" + zone.floors.Count, zone.id,
                                            revLoop, 180);
                                        splitFloor.adjSrfId = "Outside";
                                        if (level.isBasement)
                                            splitFloor.type = surfaceTypeEnum.UndergroundSlab;
                                        else if (level.isGround)
                                            splitFloor.type = surfaceTypeEnum.SlabOnGrade;
                                        else
                                            splitFloor.type = surfaceTypeEnum.RaisedFloor;
                                        zone.floors.Add(splitFloor);
                                    }
                                    else
                                    {
                                        // for a concave simply connected region
                                        // and a multiply connected region
                                        List<List<gbXYZ>> casters = RegionTessellate.Rectangle(mcrs[j]);
                                        foreach (List<gbXYZ> caster in casters)
                                        {
                                            List<gbXYZ> revLoop = GBMethod.ReorderPoly(
                                                GBMethod.ElevatePts(caster, level.elevation));
                                            revLoop.Reverse();
                                            gbSurface splitFloor = new gbSurface(zone.id + "::Floor_" + zone.floors.Count, zone.id,
                                                revLoop, 180);
                                            splitFloor.adjSrfId = "Outside";
                                            if (level.isBasement)
                                                splitFloor.type = surfaceTypeEnum.UndergroundSlab;
                                            else if (level.isGround)
                                                splitFloor.type = surfaceTypeEnum.SlabOnGrade;
                                            else
                                                splitFloor.type = surfaceTypeEnum.RaisedFloor;
                                            zone.floors.Add(splitFloor);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // interior floor adjacency check
                    // clip the tiles, do the matching, then transfer to the surfaces
                    if (level.id != levels.Count - 2 && dictZone.ContainsKey(level.id + 1))
                    {
                        // if no data within dictZone[level.id + id], cancel the matching
                        foreach (gbZone adjZone in dictZone[level.id + 1])
                        {
                            // better way to reduce the calculation?
                            List<List<gbXYZ>> sectLoops = new List<List<gbXYZ>>();
                            foreach (List<gbXYZ> tile in zone.tiles)
                            {
                                foreach (List<gbXYZ> adjTile in adjZone.tiles)
                                {
                                    List<List<gbXYZ>> result = GBMethod.ClipPoly(tile, adjTile, ClipType.ctIntersection);
                                    sectLoops.AddRange(result);
                                }
                            }


                            // record the area of each tile
                            // calculate the sigma(tile area/zone area)^2 as an index
                            // for floorplan similarity
                            double zoneSimilarity = 0;
                            foreach (List<gbXYZ> sectLoop in sectLoops)
                            {
                                double r1 = GBMethod.GetPolyArea(sectLoop) / zone.area;
                                double r2 = GBMethod.GetPolyArea(sectLoop) / adjZone.area;
                                zoneSimilarity += r1 < r2 ? Math.Pow(r1, 2) : Math.Pow(r2, 2);
                            }
                            // if boundary loops of the two region are not intersected at all
                            // ignore it
                            if (zoneSimilarity > 0)
                                sumSimilarity.Add(zoneSimilarity);

                            if (sectLoops.Count == 0)
                                continue;
                            for (int j = 0; j < sectLoops.Count; j++)
                            {
                                RegionTessellate.SimplifyPoly(sectLoops[j]);
                                if (sectLoops[j].Count <= 2)
                                    continue;
                                // the name does not matter
                                // they only have to stay coincident so the adjacent spaces can be tracked
                                string splitCeilId = zone.id + "::Ceil_" + zone.ceilings.Count;
                                string splitFloorId = adjZone.id + "::Floor_" + zone.floors.Count;
                                // be cautious here
                                // the ceiling here mean the shadowing floor, so the tilt is still 180
                                List<gbXYZ> dupLoop = GBMethod.ReorderPoly(
                                    GBMethod.ElevatePts(sectLoops[j], adjZone.level.elevation));
                                dupLoop.Reverse();

                                gbSurface splitCeil = new gbSurface(splitCeilId, zone.id, dupLoop, 180);
                                gbSurface splitFloor = new gbSurface(splitFloorId, adjZone.id, dupLoop, 180);

                                splitCeil.adjSrfId = splitFloorId;
                                splitCeil.type = surfaceTypeEnum.InteriorFloor;
                                zone.ceilings.Add(splitCeil);

                                splitFloor.adjSrfId = splitCeilId;
                                splitFloor.type = surfaceTypeEnum.InteriorFloor;
                                adjZone.floors.Add(splitFloor);

                            }
                        }
                    }
                    surfaces.AddRange(zone.floors);
                    surfaces.AddRange(zone.ceilings);
                }

                zones.AddRange(dictZone[level.id]);

                // mark the next floor as shadowing if the similarity index satisfied
                Debug.Print($"XMLGeometry:: similarity check at level-{level.nextId}: {Util.SumDoubles(sumSimilarity) / sumSimilarity.Count}");
                if (Util.SumDoubles(sumSimilarity) / sumSimilarity.Count > 0.95)
                {
                    shadowingCounter++;
                    // this iteration excludes the top floor that can never be removed, level.nestId is safe
                    // the first shadowing floor is the base floor for multiplier, not removable
                    // floor is removable staring from the second shadowing floor
                    if (shadowingCounter > 1)
                    {
                        levels[level.nextId].isShadowing = true;
                        //Debug.Print($"XMLGeometry:: shadowing floor-{level.nextId} detected");
                        Util.LogPrint($"FloorShadowing: Level-{level.nextId} shadows the previous floor plan with similarity {Util.SumDoubles(sumSimilarity) / sumSimilarity.Count}");
                    }
                }
                else
                {
                    shadowingCounter = 0;
                    // when shadowing stops, keep the current level
                    levels[level.id].isShadowing = false;
                }
            }


            // 3rd loop for floor slab clipping and shading surface
            if (Properties.Settings.Default.exportShade == true)
            {
                foreach (gbLevel level in levels)
                {
                    if (!dictShade.ContainsKey(level.id) || !dictZone.ContainsKey(level.id))
                        continue;

                    Util.LogPrint($"----------------Shape the shade on Level-{level.id}----------------");

                    // shadings are from dictShade (orphan floor slab, roof or wall) and dictFloor
                    List<List<gbXYZ>> shellClippers = new List<List<gbXYZ>>();
                    if (dictShell.ContainsKey(level.id))
                        shellClippers = dictShell[level.id];
                    else if (level.isTop && dictShell.ContainsKey(level.prevId))
                        shellClippers = dictShell[level.prevId];
                    else
                        continue;

                    int shadeCounter = 0;
                    // suits for all levels
                    if (dictShade.ContainsKey(level.id))
                    {
                        if (dictShade[level.id].Count != 0)
                        {
                            foreach (List<gbXYZ> shade in dictShade[level.id])
                            {
                                // if the shade is vertical and has 4 vertices
                                // convert it directly as a shading surface
                                if (shade.Count == 4 && shade[2].Z - shade[1].Z > 0.0001)
                                {
                                    gbSurface shading = new gbSurface($"F{level.id}::Shade_{shadeCounter}", "Void", shade, 0);
                                    shading.type = surfaceTypeEnum.Shade;
                                    surfaces.Add(shading);
                                    shadeCounter++;
                                    continue;
                                }

                                // second scenario, tessellation of a general 3D MCR
                                // -------------------pending for algorithms--------------------

                                // else you need to clip it with floorplan to avoid collision
                                int containmentCounter = 0;
                                foreach (List<gbXYZ> clipper in shellClippers)
                                    if (GBMethod.IsPolyInPoly(GBMethod.ElevatePts(shade, 0), clipper))
                                        containmentCounter++;
                                // if the shade is within any blockShell, it will not be regarded as shading surface
                                if (containmentCounter > 0)
                                    continue;
                                else
                                {
                                    List<List<gbXYZ>> results = GBMethod.ClipPoly(
                                        new List<List<gbXYZ>>() { GBMethod.ElevatePts(shade, 0) },
                                        shellClippers, ClipType.ctDifference);
                                    foreach (List<gbXYZ> result in results)
                                    {
                                        RegionTessellate.SimplifyPoly(result);
                                        if (result.Count == 0 || GBMethod.GetPolyArea(result) < 1 || GBMethod.IsClockwise(result))
                                            continue;
                                        gbSurface shading = new gbSurface($"F{level.id}::Shade_{shadeCounter}",
                                            "Void", GBMethod.ReorderPoly(GBMethod.ElevatePts(result, shade[0].Z)), 0);
                                        shading.type = surfaceTypeEnum.Shade;
                                        surfaces.Add(shading);
                                        shadeCounter++;
                                        Util.LogPrint($"Shading: Floating slabs clipped into shade {{{shading.loop[0]}}}");
                                    }
                                }
                            }
                        }
                    }

                    if (!level.isBottom && !level.isTop &&
                        dictFloor.ContainsKey(level.id) && dictShell.ContainsKey(level.prevId))
                    {
                        //shellClippers.AddRange(dictShell[level.prevId]);
                        shellClippers = GBMethod.ClipPoly(shellClippers, dictShell[level.prevId], ClipType.ctUnion);

                        List<List<gbXYZ>> slabShells = new List<List<gbXYZ>>();
                        foreach (List<List<gbXYZ>> slabs in dictFloor[level.id])
                            foreach (List<gbXYZ> slab in slabs)
                                if (!GBMethod.IsClockwise(slab))
                                    slabShells.Add(slab);

                        foreach (List<gbXYZ> slabShell in slabShells)
                        {
                            int containmentCounter = 0;
                            int isolationCounter = shellClippers.Count;
                            foreach (List<gbXYZ> clipper in shellClippers)
                            {
                                if (GBMethod.IsPolyInPoly(GBMethod.ElevatePts(slabShell, 0), clipper))
                                    containmentCounter++;
                                if (GBMethod.IsPolyOutPoly(GBMethod.ElevatePts(slabShell, 0), clipper))
                                    isolationCounter--;
                            }
                            if (isolationCounter == 0)
                            {
                                gbSurface shading = new gbSurface($"F{level.id}::Shade_{shadeCounter}",
                                    "Void", GBMethod.ReorderPoly(slabShell), 0);
                                shading.type = surfaceTypeEnum.Shade;
                                surfaces.Add(shading);
                                shadeCounter++;
                                Util.LogPrint($"Shading: Floor/roof slabs cast as shade {{{shading.loop[0]}}}");
                            }
                            if (containmentCounter == 0)
                            {
                                List<List<gbXYZ>> results = GBMethod.ClipPoly(
                                    new List<List<gbXYZ>>() { GBMethod.ElevatePts(slabShell, 0) },
                                    shellClippers, ClipType.ctDifference);
                                foreach (List<gbXYZ> result in results)
                                {
                                    RegionTessellate.SimplifyPoly(result);
                                    //Debug.Print($"XMLGeometry:: Shading area: {GBMethod.GetPolyArea(result)}");
                                    double area = GBMethod.GetPolyArea(result);
                                    if (area < 1 || result.Count == 0 || GBMethod.IsClockwise(result))
                                    {
                                        //Util.LogPrint($"Shading: Tiny facet ({area:f4} m2) removed");
                                        continue;
                                    }
                                    gbSurface shading = new gbSurface($"F{level.id}::Shade_{shadeCounter}",
                                    "Void", GBMethod.ReorderPoly(GBMethod.ElevatePts(result, level.elevation)), 0);
                                    shading.type = surfaceTypeEnum.Shade;
                                    surfaces.Add(shading);
                                    shadeCounter++;
                                    Util.LogPrint($"Shading: Floor/roof slabs clipped into shade {{{shading.loop[0]}}}");
                                }
                            }
                        }
                    }

                    //Debug.Print($"XMLGeometry:: shadings piled to {shadeCounter}");
                    Util.LogPrint($"L{level.id} has {shadeCounter} shading surfaces generated");

                }
            }


            // 4th loop summarizes all faces for each zone
            // and delete zones exposed to the outside
            // their interior boundary deleted and exterior boundary into shadings
            List<gbZone> delZones = new List<gbZone>();
            List<string> delZoneIds = new List<string>();
            List<string> delSurfaceIds = new List<string>();
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                // critical process to summarize the information
                zones[i].Summarize();
                // try to locate all spaces exposed to outdoor
                // trigger this process ONLY if the user demands to
                if (Properties.Settings.Default.punchMass)
                {
                    if (zones[i].isExposedAbove)
                    {
                        foreach (gbSurface ceiling in zones[i].ceilings)
                        {
                            if (ceiling.type == surfaceTypeEnum.Roof)
                            {
                                delZones.Add(zones[i]);
                                delZoneIds.Add(zones[i].id);
                                foreach (gbSurface delSrf in zones[i].faces)
                                    delSurfaceIds.Add(delSrf.id);
                                break;
                                // delAction = true;
                            }
                            // delZones list is expanding, avoid using foreach..
                            for (int j = 0; j < delZones.Count; j++)
                            {
                                foreach (gbSurface floor in delZones[j].floors)
                                {
                                    if (ceiling.adjSrfId == floor.id &&
                                        !delZoneIds.Contains(zones[i].id))
                                    {
                                        delZones.Add(zones[i]);
                                        delZoneIds.Add(zones[i].id);
                                        foreach (gbSurface delSrf in zones[i].faces)
                                            delSurfaceIds.Add(delSrf.id);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            // iterate zones backward deleting zones exposed to outdoor
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                if (delZoneIds.Contains(zones[i].id))
                    zones.RemoveAt(i);
            }
            // remove all surfaces included in the delZones
            // change all surfaces' boundary condition to "Outside"
            // if they are adjacent to zones in the delZones list
            for (int i = surfaces.Count - 1; i >= 0; i--)
            {
                if (delSurfaceIds.Contains(surfaces[i].id))
                    if (surfaces[i].type == surfaceTypeEnum.ExteriorWall)
                        surfaces[i].type = surfaceTypeEnum.Shade;
                    else
                        surfaces.RemoveAt(i);
                if (delSurfaceIds.Contains(surfaces[i].adjSrfId))
                {
                    surfaces[i].adjSrfId = "Outside";
                    if (surfaces[i].type == surfaceTypeEnum.InteriorWall)
                        surfaces[i].type = surfaceTypeEnum.ExteriorWall;
                    if (surfaces[i].type == surfaceTypeEnum.InteriorFloor)
                    {
                        if (surfaces[i].tilt == 180)
                            surfaces[i].type = surfaceTypeEnum.RaisedFloor;
                        if (surfaces[i].tilt == 0)
                            surfaces[i].type = surfaceTypeEnum.Roof;
                    }       
                }
                    
            }


            // appendix for structure components
            columns = new List<gbLoop>();
            foreach (KeyValuePair<int, List<Tuple<List<gbXYZ>, string>>> kvp in dictColumn)
            {
                int counter = 0;
                foreach (Tuple<List<gbXYZ>, string> label in kvp.Value)
                {
                    List<double> sizes = new List<double>();
                    foreach (Match match in Regex.Matches(label.Item2, @"\d+"))
                    {
                        double size = Convert.ToInt32(match.Value) / 1000.0;
                        sizes.Add(size);
                    }
                    if (sizes.Count == 0)
                        continue;
                    double width = sizes[0];
                    double height = sizes[0];
                    if (sizes.Count == 2)
                        height = sizes[1];
                    List<gbXYZ> loop = label.Item1;
                    loop.RemoveAt(loop.Count - 1);
                    // legacy version uses the centroid of column to transfer data
                    //loop.Add(label.Item1 + new gbXYZ(-0.5 * width, -0.5 * height, 0));
                    //loop.Add(label.Item1 + new gbXYZ(0.5 * width, -0.5 * height, 0));
                    //loop.Add(label.Item1 + new gbXYZ(0.5 * width, 0.5 * height, 0));
                    //loop.Add(label.Item1 + new gbXYZ(-0.5 * width, 0.5 * height, 0));
                    gbLoop column = new gbLoop($"F{kvp.Key}_{counter}_{label.Item2}", levels[kvp.Key], loop, 0);
                    column.dimension1 = width;
                    column.dimension2 = height;
                    columns.Add(column);
                    counter++;
                }
            }
            beams = new List<gbLoop>();
            foreach (KeyValuePair<int, List<Tuple<gbSeg, string>>> kvp in dictBeam)
            {
                int counter = 0;
                foreach (Tuple<gbSeg, string> label in kvp.Value)
                {
                    //Debug.Print($"XMLGeometry:: Checking beam existence... {label.Item2}");
                    List<double> sizes = new List<double>();
                    foreach (Match match in Regex.Matches(label.Item2, @"\d+"))
                    {
                        double size = Convert.ToInt32(match.Value) / 1000.0;
                        sizes.Add(size);
                    }
                    if (sizes.Count == 0)
                    {
                        //Debug.Print("XMLGeometry:: Skip current beam...");
                        continue;
                    }
                    double width = sizes[0];
                    double height = sizes[0];
                    if (sizes.Count == 2)
                        height = sizes[1];

                    List<gbXYZ> loop = new List<gbXYZ>();
                    // 2D plane operation
                    gbXYZ startPt = GBMethod.FlattenPt(label.Item1.Start);
                    gbXYZ endPt = GBMethod.FlattenPt(label.Item1.End);
                    gbXYZ vec1 = endPt - startPt;
                    vec1.Unitize();
                    gbXYZ vec2 = GBMethod.GetPendicularVec(vec1, true);
                    loop.Add(startPt + 0.5 * width * vec2);
                    loop.Add(endPt + 0.5 * width * vec2);
                    loop.Add(endPt - 0.5 * width * vec2);
                    loop.Add(startPt - 0.5 * width * vec2);
                    gbLoop beam = new gbLoop($"F{kvp.Key}_{counter}_{label.Item2}", levels[kvp.Key], loop, levels[kvp.Key].height);
                    beam.dimension1 = width;
                    beam.dimension2 = height;
                    beams.Add(beam);
                    counter++;
                }
            }
            shafts = new List<gbLoop>();
            foreach (KeyValuePair<int, List<List<List<gbXYZ>>>> kvp in dictFloor)
            {
                int counter = 0;
                foreach (List<List<gbXYZ>> panel in kvp.Value)
                {
                    // the outer shell may not be the first one
                    // pick the clockwise ones as the hole edges
                    for (int i = 0; i < panel.Count; i++)
                    {
                        if (GBMethod.IsClockwise(panel[i]))
                        {
                            gbLoop shaft = new gbLoop($"F{kvp.Key}_{counter}", levels[kvp.Key], 
                                GBMethod.FlattenPts(panel[i]), levels[kvp.Key].height);
                            shafts.Add(shaft);
                            counter++;
                        }
                    }
                }
            }
        }

        static bool IsOpeningOverlap(List<gbOpening> openings, gbOpening newOpening)
        {
            List<gbXYZ> loop2d = GBMethod.PolyToUV(newOpening.loop);
            foreach (gbOpening opening in openings)
            {
                List<gbXYZ> opening2d = GBMethod.PolyToUV(opening.loop);
                if (GBMethod.IsPolyOverlap(loop2d, opening2d, true))
                    return true;
            }
            return false; 
        }

        /// <summary>
        /// Sort out the hierarchy structure of multiply connected regions
        /// from a bunch of CW or CCW polygon vertices loops
        /// this is a degenerated version of MCR given the right CW/CCW order
        /// </summary>
        static List<List<List<gbXYZ>>> SortMultiplyConnectedRegions(List<List<gbXYZ>> loops)
        {
            List<int> skipLoops = new List<int>();
            List<List<List<gbXYZ>>> mcrs = new List<List<List<gbXYZ>>>();
            for (int i = 0; i < loops.Count; i++)
            {
                if (!GBMethod.IsClockwise(loops[i]))
                {
                    List<List<gbXYZ>> mcr =
                        new List<List<gbXYZ>>() { loops[i] };
                    mcrs.Add(mcr);
                    skipLoops.Add(i);
                }
            }
            for (int i = 0; i < loops.Count; i++)
            {
                if (!skipLoops.Contains(i))
                    if (GBMethod.IsClockwise(loops[i]))
                        foreach (List<List<gbXYZ>> mcr in mcrs)
                            if (GBMethod.IsPolyInPoly(loops[i], mcr[0]))
                                mcr.Add(loops[i]);
            }
            return mcrs;
        }
    }
}
