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
#endregion

namespace Gingerbread
{
    [Transaction(TransactionMode.Manual)]
    public class ExtExportXML : IExternalEventHandler
    {
        public string targetValue { get; set; }

        public ExtExportXML(UIApplication uiapp)
        {
        }

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
                out Dictionary<int, List<Tuple<gbXYZ, string>>> dictWindow,
                out Dictionary<int, List<Tuple<gbXYZ, string>>> dictDoor);

            BatchXML.Execute(dictElevation, dictWall, dictWindow, dictDoor, dictCurtain, out List<gbXYZ> joints, out List<List<gbXYZ>> hands);
            
            //using (Transaction tx = new Transaction(doc, "Sketch locations"))
            //{
            //    tx.Start();
            //    for (int i = 0; i < joints.Count; i++)
            //    {
            //        Util.SketchMarkers(doc, new List<gbXYZ>() { joints[i] }, 0.5);
            //        if (hands[i].Count > 0)
            //        {
            //            List<gbSeg> stretches = new List<gbSeg>();
            //            foreach (gbXYZ vec in hands[i])
            //                stretches.Add(new gbSeg(joints[i], joints[i] + vec));
            //            Util.SketchSegs(doc, stretches);
            //        }

            //    }
            //    tx.Commit();
            //}

        }

        public string GetName()
        {
            return "Generate gbXML";
        }
    }
}

