#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

using Gingerbread.Core;
using Gingerbread.Views;
#endregion

namespace Gingerbread
{
    [Transaction(TransactionMode.Manual)]
    public class ExtExportXML : IExternalEventHandler
    {
        public ViewExportXML CurrentUI { get; set; }
        public ProgressBarControl CurrentControl { get; set; }
        bool Cancel = false;

        private delegate void ProgressBarDelegate();

        public ExtExportXML() { }

        public void Execute(UIApplication uiapp)
        {
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            // Progress bar initiation
            CurrentControl.MaxValue = 100;
            CurrentUI.DataContext = CurrentControl;
            CurrentUI.Show();

            Report(0, "Filter geometry information ...");
            Util.LogInitiate();
            Util.LogPrint("Start baking a new gingerbread house...");

            BatchGeometry.Execute(doc,
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
                out string checkInfo);

            Util.LogPrint("----------------------------------------------------------------------------------------\n" +
                "           Ingredients are ready. Start blending...\n" +
                "           Hierarchical structure: Level - Block - Group - Region (L0B1G2R3)\n");

            int levelNum = dictElevation.Count - 1;

            // the iteration follows an hierarchy as LEVEL-BLOCK-GROUP-SPACE
            // ###################### LEVEL ########################

            // process wall lines at each level
            // process space boundary and matching relation at each level
            Dictionary<int, List<gbRegion>> dictRegion = new Dictionary<int, List<gbRegion>>();
            Dictionary<int, List<List<gbXYZ>>> dictShell = new Dictionary<int, List<List<gbXYZ>>>();
            Dictionary<int, List<gbSeg>> dictGlazing = new Dictionary<int, List<gbSeg>>();
            Dictionary<int, List<gbSeg>> dictAirwall = new Dictionary<int, List<gbSeg>>();
            Dictionary<int, List<gbSeg>> dictEnclosing = new Dictionary<int, List<gbSeg>>();
            List<gbSeg> preBlueprint = new List<gbSeg>();
            List<gbSeg> nextBlueprint = new List<gbSeg>();
            //List<gbSeg> shellBlueprint = new List<gbSeg>();

            for (int z = 0; z < levelNum; z++)
            {
                Util.LogPrint($"----------------Knead the dough on Level-{z}----------------");

                // initiate the dictionary
                dictRegion.Add(z, new List<gbRegion>());
                dictShell.Add(z, new List<List<gbXYZ>>());
                dictGlazing.Add(z, new List<gbSeg>());
                dictAirwall.Add(z, new List<gbSeg>());


                Report(10 + z * 80 / levelNum - 40 / levelNum, $"Processing floorplan on level {z} ...");

                List<gbSeg> enclosings = new List<gbSeg>();
                enclosings.AddRange(dictWall[z]);
                enclosings.AddRange(dictCurtain[z]);
                enclosings.AddRange(dictCurtaSystem[z]);
                enclosings.AddRange(dictSeparationline[z]);
                dictAirwall[z].AddRange(dictSeparationline[z]);
                List<gbSeg> flatLines = GBMethod.FlattenLines(enclosings);
                dictEnclosing.Add(z, enclosings);

                // PENDING
                // patch the columns. the patch lines may have collision with the wall lines
                // thus making the alignment unstable
                //List<gbSeg> colPatches = LayoutPatch.PatchColumn(flatLines, dictColumn[z]);
                //flatLines.AddRange(colPatches);

                // the extension copies all segments to another list
                // not stable to operate the endpoints directly for now
                for (int i = 0; i < flatLines.Count; i++)
                    for (int j = 0; j < flatLines.Count; j++)
                        if (i != j)
                            flatLines[i] = GBMethod.SegExtensionToSeg(flatLines[i], flatLines[j],
                                Properties.Settings.Default.tolPerimeter);
                //GBMethod.SegExtension2(flatLines[i], flatLines[j],
                //    Properties.Settings.Default.tolDouble, Properties.Settings.Default.tolExpand);

                // sort out building blocks. usually there is only one block for each level
                // this will cluster parallel segments with minor gaps < tolGroup
                List<List<gbSeg>> lineGroups = GBMethod.SegClusterByFuzzyIntersection(flatLines,
                    Properties.Settings.Default.tolGrouping);

                // a trash bin for stray lines that are processed after space detection
                // there are three steps that may dump debris to this trash bin
                // 1st cluster. assume that segments less than 4 are not likely to compose a region
                // 2nd alignment and lattice regeneration. 
                // 3rd after region detection. (not likely produces stray lines here if the former process done well)
                List<gbSeg> strays = new List<gbSeg>();
                for (int i = lineGroups.Count - 1; i >= 0; i--)
                {
                    if (lineGroups[i].Count <= 3)
                    {
                        strays.AddRange(lineGroups[i]);
                        lineGroups.RemoveAt(i);
                        continue;
                    }
                    for (int j = lineGroups[i].Count - 1; j >= 0; j--)
                    {
                        if (lineGroups[i][j].Length < 0.000001)
                            lineGroups[i].RemoveAt(j);
                    }
                }

                //Debug.Print($"ExtExportXML:: {lineGroups.Count} cluster detected");
                List<List<List<gbSeg>>> lineBlocks = new List<List<List<gbSeg>>>();
                foreach (List<gbSeg> lineGroup in lineGroups)
                {
                    lineBlocks.Add(new List<List<gbSeg>>() { lineGroup });
                }

                List<List<gbXYZ>> hullGroups = new List<List<gbXYZ>>();
                foreach (List<gbSeg> lineGroup in lineGroups)
                {
                    List<gbXYZ> orthoHull;
                    // try to get the block boundary first
                    List<gbSeg> aliLineGroup = GBMethod.SegsAlignment(lineGroup, Properties.Settings.Default.tolAlignment);
                    //Debug.Print($"ExtExportXML:: reduce line from {lineGroup.Count} to {aliLineGroup.Count}");
                    List<gbSeg> extLineGroup = GBMethod.SegsExtensionByLength(aliLineGroup, Properties.Settings.Default.tolPerimeter);
                    List<gbSeg> fusLineGroup = GBMethod.SegsFusion(extLineGroup, Properties.Settings.Default.tolAlignment);
                    List<gbSeg> shatteredLineGroup = GBMethod.SkimOut(GBMethod.ShatterSegs(fusLineGroup), 0.001);
                    orthoHull = RegionDetect.GetShell(shatteredLineGroup);

                    // PENDING for minimum hull for 2D segments
                    if (orthoHull.Count < 4)
                        orthoHull = OrthoHull.GetOrthoHull(GBMethod.PilePts(lineGroup));

                    //Debug.Print($"OrthoHull at F{z} B{lineGroups.IndexOf(lineGroup)} size {orthoHull.Count}");
                    RegionTessellate.SimplifyPoly(orthoHull);
                    orthoHull.Add(orthoHull[0]);
                    hullGroups.Add(orthoHull);
                }


                // check the containment of ortho-hulls then add enclosing segments to the relevant cluster
                List<int> redundantBlockIds = new List<int>();
                for (int i = hullGroups.Count - 1; i >= 0; i--)
                {
                    for (int j = hullGroups.Count - 1; j >= 0; j--)
                    {
                        if (i != j)
                            if (GBMethod.IsPolyInPoly(hullGroups[i], hullGroups[j]))
                            {
                                lineBlocks[j].Add(lineGroups[i]);
                                redundantBlockIds.Add(i);
                            }
                    }
                }

                // ABANDONED
                // another choice is to keep the redundant block but skip it during the iteration
                /*for (int i = redundantBlockIds.Count - 1; i >= 0; i--)
                {
                    lineBlocks.RemoveAt(i);
                }*/

                //Debug.Print($"ExtExportXML:: lineBlocks-{lineBlocks.Count}");
                Util.LogPrint($"Level-{z} has {flatLines.Count} Centerlines kneaded into {lineBlocks.Count - redundantBlockIds.Count} Blocks");


                // ###################### BLOCK ########################

                for (int b = 0; b < lineBlocks.Count; b++)
                {
                    //PENDING
                    if (redundantBlockIds.Contains(b))
                        continue;

                    if (false)
                    {
                        string loopCoords = $"L{z}-B{b} outline of block\n";
                        foreach (gbXYZ vertex in hullGroups[b])
                        {
                            loopCoords += $"           {{{vertex}}}\n";
                        }
                        Util.LogPrint(loopCoords);
                    }
                    

                    // point alignment and space detection of each segment group
                    List<List<gbRegion>> nestedRegion = new List<List<gbRegion>>();

                    // ###################### GROUP ########################

                    // enter point alignment and space detection of each segment group
                    for (int g = 0; g < lineBlocks[b].Count; g++)
                    {
                        // g = 0 indicates the hull of this block, g > 0 indicates the groups nested within
                        // if loop to the first group within the lineBlocks[b]
                        // add the ortho-hull of this block
                        // PENDING encounter with minor block, like a square shaft, just skip it
                        // prevent the block dimension smaller than the offset distance
                        // need more cunning way to do this?
                        // now compare the area of offsets and the hull 22-04-15
                        double areaHull = GBMethod.GetPolyArea(hullGroups[b]);
                        double areaOffset = GBMethod.GetPolyPerimeter(hullGroups[b]) * Properties.Settings.Default.tolPerimeter;
                        if (g == 0 && areaHull > areaOffset)
                        {
                            lineBlocks[b][g] = LayoutPatch.PatchPerimeter(lineBlocks[b][g], hullGroups[b],
                                dictWall[z], dictCurtain[z], dictWindow[z], dictDoor[z], dictFloor[z], 
                                Properties.Settings.Default.tolPerimeter, Properties.Settings.Default.tolGrouping, z == 0 ? false : true, 
                                out List<gbSeg> glazings, out List<gbSeg> airwalls, out List<List<gbXYZ>> voidLoops);

                            dictGlazing[z].AddRange(glazings);
                            dictAirwall[z].AddRange(airwalls);
                            foreach (List<gbXYZ> loop in voidLoops)
                            {
                                dictRoom[z].Add(new Tuple<List<List<gbXYZ>>, string>(
                                    new List<List<gbXYZ>>() { loop }, "Void"));
                            }
                            Util.LogPrint($"PerimeterPatch: L{z}-B{b} has {glazings.Count} glazing, {voidLoops.Count} void, {airwalls.Count} airwall");
                        }


                        //List<gbSeg> lineExtended = GBMethod.ExtendSegs(lineBlocks[b][g], 0.5);
                        List<gbSeg> lineFused = GBMethod.SegsFusion(lineBlocks[b][g], Properties.Settings.Default.tolAlignment);
                        List<gbSeg> lineShatters = GBMethod.SkimOut(
                            GBMethod.ShatterSegs(lineFused), 0.01);


                        List<gbXYZ> joints = PointAlign.GetJoints(lineShatters,
                            Properties.Settings.Default.tolDouble, out List<List<gbXYZ>> hands);


                        List<List<gbXYZ>> anchorInfo_temp, anchorInfo;
                        List<gbSeg> nextBlueprint_1, nextBlueprint_2;
                        List<gbXYZ> ptAlign_temp = PointAlign.AlignPts(joints, hands, preBlueprint,
                            Properties.Settings.Default.tolTheta,
                            Properties.Settings.Default.tolAlignment,
                            Properties.Settings.Default.tolDouble,
                            out anchorInfo_temp, out nextBlueprint_1);
                        //List<gbSeg> lattice_temp = PointAlign.GetLattice(ptAlign_temp, anchorInfo_temp,
                        //    Properties.Settings.Default.tolDouble, out List<gbSeg> someDebris);
                        List<string> jointsLabels_temp = Util.HandString(anchorInfo_temp);
                        List<gbXYZ> ptAlign = PointAlign.AlignPts(ptAlign_temp, anchorInfo_temp, preBlueprint,
                            Properties.Settings.Default.tolTheta - Math.PI / 2,
                            Properties.Settings.Default.tolAlignment,
                            Properties.Settings.Default.tolDouble,
                            out anchorInfo, out nextBlueprint_2);
                        List<string> jointsLabels = Util.HandString(anchorInfo);
                        nextBlueprint.AddRange(nextBlueprint_1);
                        nextBlueprint.AddRange(nextBlueprint_2);
                        preBlueprint = nextBlueprint;


                        // VISUALIZATION
                        //using (Transaction tx = new Transaction(doc, "Sketch anchors"))
                        //{
                        //    tx.Start();
                        //    View currentView = doc.ActiveView;
                        //    ElementId defaultTypeId = doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
                        //    Util.SketchMarkers(doc, Util.gbXYZsConvert(ptAlign), 0.4);
                        //    gbXYZ northAxis = new gbXYZ(0, 1, 0);
                        //    for (int i = 0; i < ptAlign.Count; i++)
                        //    {
                        //        string handInfo = "";
                        //        foreach (gbXYZ hand in anchorInfo[i])
                        //            handInfo += Math.Round(GBMethod.VectorAngle(northAxis, hand), 1).ToString() + "-";
                        //        TextNote note = TextNote.Create(doc, currentView.Id, Util.gbXYZConvert(ptAlign[i]),
                        //            handInfo, defaultTypeId);
                        //    }
                        //    tx.Commit();
                        //}


                        List<gbSeg> latticeDebries; // abandoned for now
                        List<gbSeg> lattice = PointAlign.GetLattice(ptAlign, anchorInfo,
                            Properties.Settings.Default.tolDouble, out latticeDebries);
                        strays.AddRange(latticeDebries);


                        List<gbRegion> regions;
                        // the block shell is nested into regions as the first list element
                        //List<gbXYZ> regionShell;
                        List<List<gbSeg>> regionDebris;
                        
                        Report(10 + z * 80 / levelNum, $"Processing floorplan on level {z} ...");

                        RegionDetect.GetRegion(lattice, z, b, g, out regions, out regionDebris);
                        strays.AddRange(Util.FlattenList(regionDebris));

                        //Debug.Print($"At level-{z} block-{b} group-{g} with {regions.Count} regions");


                        //nestedShell.Add(regionShell);
                        nestedRegion.Add(regions);
                        string temp_debug = Util.RegionString(regions);

                        Util.LogPrint($"RegionDetect: L{z}-B{b}-G{g} has {regions.Count} region, {regionDebris.Count} orphan");
                    } // end of the GROUP loop

                    //Debug.Print($"At level-{z} block-{b} with {nestedRegion.Count} clusters");

                    // MCR split process
                    if (nestedRegion.Count > 1)
                    {
                        RegionDetect.GetMCR(nestedRegion); //, nestedShell
                        //Debug.Print("ExtExportXML:: there seems to be an MCR");
                        Util.LogPrint($"Tessellation: L{z}-B{b} has multiply connected regions");
                    }

                    // summarize geometries and flatten the list
                    List<gbRegion> thisLevelRegions = new List<gbRegion>();
                    List<gbXYZ> thisBlockShell = new List<gbXYZ>();
                    foreach (List<gbRegion> regions in nestedRegion)
                    {
                        for (int i = 0; i < regions.Count; i++)
                        {
                            if (regions[i].innerLoops != null) // check MCR
                            {
                                //Debug.Print($"ExtExportXML:: Got MCR with {regions[i].innerLoops.Count} holes {regions[i].tiles.Count} tiles");
                                Util.LogPrint($"Tessellation: L{z}-B{b}-R{i} is MCR with {regions[i].innerLoops.Count} holes, {regions[i].tiles.Count} tiles");
                            }
                            if (regions[i].isShell == true) // check shell
                                thisBlockShell = regions[i].loop;
                            if (i != 0) // check space region
                                thisLevelRegions.Add(regions[i]);
                        }
                    }

                    dictRegion[z].AddRange(thisLevelRegions);
                    if (thisBlockShell.Count > 0)
                        dictShell[z].Add(thisBlockShell);

                    // VISUALIZATION
                    //List<gbSeg> shell = new List<gbSeg>();
                    //foreach (List<gbXYZ> blockShell in dictShell[z])
                    //    for (int i = 0; i < blockShell.Count - 1; i++)
                    //        shell.Add(new gbSeg(blockShell[i], blockShell[i + 1]));
                    //Util.DrawDetailLines(doc, Util.gbSegsConvert(shell));
                    //Debug.Print("Region sketched");

                    // the above process is stand-alone among all lineBlocks
                    // the label convention 


                } // end of the block loop

                Util.LogPrint($"Level-{z} summation: BlockShell-{dictShell[z].Count} Region-{dictRegion[z].Count}\n");
            }// end of the level loop


            // 20221022
            // summarize geometries for Json file export (attr: geomInfo), pending
            // the generated Json string will be cached in geomInfo properties for now
            List<JsonSchema.Level> jsLevels = new List<JsonSchema.Level>() { };
            List<gbXYZ> ptSwarm = new List<gbXYZ>() { };
            // key indicates the index of current floor, List<> contains the regions in it
            foreach (KeyValuePair<int, List<gbRegion>> rooms in dictRegion)
            {
                List<JsonSchema.Seg> jsSegs = new List<JsonSchema.Seg>() { };
                foreach (gbSeg seg in dictEnclosing[rooms.Key])
                {
                    JsonSchema.UV startPt = new JsonSchema.UV { coordU = seg.Start.X, coordV = seg.Start.Y };
                    JsonSchema.UV endPt = new JsonSchema.UV { coordU = seg.End.X, coordV = seg.End.Y };
                    jsSegs.Add(new JsonSchema.Seg { name = "", start = startPt, end = endPt });
                }

                List<JsonSchema.Poly> jsPolys = new List<JsonSchema.Poly>() { };
                foreach (gbRegion room in rooms.Value)
                {
                    List<JsonSchema.UV> jsPts = new List<JsonSchema.UV>() { };
                    if (room.loop == null)
                        continue;
                    foreach (gbXYZ pt in room.loop)
                    {
                        jsPts.Add(new JsonSchema.UV { coordU = pt.X, coordV = pt.Y });
                        ptSwarm.Add(pt);
                    }
                    jsPolys.Add(new JsonSchema.Poly { name = room.label, vertice = jsPts });
                }
                jsLevels.Add(new JsonSchema.Level { 
                    name = dictElevation[rooms.Key].Item1, 
                    elevation = dictElevation[rooms.Key].Item2, 
                    height = dictElevation[rooms.Key + 1].Item2 - dictElevation[rooms.Key].Item2, 
                    rooms = jsPolys, 
                    walls = jsSegs});
            }

            List<gbXYZ> boundingBox = OrthoHull.GetRectHull(ptSwarm);
            List<JsonSchema.UV> jsBoundingBox = Util.gbXYZ2Json(boundingBox);
            JsonSchema.Building jsBuilding = new JsonSchema.Building
            {
                name = Properties.Settings.Default.projName,
                canvas = new JsonSchema.Poly { name = "canvas", vertice = jsBoundingBox },
                levels = jsLevels
            };
            Properties.Settings.Default.geomInfo = JsonSerializer.Serialize(jsBuilding);
            // end Json serialization


            if (dictRegion.Count == 0 || dictShell.Count == 0)
            {
                System.Windows.MessageBox.Show("Something wrong with the space detection. \n" +
                    "The process will be terminated.", "Warning");
                return;
            }

            Report(90, "Create gbXML geometry information ...");
            Util.LogPrint("----------------------------------------------------------------------------------------\n" +
                "           Dough is ready. Start shaping...\n");

            XMLGeometry.Generate(dictElevation,
                dictRegion, dictShell,
                dictWindow, dictDoor, dictColumn, dictBeam, dictGlazing, dictAirwall, dictFloor, dictShade, dictRoom, 
                out List<gbZone> zones,
                out List<gbLoop> floors,
                out List<gbSurface> surfaces,
                out List<gbLoop> columns,
                out List<gbLoop> beams,
                out List<gbLoop> shafts);

            Report(95, "Serialize gbXML file ...");

            Util.LogPrint("----------------------------------------------------------------------------------------\n" +
                "           Heat the oven. Start baking...\n");

            string fileName = "GingerbreadXML.xml";
            string thisAssemblyFolderPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            XMLSerialize.Generate(thisAssemblyFolderPath + "/" + fileName, zones, floors, surfaces, columns, beams, shafts);

            Report(100, "Done export to " + thisAssemblyFolderPath);
            Util.LogPrint("----------------------------------------------------------------------------------------\n" +
                "           Ready to serve.\n");

            CurrentUI.btnCancel.Visibility = System.Windows.Visibility.Collapsed;
            CurrentUI.btnGenerate.Visibility = System.Windows.Visibility.Visible;

            return;

        }

        // private methods for progress report
        private void Report(int progress, string status)
        {
            CurrentControl.CurrentContext = status;
            CurrentControl.CurrentValue = progress;
            CurrentUI.Dispatcher.Invoke(new ProgressBarDelegate(CurrentControl.NotifyUI), System.Windows.Threading.DispatcherPriority.Background);
            CurrentUI.btnCancel.Click += CurrentUI_Closed;
            //Debug.Print("ExtExportXML:: " + status + " / " + progress);
            if (Cancel)
            {
                CurrentControl.CurrentContext = "Aborted";
                CurrentUI.btnCancel.Visibility = System.Windows.Visibility.Visible;
                CurrentUI.btnGenerate.Visibility = System.Windows.Visibility.Collapsed;
                return;
            }
        }

        private void CloseWindow()
        {
            CurrentUI.Closed -= CurrentUI_Closed;
            CurrentUI.Close();
        }

        private void CurrentUI_Closed(object sender, EventArgs e)
        {
            Cancel = true;
        }

        public string GetName()
        {
            return "Generate gbXML";
        }
    }
}

