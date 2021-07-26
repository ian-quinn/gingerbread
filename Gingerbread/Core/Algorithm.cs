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
    public class Algorithm
    {
        #region XYZ method
        /// <summary>
        /// Return the same point with coarser coordinates
        /// </summary>
        /// <param name="pt"></param>
        /// <returns></returns>
        public static XYZ RoundXYZ(XYZ pt)
        {
            return new XYZ(Math.Round(pt.X, 4), Math.Round(pt.Y, 4), Math.Round(pt.Z, 4));
        }


        /// <summary>
        /// Calculate the clockwise angle from vec1 to vec2
        /// </summary>
        /// <param name="vec1"></param>
        /// <param name="vec2"></param>
        /// <returns></returns>
        public static double AngleTo2PI(XYZ vec1, XYZ vec2)
        {
            double dot = vec1.X * vec2.X + vec1.Y * vec2.Y;    // dot product between [x1, y1] and [x2, y2]
            double det = vec1.X * vec2.Y - vec1.Y * vec2.X;    // determinant
            double angle = Math.Atan2(det, dot);  // Atan2(y, x) or atan2(sin, cos)
            return angle;
        }

        /// <summary>
        /// Return XYZ after axis system rotation
        /// </summary>
        /// <param name="pt"></param>
        /// <param name="angle"></param>
        /// <returns></returns>
        public static XYZ PtAxisRotation2D(XYZ pt, double angle)
        {
            double Xtrans = pt.X * Math.Cos(angle) + pt.Y * Math.Sin(angle);
            double Ytrans = pt.Y * Math.Cos(angle) - pt.X * Math.Sin(angle);
            return new XYZ(Xtrans, Ytrans, pt.Z);
        }

        
        #endregion



        #region Curve method
        


        /// <summary>
        /// Recreate a line in replace of the joining/overlapping lines
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        public static Curve FuseLines(List<Curve> lines)
        {
            double Z = lines[0].GetEndPoint(0).Z;
            List<XYZ> pts = new List<XYZ>();
            foreach (Curve line in lines)
            {
                pts.Add(line.GetEndPoint(0));
                pts.Add(line.GetEndPoint(1));
            }
            double Xmin = double.PositiveInfinity;
            double Xmax = double.NegativeInfinity;
            double Ymin = double.PositiveInfinity;
            double Ymax = double.NegativeInfinity;
            foreach (XYZ pt in pts)
            {
                if (pt.X < Xmin) { Xmin = pt.X; }
                if (pt.X > Xmax) { Xmax = pt.X; }
                if (pt.Y < Ymin) { Ymin = pt.Y; }
                if (pt.Y > Ymax) { Ymax = pt.Y; }
            }
            return Line.CreateBound(new XYZ(Xmin, Ymin, Z), new XYZ(Xmax, Ymax, Z));
        }


        /// <summary>
        /// Cluster and merge the overlapping lines from a bunch of strays (on top of FuseLines)
        /// </summary>
        /// <param name="axes"></param>
        /// <returns></returns>
        public static List<Curve> MergeAxes(List<Curve> axes)
        {
            List<List<Curve>> axisGroups = new List<List<Curve>>();
            axisGroups.Add(new List<Curve>() { axes[0] });

            while (axes.Count != 0)
            {
                foreach (Line element in axes)
                {
                    int iterCounter = 0;
                    foreach (List<Curve> sublist in axisGroups)
                    {
                        iterCounter += 1;
                        if (Basic.IsLineOverlapLines(element, sublist))
                        {
                            sublist.Add(element);
                            axes.Remove(element);
                            goto a;
                        }
                        if (iterCounter == axisGroups.Count)
                        {
                            axisGroups.Add(new List<Curve>() { element });
                            axes.Remove(element);
                            goto a;
                        }
                    }
                }
            a:;
            }

            List<Curve> mergedLines = new List<Curve>();
            foreach (List<Curve> axisGroup in axisGroups)
            {
                mergedLines.Add(FuseLines(axisGroup));
            }
            return mergedLines;
        }


        /// <summary>
        /// Offset the curve a little bit to check if it contacts with others.
        /// </summary>
        /// <param name="crv"></param>
        /// <param name="target"></param>
        /// <returns></returns>
        public static bool IsVagueIntersected(Curve crv, Curve target)
        {
            double tolerance = 0.01;
            Transform up = Transform.CreateTranslation(tolerance * XYZ.BasisY);
            Transform down = Transform.CreateTranslation(-tolerance * XYZ.BasisY);
            Transform left = Transform.CreateTranslation(-tolerance * XYZ.BasisX);
            Transform right = Transform.CreateTranslation(tolerance * XYZ.BasisX);
            // Curves are basically not joined. It is better to round the coordinates to get a better intersection recognition
            //Curve crv0 = Line.CreateBound(RoundXYZ(crv.GetEndPoint(0)), RoundXYZ(crv.GetEndPoint(1))) as Curve;
            Curve crv0 = crv.Clone();
            crv0 = Core.DetectRegion.ExtendCrv(crv0, 0.01);
            Curve crv1 = crv0.CreateTransformed(up);
            Curve crv2 = crv0.CreateTransformed(down);
            Curve crv3 = crv0.CreateTransformed(left);
            Curve crv4 = crv0.CreateTransformed(right);
            if (Basic.IsIntersected(crv0, target) ||
                Basic.IsIntersected(crv1, target) ||
                Basic.IsIntersected(crv2, target) ||
                Basic.IsIntersected(crv3, target) ||
                Basic.IsIntersected(crv4, target))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Cluster curves by their intersection. The judgement function can be replaced by vague ones.
        /// </summary>
        /// <param name="crvs"></param>
        /// <returns></returns>
        public static List<List<Curve>> ClusterByIntersect(List<Curve> crvs)
        {
            /*
            for (int i = 0; i < crvs.Count; i++)
            {
                Debug.Print("Line{0} " + Util.PrintXYZ(crvs[i].GetEndPoint(0)) + " " + Util.PrintXYZ(crvs[i].GetEndPoint(1)), i);
            }
            */

            List<int> ids = Enumerable.Range(0, crvs.Count).ToList();
            List<List<Curve>> clusters = new List<List<Curve>>();
            while (ids.Count != 0)
            {
                List<Curve> cluster = new List<Curve>();
                List<int> idCluster = new List<int>() { ids[0] };
                List<int> idTemp = new List<int>() { ids[0] };
                ids.Remove(ids[0]);
                while (idTemp.Count != 0)
                {
                    List<int> idNextTemp = new List<int>();
                    for (int i = 0; i < idTemp.Count; i++)
                    {
                        int intersectionCount = 0;
                        List<int> idDel = new List<int>();
                        for (int j = 0; j < ids.Count; j++)
                        {
                            if (!idTemp.Contains(ids[j]))
                            {
                                if (IsVagueIntersected(crvs[idTemp[i]], crvs[ids[j]]))
                                {
                                    //Debug.Print("Intersected Line{0} and Line{1}", idTemp[i], ids[j]);
                                    idCluster.Add(ids[j]);
                                    idNextTemp.Add(ids[j]);
                                    idDel.Add(ids[j]);
                                    intersectionCount += 1;
                                }
                            }
                        }
                        if (intersectionCount == 0) { continue; }
                        foreach (int element in idDel)
                        {
                            ids.Remove(element);
                        }
                    }
                    idTemp = idNextTemp;
                }
                foreach (int id in idCluster)
                {
                    cluster.Add(crvs[id]);
                }
                clusters.Add(cluster);
                //Debug.Print("Cluster has " + Util.PrintSeq(idCluster));
            }
            return clusters;
        }

        /// <summary>
        /// Cluster line segments if they were all piled upon a single line.
        /// </summary>
        /// <param name="crvs"></param>
        /// <returns></returns>
        public static List<List<Curve>> ClusterByOverlap(List<Curve> crvs)
        {
            List<int> ids = Enumerable.Range(0, crvs.Count).ToList();
            List<List<Curve>> clusters = new List<List<Curve>>();
            while (ids.Count != 0)
            {
                List<Curve> cluster = new List<Curve>();
                List<int> idCluster = new List<int>() { ids[0] };
                List<int> idTemp = new List<int>() { ids[0] };
                ids.Remove(ids[0]);
                while (idTemp.Count != 0)
                {
                    List<int> idNextTemp = new List<int>();
                    for (int i = 0; i < idTemp.Count; i++)
                    {
                        int intersectionCount = 0;
                        List<int> idDel = new List<int>();
                        for (int j = 0; j < ids.Count; j++)
                        {
                            if (!idTemp.Contains(ids[j]))
                            {
                                // Use this only to line segments
                                if (Basic.IsIntersected(crvs[idTemp[i]], crvs[ids[j]])
                                    && Basic.IsParallel(crvs[idTemp[i]] as Line, crvs[ids[j]] as Line))
                                {
                                    idCluster.Add(ids[j]);
                                    idNextTemp.Add(ids[j]);
                                    idDel.Add(ids[j]);
                                    intersectionCount += 1;
                                }
                            }
                        }
                        if (intersectionCount == 0) { continue; }
                        foreach (int element in idDel)
                        {
                            ids.Remove(element);
                        }
                    }
                    idTemp = idNextTemp;
                }
                foreach (int id in idCluster)
                {
                    cluster.Add(crvs[id]);
                }
                clusters.Add(cluster);
            }
            return clusters;
        }

        /*
        /// <summary>
        /// Cluster lines if they are almost joined at end point.
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        public static List<List<Line>> ClusterByKnob(List<Line> lines)
        {
            List<List<Line>> clusters = new List<List<Line>> { };
            clusters.Add(new List<Line> { lines[0] });
            for (int i = 1; i < lines.Count; i++)
            {
                if (null == lines[i]) { continue; }
                foreach (List<Line> cluster in clusters)
                {
                    if (IsLineAlmostJoinedLines(lines[i], cluster))
                    {
                        cluster.Add(lines[i]);
                        goto a;
                    }
                }
                clusters.Add(new List<Line> { lines[i] });
            a:
                continue;
            }
            return clusters;
        }
        */

        /// <summary>
        /// Get non-duplicated points of a bunch of curves.
        /// </summary>
        /// <param name="crvs"></param>
        /// <returns></returns>
        public static List<XYZ> GetPtsOfCrvs(List<Curve> crvs)
        {
            List<XYZ> pts = new List<XYZ> { };
            foreach (Curve crv in crvs)
            {
                XYZ ptStart = crv.GetEndPoint(0);
                XYZ ptEnd = crv.GetEndPoint(1);
                pts.Add(ptStart);
                pts.Add(ptEnd);
            }
            for (int i = 0; i < pts.Count; i++)
            {
                for (int j = pts.Count - 1; j > i; j--)
                {
                    if (pts[i].IsAlmostEqualTo(pts[j]))
                    {
                        pts.RemoveAt(j);
                    }
                }
            }
            //Debug.Print("Vertices in all: " + pts.Count.ToString());
            return pts;
        }

        /// <summary>
        /// Calculate the distance of double lines
        /// </summary>
        /// <param name="line1"></param>
        /// <param name="line2"></param>
        /// <returns></returns>
        public static double LineSpacing(Line line1, Line line2)
        {
            XYZ midPt = line1.Evaluate(0.5, true);
            Line target = line2.Clone() as Line;
            target.MakeUnbound();
            double spacing = target.Distance(midPt);
            return spacing;
        }

        /// <summary>
        /// Generate axis by offset wall boundary
        /// </summary>
        /// <param name="line1"></param>
        /// <param name="line2"></param>
        /// <returns></returns>
        public static Line GenerateAxis(Line line1, Line line2)
        {
            Curve baseline = line1.Clone();
            Curve targetline = line2.Clone();
            if (line1.Length < line2.Length)
            {
                baseline = line2.Clone();
                targetline = line1.Clone();
            }
            targetline.MakeUnbound();
            XYZ midPt = baseline.Evaluate(0.5, true);
            XYZ midPt_proj = targetline.Project(midPt).XYZPoint;
            XYZ vec = (midPt_proj - midPt) / 2;
            double offset = vec.GetLength() / 2.0;
            //Debug.Print(offset.ToString());
            if (offset != 0)
            {
                Line axis = Line.CreateBound(baseline.GetEndPoint(0) + vec, baseline.GetEndPoint(1) + vec);
                return axis;
            }
            else
            {
                return null;
            }
            //Line axis = baseline.CreateOffset(offset, vec.Normalize()) as Line;
        }

        /// <summary>
        /// Extend a line segment with certain extension(mm) on both ends.
        /// </summary>
        /// <param name="line"></param>
        /// <param name="extension"></param>
        /// <returns></returns>
        public static Curve ExtendLine(Curve line, double extension)
        {
            XYZ ptStart = line.GetEndPoint(0);
            XYZ ptEnd = line.GetEndPoint(1);
            XYZ vec = (ptEnd - ptStart).Normalize();
            return Line.CreateBound(ptStart - vec * Util.MmToFoot(extension),
                ptEnd + vec * Util.MmToFoot(extension));
        }


        #endregion



        #region Region method

        /// <summary>
        /// Check if a bunch of lines enclose a rectangle.
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        public static bool IsRectangle(List<Curve> lines)
        {
            if (lines.Count() == 4)
            {
                if (GetPtsOfCrvs(lines).Count() == lines.Count())
                {
                    CurveArray edges = Core.DetectRegion.AlignCrv(lines);
                    if (Basic.IsPerpendicular(edges.get_Item(0), edges.get_Item(1)) &&
                        Basic.IsPerpendicular(edges.get_Item(1), edges.get_Item(2)))
                    { return true; }
                    else { return false; }
                }
                else { return false; }
            }
            else { return false; }
        }

        /// <summary>
        /// Return the bounding box of curves. 
        /// The box has the minimum area with axis in align with the curve direction.
        /// </summary>
        /// <param name="crvs"></param>
        /// <returns></returns>
        public static List<Curve> CreateBoundingBox2D(List<Curve> crvs)
        {
            // There can be a bounding box of an arc
            // but it is ambiguous to define the deflection of an arc-block
            // which is not the case as a door-block or window-block
            if (crvs.Count <= 1) { return null; }

            // Tolerance is to avoid generating boxes too small
            double tolerance = 0.001;
            List<XYZ> pts = GetPtsOfCrvs(crvs);
            double ZAxis = pts[0].Z;
            List<double> processions = new List<double> { };
            foreach (Curve crv in crvs)
            {
                // The Arc features no deflection of the door block
                if (crv.GetType().ToString() == "Autodesk.Revit.DB.Line")
                {
                    double angle = XYZ.BasisX.AngleTo(crv.GetEndPoint(1) - crv.GetEndPoint(0));
                    if (angle > Math.PI / 2)
                    {
                        angle = Math.PI - angle;
                    }
                    if (!processions.Contains(angle))
                    {
                        processions.Add(angle);
                    }
                }

            }
            //Debug.Print("Deflections in all: " + processions.Count.ToString());

            double area = double.PositiveInfinity;  // Mark the minimum bounding box area
            double deflection = 0;  // Mark the corresponding deflection angle
            double X0 = 0;
            double X1 = 0;
            double Y0 = 0;
            double Y1 = 0;
            foreach (double angle in processions)
            {
                double Xmin = double.PositiveInfinity;
                double Xmax = double.NegativeInfinity;
                double Ymin = double.PositiveInfinity;
                double Ymax = double.NegativeInfinity;
                foreach (XYZ pt in pts)
                {
                    double Xtrans = PtAxisRotation2D(pt, angle).X;
                    double Ytrans = PtAxisRotation2D(pt, angle).Y;
                    if (Xtrans < Xmin) { Xmin = Xtrans; }
                    if (Xtrans > Xmax) { Xmax = Xtrans; }
                    if (Ytrans < Ymin) { Ymin = Ytrans; }
                    if (Ytrans > Ymax) { Ymax = Ytrans; }
                }
                if (((Xmax - Xmin) * (Ymax - Ymin)) < area)
                {
                    area = (Xmax - Xmin) * (Ymax - Ymin);
                    deflection = angle;
                    X0 = Xmin;
                    X1 = Xmax;
                    Y0 = Ymin;
                    Y1 = Ymax;
                }
            }

            if (X1 - X0 < tolerance || Y1 - Y0 < tolerance)
            {
                Debug.Print("WARNING! Bounding box too small to be generated! ");
                return null;
            }

            else
            {
                // Inverse transformation
                XYZ pt1 = PtAxisRotation2D(new XYZ(X0, Y0, ZAxis), -deflection);
                XYZ pt2 = PtAxisRotation2D(new XYZ(X1, Y0, ZAxis), -deflection);
                XYZ pt3 = PtAxisRotation2D(new XYZ(X1, Y1, ZAxis), -deflection);
                XYZ pt4 = PtAxisRotation2D(new XYZ(X0, Y1, ZAxis), -deflection);
                Curve crv1 = Line.CreateBound(pt1, pt2) as Curve;
                Curve crv2 = Line.CreateBound(pt2, pt3) as Curve;
                Curve crv3 = Line.CreateBound(pt3, pt4) as Curve;
                Curve crv4 = Line.CreateBound(pt4, pt1) as Curve;
                List<Curve> boundingBox = new List<Curve> { crv1, crv2, crv3, crv4 };
                return boundingBox;
            }
        }

        // Center point of list of lines
        // Need upgrade to polygon center point method
        public static XYZ GetCenterPt(List<Curve> lines)
        {
            double ptSum_X = 0;
            double ptSum_Y = 0;
            double ptSum_Z = lines[0].GetEndPoint(0).Z;
            foreach (Line line in lines)
            {
                ptSum_X += line.GetEndPoint(0).X;
                ptSum_X += line.GetEndPoint(1).X;
                ptSum_Y += line.GetEndPoint(0).Y;
                ptSum_Y += line.GetEndPoint(1).Y;
            }
            XYZ centerPt = new XYZ(ptSum_X / lines.Count / 2, ptSum_Y / lines.Count / 2, ptSum_Z);
            return centerPt;
        }

        // Retrieve the width and depth of a rectangle
        public static Tuple<double, double, double> GetSizeOfRectangle(List<Line> lines)
        {
            List<double> rotations = new List<double> { };  // in radian
            List<double> lengths = new List<double> { };  // in milimeter
            foreach (Line line in lines)
            {
                XYZ vec = line.GetEndPoint(1) - line.GetEndPoint(0);
                double angle = AngleTo2PI(vec, XYZ.BasisX);
                //Debug.Print("Iteration angle is " + angle.ToString());
                rotations.Add(angle);
                lengths.Add(Util.FootToMm(line.Length));
            }
            int baseEdgeId = rotations.IndexOf(rotations.Min());
            double width = lengths[baseEdgeId];
            double depth = width;
            if (width == lengths.Min()) { depth = lengths.Max(); }
            else { depth = lengths.Min(); }

            return Tuple.Create(Math.Round(width, 2), Math.Round(depth, 2), rotations.Min());
            // clockwise rotation in radian measure
            // x pointing right and y down as is common for computer graphics
            // this will mean you get a positive sign for clockwise angles
        }

        // 
        public static CurveArray RectifyPolygon(List<Line> lines)
        {
            CurveArray boundary = new CurveArray();
            List<XYZ> vertices = new List<XYZ>() { };
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
            /*
            Debug.Print("number of vertices: " + vertices.Count());
            foreach (XYZ pt in vertices)
            {
                Debug.Print(Util.PrintXYZ(pt));
            }
            */
            for (int i = 0; i < lines.Count; i++)
            {
                boundary.Append(Line.CreateBound(vertices[i], vertices[i + 1]));
            }
            return boundary;
        }


        public static List<Curve> CenterLinesOfBox(List<Curve> box)
        {
            XYZ centPt = GetCenterPt(box);
            List<Curve> centerLines = new List<Curve>();
            foreach (Line edge in box)
            {
                centerLines.Add(Line.CreateBound(centPt, edge.Evaluate(0.5, true)));
            }
            return centerLines;
        }



        // 
        public static Tuple<double, double, double> GetSizeOfFootprint(List<Line> lines)
        {
            return null;
        }
        #endregion

    }
}
