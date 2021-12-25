#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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

            // Progressbar initiation
            CurrentControl.MaxValue = 100;
            CurrentUI.DataContext = CurrentControl;
            CurrentUI.Show();

            Report(0, "Filter geometry information ...");

            BatchGeometry.Execute(doc,
                out Dictionary<int, Tuple<string, double>> dictElevation,
                out Dictionary<int, List<gbSeg>> dictWall,
                out Dictionary<int, List<gbSeg>> dictCurtain,
                out Dictionary<int, List<gbSeg>> dictCurtaSystem, 
                out Dictionary<int, List<Tuple<gbXYZ, string>>> dictColumn,
                out Dictionary<int, List<Tuple<gbSeg, string>>> dictBeam,
                out Dictionary<int, List<Tuple<gbXYZ, string>>> dictWindow,
                out Dictionary<int, List<Tuple<gbXYZ, string>>> dictDoor,
                out Dictionary<int, List<List<List<gbXYZ>>>> dictFloor, 
                out Dictionary<int, List<gbSeg>> dictSeparationline,
                out Dictionary<int, List<gbSeg>> dictGrid,
                out Dictionary<int, List<gbXYZ>> dictRoom,
                out Dictionary<string, List<Tuple<string, double>>> dictWindowplus,
                out Dictionary<string, List<Tuple<string, double>>> dictDoorplus,
                out string checkInfo);


            int levelNum = dictElevation.Count - 1;

            // info check
            //if (dictWall.Count != levelNum ||
            //    dictWindow.Count != levelNum ||
            //    dictDoor.Count != levelNum ||
            //    dictCurtain.Count != levelNum)
            //    return;

            // process wall lines at each level
            // process space boundary and matching relation at each level
            Dictionary<int, List<gbRegion>> dictRegion = new Dictionary<int, List<gbRegion>>();
            Dictionary<int, List<gbXYZ>> dictShell = new Dictionary<int, List<gbXYZ>>();
            List<gbSeg> preBlueprint = new List<gbSeg>();
            List<gbSeg> nextBlueprint = new List<gbSeg>();

            for (int z = 0; z < levelNum; z++)
            {
                Report(10 + z * 80 / levelNum - 40 / levelNum, $"Processing floorplan on level {z} ...");

                List<gbSeg> enclosings = new List<gbSeg>();
                enclosings.AddRange(dictWall[z]);
                enclosings.AddRange(dictCurtain[z]);
                enclosings.AddRange(dictCurtaSystem[z]);

                List<gbSeg> flatLines = GBMethod.FlattenLines(enclosings);

                List<gbXYZ> pilePts = GBMethod.PilePts(flatLines);
                List<gbXYZ> boundary = OrthoHull.GetOrthoHull(pilePts);

                for (int i = 0; i < flatLines.Count; i++)
                    for (int j = 0; j < flatLines.Count; j++)
                        if (i != j)
                            flatLines[i] = GBMethod.SegExtension(flatLines[i], flatLines[j],
                                Properties.Settings.Default.tolExpand);
                //GBMethod.SegExtension2(flatLines[i], flatLines[j],
                //    Properties.Settings.Default.tolDouble, Properties.Settings.Default.tolExpand);

                //List<gbSeg> edges = new List<gbSeg>();
                //List<gbXYZ> offset = GBMethod.OffsetPoly(boundary, -0.5)[0];
                //RegionTessellate.SimplifyPoly(offset);
                //offset.Add(offset[0]);
                //for (int i = 0; i < offset.Count - 1; i++)
                //{
                //    gbSeg edge = new gbSeg(offset[i], offset[i + 1]);
                //    if (edge.Length != 0)
                //        edges.Add(new gbSeg(offset[i], offset[i + 1]));
                //}

                //RegionTessellate.SimplifyPoly(boundary);
                //boundary.Add(boundary[0]);
                ////Util.DrawDetailLines(doc, Util.gbSegsConvert(edges));

                //LayoutPatch.PerimeterPatch(flatLines, boundary, -0.5);

                // VISUALIZATION
                //using (Transaction tx = new Transaction(doc, "Sketch extended lines"))
                //{
                //    tx.Start();
                //    Util.SketchSegs(doc, flatLines);
                //    tx.Commit();
                //}

                // patch the column

                // this will cluster parallel segments with minor gaps < tolGroup
                List<List<gbSeg>> lineGroups = GBMethod.SegClusterByFuzzyIntersection(flatLines,
                    Properties.Settings.Default.tolGroup);

                /*
                // pile the points of the line groups and get the ortho-hull
                List<List<gbXYZ>> orthoHulls = new List<List<gbXYZ>>();
                bool[] isBlock = new bool[orthoHulls.Count];

                foreach (List<gbSeg> lineGroup in lineGroups)
                    orthoHulls.Add(OrthoHull.GetOrthoHull(GBMethod.PilePts(lineGroup)));

                for (int i = 0; i < orthoHulls.Count; i++)
                {
                    int counter = 0;
                    for (int j = 0; j < orthoHulls.Count; j++)
                    {
                        if (i != j)
                            if (GBMethod.IsPtInPoly(orthoHulls[i][0], orthoHulls[j]))
                                counter++;
                    }
                    if (counter > 0)
                        isBlock[i] = false;
                    else
                        isBlock[i] = true;
                }
                // usually there will be only one block per floor
                // floor panel that is not enclosed by a wall block will be regarded as shading surface
                // floor panel that is enclosed will be nested for boolean union operation
                List<List<gbXYZ>> shadings = new List<List<gbXYZ>>();
                //List<List<gbXYZ>> blocks = new List<List<gbXYZ>>();
                Dictionary<int, List<List<gbXYZ>>> nestedPanel = new Dictionary<int, List<List<gbXYZ>>>();
                foreach (List<List<gbXYZ>> panel in dictFloor[z])
                {
                    gbXYZ centroid = GBMethod.GetPolyCentroid(panel[0]); // sort the outer shell to the first
                    for (int i = 0; i < orthoHulls.Count; i++)
                    {
                        if (isBlock[i])
                        {
                            if (GBMethod.IsPtInPoly(centroid, orthoHulls[i]))
                            {
                                if (nestedPanel.ContainsKey(i))
                                    nestedPanel[i].Add(panel[0]);
                                else
                                    nestedPanel.Add(i, new List<List<gbXYZ>>() { panel[0] });
                            }
                            else
                                shadings.Add(panel[0]);
                        }
                    }
                }

                // boolean union within each nestedPanel, e.g. the block

                // boolean operation: floor - wall = void & wall - floor = shading
                */



                // a trush bin for stray lines that are processed after space detection
                // three steps are dumping debris to this trush bin
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
                    }
                }

                // enter point alignment and space detection of each segment group
                List<List<gbRegion>> nestedRegion = new List<List<gbRegion>>();
                List<List<gbXYZ>> nestedShell = new List<List<gbXYZ>>();

                // enter point alignment and space detection of each segment group
                for (int g = 0; g < lineGroups.Count; g++)
                {
                    List<gbSeg> lineShatters = GBMethod.SkimOut(GBMethod.ShatterSegs(lineGroups[g]), 0.001);

                    //VISUALIZATION
                    //using (Transaction tx = new Transaction(doc, "Sketch shatters"))
                    //{
                    //    tx.Start();
                    //    Util.SketchSegs(doc, lineShatters);
                    //    Debug.Print("Gridline sketched");
                    //    tx.Commit();
                    //}


                    List<gbXYZ> joints = PointAlign.GetJoints(lineShatters, 
                        Properties.Settings.Default.tolDouble, out List<List<gbXYZ>> hands);


                    // deepcopy hands for debugging
                    //List<List<gbXYZ>> handsCopy = new List<List<gbXYZ>>();
                    //foreach (List<gbXYZ> hand in hands)
                    //{
                    //    List<gbXYZ> handCopy = new List<gbXYZ>();
                    //    foreach (gbXYZ h in hand)
                    //        handCopy.Add(h);
                    //    handsCopy.Add(handCopy);
                    //}

                    // VISUALIZATION
                    if (z == 1)
                        using (Transaction tx = new Transaction(doc, "Sketch blueprint"))
                        {
                            tx.Start();
                            Util.SketchSegs(doc, preBlueprint);
                            Debug.Print("Gridline sketched");
                            tx.Commit();
                        }

                    List<List<gbXYZ>> anchorInfo_temp, anchorInfo;
                    List<gbSeg> nextBlueprint_1, nextBlueprint_2;
                    List<gbXYZ> ptAlign_temp = PointAlign.AlignPts(joints, hands, preBlueprint,
                        Properties.Settings.Default.tolTheta,
                        Properties.Settings.Default.tolDelta,
                        Properties.Settings.Default.tolDouble,
                        out anchorInfo_temp, out nextBlueprint_1);
                    List<gbXYZ> ptAlign = PointAlign.AlignPts(ptAlign_temp, anchorInfo_temp, preBlueprint, 
                        Properties.Settings.Default.tolTheta - Math.PI / 2,
                        Properties.Settings.Default.tolDelta,
                        Properties.Settings.Default.tolDouble,
                        out anchorInfo, out nextBlueprint_2);
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


                    // VISUALIZATION
                    //using (Transaction tx = new Transaction(doc, "Sketch grids"))
                    //{
                    //    tx.Start();
                    //    Util.SketchSegs(doc, lattice);
                    //    Debug.Print("Gridline sketched");
                    //    tx.Commit();
                    //}



                    List<gbRegion> regions;
                    // shell is merged into regions as the first list element
                    //List<gbXYZ> regionShell;
                    List<List<gbSeg>> regionDebris;

                    Report(10 + z * 80 / levelNum, $"Processing floorplan on level {z} ...");

                    SpaceDetect.GetRegion(lattice, z, g, out regions, out regionDebris);
                    strays.AddRange(Util.FlattenList(regionDebris));

                    Debug.Print($"At level-{z} group-{g} with {regions.Count} regions");

                    // VISUALIZATION
                    //List<gbSeg> loop = new List<gbSeg>();
                    //foreach (gbRegion region in regions)
                    //{
                    //    for (int k = 0; k < region.loop.Count - 1; k++)
                    //        loop.Add(new gbSeg(region.loop[k], region.loop[k + 1]));
                    //}
                    //Util.DrawDetailLines(doc, Util.gbSegsConvert(loop));
                    //Debug.Print("Region sketched");



                    //nestedShell.Add(regionShell);
                    nestedRegion.Add(regions);
                }

                Debug.Print($"At level-{z} with {nestedRegion.Count} clusters");

                // MCR split
                SpaceDetect.GetMCR(nestedRegion); //, nestedShell

                // summarize geometries and flatten the list
                List<gbRegion> thisLevelRegions = new List<gbRegion>();
                List<gbXYZ> thisLevelShell = new List<gbXYZ>();
                foreach (List<gbRegion> regions in nestedRegion)
                {
                    for (int i = 0; i < regions.Count; i++)
                    {
                        if (regions[i].innerLoops != null) // check MCR
                            Debug.Print($"ExtExportEXML:: Got MCR with {regions[i].innerLoops.Count} holes");
                        if (regions[i].isShell == true) // check shell
                            thisLevelShell = regions[i].loop;
                        if (i != 0) // check space region
                            thisLevelRegions.Add(regions[i]);
                    }
                }
                dictRegion.Add(z, thisLevelRegions);
                dictShell.Add(z, thisLevelShell);
            }


            if (dictRegion.Count == 0 || dictShell.Count == 0)
            {
                System.Windows.MessageBox.Show("Something wrong with the space detection. \n" +
                    "The process will be terminated.", "Warning");
                return;
            }

            Report(90, "Create gbXML geometry information ...");


            XMLGeometry.Generate(dictElevation,
                dictRegion, dictShell, 
                dictWindow, dictDoor, dictColumn, dictBeam, dictCurtain, dictFloor, 
                out List<gbZone> zones,
                out List<gbLoop> floors,
                out List<gbSurface> surfaces,
                out List<gbLoop> columns, 
                out List<gbLoop> beams, 
                out List<gbLoop> shafts);

            Report(95, "Serilaize gbXML file ...");

            string fileName = "GingerbreadXML.xml";
            string thisAssemblyFolderPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            XMLSerialization.Generate(thisAssemblyFolderPath + "/" + fileName, zones, floors, surfaces, columns, beams, shafts);

            Report(100, "Done export to " + thisAssemblyFolderPath);
            CurrentUI.btnCancel.Visibility = System.Windows.Visibility.Collapsed;
            CurrentUI.btnGenerate.Visibility = System.Windows.Visibility.Visible;


            return;

        }
        private void Report(int progress, string status)
        {
            CurrentControl.CurrentContext = status;
            CurrentControl.CurrentValue = progress;
            CurrentUI.Dispatcher.Invoke(new ProgressBarDelegate(CurrentControl.NotifyUI), System.Windows.Threading.DispatcherPriority.Background);
            CurrentUI.btnCancel.Click += CurrentUI_Closed;
            Debug.Print("ExtExportEXML:: " + status + " / " + progress);
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

