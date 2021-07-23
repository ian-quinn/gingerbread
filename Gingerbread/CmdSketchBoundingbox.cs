#region Namespaces
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
#endregion

namespace Gingerbread
{
    [Transaction(TransactionMode.Manual)]
    class CmdSketchBoundingbox : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication app = commandData.Application;
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            Selection sel = uidoc.Selection;
            ICollection<ElementId> ids = sel.GetElementIds();

            // Accepts all walls pre-selected
            if (ids.Count != 0)
            {
                foreach (ElementId id in ids)
                {
                    Element e = doc.GetElement(id);
                    if (e is Wall)
                    {
                    }
                    if (e is FamilyInstance)
                    {
                    }
                }
            }

            // If there is no pre-selection,
            // ask the user to pick one element with Wall type
            else
            {
                Element e = null;
                try
                {
                    Reference r = uidoc.Selection.PickObject(ObjectType.Element, new UtilElementsOfClassSelectionFilter<Element>());
                    e = doc.GetElement(r);
                }
                catch
                {
                    return Result.Cancelled;
                }
                if (e is Wall)
                {
                }
                else if (e is FamilyInstance)
                {
                }
            }

            return Result.Succeeded;
        }
    }
}