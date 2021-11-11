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
            Dictionary<int, List<List<gbXYZ>>> dictLoop,
            Dictionary<int, List<gbXYZ>> dictShell,
            Dictionary<int, List<List<string>>> dictMatch,
            Dictionary<int, List<Tuple<gbXYZ, string>>> dictWindow,
            Dictionary<int, List<Tuple<gbXYZ, string>>> dictDoor,
            Dictionary<int, List<gbSeg>> dictCurtain,
            out List<gbZone> zones,
            out List<gbFloor> floors,
            out List<gbSurface> surfaces)
        {
            List<gbLevel> levels = new List<gbLevel>();
            int numLevels = dictElevation.Keys.Count;
            foreach (KeyValuePair<int, Tuple<string, double>> kvp in dictElevation)
                levels.Add(new gbLevel(kvp.Key, kvp.Value.Item1, kvp.Value.Item2, numLevels));
            for (int i = 0; i < levels.Count - 1; i++)
                levels[i].height = levels[i + 1].elevation - levels[i].elevation;
            
            foreach (gbLevel level in levels)
            {
                Debug.Print($"On level {level.id} elevation {level.elevation} height {level.height}");
            }

            // cached intermediate data
            zones = new List<gbZone>();
            surfaces = new List<gbSurface>();
            floors = new List<gbFloor>();
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

                List<gbZone> thisZone = new List<gbZone>();
                List<gbSurface> thisSurface = new List<gbSurface>();
                for (int j = 0; j < dictLoop[level.id].Count; j++)
                {
                    if (dictLoop[level.id][j].Count == 0)
                        continue;
                    gbZone newZone = new gbZone("Level_" + level.id + "::Zone_" + j, level, dictLoop[level.id][j]);
                    thisZone.Add(newZone);
                    //List<string> srfId = new List<string>();
                    //List<Line> boundaryLine = new List<Line>();
                    for (int k = 0; k < dictLoop[level.id][j].Count - 1; k++)
                    {
                        //srfId.Add(newZone.walls[k].id);
                        //boundaryLine.Add(new Line(dictLoop[levelLabel[i]][j][k], dictLoop[levelLabel[i]][j][k + 1]));
                        string adjacency = dictMatch[level.id][j][k];
                        newZone.walls[k].adjSrfId = adjacency;
                        if (adjacency == "Outside")
                            newZone.walls[k].type = surfaceTypeEnum.ExteriorWall;
                        else
                            newZone.walls[k].type = surfaceTypeEnum.InteriorWall;
                    }
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
                foreach (Tuple<gbXYZ, string> opening in dictWindow[level.id])
                {
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

                    double minDistance = Double.PositiveInfinity;
                    double minParam = 0;
                    gbXYZ minPlummet = opening.Item1;
                    int hostId = 0;

                    for (int k = 0; k < thisSurface.Count; k++)
                    {
                        double distance = GBMethod.PtDistanceToSeg(opening.Item1, thisSurface[k].locationLine,
                            out gbXYZ plummet, out double sectParam);
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
                    if (minDistance > Properties.Settings.Default.latticeDelta)
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

                        //Debug.Print("Srf location: " + srf.locationLine.ToString());
                        gbXYZ srfVec = srf.locationLine.Direction;
                        gbXYZ srfOrigin = srf.locationLine.PointAt(0);
                        List<gbXYZ> openingLoop = new List<gbXYZ>();
                        foreach (gbXYZ pt in boundingBox)
                        {
                            //Debug.Print("Pt before transformation: " + pt.ToString());
                            gbXYZ _pt = pt.SwapPlaneZY().RotateOnPlaneZ(srfVec).Move(srfOrigin);
                            openingLoop.Add(_pt);
                            //Debug.Print("Pt after transformation: " + _pt.ToString());
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
                foreach (Tuple<gbXYZ, string> opening in dictDoor[level.id])
                {
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

                    double minDistance = Double.PositiveInfinity;
                    double minParam = 0;
                    gbXYZ minPlummet = opening.Item1;
                    int hostId = 0;

                    for (int k = 0; k < thisSurface.Count; k++)
                    {
                        double distance = GBMethod.PtDistanceToSeg(opening.Item1, thisSurface[k].locationLine,
                            out gbXYZ plummet, out double sectParam);
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
                    if (minDistance > Properties.Settings.Default.latticeDelta)
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
                        boundingBox.RemoveAt(boundingBox.Count - 1); // transfer to open polyloop

                        //Debug.Print("Srf location: " + srf.locationLine.ToString());
                        gbXYZ srfVec = srf.locationLine.Direction;
                        gbXYZ srfOrigin = srf.locationLine.PointAt(0);
                        List<gbXYZ> openingLoop = new List<gbXYZ>();
                        foreach (gbXYZ pt in boundingBox)
                        {
                            //Debug.Print("Pt before transformation: " + pt.ToString());
                            gbXYZ _pt = pt.SwapPlaneZY().RotateOnPlaneZ(srfVec).Move(srfOrigin);
                            openingLoop.Add(_pt);
                            //Debug.Print("Pt after transformation: " + _pt.ToString());
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

                floors.Add(new gbFloor("F" + level.id, level, dictShell[level.id]));
            }

            // second loop solve adjacencies among floors
            // perform on already created zones
            foreach (gbLevel level in levels)
            {
                if (level.isTop) break;

                foreach (gbZone zone in dictZone[level.id])
                {
                    // ground slab or roof check
                    if (level.isBottom)
                    {
                        List<gbXYZ> revLoop = zone.loop;
                        revLoop.Reverse();
                        gbSurface floor = new gbSurface(zone.id + "::Floor_0", zone.id, revLoop, 180);
                        floor.type = surfaceTypeEnum.SlabOnGrade;
                        floor.adjSrfId = "Outside";
                        zone.floors.Add(floor);
                    }
                    if (level.id == levels.Count - 2)
                    {
                        gbSurface ceiling = new gbSurface(zone.id + "::Ceil_0", zone.id,
                            GBMethod.ElevatePtsLoop(zone.loop, level.height), 0);
                        ceiling.type = surfaceTypeEnum.Roof;
                        ceiling.adjSrfId = "Outside";
                        zone.ceilings.Add(ceiling);
                    }

                    // exposed floor or offset roof check
                    if (level.id != levels.Count - 2)
                        if (!GBMethod.IsPolyInPoly(GBMethod.ElevatePtsLoop(zone.loop, 0), dictShell[level.nextId]))
                        {
                            List<List<gbXYZ>> sectLoops = GBMethod.ClipPoly(zone.loop, dictShell[level.nextId], ClipType.ctDifference);
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
                            List<List<gbXYZ>> sectLoops = GBMethod.ClipPoly(zone.loop, dictShell[level.prevId], ClipType.ctDifference);
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
                    if (level.id != levels.Count - 2)
                        foreach (gbZone adjZone in dictZone[level.id + 1])
                        {
                            List<List<gbXYZ>> sectLoops = GBMethod.ClipPoly(zone.loop, adjZone.loop, ClipType.ctIntersection);
                            if (sectLoops.Count == 0)
                                continue;
                            for (int j = 0; j < sectLoops.Count; j++)
                            {
                                // the name does not matter
                                // they only have to stay coincident so the adjacent spaces can be tracked
                                string splitCeilId = zone.id + "::Ceil_" + zone.ceilings.Count;
                                string splitFloorId = adjZone.id + "::Floor_" + zone.floors.Count;
                                // be cautious here
                                // the ceiling here mean the shadowing floor, so the tile is still 180
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
            }

            // third loop summarize all faces
            foreach (gbZone zone in zones)
                zone.Summarize();
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
