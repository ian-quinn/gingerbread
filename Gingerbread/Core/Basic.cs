#region Namespaces
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Autodesk.Revit.DB;
#endregion

namespace Gingerbread.Core
{
    public static class Basic
    {
        #region Determination

        public const double _eps = 1.0e-9;

        public static bool IsZero(double a, double tolerance = _eps)
        {
            return tolerance > Math.Abs(a);
        }

        public static bool IsVertical(XYZ v)
        {
            return IsZero(v.X) && IsZero(v.Y);
        }
        public static bool IsVertical(XYZ v, double tolerance)
        {
            return IsZero(v.X, tolerance)
              && IsZero(v.Y, tolerance);
        }

        /// <summary>
        /// Check if two lines are perpendicular to each other.
        /// </summary>
        /// <param name="line1"></param>
        /// <param name="line2"></param>
        /// <returns></returns>
        public static bool IsPerpendicular(Curve line1, Curve line2)
        {
            XYZ vec1 = line1.GetEndPoint(1) - line1.GetEndPoint(0);
            XYZ vec2 = line2.GetEndPoint(1) - line2.GetEndPoint(0);
            if (vec1.DotProduct(vec2) == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        /// <summary>
        /// Check if a point is on a line
        /// </summary>
        /// <param name="pt"></param>
        /// <param name="line"></param>
        /// <returns></returns>
        public static bool IsPtOnLine(XYZ pt, Line line)
        {
            XYZ ptStart = line.GetEndPoint(0);
            XYZ ptEnd = line.GetEndPoint(1);
            XYZ vec1 = (ptStart - pt).Normalize();
            XYZ vec2 = (ptEnd - pt).Normalize();
            if (!vec1.IsAlmostEqualTo(vec2) || pt.IsAlmostEqualTo(ptStart) || pt.IsAlmostEqualTo(ptEnd)) { return true; }
            else { return false; }
        }


        /// <summary>
        /// Check if two line segments are intersected. May not save that much of computation time.
        /// </summary>
        /// <param name="c1"></param>
        /// <param name="c2"></param>
        /// <returns></returns>
        public static bool IsLineLineIntersected(Curve c1, Curve c2)
        {
            XYZ p1 = c1.GetEndPoint(0);
            XYZ q1 = c1.GetEndPoint(1);
            XYZ p2 = c2.GetEndPoint(0);
            XYZ q2 = c2.GetEndPoint(1);
            XYZ v1 = q1 - p1;
            XYZ v2 = q2 - p2;
            if (v1.Normalize().IsAlmostEqualTo(v2.Normalize()) || v1.Normalize().IsAlmostEqualTo(-v2.Normalize()))
            {
                return false;
            }
            else
            {
                XYZ w = p2 - p1;
                XYZ p5 = null;

                double c = (v2.X * w.Y - v2.Y * w.X)
                  / (v2.X * v1.Y - v2.Y * v1.X);

                if (!double.IsInfinity(c))
                {
                    double x = p1.X + c * v1.X;
                    double y = p1.Y + c * v1.Y;

                    p5 = new XYZ(x, y, 0);
                }

                if (IsPtOnLine(p5, c1 as Line) && IsPtOnLine(p5, c2 as Line))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }


        // Check the parallel lines
        public static bool IsParallel(Curve line1, Curve line2)
        {
            XYZ line1_Direction = (line1.GetEndPoint(1) - line1.GetEndPoint(0)).Normalize();
            XYZ line2_Direction = (line2.GetEndPoint(1) - line2.GetEndPoint(0)).Normalize();
            if (line1_Direction.IsAlmostEqualTo(line2_Direction) ||
                line1_Direction.Negate().IsAlmostEqualTo(line2_Direction))
            {
                return true;
            }
            else
            {
                return false;
            }
        }


        // Check the shadowing lines
        public static bool IsShadowing(Curve line1, Curve line2)
        {
            XYZ ptStart1 = line1.GetEndPoint(0);
            XYZ ptEnd1 = line1.GetEndPoint(1);
            XYZ ptStart2 = line2.GetEndPoint(0);
            XYZ ptEnd2 = line2.GetEndPoint(1);
            Line baseline = line2.Clone() as Line;
            baseline.MakeUnbound();
            XYZ _ptStart = baseline.Project(ptStart1).XYZPoint;
            XYZ _ptEnd = baseline.Project(ptEnd1).XYZPoint;
            Line checkline = Line.CreateBound(_ptStart, _ptEnd);
            SetComparisonResult result = checkline.Intersect(line2, out IntersectionResultArray results);
            if (result == SetComparisonResult.Equal) { return true; }
            // Equal is a cunning way here if they intersected by a line segment
            else { return false; }
        }

        //
        public static bool IsOverlapped(Curve crv1, Curve crv2)
        {
            SetComparisonResult result = crv1.Intersect(crv2, out IntersectionResultArray results);
            if (result == SetComparisonResult.Subset
                || result == SetComparisonResult.Equal)
            { return true; }
            else { return false; }
        }

        /// <summary>
        /// Check if two curves are strictly intersected
        /// </summary>
        /// <param name="crv1"></param>
        /// <param name="crv2"></param>
        /// <returns></returns>
        public static bool IsIntersected(Curve crv1, Curve crv2)
        {
            // Can be safely apply to lines
            // Line segment can only have 4 comparison results: Disjoint, subset, overlap, equal
            SetComparisonResult result = crv1.Intersect(crv2, out IntersectionResultArray results);
            if (result == SetComparisonResult.Overlap
                || result == SetComparisonResult.Subset
                || result == SetComparisonResult.Superset
                || result == SetComparisonResult.Equal)
            { return true; }
            else { return false; }
        }


        /// <summary>
        /// Check if two lines are almost joined.
        /// </summary>
        /// <param name="crv1"></param>
        /// <param name="crv2"></param>
        /// <returns></returns>
        public static bool IsAlmostJoined(Curve line1, Curve line2)
        {
            double radius = Util.MmToFoot(200);
            XYZ ptStart = line1.GetEndPoint(0);
            XYZ ptEnd = line1.GetEndPoint(1);
            XYZ xAxis = new XYZ(1, 0, 0);   // The x axis to define the arc plane. Must be normalized
            XYZ yAxis = new XYZ(0, 1, 0);   // The y axis to define the arc plane. Must be normalized
            Curve knob1 = Arc.Create(ptStart, radius, 0, 2 * Math.PI, xAxis, yAxis);
            Curve knob2 = Arc.Create(ptEnd, radius, 0, 2 * Math.PI, xAxis, yAxis);
            SetComparisonResult result1 = knob1.Intersect(line2, out IntersectionResultArray results1);
            SetComparisonResult result2 = knob2.Intersect(line2, out IntersectionResultArray results2);
            if (result1 == SetComparisonResult.Overlap || result2 == SetComparisonResult.Overlap)
            { return true; }
            else { return false; }
        }


        // Check a line is overlapping with a group of lines
        public static bool IsLineIntersectLines(Curve line, List<Curve> list)
        {
            int judgement = 0;
            foreach (Curve element in list)
            {
                if (IsIntersected(line, element))
                {
                    judgement += 1;
                }
            }
            if (judgement == 0) { return false; }
            else { return true; }
        }


        // Check a line is overlapping with a group of lines
        public static bool IsLineOverlapLines(Curve line, List<Curve> list)
        {
            int judgement = 0;
            foreach (Curve element in list)
            {
                if (IsParallel(line, element) && IsIntersected(line, element))
                {
                    judgement += 1;
                }
            }
            if (judgement == 0) { return false; }
            else { return true; }
        }

        // Check a line is parallel with a group of lines
        public static bool IsLineParallelLines(Curve line, List<Curve> list)
        {
            int judgement = 0;
            foreach (Curve element in list)
            {
                if (IsParallel(line, element))
                {
                    judgement += 1;
                }
            }
            if (judgement == 0) { return false; }
            else { return true; }
        }

        // Check a line is almost joined to a group of lines
        public static bool IsLineAlmostJoinedLines(Curve line, List<Curve> list)
        {
            int judgement = 0;
            if (list.Count == 0) { return true; }
            else
            {
                foreach (Curve element in list)
                {
                    if (IsAlmostJoined(line, element))
                    {
                        judgement += 1;
                    }
                }
            }
            if (judgement == 0) { return false; }
            else { return true; }
        }

        // Check a line is almost subset to a group of lines
        public static bool IsLineAlmostSubsetLines(Curve line, List<Curve> list)
        {
            int judgement = 0;
            if (list.Count == 0) { return true; }
            else
            {
                foreach (Line element in list)
                {
                    if (IsParallel(line, element) && IsAlmostJoined(line, element))
                    {
                        judgement += 1;
                    }
                }
            }
            if (judgement == 0) { return false; }
            else { return true; }
        }

        #endregion


        #region Calculation
        /// <summary>
        /// The distance of a point from a line made from point1 and point2.
        /// </summary>
        /// <param name="pt1">The PT1.</param>
        /// <param name="pt2">The PT2.</param>
        /// <param name="pt">The p.</param>
        /// <returns></returns>
        public static double PerpendicularDistance(XYZ pt1, XYZ pt2, XYZ pt)
        {
            //Area = |(1/2)(x1y2 + x2y3 + x3y1 - x2y1 - x3y2 - x1y3)|   *Area of triangle
            //Base = v((x1-x2)²+(x1-x2)²)                               *Base of Triangle*
            //Area = .5*Base*H                                          *Solve for height
            //Height = Area/.5/Base

            double area = Math.Abs(.5 * (pt1.X * pt2.Y + pt2.X *
            pt.Y + pt.X * pt1.Y - pt2.X * pt1.Y - pt.X *
            pt2.Y - pt1.X * pt.Y));
            double bottom = Math.Sqrt(Math.Pow(pt1.X - pt2.X, 2) +
            Math.Pow(pt1.Y - pt2.Y, 2));
            double height = area / bottom * 2;
            Debug.Print("we got the distance for measure is: " + height.ToString());
            return height;
        }
        #endregion


        #region Curve

        public static double PolyLineLength(PolyLine ply)
        {
            if (ply == null) return 0;
            double length = 0;
            List<XYZ> pts = new List<XYZ>(ply.GetCoordinates());
            for (int i = 0; i < pts.Count - 2; i++)
            {
                length += pts[i].DistanceTo(pts[i + 1]);
            }
            return length;
        }


        /// <summary>
        /// Convert curve to shattered lines.
        /// Based on RevitAPI Curve.Tessellate()
        /// </summary>
        /// <param name="crv"></param>
        /// <returns></returns>
        public static List<Line> TessellateCurve(Curve crv)
        {
            List<Line> edges = new List<Line>();
            IList<XYZ> pts = crv.Tessellate();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                edges.Add(Line.CreateBound(pts[i], pts[i + 1]));
            }
            return edges;
        }



        /// <summary>
        /// Remove duplicate points of a CurveLoop
        /// </summary>
        /// <param name="crvLoop"></param>
        /// <returns></returns>
        public static CurveLoop SimplifyCurveLoop(CurveLoop crvLoop)
        {
            if (crvLoop.IsOpen())
            {
                return crvLoop;
            }

            CurveLoop boundary = new CurveLoop();
            List<XYZ> vertices = new List<XYZ>() { };
            List<XYZ> reducedVertices = new List<XYZ>() { };

            foreach (Curve crv in crvLoop)
            {
                vertices.Add(crv.GetEndPoint(0));
            }

            for (int i = 0; i < vertices.Count; i++)
            {
                Debug.Print(i.ToString());
                XYZ thisDirection, prevDirection;
                if (i == 0) { prevDirection = vertices[i] - vertices.Last(); }
                else { prevDirection = vertices[i] - vertices[i - 1]; }
                if (i == vertices.Count - 1) { thisDirection = vertices[0] - vertices[i]; }
                else { thisDirection = vertices[i + 1] - vertices[i]; }

                if (!thisDirection.Normalize().IsAlmostEqualTo(prevDirection.Normalize()))
                {
                    reducedVertices.Add(vertices[i]);
                }
            }

            Debug.Print("the curveloop has vertices: " + vertices.Count.ToString() + " and reducted to " + reducedVertices.Count.ToString());

            reducedVertices.Add(reducedVertices[0]);
            for (int i = 0; i < reducedVertices.Count - 1; i++)
            {
                boundary.Append(Line.CreateBound(reducedVertices[i], reducedVertices[i + 1]) as Curve);
            }
            return boundary;
        }


        /// <summary>
        /// Join serveral connected line segments end-to-end as Polyline CurveLoop
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        public static PolyLine JoinLine(List<Line> lines)
        {
            if (lines.Count == 0)
            {
                return null;
            }
            // All Arcs need to be tessellated before put into this
            List<XYZ> vertices = new List<XYZ>();
            vertices.Add(lines[0].GetEndPoint(0));
            foreach (Line line in lines)
            {
                XYZ ptStart = line.GetEndPoint(0);
                XYZ ptEnd = line.GetEndPoint(1);
                if (vertices.Last().IsAlmostEqualTo(ptStart))
                {
                    vertices.Add(ptEnd);
                    continue;
                }
                if (vertices.Last().IsAlmostEqualTo(ptEnd))
                {
                    vertices.Add(ptStart);
                    continue;
                }
            }

            return PolyLine.Create(vertices);
        }


        /// <summary>
        /// Cannot solve situations where lines form a tree-like structure
        /// Only generate a random polyline from an intersected cluster
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        public static List<PolyLine> JoinLineByCluster(List<Line> lines)
        {
            List<PolyLine> plys = new List<PolyLine>();
            
            List<Line> remains = lines.ToList();
            List<Line> _lines;

            while (remains.Count > 0)
            {
                List<XYZ> cluster = new List<XYZ>();
                bool flag = true;
                while (flag)
                {
                    flag = false;
                    _lines = remains.ToList();
                    foreach (Line line in _lines)
                    {
                        XYZ startPt = line.GetEndPoint(0);
                        XYZ endPt = line.GetEndPoint(1);
                        if (cluster.Count == 0)
                        {
                            cluster.Add(startPt);
                            cluster.Add(endPt);
                            remains.Remove(line);
                            flag = true;
                        }
                        else
                        {
                            if (startPt.IsAlmostEqualTo(cluster[0]))
                            {
                                cluster.Insert(0, endPt);
                                remains.Remove(line);
                                flag = true;
                            }
                            else if (startPt.IsAlmostEqualTo(cluster.Last()))
                            {
                                cluster.Add(endPt);
                                remains.Remove(line);
                                flag = true;
                            }
                            else if (endPt.IsAlmostEqualTo(cluster.Last()))
                            {
                                cluster.Add(startPt);
                                remains.Remove(line);
                                flag = true;
                            }
                            else if (endPt.IsAlmostEqualTo(cluster[0]))
                            {
                                cluster.Insert(0, startPt);
                                remains.Remove(line);
                                flag = true;
                            }
                        }
                    }
                }
                plys.Add(PolyLine.Create(cluster));
                cluster.Clear();
                //Debug.Print("remaining elements are: " + remains.Count.ToString());
            }

            return plys;
        }

        #endregion
    }
}
