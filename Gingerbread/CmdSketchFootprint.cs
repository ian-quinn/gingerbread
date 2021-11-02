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
    class CmdSketchFootprint : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;


            // Private method
            // Iterate to get the bottom face of a solid
            Face GetBottomFace(Solid solid)
            {
                PlanarFace pf = null;
                foreach (Face face in solid.Faces)
                {
                    pf = face as PlanarFace;
                    if (null != pf)
                    {
                        if (Core.Basic.IsVertical(pf.FaceNormal, Properties.Settings.Default.doubleTolerance)
                            && pf.FaceNormal.Z < 0)
                        {
                            break;
                        }
                    }
                }
                return pf;
            }


            // Private method
            // Generate the CurveLoop of the footprint
            List<CurveLoop> GetFootprintOfElement(Element e)
            {
                List<CurveLoop> footprints = new List<CurveLoop>();

                if (e is Wall)
                {
                    Wall wall = e as Wall;
                    if (wall.WallType.Name == "Curtain")
                    {
                        return footprints;
                    }

                    Options opt = app.Create.NewGeometryOptions();
                    GeometryElement ge = wall.get_Geometry(opt);

                    foreach (GeometryObject obj in ge)
                    {
                        Solid solid = obj as Solid;
                        if (null != solid)
                        {
                            Face bottomFace = GetBottomFace(solid);
                            if (null != bottomFace)
                            {
                                foreach (CurveLoop edge in bottomFace.GetEdgesAsCurveLoops())
                                {
                                    footprints.Add(edge);
                                }
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
                            Face bottomFace = GetBottomFace(obj as Solid);
                            if (null != bottomFace)
                            {
                                foreach (CurveLoop edge in bottomFace.GetEdgesAsCurveLoops())
                                {
                                    footprints.Add(edge);
                                }
                            }
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
                                        Face bottomFace = GetBottomFace(solid2);
                                        foreach (CurveLoop edge in bottomFace.GetEdgesAsCurveLoops())
                                        {
                                            footprints.Add(edge);
                                        }
                                    }
                                }
                            }
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
                    foreach(Curve crv in loop)
                    {
                        shatteredCrvs.Add(crv);
                    }
                }

                using (Transaction tx = new Transaction(doc, "Sketch curves"))
                {
                    tx.Start();
                    Util.DrawDetailLines(doc, shatteredCrvs);
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

                List<CurveLoop> footprints = GetFootprintOfElement(e);
                    
                List<Curve> shatteredCrvs = new List<Curve>();
                foreach (CurveLoop loop in footprints)
                {
                    foreach (Curve crv in loop)
                    {
                        shatteredCrvs.Add(crv);
                    }
                }
                using (Transaction tx = new Transaction(doc, "Sketch curves"))
                {
                    tx.Start();
                    Util.DrawDetailLines(doc, shatteredCrvs);
                    tx.Commit();
                }
            }

            return Result.Succeeded;
        }
    }
}