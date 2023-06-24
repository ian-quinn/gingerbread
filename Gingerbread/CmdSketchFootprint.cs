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
    class CmdSketchFootprint : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

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

            // Private method
            // Iterate to get the bottom face of a solid
            Face GetSolidBottomFace(Solid solid)
            {
                List<Face> faces = new List<Face>() { };
                List<double> elevations = new List<double>() { };
                double min = double.PositiveInfinity;
                PlanarFace pf = null;
                foreach (Face face in solid.Faces)
                {
                    pf = face as PlanarFace;
                    if (null != pf)
                    {
                        if (Core.Basic.IsVertical(pf.FaceNormal, Properties.Settings.Default.tolDouble)
                            && pf.FaceNormal.Z < 0)
                        {
                            faces.Add(pf);
                            elevations.Add(pf.Origin.Z);
                            if (elevations.Last() < min) min = elevations.Last();
                        }
                    }
                }
                if (faces.Count == 0) return null;
                return faces[elevations.IndexOf(min)];
            }

            // get the location curve of a curain grid by panel solid intersected with plane
            List<CurveLoop> GetPanelPlaneIntersectionCurve(CurtainGrid cg, double z)
            {
                List<Solid> solids = new List<Solid>() { };
                List<CurveLoop> bounds = new List<CurveLoop>() { };
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
                    List<CurveLoop> bound = Util.GetSolidPlaneIntersectionCurve(plane, solid);
                    if (bound != null)
                        bounds.AddRange(bound);
                }
                return bounds;
            }

            List<CurveLoop> GetMullionPlaneIntersectionCurve(CurtainGrid cg, double z)
            {
                List<Solid> solids = new List<Solid>() { };
                List<CurveLoop> bounds = new List<CurveLoop>() { };
                Plane plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, z));

                Options ops = app.Create.NewGeometryOptions();
                ops.IncludeNonVisibleObjects = false;
                foreach (ElementId id in cg.GetMullionIds())
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
                    List<CurveLoop> bound = Util.GetSolidPlaneIntersectionCurve(plane, solid);
                    if (bound != null)
                        bounds.AddRange(bound);
                }
                return bounds;
            }


            // Private method
            // Generate the CurveLoop of the footprint
            List<CurveLoop> GetFootprintOfElement(Element e)
            {
                List<CurveLoop> footprints = new List<CurveLoop>();

                if (e is Wall)
                {
                    Options ops = app.Create.NewGeometryOptions();
                    ops.IncludeNonVisibleObjects = true;

                    Wall wall = e as Wall;
                    if (wall.WallType.Kind == WallKind.Curtain)
                    {
                        CurtainGrid cg = wall.CurtainGrid;
                        footprints.AddRange(GetPanelPlaneIntersectionCurve(cg, activeViewElevation + tol));
                        footprints.AddRange(GetMullionPlaneIntersectionCurve(cg, activeViewElevation + tol));
                    }

                    else
                    {
                        // not using boolean intersection
                        // a test module for the GetSolidBottomFace() method
                        GeometryElement ge = wall.get_Geometry(ops);
                        foreach (GeometryObject obj in ge)
                        {
                            Solid solid = obj as Solid;
                            if (null != solid)
                            {
                                var bottomFace = GetSolidBottomFace(solid);
                                if (bottomFace != null)
                                    foreach (CurveLoop edge in bottomFace.GetEdgesAsCurveLoops())
                                        footprints.Add(edge);
                            }
                        }
                    }
                }

                if (e is FamilyInstance)
                {
                    FamilyInstance fi = e as FamilyInstance;

                    if (fi.Category.Name != "Columns" && fi.Category.Name != "Structural Columns")
                    {
                        return footprints;
                    }

                    Options opt = new Options();
                    opt.ComputeReferences = true;
                    opt.DetailLevel = Autodesk.Revit.DB.ViewDetailLevel.Medium;
                    GeometryElement ge = fi.get_Geometry(opt);

                    foreach (GeometryObject obj in ge)
                    {
                        if (obj is Solid)
                        {
                            Face bottomFace = GetSolidBottomFace(obj as Solid);
                            if (bottomFace != null)
                                foreach (CurveLoop edge in bottomFace.GetEdgesAsCurveLoops())
                                    footprints.Add(edge);
                        }
                        else if (obj is GeometryInstance)
                        {
                            GeometryInstance geoInstance = obj as GeometryInstance;
                            GeometryElement geoElement = geoInstance.GetInstanceGeometry();
                            foreach (GeometryObject obj2 in geoElement)
                            {
                                if (obj2 is Solid)
                                {
                                    Solid solid2 = obj2 as Solid;
                                    if (solid2.Faces.Size > 0)
                                    {
                                        Face bottomFace = GetSolidBottomFace(solid2);
                                        if (bottomFace != null)
                                            foreach (CurveLoop edge in bottomFace.GetEdgesAsCurveLoops())
                                                footprints.Add(edge);
                                    }
                                }
                            }
                        }
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
                            footprints.AddRange(GetPanelPlaneIntersectionCurve(cg, activeViewElevation + tol));
                            footprints.AddRange(GetMullionPlaneIntersectionCurve(cg, activeViewElevation + tol));
                        }
                    }
                }
                return footprints;
            }


            // MAIN THREAD
            Selection sel = uidoc.Selection;
            ICollection<ElementId> ids = sel.GetElementIds();

            // Accepts all walls pre-selected
            if (ids.Count != 0)
            {
                List<CurveLoop> footprints = new List<CurveLoop>();

                foreach (ElementId id in ids)
                {
                    Element e = doc.GetElement(id);
                    footprints.AddRange(GetFootprintOfElement(e));
                }

                List<Curve> shatteredCrvs = new List<Curve>();
                foreach(CurveLoop loop in footprints)
                {
                    if (loop == null)
                        continue;
                    foreach(Curve crv in loop)
                    {
                        shatteredCrvs.Add(crv);
                    }
                }

                //using (Transaction tx = new Transaction(doc, "Sketch curves"))
                //{
                //    tx.Start();
                //    Util.DrawDetailLines(doc, shatteredCrvs);
                //    tx.Commit();
                //}

                // note that the detail lines are drawn on the current view
                Util.DrawDetailLines(doc, shatteredCrvs);
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

                List<CurveLoop> footprints = GetFootprintOfElement(e);
                    
                List<Curve> shatteredCrvs = new List<Curve>();
                foreach (CurveLoop loop in footprints)
                {
                    foreach (Curve crv in loop)
                    {
                        shatteredCrvs.Add(crv);
                    }
                }
                Util.DrawDetailLines(doc, shatteredCrvs);
            }

            return Result.Succeeded;
        }
    }
}