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
    public enum FuncSwitch { Select, Depict, Erase }

    [Transaction(TransactionMode.Manual)]
    public class ExtPickShade : IExternalEventHandler
    {
        public FuncSwitch runMode { get; set; } = FuncSwitch.Select;

        public ViewPickShade CurrentUI { get; set; }

        public ExtPickShade() { }

        public void Execute(UIApplication uiapp)
        {
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            switch (runMode)
            {
                case FuncSwitch.Select:
                    try
                    {
                        Autodesk.Revit.DB.Reference faceRef = uidoc.Selection.PickObject(ObjectType.Face,
                        new PlanarFaceFilter(doc), "Please pick a planar face as the shading surface. ESC for cancel.");
                        string refSerialized = "";
                        refSerialized = faceRef.ConvertToStableRepresentation(doc);

                        string spliter = "#";
                        if (Properties.Settings.Default.shadeIds == "") spliter = "";

                        if (refSerialized != "")
                        {
                            Properties.Settings.Default.shadeIds += spliter + refSerialized;
                            string[] tags = refSerialized.Split(':');
                            Properties.Settings.Default.shadeNames += spliter + $"{tags[2]}-{tags[1]}@"
                                + doc.GetElement(faceRef).Name + "@" + faceRef.ElementId;
                            // note that the tages[2] reprents the surface type. Beware if it is "INSTANCE",
                            // which cannot be depicted in global coordinate (but relative to grid system)
                            // the surface from a grid system need a coordinate transformation, apparently.
                        }

                        string[] elementIds = Properties.Settings.Default.shadeNames.Split('#');
                        CurrentUI.shadeList.ItemsSource = new List<string>(elementIds);
                    }
                    catch
                    {
                        return;
                    }
                    break;

                case FuncSwitch.Depict:
                    string shadeRefSerialized = Properties.Settings.Default.shadeCurrent;
                    if (shadeRefSerialized == "") return;
                    Autodesk.Revit.DB.Reference shadeRef = Autodesk.Revit.DB.Reference
                        .ParseFromStableRepresentation(doc, shadeRefSerialized);
                    if (shadeRef == null) return;

                    GeometryObject geoObject = doc.GetElement(shadeRef).GetGeometryObjectFromReference(shadeRef);
                    PlanarFace planarFace = geoObject as PlanarFace;
                    IList<Curve> drawings = new List<Curve>() { };
                    foreach (CurveLoop loop in planarFace.GetEdgesAsCurveLoops())
                    {
                        foreach (Curve crv in loop)
                        {
                            drawings.Add(crv);
                        }
                    }

                    Color color = new Color(150, 30, 70);
                    OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                    ogs.SetProjectionLineColor(color);
                    ogs.SetProjectionLineWeight(8);

                    using (Transaction tx = new Transaction(doc, "Highlight picked surface"))
                    {
                        tx.Start();
                        List<GeometryObject> objs = new List<GeometryObject>(drawings);
                        DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                        ds.SetShape(objs);
                        Properties.Settings.Default.shadeCurrentId = ds.Id.IntegerValue;
                        doc.ActiveView.SetElementOverrides(ds.Id, ogs);
                        tx.Commit();
                    }
                    break;

                case FuncSwitch.Erase:
                    int delId = Properties.Settings.Default.shadeCurrentId;
                    if (delId != -1)
                    {
                        using (Transaction tx = new Transaction(doc, "Delete previous drawing"))
                        {
                            tx.Start();
                            ElementId delDsId = new ElementId(delId);
                            ICollection<ElementId> deletedIdSet = doc.Delete(delDsId);
                            tx.Commit();
                        }
                        Properties.Settings.Default.shadeCurrentId = -1;
                    }
                    break;
            }
            return;
        }

        public string GetName()
        {
            return "Pick Shade";
        }
    }
}