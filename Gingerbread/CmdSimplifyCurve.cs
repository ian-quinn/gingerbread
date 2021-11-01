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
    class CmdSimplifyCurve : IExternalCommand
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
                //List<Curve> crvs = new List<Curve>();

                foreach (ElementId id in ids)
                {
                    Element e = doc.GetElement(id);
                    if (e is ModelLine)
                    {
                        ModelLine ml = e as ModelLine;
                        segments.Add(ml.GeometryCurve as Line);
                    }
                    //else if (e is DetailLine)
                    //{
                    //    DetailLine dl = e as DetailLine;
                    //    segments.Add(dl.GeometryCurve as Line);
                    //}
                    else if (e is ModelCurve)
                    {
                        ModelCurve mc = e as ModelCurve;
                        segments.AddRange(Core.Basic.TessellateCurve(mc.GeometryCurve));
                        //crvs.Add(mc.GeometryCurve);
                    }
                    //else if (e is DetailCurve)
                    //{
                    //    DetailCurve dc = e as DetailCurve;
                    //    segments.AddRange(Core.Basic.TessellateCurve(dc.GeometryCurve));
                    //    //crvs.Add(dc.GeometryCurve);
                    //}
                }
                // Here merging shattered lines to polylines is time-consuming
                // This is for the simplification of polyline boudnaries while controlingn the number of vertices
                List<PolyLine> plys = Core.Basic.JoinLineByCluster(segments);
                Debug.Print("There are {0} polylines", plys.Count.ToString());


                List<PolyLine> simplePlys = new List<PolyLine>();

                // For now we test polyline and bezier separately for different algorithm choices
                foreach (PolyLine ply in plys)
                {
                    simplePlys.Add(Core.CurveSimplify.DouglasPeuckerReduction(ply, Util.MmToFoot(1000)));
                }
                //foreach (Curve crv in crvs)
                //{
                //    simplePlys.Add(Core.SimplifyCurve.DouglasPeuckerReduction(crv, Util.MmToFoot(500)));
                //}
                Debug.Print("There are {0} simplified polylines", simplePlys.Count.ToString());


                using (Transaction tx = new Transaction(doc, "Draw simplified polyline"))
                {
                    tx.Start();
                    foreach (PolyLine polyline in simplePlys)
                    {
                        Util.DrawPolyLine(doc, polyline, false);
                    }
                    tx.Commit();
                }
                
            }
            // If there is no pre-selection,
            // ask the user to pick one element with Wall type
            else
            {
                Element e = null;
                try
                {
                    Reference r = uidoc.Selection.PickObject(ObjectType.Element, new UtilElementsOfClassSelectionFilter<ModelCurve>());
                    e = doc.GetElement(r);
                }
                catch
                {
                    return Result.Cancelled;
                }
                ModelCurve mc = e as ModelCurve;
                PolyLine simplePly = Core.CurveSimplify.MaxLengthReduction(mc.GeometryCurve, 10);
                using (Transaction tx = new Transaction(doc, "Draw simplified polyline"))
                {
                    tx.Start();
                    Util.DrawPolyLine(doc, simplePly, false);
                    tx.Commit();
                }
            }

            return Result.Succeeded;
        }
    }
}

