#region Namespaces
using System.Collections.Generic;
using System.Diagnostics;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
#endregion

namespace Gingerbread
{
    [Transaction(TransactionMode.Manual)]
    class CmdCoreSimplifyCurve : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            Selection sel = uidoc.Selection;
            ICollection<ElementId> ids = sel.GetElementIds();

            // Accepts all selected model lines
            if (ids.Count != 0)
            {
                List<Line> segments = new List<Line>();

                foreach (ElementId id in ids)
                {
                    Element e = doc.GetElement(id);
                    if (e is ModelLine)
                    {
                        ModelLine ml = e as ModelLine;
                        segments.Add(ml.GeometryCurve as Line);
                    }
                    if (e is DetailLine)
                    {
                        DetailLine ml = e as DetailLine;
                        segments.Add(ml.GeometryCurve as Line);
                    }

                }
                Debug.Print("Got model lines: " + segments.Count.ToString());
                
                PolyLine mergedPly = Core.Basic.JoinLine(segments);
                List<XYZ> vertices = new List<XYZ>(mergedPly.GetCoordinates());
                PolyLine simplifiedPly = PolyLine.Create(Core.SimplifyCurve.DouglasPeuckerReduction(vertices, Util.MmToFoot(2000)));


                if (null != simplifiedPly)
                {
                    using (Transaction tx = new Transaction(doc, "Draw simplified polyline"))
                    {
                        tx.Start();
                        Util.DrawPolyLine(doc, simplifiedPly, false);
                        tx.Commit();
                    }
                }
                else
                {
                    Debug.Print("no polyline generated.");
                }
                
            }
            else
            {
                return Result.Cancelled;
            }

            return Result.Succeeded;
        }
    }
}

