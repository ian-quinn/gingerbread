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

            // offset the level of current ViewPlan a little bit to ensure
            // there is an intersection between the floor plane and the element
            double tol = 0.01;

            // prepare the elevation of the current ViewPlan
            double activeViewElevation = 0;
            IList<Element> eLevels = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Levels)
                .WhereElementIsNotElementType()
                .ToElements();
            foreach (Element eLevel in eLevels)
            {
                if (eLevel is Level)
                {
                    Level lv = eLevel as Level;
                    if (lv.Name == doc.ActiveView.Name)
                    {
                        activeViewElevation = lv.Elevation;
                    }
                }
            }

            // get the location curve of a curtain grid by vertical gridline-plane intersection
            List<Curve> GetVGridLinePlaneIntersectionCurve(CurtainGrid cg, double z)
            {
                List<Line> lines = Util.GetCurtainGridVerticalLattice(doc, cg);
                List<XYZ> vertice = new List<XYZ>() { };
                foreach (Line line in lines)
                {
                    XYZ start = line.GetEndPoint(0);
                    XYZ end = line.GetEndPoint(1);
                    //Debug.Print($"S({start.X}/{start.Y}/{start.Z})");
                    //Debug.Print($"E({end.X}/{end.Y}/{end.Z})");
                    if ((start.Z - z) * (end.Z - z) <= 0 )
                    {
                        double xcoord = (z - start.Z) / (end.Z - start.Z) * (end.X - start.X) + start.X;
                        double ycoord = (z - start.Z) / (end.Z - start.Z) * (end.Y - start.Y) + start.Y;
                        vertice.Add(new XYZ(xcoord, ycoord, z));
                    }
                }
                List<Curve> trace = new List<Curve>() { };
                for (int i = 0; i < vertice.Count - 1; i++)
                {
                    trace.Add(Line.CreateBound(vertice[i], vertice[i + 1]) as Curve);
                }
                return trace;
            }

            // get the location curve of a curain grid by panel solid intersected with plane
            List<Curve> GetPanelPlaneIntersectionCurveCenterline(CurtainGrid cg, double z)
            {
                List<Solid> solids = new List<Solid>() { };
                List<Curve> trace = new List<Curve>() { };
                Plane plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, z));

                Options ops = app.Create.NewGeometryOptions();
                ops.IncludeNonVisibleObjects = false;
                foreach (ElementId id in cg.GetPanelIds())
                {
                    Element e = doc.GetElement(id);
                    GeometryElement ge = e.get_Geometry(ops);
                    foreach (GeometryObject obj in ge)
                    {
                        if (obj is Solid)
                        {
                            Solid solid = obj as Solid;
                            if (solid != null) solids.Add(solid);
                        }
                        else if (obj is GeometryInstance)
                        {
                            GeometryInstance _gi = obj as GeometryInstance;
                            GeometryElement _ge = _gi.GetInstanceGeometry();
                            foreach (GeometryObject _obj in _ge)
                            {
                                if (_obj is Solid)
                                {
                                    Solid solid = _obj as Solid;
                                    if (solid != null) solids.Add(solid);
                                }    
                            }
                        }
                    }
                }
                foreach (Solid solid in solids)
                {
                    if (solid.Edges.Size == 0 || solid.Faces.Size == 0) continue;
                    List<CurveLoop> bounds = Util.GetSolidPlaneIntersectionCurve(plane, solid);
                    if (bounds is null) continue;
                    foreach (CurveLoop bound in bounds)
                    {
                        //if (bound != null) continue;
                        trace.Add(Util.GetCenterlineOfRectangle(bound));
                    }
                }
                return trace;
            }

            List<Curve> GetWallPlaneIntersectionCurve(Wall wall, double z)
            {
                List<Solid> solids = new List<Solid>() { };
                List<Curve> trace = new List<Curve>() { };
                Plane plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, z));

                Options ops = wall.Document.Application.Create.NewGeometryOptions();
                ops.IncludeNonVisibleObjects = false;
                GeometryElement ge = wall.get_Geometry(ops);
                foreach (GeometryObject obj in ge)
                {
                    if (obj is Solid)
                    {
                        Solid solid = obj as Solid;
                        if (solid != null) solids.Add(solid);
                    }
                    else if (obj is GeometryInstance)
                    {
                        GeometryInstance gi = obj as GeometryInstance;
                        GeometryElement _ge = gi.GetInstanceGeometry();
                        foreach (GeometryObject _obj in _ge)
                        {
                            if (_obj is Solid)
                            {
                                Solid solid = _obj as Solid;
                                if (solid != null) solids.Add(solid);
                            }
                        }
                    }
                }
                foreach (Solid solid in solids)
                {
                    if (solid.Edges.Size == 0 || solid.Faces.Size == 0) continue;
                    List<CurveLoop> bounds = Util.GetSolidPlaneIntersectionCurve(plane, solid);
                    if (bounds == null) continue;
                    foreach (CurveLoop bound in bounds)
                    {
                        trace.Add(Util.GetCenterlineOfRectangle(bound));
                    }
                }
                return trace;
            }

            // Accepts all elements pre-selected
            if (ids.Count != 0)
            {
                List<Curve> axes = new List<Curve>();
                List<XYZ> pts = new List<XYZ>();
                foreach (ElementId id in ids)
                {
                    Element e = doc.GetElement(id);
                    if (e is Wall)
                    {
                        Wall wall = e as Wall;
                        if (wall.WallType.Kind == WallKind.Curtain)
                        {
                            CurtainGrid cg = wall.CurtainGrid;
                            axes.AddRange(GetPanelPlaneIntersectionCurveCenterline(cg, activeViewElevation + tol));
                        }
                        else
                        {
                            //LocationCurve lc = e.Location as LocationCurve;
                            //axes.Add(lc.Curve);

                            // use solid intersection method
                            axes.AddRange(GetWallPlaneIntersectionCurve(wall, activeViewElevation + tol));
                        }
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
                    if (e is CurtainSystem)
                    {
                        CurtainSystem cs = e as CurtainSystem;
                        if (cs != null)
                        {
                            foreach (CurtainGrid cg in cs.CurtainGrids)
                            {
                                //axes.AddRange(GetVGridLinePlaneIntersectionCurve(cg, 0.001));
                                axes.AddRange(GetPanelPlaneIntersectionCurveCenterline(cg, activeViewElevation + tol));
                            }
                        }
                    }
                }

                //using (Transaction tx = new Transaction(doc, "Sketch locations"))
                //{
                //    tx.Start();
                //    Util.SketchCurves(doc, axes);
                //    Util.SketchMarkers(doc, pts);
                //    tx.Commit();
                //}
                Util.DrawDetailLines(doc, axes);
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

                if (e is Wall)
                {
                    LocationCurve lc = e.Location as LocationCurve;
                    Curve crv = lc.Curve;
                    //Util.SketchCurves(doc, new List<Curve> { crv });
                    Util.DrawDetailLines(doc, new List<Curve>() { crv });
                }
                else if (e is FamilyInstance)
                {
                    XYZ lp = Util.GetFamilyInstanceLocation(e as FamilyInstance);
                    using (Transaction tx = new Transaction(doc, "Sketch locations"))
                    {
                        tx.Start();
                        Util.SketchMarkers(doc, new List<XYZ> { lp }, activeViewElevation);
                        tx.Commit();
                    }
                }
                else
                {
                    return Result.Cancelled;
                }
            }

            return Result.Succeeded;
        }
    }
}