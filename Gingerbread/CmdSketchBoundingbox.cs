#region Namespaces
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
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
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            Selection sel = uidoc.Selection;
            ICollection<ElementId> ids = sel.GetElementIds();

            Color color = new Color(150, 30, 70);
            OverrideGraphicSettings ogs = new OverrideGraphicSettings();
            ogs.SetSurfaceTransparency(90);
            ogs.SetProjectionLineColor(color);
            ogs.SetProjectionLineWeight(4);

            Options ops = uiapp.Application.Create.NewGeometryOptions();
            ops.IncludeNonVisibleObjects = true;

            DirectShape CreateDirectShapeBox(XYZ pt1, XYZ pt2)
            {
                double x_min = pt1.X;
                double x_max = pt2.X;
                double y_min = pt1.Y;
                double y_max = pt2.Y;
                double z_min = pt1.Z;
                double z_max = pt2.Z;
                if (x_min > x_max) Util.Swap(ref x_min, ref x_max);
                if (y_min > y_max) Util.Swap(ref y_min, ref y_max);
                if (z_min > z_max) Util.Swap(ref z_min, ref z_max);

                List<Curve> loop = new List<Curve>(4);
                loop.Add(Line.CreateBound(new XYZ(x_min, y_min, z_min), new XYZ(x_max, y_min, z_min)));
                loop.Add(Line.CreateBound(new XYZ(x_max, y_min, z_min), new XYZ(x_max, y_max, z_min)));
                loop.Add(Line.CreateBound(new XYZ(x_max, y_max, z_min), new XYZ(x_min, y_max, z_min)));
                loop.Add(Line.CreateBound(new XYZ(x_min, y_max, z_min), new XYZ(x_min, y_min, z_min)));
                IList<CurveLoop> loops = new List<CurveLoop>();
                loops.Add(CurveLoop.Create(loop));

                Solid boundingBox = GeometryCreationUtilities
                    .CreateExtrusionGeometry(loops, XYZ.BasisZ, z_max - z_min);

                List<GeometryObject> objs = new List<GeometryObject>() { boundingBox };

                DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
                ds.SetShape(objs);

                return ds;
            }

            // Accepts all walls pre-selected
            if (ids.Count != 0)
            {
                double x_min = double.PositiveInfinity;
                double y_min = double.PositiveInfinity;
                double z_min = double.PositiveInfinity;
                double x_max = -double.PositiveInfinity;
                double y_max = -double.PositiveInfinity;
                double z_max = -double.PositiveInfinity;
                XYZ pt1 = new XYZ(); XYZ pt2 = new XYZ();
                int validElement = 0;
                foreach (ElementId id in ids)
                {
                    Element e = doc.GetElement(id);
                    if (e is Wall)
                    {
                        Wall wall = e as Wall;
                        if (wall !=null)
                        {
                            validElement++;
                            GeometryElement ge = wall.get_Geometry(ops);
                            BoundingBoxXYZ box = ge.GetBoundingBox();
                            pt1 = box.Min; pt2 = box.Max;
                        }
                    }
                    if (e is FamilyInstance)
                    {
                        FamilyInstance fi = e as FamilyInstance;
                        if (fi != null)
                        {
                            validElement++;
                            GeometryElement ge = fi.get_Geometry(ops);
                            BoundingBoxXYZ box = ge.GetBoundingBox();
                            pt1 = box.Min; pt2 = box.Max;
                        }
                    }
                    if (e is CurtainSystem)
                    {
                        CurtainSystem cs = e as CurtainSystem;
                        if (cs != null)
                        {
                            validElement++;
                            GeometryElement ge = cs.get_Geometry(ops);
                            BoundingBoxXYZ box = ge.GetBoundingBox();
                            pt1 = box.Min; pt2 = box.Max;
                        }
                    }
                    if (pt1.X < x_min) x_min = pt1.X;
                    if (pt1.Y < y_min) y_min = pt1.Y;
                    if (pt1.Z < z_min) z_min = pt1.Z;
                    if (pt2.X > x_max) x_max = pt2.X;
                    if (pt2.Y > y_max) y_max = pt2.Y;
                    if (pt2.Z > z_max) z_max = pt2.Z;
                }

                if (validElement == 0) return Result.Cancelled;

                using (Transaction tx = new Transaction(doc, "Create bounding box"))
                {
                    tx.Start();
                    DirectShape ds = CreateDirectShapeBox(new XYZ(x_min, y_min, z_min), new XYZ(x_max, y_max, z_max));
                    doc.ActiveView.SetElementOverrides(ds.Id, ogs);
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
                    Reference r = uidoc.Selection.PickObject(ObjectType.Element, new UtilElementsOfClassSelectionFilter<Element>());
                    e = doc.GetElement(r);
                }
                catch
                {
                    return Result.Cancelled;
                }
                if (e is Wall)
                {
                    Wall wall = e as Wall;
                    GeometryElement ge = wall.get_Geometry(ops);
                    BoundingBoxXYZ box = ge.GetBoundingBox();

                    using (Transaction tx = new Transaction(doc, "Create bounding box"))
                    {
                        tx.Start();
                        DirectShape ds = CreateDirectShapeBox(box.Max, box.Min);
                        doc.ActiveView.SetElementOverrides(ds.Id, ogs);
                        tx.Commit();
                    }
                }
                else if (e is FamilyInstance)
                {
                    FamilyInstance fi = e as FamilyInstance;
                    GeometryElement ge = fi.get_Geometry(ops);
                    BoundingBoxXYZ box = ge.GetBoundingBox();

                    using (Transaction tx = new Transaction(doc, "Create bounding box"))
                    {
                        tx.Start();
                        DirectShape ds = CreateDirectShapeBox(box.Max, box.Min);
                        doc.ActiveView.SetElementOverrides(ds.Id, ogs);
                        tx.Commit();
                    }
                }
                else if (e is CurtainSystem)
                {
                    CurtainSystem cs = e as CurtainSystem;
                    GeometryElement ge = cs.get_Geometry(ops);
                    BoundingBoxXYZ box = ge.GetBoundingBox();

                    using (Transaction tx = new Transaction(doc, "Create bounding box"))
                    {
                        tx.Start();
                        DirectShape ds = CreateDirectShapeBox(box.Max, box.Min);
                        doc.ActiveView.SetElementOverrides(ds.Id, ogs);
                        tx.Commit();
                    }
                }
                else { return Result.Cancelled; }
            }

            return Result.Succeeded;
        }
    }
}