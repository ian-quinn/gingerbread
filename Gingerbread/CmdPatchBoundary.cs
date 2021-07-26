#region Namespaces
using System;
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
    public class CmdPatchBoundary : IExternalCommand
    {
        /// <summary>
        /// Extend the line to a boundary line. If the line has already surpassed it, trim the line instead.
        /// </summary>
        /// <param name="line"></param>
        /// <param name="terminal"></param>
        /// <returns></returns>
        public static Curve ExtendLine(Curve line, Curve terminal)
        {
            Line line_unbound = line.Clone() as Line;
            Line terminal_unbound = terminal.Clone() as Line;
            line_unbound.MakeUnbound();
            terminal_unbound.MakeUnbound();
            SetComparisonResult result = line_unbound.Intersect(terminal_unbound, out IntersectionResultArray results);
            if (result == SetComparisonResult.Overlap)
            {
                XYZ sectPt = results.get_Item(0).XYZPoint;
                XYZ extensionVec = (sectPt - line.GetEndPoint(0)).Normalize();
                if (Core.Basic.IsPtOnLine(sectPt, line as Line))
                {
                    double distance1 = sectPt.DistanceTo(line.GetEndPoint(0));
                    double distance2 = sectPt.DistanceTo(line.GetEndPoint(1));
                    if (distance1 > distance2)
                    {
                        return Line.CreateBound(line.GetEndPoint(0), sectPt);
                    }
                    else
                    {
                        return Line.CreateBound(line.GetEndPoint(1), sectPt);
                    }
                }
                else
                {
                    if (extensionVec.IsAlmostEqualTo(line_unbound.Direction))
                    {
                        return Line.CreateBound(line.GetEndPoint(0), sectPt);
                    }
                    else
                    {
                        return Line.CreateBound(sectPt, line.GetEndPoint(1));
                    }
                }
            }
            else
            {
                Debug.Print("Cannot locate the intersection point.");
                return null;
            }
        }

        /* abandoned for now
        public static Curve ExtendLineToBox(Curve line, List<Curve> box)
        {
            Line result = null;
            foreach (Curve edge in box)
            {
                var test = ExtendLine(line, edge);
                if (null == test) { continue; }
                if (test.Length > line.Length)
                {
                    return result;
                }
            }
            Debug.Print("Failure at line extension to box");
            return result;
        }
        */

        /// <summary>
        /// Fuse two collinear segments if they are joined or almost joined.
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        public static List<Curve> CloseGapAtBreakpoint(List<Curve> lines)
        {
            List<List<Curve>> mergeGroups = new List<List<Curve>>();
            mergeGroups.Add(new List<Curve>() { lines[0] });
            lines.RemoveAt(0);

            while (lines.Count != 0)
            {
                foreach (Line element in lines)
                {
                    int iterCounter = 0;
                    foreach (List<Curve> sublist in mergeGroups)
                    {
                        iterCounter += 1;
                        if (Core.Basic.IsLineAlmostSubsetLines(element, sublist))
                        {
                            sublist.Add(element);
                            lines.Remove(element);
                            goto a;
                        }
                        if (iterCounter == mergeGroups.Count)
                        {
                            mergeGroups.Add(new List<Curve>() { element });
                            lines.Remove(element);
                            goto a;
                        }
                    }
                }
            a:;
            }
            Debug.Print("The resulting lines should be " + mergeGroups.Count.ToString());

            List<Curve> mergeLines = new List<Curve>();
            foreach (List<Curve> mergeGroup in mergeGroups)
            {
                if (mergeGroup.Count > 1)
                {
                    Debug.Print("Got lines to be merged " + mergeGroup.Count.ToString());
                    foreach (Line line in mergeGroup)
                    {
                        Debug.Print("Line{0} ({1}, {2}) -> ({3}, {4})", mergeGroup.IndexOf(line), line.GetEndPoint(0).X,
                            line.GetEndPoint(0).Y, line.GetEndPoint(1).X, line.GetEndPoint(1).Y);
                    }
                    var merged = Core.Algorithm.FuseLines(mergeGroup);
                    mergeLines.Add(merged);
                }
                else
                {
                    mergeLines.Add(mergeGroup[0]);
                }

            }
            return mergeLines;
        }

        /// <summary>
        /// Fix the gap when two lines are not met at the corner.
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        public static List<Curve> CloseGapAtCorner(List<Curve> lines)
        {
            List<Curve> linePatches = new List<Curve>();
            List<int> removeIds = new List<int>();
            for (int i = 0; i < lines.Count; i++)
            {
                for (int j = i + 1; j < lines.Count; j++)
                {

                    if (!Core.Basic.IsIntersected(lines[i], lines[j]) &&
                        Core.Basic.IsAlmostJoined(lines[i], lines[j]))
                    {
                        removeIds.Add(i);
                        removeIds.Add(j);
                        linePatches.Add(ExtendLine(lines[i], lines[j]));
                        linePatches.Add(ExtendLine(lines[j], lines[i]));
                    }
                }
            }
            removeIds.Sort();
            for (int k = removeIds.Count - 1; k >= 0; k--)
            {
                lines.RemoveAt(removeIds[k]);
            }
            lines.AddRange(linePatches);
            return lines;
        }




        // Main thread
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            View view = doc.ActiveView;

            double tolerance = app.ShortCurveTolerance;

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
                        if (Core.Basic.IsVertical(pf.FaceNormal, Properties.Settings.Default.tolerance)
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

            //
            List<Curve> GetAxisOfElement(Element e)
            {
                List<Curve> locationAxes = new List<Curve>();
                if (e is Wall)
                {
                    LocationCurve lc = e.Location as LocationCurve;
                    locationAxes.Add(lc.Curve);
                }
                return locationAxes;
            }

            //
            List<XYZ> GetLocationOfElement(Element e)
            {
                List<XYZ> locationXYZ = new List<XYZ>();
                if (e is FamilyInstance)
                {
                    FamilyInstance fi = e as FamilyInstance;
                    if (fi.Category.Name == "Columns" || fi.Category.Name == "Structural Columns")
                    {
                        XYZ lp = Util.GetFamilyInstanceLocation(fi);
                        locationXYZ.Add(lp);
                    }
                }
                return locationXYZ;
            }



            // MAIN THREAD
            Selection sel = uidoc.Selection;
            ICollection<ElementId> ids = sel.GetElementIds();

            List<CurveLoop> columnFootprints = new List<CurveLoop>();
            List<XYZ> columnLocations = new List<XYZ>();
            List<Curve> wallCrvs = new List<Curve>();

            // Accepts all walls pre-selected
            if (ids.Count != 0)
            {
                foreach (ElementId id in ids)
                {
                    Element e = doc.GetElement(id);
                    columnFootprints.AddRange(GetFootprintOfElement(e));
                    wallCrvs.AddRange(GetAxisOfElement(e));
                    columnLocations.AddRange(GetLocationOfElement(e));
                }
            }
            else
            {
                return Result.Cancelled;
            }

            List<CurveLoop> joinedFootprint = new List<CurveLoop>();
            foreach (CurveLoop columnFootprint in columnFootprints)
            {
                joinedFootprint.Add(Core.Basic.SimplifyCurveLoop(columnFootprint));
            }

            List<Curve> columnCrvs = new List<Curve>();
            foreach (CurveLoop loop in joinedFootprint)
            {
                foreach (Curve crv in loop)
                {
                    columnCrvs.Add(crv);
                }
            }


            // INPUT List<Line> axes
            #region Merge axis joined/overlapped

            List<Curve> axesExtended = new List<Curve>();
            foreach (Curve axis in wallCrvs)
            {
                axesExtended.Add(Core.Algorithm.ExtendLine(axis, 200));
            }
            // Axis merge 
            List<List<Curve>> axisGroups = Core.Algorithm.ClusterByOverlap(axesExtended);
            List<Curve> centerLines = new List<Curve>();
            foreach (List<Curve> axisGroup in axisGroups)
            {
                var merged = Core.Algorithm.FuseLines(axisGroup);
                centerLines.Add(merged);
            }

            #endregion
            // OUTPUT List<Line> centerLines
            Debug.Print("WINDOW / DOOR LINES JOINED!");



            // INPUT List<Curve> columnCrvs
            // INPUT List<Curve> centerLines
            #region Extend and trim the axis (include column corner)

            foreach (CurveLoop columnFootprint in joinedFootprint)
            {
                List<Curve> columnEdges = new List<Curve>();
                foreach(Curve edge in columnFootprint)
                {
                    columnEdges.Add(edge);
                }

                List<Curve> nestLines = new List<Curve>();
                for (int i = 0; i < columnEdges.Count; i++)
                {
                    foreach (Line centerLine in centerLines)
                    {
                        SetComparisonResult result = columnEdges[i].Intersect(centerLine, out IntersectionResultArray results);
                        if (result == SetComparisonResult.Overlap)
                        {
                            for (int j = 0; j < columnEdges.Count; j++)
                            {
                                if (j != i)
                                {
                                    if (null != ExtendLine(centerLine, columnEdges[j]))
                                    {
                                        nestLines.Add(ExtendLine(centerLine, columnEdges[j]));
                                    }
                                }
                            }
                        }
                    }
                }
                Debug.Print("Got nested lines: " + nestLines.Count.ToString());
                if (nestLines.Count < 2) { continue; }
                else
                {
                    centerLines.AddRange(nestLines);
                    int count = 0;
                    for (int i = 1; i < nestLines.Count; i++)
                    {
                        if (!Core.Basic.IsParallel(nestLines[0], nestLines[i]))
                        { count += 1; }
                    }
                    if (count == 0)
                    {
                        var patches = Core.Algorithm.CenterLinesOfBox(columnEdges);
                        foreach (Line patch in patches)
                        {
                            if (Core.Basic.IsLineIntersectLines(patch, nestLines)) { centerLines.Add(patch); }
                        }
                    }
                }
            }

            #endregion
            // OUTPUT List<Curve> centerLines
            Debug.Print("AXES JOINED AT COLUMN");


            // INPUT List<Curve> centerLines
            //#The region detect function has fatal bug during boolean union operation
            #region Call region detection
            // Axis merge 
            List<List<Curve>> tempStrays = Core.Algorithm.ClusterByOverlap(centerLines);
            List<Curve> strays = new List<Curve>();
            foreach (List<Curve> tempStray in tempStrays)
            {
                Curve merged = Core.Algorithm.FuseLines(tempStray);
                strays.Add(merged);
            }

            //var strayClusters = Algorithm.ClusterByIntersect(Util.LinesToCrvs(strays));
            //Debug.Print("Cluster of strays: " + strayClusters.Count.ToString());
            //Debug.Print("Cluster of strays[0]: " + strayClusters[0].Count.ToString());
            //Debug.Print("Cluster of strays[1]: " + strayClusters[1].Count.ToString());
            // The RegionCluster method should be applied to each cluster of the strays
            // It only works on a bunch of intersected line segments
            List<CurveArray> loops = Core.DetectRegion.RegionCluster(strays);
            // The boolean union method of the loops needs to fix
            var perimeter = Core.DetectRegion.GetBoundary(loops);
            var recPerimeter = CloseGapAtBreakpoint(perimeter);
            var arrayPerimeter = Core.DetectRegion.AlignCrv(recPerimeter);
            for (int i = 0; i < arrayPerimeter.Size; i++)
            {
                Debug.Print("Line-{0} {1} {2}", i, Util.PointString(arrayPerimeter.get_Item(i).GetEndPoint(0)),
                    Util.PointString(arrayPerimeter.get_Item(i).GetEndPoint(1)));
            }
            #endregion
            // OUTPUT List<CurveArray> loops
            Debug.Print("REGION COMPLETE!");




            // Get the linestyle of "long-dashed"
            FilteredElementCollector fec = new FilteredElementCollector(doc)
                .OfClass(typeof(LinePatternElement));
            LinePatternElement linePatternElem = fec.FirstElement() as LinePatternElement;

            // Main visualization process
            using (Transaction tx = new Transaction(doc))
            {
                tx.Start("Generate Floor");

                // Draw wall patch lines
                /*
                foreach (Curve patchLine in patchLines)
                {
                    DetailLine axis = doc.Create.NewDetailCurve(view, patchLine) as DetailLine;
                    GraphicsStyle gs = axis.LineStyle as GraphicsStyle;
                    gs.GraphicsStyleCategory.LineColor = new Color(202, 51, 82);
                    gs.GraphicsStyleCategory.SetLineWeight(3, gs.GraphicsStyleType);
                }
                */

                Plane Geomplane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero);
                SketchPlane sketch = SketchPlane.Create(doc, Geomplane);

                /*
                // Draw bounding boxes
                foreach (List<Curve> wallBlock in wallBlocks)
                {
                    foreach (Curve edge in wallBlock)
                    {
                        DetailLine axis = doc.Create.NewDetailCurve(view, edge) as DetailLine;
                        GraphicsStyle gs = axis.LineStyle as GraphicsStyle;
                        gs.GraphicsStyleCategory.LineColor = new Color(210, 208, 185);
                        gs.GraphicsStyleCategory.SetLineWeight(1, gs.GraphicsStyleType);
                        gs.GraphicsStyleCategory.SetLinePatternId(linePatternElem.Id, gs.GraphicsStyleType);
                    }
                }
                */

                /*
                // Draw Axes
                Debug.Print("Axes all together: " + strays.Count.ToString());
                foreach (Line centerLine in strays)
                {
                    ModelCurve modelline = doc.Create.NewModelCurve(centerLine, sketch) as ModelCurve;
                }
                */

                // Draw Regions

                foreach (CurveArray loop in loops)
                {
                    foreach (Curve edge in loop)
                    {
                        ModelCurve modelline = doc.Create.NewModelCurve(edge, sketch) as ModelCurve;
                    }
                }

                foreach (Curve edge in arrayPerimeter)
                {
                    DetailLine axis = doc.Create.NewDetailCurve(view, edge) as DetailLine;
                    GraphicsStyle gs = axis.LineStyle as GraphicsStyle;
                    gs.GraphicsStyleCategory.LineColor = new Color(202, 51, 82);
                    gs.GraphicsStyleCategory.SetLineWeight(8, gs.GraphicsStyleType);
                }


                tx.Commit();
            }

            return Result.Succeeded;
        }
    }
}
