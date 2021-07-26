#region Namespaces
using System.Collections.Generic;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
#endregion

namespace Gingerbread
{
    [Transaction(TransactionMode.Manual)]
    class CmdSketchLocation : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            Selection sel = uidoc.Selection;
            ICollection<ElementId> ids = sel.GetElementIds();

            // Accepts all walls pre-selected
            if (ids.Count != 0)
            {
                List<Curve> axes = new List<Curve>();
                List<XYZ> pts = new List<XYZ>();
                foreach (ElementId id in ids)
                {
                    Element e = doc.GetElement(id);
                    if (e is Wall)
                    {
                        LocationCurve lc = e.Location as LocationCurve;
                        axes.Add(lc.Curve);
                    }
                    if (e is FamilyInstance)
                    {
                        FamilyInstance fi = e as FamilyInstance;
                        if (fi.Category.Name == "Columns" || fi.Category.Name == "Structural Columns")
                        {
                            XYZ lp = Util.GetFamilyInstanceLocation(fi);
                            pts.Add(lp);
                        }
                    }
                }

                using (Transaction tx = new Transaction(doc, "Sketch locations"))
                {
                    tx.Start();
                    Util.SketchCurves(doc, axes);
                    Util.SketchMarkers(doc, pts);
                    tx.Commit();
                }
            }
            // If there is no pre-selection,
            // ask the user to pick one element with Wall type
            else
            {
                Element e;
                try
                {
                    Reference r = uidoc.Selection.PickObject(ObjectType.Element, new UtilElementsOfClassSelectionFilter<Element>());
                    e = doc.GetElement(r);
                }
                catch
                {
                    return Result.Cancelled;
                }

                using (Transaction tx = new Transaction(doc, "Sketch locations"))
                {
                    tx.Start();
                    if (e is Wall)
                    {
                        LocationCurve lc = e.Location as LocationCurve;
                        Curve crv = lc.Curve;
                        Util.SketchCurves(doc, new List<Curve> { crv });
                    }
                    else if (e is FamilyInstance)
                    {
                        XYZ lp = Util.GetFamilyInstanceLocation(e as FamilyInstance);
                        Util.SketchMarkers(doc, new List<XYZ> { lp });
                    }
                    tx.Commit();
                }
            }

            return Result.Succeeded;
        }
    }
}