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
            Dictionary<int, List<gbXYZ>> dictShell,
            //Dictionary<int, List<List<string>>> dictMatch,
            Dictionary<int, List<Tuple<gbXYZ, string>>> dictWindow,
            Dictionary<int, List<Tuple<gbXYZ, string>>> dictDoor,
            Dictionary<int, List<Tuple<gbXYZ, string>>> dictColumn, 
            Dictionary<int, List<Tuple<gbSeg, string>>> dictBeam,
            Dictionary<int, List<gbSeg>> dictCurtain,
            Dictionary<int, List<List<List<gbXYZ>>>> dictFloor, 
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

            // first loop to add spaces, walls, adjacencies and adhering openings
            foreach (gbLevel level in levels)
            {
                if (level.isTop) break;

                // prepare the label list 
                List<Tuple<gbXYZ, double>> hollows = new List<Tuple<gbXYZ, double>>();
                foreach (List<List<gbXYZ>> panel in dictFloor[level.id])
                    for (int i = 1; i < panel.Count; i++)
                    {
                        double area = GBMethod.GetPolyArea(panel[i]);
                        gbXYZ centroid = GBMethod.GetPolyCentroid(panel[i]);
                        hollows.Add(new Tuple<gbXYZ, double>(centroid, area));
                    }

                List<gbZone> thisZone = new List<gbZone>();
                List<gbSurface> thisSurface = new List<gbSurface>();
                foreach (gbRegion region in dictRegion[level.id])
                {
                    // skip the void region that is just a place holder
                    if (region.loop == null || region.loop.Count == 0)
                        continue;

                    gbZone newZone = new gbZone(region.label, level, region);
                    newZone.function = "Office";

                    foreach (Tuple<gbXYZ, double> hollow in hollows)
                        if (GBMethod.IsPtInPoly(hollow.Item1, region.loop))
                        {
                            double areaRatio = hollow.Item2 / newZone.area;
                            if (areaRatio > 0.5 && areaRatio < 1)
                            {
                                if (hollow.Item2 < 10)
                                    newZone.function = "Shaft";
                                else
                                    newZone.function = "Stair";
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
                    if (minDistance > 2 * Properties.Settings.Default.tolDelta)
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
                    List<List<List<gbXYZ>>> loopClusters = GBMethod.PolyClusterByOverlap(srf.subLoops);
                    foreach (List<List<gbXYZ>> loopCluster in loopClusters)
                    {
                        List<gbXYZ> scatterPts = Util.FlattenList(loopCluster);
                        List<gbXYZ> boundingBox = OrthoHull.GetRectHull(scatterPts);
                        boundingBox.RemoveAt(boundingBox.Count - 1); // transfer to open polyloop

                        //Debug.Print("XMLGeometry:: " + "Srf location: " + srf.locationLine.ToString());
                        gbXYZ srfVec = srf.locationLine.Direction;
                        gbXYZ srfOrigin = srf.locationLine.PointAt(0);
                        List<gbXYZ> openingLoop = new List<gbXYZ>();
                        foreach (gbXYZ pt in boundingBox)
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


                // as to doors
                // such process is the same as the one creating windows
                // they will be merged if there is no difference detected in future
                Debug.Print($"XMLGeometry:: There are {dictDoor[level.id].Count} doors on level-{level.id}");
                foreach (Tuple<gbXYZ, string> opening in dictDoor[level.id])
                {
                    Debug.Print($"XMLGeometry:: Door inserts at {opening.Item1} with dimension {opening.Item2}");
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
                    if (minDistance > 2 * Properties.Settings.Default.tolDelta)
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
                    List<List<List<gbXYZ>>> loopClusters = GBMethod.PolyClusterByOverlap(srf.subLoops);
                    foreach (List<List<gbXYZ>> loopCluster in loopClusters)
                    {
                        List<gbXYZ> scatterPts = Util.FlattenList(loopCluster);
                        List<gbXYZ> boundingBox = OrthoHull.GetRectHull(scatterPts);

                        // clip by surface boundary. take intersection
                        List<gbXYZ> srfLoop2d = new List<gbXYZ>();
                        srfLoop2d.Add(new gbXYZ(0, 0, 0));
                        srfLoop2d.Add(new gbXYZ(srf.width, 0, 0));
                        srfLoop2d.Add(new gbXYZ(srf.width, srf.height, 0));
                        srfLoop2d.Add(new gbXYZ(0, srf.height, 0));
                        srfLoop2d.Add(srfLoop2d[0]);
                        Debug.Write($"XMLGeometry:: Doorloop");
                        foreach (gbXYZ pt in boundingBox)
                            Debug.Write($"{pt}-");
                        Debug.Write($"\n");
                        Debug.Write($"XMLGeometry:: Srfloop");
                        foreach (gbXYZ pt in srfLoop2d)
                            Debug.Write($"{pt}-");
                        Debug.Write($"\n");

                        List<gbXYZ> rectifiedOpening = new List<gbXYZ>();
                        if (GBMethod.IsPolyInPoly(boundingBox, srfLoop2d))
                            rectifiedOpening = boundingBox;
                        else
                        {
                            List<List<gbXYZ>> section = GBMethod.ClipPoly(boundingBox, srfLoop2d, ClipType.ctIntersection);
                            rectifiedOpening = section[0];
                            Debug.Write($"XMLGeometry:: Rectified");
                            foreach (gbXYZ pt in rectifiedOpening)
                                Debug.Write($"{pt}-");
                            Debug.Write($"\n");
                            Debug.Print("Did boolean operation");
                        }

                        // the returning rectified Opening has already been an open loop
                        //rectifiedOpening.RemoveAt(rectifiedOpening.Count - 1); // transfer to open polyloop

                        Debug.Print("XMLGeometry:: " + "Srf location: " + srf.locationLine.ToString());
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


                // curtain wall is the most likely to go wild
                foreach (gbSeg opening in dictCurtain[level.id])
                {
                    gbSeg projection;

                    for (int k = 0; k < thisSurface.Count; k++)
                    {
                        // note that the second segment is the baseline
                        // the projection has the same direction as the second segment
                        projection = GBMethod.SegProjection(opening, thisSurface[k].locationLine,
                            out double distance);
                        if (projection.Length > 0.5 && distance < 0.1)
                        {
                            List<gbXYZ> openingLoop = new List<gbXYZ>();

                            double elevation = thisSurface[k].loop[0].Z;
                            double height = thisSurface[k].height;
                            openingLoop.Add(projection.PointAt(0) + new gbXYZ(0, 0, elevation));
                            openingLoop.Add(projection.PointAt(1) + new gbXYZ(0, 0, elevation));
                            openingLoop.Add(projection.PointAt(1) + new gbXYZ(0, 0, elevation + height));
                            openingLoop.Add(projection.PointAt(0) + new gbXYZ(0, 0, elevation + height));

                            gbOpening newOpening = new gbOpening(thisSurface[k].id + "::Opening_" +
                               thisSurface[k].openings.Count, GBMethod.PolyOffset(openingLoop, 0.1, true))
                               //thisSurface[k].openings.Count, GBMethod.OffsetPoly(openingLoop, 0.1)[0])
                            {
                                width = projection.Length,
                                height = height,
                                type = openingTypeEnum.FixedWindow
                            };
                            if (!IsOpeningOverlap(thisSurface[k].openings, newOpening))
                                thisSurface[k].openings.Add(newOpening);
                        }
                    }
                }

                floors.Add(new gbLoop("F" + level.id, level, dictShell[level.id]));
            }

            // second loop solve adjacencies among floors
            // perform on already created zones
            // prepare a counter for shadowing level indexation
            int shadowingCounter = 0;

            foreach (gbLevel level in levels)
            {
                if (level.isTop) break;

                // sum of area ratio to check if the next floor is shadowing the current one
                List<double> sumSimilarity = new List<double>();

                foreach (gbZone zone in dictZone[level.id])
                {
                    // ground slab or roof check
                    // translate zone tiles to surfaces
                    if (level.isBottom)
                    {
                        // to prevent empty tesselaltion
                        if (zone.tiles == null)
                        {
                            Debug.Print("XMLGeometry:: WARNING No tiles due to MCR tessellation failure!");
                        }
                        else if (zone.tiles.Count == 1)
                        {
                            // the existance of zone.loop has been checked before
                            // consider to add another check here
                            List<gbXYZ> revLoop = zone.loop;
                            revLoop.Reverse();
                            gbSurface floor = new gbSurface(zone.id + "::Floor_0", zone.id, revLoop, 180);
                            if (zone.level.elevation - 0 > 0.1)
                                floor.type = surfaceTypeEnum.ExposedFloor;
                            else
                                floor.type = surfaceTypeEnum.SlabOnGrade;
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
                                    Debug.Print($"XMLGeometry:: WARNING This MCR zone tile is null!");
                                    continue;
                                }
                                    
                                List<gbXYZ> revTile = new List<gbXYZ>();
                                foreach (gbXYZ pt in tile)
                                    revTile.Add(pt);
                                revTile.Reverse();
                                gbSurface floorTile = new gbSurface(zone.id + "::Floor_" + counter, zone.id, revTile, 180);
                                floorTile.type = surfaceTypeEnum.SlabOnGrade;
                                floorTile.adjSrfId = "Outside";
                                zone.floors.Add(floorTile);
                                counter++;
                            }
                        }
                    }
                    if (level.id == levels.Count - 2)
                    {
                        if (zone.tiles.Count == 1)
                        {
                            gbSurface ceiling = new gbSurface(zone.id + "::Ceil_0", zone.id,
                            GBMethod.ElevatePtsLoop(zone.loop, level.height), 0);
                            ceiling.type = surfaceTypeEnum.Roof;
                            ceiling.adjSrfId = "Outside";
                            zone.ceilings.Add(ceiling);
                        }
                        else
                        {
                            int counter = 0;
                            foreach (List<gbXYZ> tile in zone.tiles)
                            {
                                gbSurface ceilingTile = new gbSurface(zone.id + "::Ceil_" + counter, zone.id,
                                    GBMethod.ElevatePtsLoop(tile, level.height), 0);
                                ceilingTile.type = surfaceTypeEnum.Roof;
                                ceilingTile.adjSrfId = "Outside";
                                zone.ceilings.Add(ceilingTile);
                                counter++;
                            }
                        }
                    }

                    // exposed floor or offset roof check
                    // clip the zone tiles then translate them to surfaces
                    if (level.id != levels.Count - 2)
                        if (!GBMethod.IsPolyInPoly(GBMethod.ElevatePtsLoop(zone.loop, 0), dictShell[level.nextId]))
                        {
                            List<List<gbXYZ>> sectLoops = new List<List<gbXYZ>>();
                            foreach (List<gbXYZ> tile in zone.tiles)
                            {
                                List<List<gbXYZ>> result = GBMethod.ClipPoly(GBMethod.ElevatePtsLoop(tile, 0), 
                                    dictShell[level.nextId], ClipType.ctDifference);
                                sectLoops.AddRange(result);
                            }
                            if (sectLoops.Count != 0)
                            {
                                for (int j = 0; j < sectLoops.Count; j++)
                                {
                                    gbSurface splitCeil = new gbSurface(zone.id + "::Ceil_" + zone.ceilings.Count, zone.id,
                                        GBMethod.ElevatePtsLoop(sectLoops[j], level.elevation + level.height), 0);
                                    splitCeil.adjSrfId = "Outside";
                                    splitCeil.type = surfaceTypeEnum.Roof;
                                    zone.ceilings.Add(splitCeil);
                                }
                            }
                        }
                    if (!level.isBottom)
                        if (!GBMethod.IsPolyInPoly(GBMethod.ElevatePtsLoop(zone.loop, 0), dictShell[level.prevId]))
                        {

                            List<List<gbXYZ>> sectLoops = new List<List<gbXYZ>>();
                            foreach (List<gbXYZ> tile in zone.tiles)
                            {
                                List<List<gbXYZ>> result = GBMethod.ClipPoly(GBMethod.ElevatePtsLoop(tile, 0), 
                                    dictShell[level.prevId], ClipType.ctDifference);
                                sectLoops.AddRange(result);
                            }
                            if (sectLoops.Count != 0)
                            {
                                for (int j = 0; j < sectLoops.Count; j++)
                                {
                                    List<gbXYZ> revLoop = GBMethod.ElevatePtsLoop(sectLoops[j], level.elevation);
                                    revLoop.Reverse();
                                    gbSurface splitFloor = new gbSurface(zone.id + "::Floor_" + zone.floors.Count, zone.id,
                                        revLoop, 180);
                                    splitFloor.adjSrfId = "Outside";
                                    splitFloor.type = surfaceTypeEnum.ExposedFloor;
                                    zone.floors.Add(splitFloor);
                                }
                            }
                        }

                    // interior floor adjacency check
                    // clip the tiles, do the matching, then transfer to the surfaces
                    if (level.id != levels.Count - 2)
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
                                // the name does not matter
                                // they only have to stay coincident so the adjacent spaces can be tracked
                                string splitCeilId = zone.id + "::Ceil_" + zone.ceilings.Count;
                                string splitFloorId = adjZone.id + "::Floor_" + zone.floors.Count;
                                // be cautious here
                                // the ceiling here mean the shadowing floor, so the tilt is still 180
                                List<gbXYZ> revLoop = GBMethod.ElevatePtsLoop(sectLoops[j], adjZone.level.elevation);
                                revLoop.Reverse();
                                gbSurface splitCeil = new gbSurface(splitCeilId, zone.id, revLoop, 180);
                                gbSurface splitFloor = new gbSurface(splitFloorId, adjZone.id, revLoop, 180);

                                splitCeil.adjSrfId = splitFloorId;
                                splitCeil.type = surfaceTypeEnum.InteriorFloor;
                                zone.ceilings.Add(splitCeil);

                                splitFloor.adjSrfId = splitCeilId;
                                splitFloor.type = surfaceTypeEnum.InteriorFloor;
                                adjZone.floors.Add(splitFloor);

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
                        Debug.Print($"XMLGeometry:: shadowing floor-{level.nextId} detected");
                    }
                }
                else
                {
                    shadowingCounter = 0;
                    // when shadowing stops, keep the current level
                    levels[level.id].isShadowing = false;
                }
            }

            // third loop summarize all faces
            foreach (gbZone zone in zones)
                zone.Summarize();

            //appendix
            columns = new List<gbLoop>();
            foreach (KeyValuePair<int, List<Tuple<gbXYZ, string>>> kvp in dictColumn)
            {
                int counter = 0;
                foreach (Tuple<gbXYZ, string> label in kvp.Value)
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
                    List<gbXYZ> loop = new List<gbXYZ>();
                    loop.Add(label.Item1 + new gbXYZ(-0.5 * width, -0.5 * height, 0));
                    loop.Add(label.Item1 + new gbXYZ(0.5 * width, -0.5 * height, 0));
                    loop.Add(label.Item1 + new gbXYZ(0.5 * width, 0.5 * height, 0));
                    loop.Add(label.Item1 + new gbXYZ(-0.5 * width, 0.5 * height, 0));
                    gbLoop column = new gbLoop($"F{kvp.Key}_{counter}_{label.Item2}", levels[kvp.Key], loop);
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
                    List<gbXYZ> loop = new List<gbXYZ>();
                    gbXYZ startPt = label.Item1.PointAt(0);
                    gbXYZ endPt = label.Item1.PointAt(1);
                    gbXYZ vec1 = endPt - startPt;
                    vec1.Unitize();
                    gbXYZ vec2 = GBMethod.GetPendicularVec(vec1, true);
                    loop.Add(startPt + 0.5 * width * vec2);
                    loop.Add(endPt + 0.5 * width * vec2);
                    loop.Add(endPt - 0.5 * width * vec2);
                    loop.Add(startPt - 0.5 * width * vec2);
                    gbLoop beam = new gbLoop($"F{kvp.Key}_{counter}_{label.Item2}", levels[kvp.Key], loop);
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
                            gbLoop shaft = new gbLoop($"F{kvp.Key}_{counter}", levels[kvp.Key], panel[i]);
                            shafts.Add(shaft);
                            counter++;
                        }
                    }
                }
            }
        }

        static bool IsOpeningOverlap(List<gbOpening> openings, gbOpening newOpening)
        {
            List<gbXYZ> loop2d = GBMethod.PolyToPoly2D(newOpening.loop);
            foreach (gbOpening opening in openings)
            {
                List<gbXYZ> opening2d = GBMethod.PolyToPoly2D(opening.loop);
                if (GBMethod.IsPolyOverlap(loop2d, opening2d))
                    return true;
            }
            return false; 
        }
    }
}
