#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

using Gingerbread.Views;
#endregion

namespace Gingerbread
{

    [Transaction(TransactionMode.Manual)]
    public class ExtPickSpace : IExternalEventHandler
    {
        public ViewPreview CurrentUI { get; set; }

        public ExtPickSpace() { }

        public void Execute(UIApplication uiapp)
        {
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            try
            {
                Autodesk.Revit.DB.Reference dsRef = uidoc.Selection.PickObject(ObjectType.Element,
                    new DirectShapeFilter(), "In Generic_Model selection mode. ESC for cancel.");

                // retrieve the element, make it selected
                Element dsElement = doc.GetElement(dsRef);
                ICollection<ElementId> selectIds = new List<ElementId>() { dsElement.Id };
                uidoc.Selection.SetElementIds(selectIds);

                // zoom in the selection
                //uidoc.ShowElements(selectIds);

                DirectShape ds = dsElement as DirectShape;
                CurrentUI.statusBar.Text = ds.Parameters.ToString();
            }
            catch
            {
                return;
            }
        }

        public string GetName()
        {
            return "Pick Space";
        }
    }
}