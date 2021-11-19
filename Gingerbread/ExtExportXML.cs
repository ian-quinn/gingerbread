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
                out Dictionary<int, List<Tuple<gbXYZ, string>>> dictColumn,
                out Dictionary<int, List<Tuple<gbXYZ, string>>> dictWindow,
                out Dictionary<int, List<Tuple<gbXYZ, string>>> dictDoor, 
                out Dictionary<int, List<List<List<gbXYZ>>>> dictFloor, 
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

            for (int z = 0; z < levelNum; z++)
            {
                Report(10 + z * 80 / levelNum - 40 / levelNum, $"Processing floorplan on level {z} ...");

                List<gbSeg> flatLines = GBMethod.FlattenLines(dictWall[z]);

                for (int i = 0; i < flatLines.Count; i++)
                    for (int j = 0; j < flatLines.Count; j++)
                        if (i != j)
                            flatLines[i] = GBMethod.SegExtension(flatLines[i], flatLines[j],
                                Properties.Settings.Default.tolExpand);
                //Debug.Print(flatLines[i].Start.Serialize() + " / " + flatLines[i].End.Serialize());

                List<List<gbSeg>> lineGroups = GBMethod.SegClusterByFuzzyIntersection(flatLines,
                    Properties.Settings.Default.tolGroup);

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

                    List<gbSeg> lineShatters = GBMethod.SkimOut(GBMethod.ShatterSegs(lineGroups[g]), 0.00001);


                    List<gbXYZ> joints = PointAlign.GetJoints(lineShatters, 
                        Properties.Settings.Default.tolDouble, out List<List<gbXYZ>> hands);


                    // deepcopy hands for debugging
                    List<List<gbXYZ>> handsCopy = new List<List<gbXYZ>>();
                    foreach (List<gbXYZ> hand in hands)
                    {
                        List<gbXYZ> handCopy = new List<gbXYZ>();
                        foreach (gbXYZ h in hand)
                            handCopy.Add(h);
                        handsCopy.Add(handCopy);
                    }


                    List<List<gbXYZ>> anchorInfo_temp;
                    List<List<gbXYZ>> anchorInfo;
                    List<gbXYZ> ptAlign_temp = PointAlign.AlignPts(joints, hands,
                        Properties.Settings.Default.tolTheta,
                        Properties.Settings.Default.tolDelta,
                        Properties.Settings.Default.tolDouble,
                        out anchorInfo_temp);
                    List<gbXYZ> ptAlign = PointAlign.AlignPts(ptAlign_temp, anchorInfo_temp,
                        Properties.Settings.Default.tolTheta - Math.PI / 2,
                        Properties.Settings.Default.tolDelta,
                        Properties.Settings.Default.tolDouble,
                        out anchorInfo);


                    List<gbSeg> latticeDebries; // abandoned for now
                    List<List<gbSeg>> nestedLattice = PointAlign.GetLattice(ptAlign, anchorInfo,
                        Properties.Settings.Default.tolDouble, out latticeDebries);
                    List<gbSeg> lattice = Util.FlattenList(nestedLattice);
                    strays.AddRange(latticeDebries);

                    List<gbRegion> regions;
                    List<gbXYZ> regionShell;
                    List<List<gbSeg>> regionDebris;

                    Report(10 + z * 80 / levelNum, $"Processing floorplan on level {z} ...");

                    SpaceDetect.GetRegion(lattice, z, g, out regions, out regionShell, out regionDebris);
                    strays.AddRange(Util.FlattenList(regionDebris));

                    nestedShell.Add(regionShell);
                    nestedRegion.Add(regions);
                }

                // left for some MCR coupling work
                // only a placeholder that solves nothing
                //SpaceDetect.GetMCR(nestedRegion, nestedShell, out List<List<List<gbXYZ>>> mcrs);

                // summarization after solving the MCR issues
                dictRegion.Add(z, Util.FlattenList(nestedRegion));
                dictShell.Add(z, Util.FlattenList(nestedShell));
            }


            //if (dictLoop.Count == 0 || dictShell.Count == 0)
            //{
            //    System.Windows.MessageBox.Show("Something wrong with the space detection. \n" +
            //        "The process will be terminated.", "Warning");
            //    return;
            //}

            Report(90, "Create gbXML geometry information ...");


            XMLGeometry.Generate(dictElevation,
                dictRegion, dictShell, 
                dictWindow, dictDoor, dictCurtain,
                out List<gbZone> zones,
                out List<gbFloor> floors,
                out List<gbSurface> surfaces);

            Report(95, "Serilaize gbXML file ...");

            string fileName = "GingerbreadXML.xml";
            string thisAssemblyFolderPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            XMLSerialization.Generate(thisAssemblyFolderPath + "/" + fileName, zones, floors, surfaces);

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
            Debug.Print(status + " / " + progress);
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

