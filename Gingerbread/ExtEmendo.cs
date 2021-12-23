#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;

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
    public class ExtEmendo : IExternalEventHandler
    {
        public ViewEmendo CurrentUI { get; set; }
        public ProgressBarControl CurrentControl { get; set; }

        public ExtEmendo() { }

        public void Execute(UIApplication uiapp)
        {
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            BatchGeometry.Execute(doc,
                out Dictionary<int, Tuple<string, double>> dictElevation,
                out Dictionary<int, List<gbSeg>> dictWall,
                out Dictionary<int, List<gbSeg>> dictCurtain,
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

            CurrentControl.CurrentContext = checkInfo;
            CurrentUI.DataContext = CurrentControl;
            CurrentUI.Show();

            // ----------------------------------- Part C ends here ----------------------------------------//


            //List<gbSeg> flatLines = GBMethod.FlattenLines(dictWall[0]);

            //for (int i = 0; i < flatLines.Count; i++)
            //    for (int j = 0; j < flatLines.Count; j++)
            //        if (i != j)
            //            flatLines[i] = GBMethod.SegExtension(flatLines[i], flatLines[j],
            //                Properties.Settings.Default.expandTolerance);
            ////Debug.Print("ExtEmendo:: " + flatLines[i].Start.Serialize() + " / " + flatLines[i].End.Serialize());

            //List<List<gbSeg>> lineGroups = GBMethod.SegClusterByFuzzyIntersection(flatLines,
            //    Properties.Settings.Default.groupTolerance);
            //List<gbSeg> orphans = new List<gbSeg>();
            //// dump some orphan segments that will be processed later
            //for (int i = lineGroups.Count - 1; i >= 0; i--)
            //{
            //    if (lineGroups[i].Count <= 3)
            //    {
            //        orphans.AddRange(lineGroups[i]);
            //        lineGroups.RemoveAt(i);
            //    }
            //}

            //List<gbSeg> lineShatters = new List<gbSeg>();

            //// enter point alignment and space detection of each segment group
            //foreach (List<gbSeg> lineGroup in lineGroups)
            //{
            //    lineShatters = GBMethod.SkimOut(GBMethod.ShatterSegs(lineGroup), 0.0001);
            //    //System.Windows.MessageBox.Show("Shattered lines: " + lineShatters.Count + " at F-", "Warning");
            //    break;
            //}


            //using (Transaction tx = new Transaction(doc, "Sketch locations"))
            //{
            //    tx.Start();
            //    foreach (Tuple<gbXYZ, string> door in dictDoor[1])
            //        Util.SketchMarker(doc, Util.gbXYZConvert(door.Item1));
            //    tx.Commit();
            //}
        }

        public string GetName()
        {
            return "Check RVT";
        }
    }
}