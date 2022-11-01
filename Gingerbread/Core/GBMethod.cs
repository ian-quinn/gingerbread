using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClipperLib;

namespace Gingerbread.Core
{
#region basic operations
    public enum segIntersectEnum
    {
        IntersectOnBoth,
        IntersectOnA,
        IntersectOnB,
        IntersectOnLine,
        ColineDisjoint,
        ColineOverlap,
        ColineJoint,
        ColineAContainB,
        ColineBContainA,
        Parallel,
        Intersect, 
        Coincident
    }
    public enum polyIntersectEnum
    {
        Intersect,
        Overlap,
        AContainB,
        BContainA,
        Coincide,
        Isolate
    }
    // Seg - Segment, not line
    // Poly - Polyline, including polygon, represented by a loop of points
    public class GBMethod
    {
        public const double _eps = 1.0e-6;

        public static gbXYZ GetPendicularVec(gbXYZ vec, bool isClockwise)
        {
            if (isClockwise)
                return new gbXYZ(vec.Y, -vec.X, 0);
            return new gbXYZ(-vec.Y, vec.X, 0);
        }

        /// <summary>
        /// Return the angle (0~360) of two vector by calculating arctangent
        /// </summary>
        public static double VectorAngle(gbXYZ vec1, gbXYZ vec2)
        {
            //double x1 = endPt1[0] - connectingPt[0]; //Vector 1 - x
            //double y1 = endPt1[1] - connectingPt[1]; //Vector 1 - y
            //double x2 = endPt2[0] - connectingPt[0]; //Vector 2 - x
            //double y2 = endPt2[1] - connectingPt[1]; //Vector 2 - y

            // for angle 0 ~ 180 use Math.Atan
            // for angle 0 ~ 360 use Math.Atan2
            double angle = Math.Atan2(vec1.Y, vec1.X) - Math.Atan2(vec2.Y, vec2.X);
            angle = angle * 360 / (2 * Math.PI);

            if (angle < 0)
                angle += 360;

            return angle;
        }

        /// <summary>
        /// Return the angle (0~180) of two vector by calculating arccosin
        /// The two vector must be on the same plane.
        /// </summary>
        public static double VectorAngle2D(gbXYZ vec1, gbXYZ vec2)
        {
            double value = Math.Round(vec1.DotProduct(vec2) / vec1.Norm() / vec2.Norm(), 6);
            double angle = Math.Acos(value);
            angle = angle * 180 / Math.PI;

            return angle;
        }

        // for pre-process of the wall centerlines
        // further this function will be used to cast XYZ class to gbPoint class
        // here it only serves to flatten Point3d to Point2d (pseudo)
        // regenerate all lines, we don't mess with the original data
        public static List<gbSeg> FlattenLines(List<gbSeg> segs)
        {
            List<gbSeg> worldPlaneLines = new List<gbSeg>();
            foreach (gbSeg seg in segs)
            {
                gbXYZ startPt = new gbXYZ(seg.PointAt(0).X, seg.PointAt(0).Y, 0);
                gbXYZ endPt = new gbXYZ(seg.PointAt(1).X, seg.PointAt(1).Y, 0);
                worldPlaneLines.Add(new gbSeg(startPt, endPt));
            }
            return worldPlaneLines;
        }

        /// <summary>
        /// Just pile all endpoints in a list
        /// </summary>
        public static List<gbXYZ> PilePts(List<gbSeg> segs)
        {
            List<gbXYZ> pts = new List<gbXYZ>();
            foreach (gbSeg seg in segs)
            {
                if (!pts.Contains(seg.PointAt(0)))
                    pts.Add(seg.PointAt(0));
                if (!pts.Contains(seg.PointAt(1)))
                    pts.Add(seg.PointAt(1));
            }
            return pts;
        }


        /// <summary>
        /// Change the z coordinate of a list of points by elevation.
        /// </summary>
        public static List<gbXYZ> ElevatePts(List<gbXYZ> pts, double elevation)
        {
            List<gbXYZ> elevatedPts = new List<gbXYZ>();
            for (int i = 0; i < pts.Count; i++)
                elevatedPts.Add(pts[i] + new gbXYZ(0, 0, elevation));
            return elevatedPts;
        }
        /// <summary>
        /// Make the z coordinate of a point zero.
        /// </summary>
        public static gbXYZ FlattenPt(gbXYZ pt)
        {
            return new gbXYZ(pt.X, pt.Y, 0);
        }
        public static List<gbXYZ> FlattenPts(List<gbXYZ> pts)
        {
            List<gbXYZ> flattenedPts = new List<gbXYZ>();
            for (int i = 0; i < pts.Count; i++)
                flattenedPts.Add(new gbXYZ(pts[i].X, pts[i].Y, 0));
            return flattenedPts;
        }

        public static gbXYZ RelativePt(gbXYZ pt, gbXYZ origin)
        {
            return new gbXYZ(
                Math.Abs(pt.X - origin.X),
                Math.Abs(pt.Y - origin.Y),
                Math.Abs(pt.Z - origin.Z));
        }
#endregion basic operations


#region seg relations
        /// <summary>
        /// Return the intersectEnum, output the intersection point and the ratio if the point falls on the first segment.
        /// </summary>
        public static segIntersectEnum SegIntersection(gbXYZ p1, gbXYZ p2, gbXYZ p3, gbXYZ p4, double tol, 
            out gbXYZ intersection, out double t1, out double t2)
        {
            // represents stretch vector of seg1 vec1 = (dx12, dy12)
            double dx12 = p2.X - p1.X;
            double dy12 = p2.Y - p1.Y;
            // represents stretch vector of seg2 vec2 = (dx34, dy34)
            double dx34 = p4.X - p3.X;
            double dy34 = p4.Y - p3.Y;

            segIntersectEnum intersect;
            intersection = new gbXYZ(double.NaN, double.NaN, 0);

            // checker as cross product of vec1 and vec2
            double denominator = dy12 * dx34 - dx12 * dy34;
            // co-line checker as cross product of (p3 - p1) and (p2 - p1)
            // this value represents the area of the parallelogram.
            // If near to zero,  the parallel edges are co-lined
            double stretch = (p3.X - p1.X) * dy12 + (p1.Y - p3.Y) * dx12;
            t1 = 0;
            t2 = 0;

            if (Math.Abs(denominator) < _eps && Math.Abs(stretch) > _eps)
                return segIntersectEnum.Parallel;
            if (Math.Abs(denominator) < _eps && Math.Abs(stretch) < _eps)
            {
                // express endpoints of seg2 in terms of seg1 parameter
                double s1 = ((p3.X - p1.X) * dx12 + (p3.Y - p1.Y) * dy12) / (dx12 * dx12 + dy12 * dy12);
                double s2 = s1 + (dx12 * dx34 + dy12 * dy34) / (dx12 * dx12 + dy12 * dy12);
                if (s1 > s2)
                {
                    Util.Swap(ref s1, ref s2);
                    Util.Swap(ref p3, ref p4);
                }

                if (Math.Abs(s1) < tol) s1 = 0;
                if (Math.Abs(s2) < tol) s2 = 0;
                if (Math.Abs(s1 - 1) < tol) s1 = 1;
                if (Math.Abs(s2 - 1) < tol) s2 = 1;

                if (s1 == 0 && s2 == 1)
                    return segIntersectEnum.Coincident;
                if (s1 > 1 || s2 < 0)
                    return segIntersectEnum.ColineDisjoint;
                if ((s1 >= 0 && s1 <= 1) || (s2 >= 0 && s2 <= 1))
                    if ((s1 >= 0 && s1 <= 1) && (s2 >= 0 && s2 <= 1))
                        return segIntersectEnum.ColineAContainB;
                    else
                    {
                        if (s1 == 1)
                            return segIntersectEnum.ColineJoint;
                        if (s1 == 0)
                            return segIntersectEnum.ColineBContainA;
                        if (s2 == 0)
                            return segIntersectEnum.ColineJoint;
                        if (s2 == 1)
                            return segIntersectEnum.ColineBContainA;
                        return segIntersectEnum.ColineOverlap;
                    } 
                else
                {
                    return segIntersectEnum.ColineBContainA;
                }
            }

            intersect = segIntersectEnum.IntersectOnLine;

            t1 = ((p1.X - p3.X) * dy34 + (p3.Y - p1.Y) * dx34) / denominator;
            t2 = ((p3.X - p1.X) * dy12 + (p1.Y - p3.Y) * dx12) / -denominator;
            //fractile = t1;

            if (t1 > 10000 || t2 > 10000)
            {
                //Debug.Print($"GBMethod:: Wrong at intersection checking");
            }

            if ((t1 >= 0 - tol) && (t1 <= 1 + tol))
                intersect = segIntersectEnum.IntersectOnA;
            if ((t2 >= 0 - tol) && (t2 <= 1 + tol))
                intersect = segIntersectEnum.IntersectOnB;
            if ((t1 >= 0 - tol) && (t1 <= 1 + tol) && (t2 >= 0 - tol) && (t2 <= 1 + tol))
                intersect = segIntersectEnum.IntersectOnBoth;

            intersection = new gbXYZ(p1.X + dx12 * t1, p1.Y + dy12 * t1, 0);

            return intersect;
        }
        public static segIntersectEnum SegIntersection(gbSeg a, gbSeg b, double tol, 
            out gbXYZ intersection, out double t1, out double t2)
        {
            gbXYZ p1 = a.PointAt(0);
            gbXYZ p2 = a.PointAt(1);
            gbXYZ p3 = b.PointAt(0);
            gbXYZ p4 = b.PointAt(1);
            intersection = new gbXYZ();
            return SegIntersection(p1, p2, p3, p4, tol, out intersection, out t1, out t2);
        }
        public static segIntersectEnum SegFusion(gbSeg a, gbSeg b, double tol, out gbSeg fusion)
        {
            gbXYZ p1 = a.Start;
            gbXYZ p2 = a.End;
            gbXYZ p3 = b.Start;
            gbXYZ p4 = b.End;
            // represents stretch vector of seg1 vec1 = (dx12, dy12)
            double dx12 = p2.X - p1.X;
            double dy12 = p2.Y - p1.Y;
            // represents stretch vector of seg2 vec2 = (dx34, dy34)
            double dx34 = p4.X - p3.X;
            double dy34 = p4.Y - p3.Y;

            fusion = null;

            // checker as cross product of vec1 and vec2
            double denominator = dy12 * dx34 - dx12 * dy34;
            // co-line checker as cross product of (p3 - p1) and vec1/vec
            double stretch = (p3.X - p1.X) * dy12 + (p1.Y - p3.Y) * dx12;
            // check the gap between two almost parallel segments
            double gap = SegDistanceToSeg(a, b, out double overlap, out gbSeg proj);
            //if (gap < 0.000001 && overlap > 0.000001)
            //{
            //    Debug.Print("GBMethod:: experiencing the gap");
            //    double d1 = PtDistanceToSeg(p1, b, out gbXYZ plummet1, out double s1);
            //    double d2 = PtDistanceToSeg(p1, b, out gbXYZ plummet2, out double s2);
            //    p1 = plummet1;
            //    p2 = plummet2;
            //}

            if (Math.Abs(denominator) < _eps && Math.Abs(stretch) > _eps)
            {
                return segIntersectEnum.Parallel;
            }
            if (Math.Abs(denominator) < _eps && Math.Abs(stretch) < _eps)
            {
                //Debug.Print($"GBMethod:: Seg fused {a} {b}");
                // express endpoints of seg2 in terms of seg1 parameter
                double s1 = ((p3.X - p1.X) * dx12 + (p3.Y - p1.Y) * dy12) / (dx12 * dx12 + dy12 * dy12);
                double s2 = s1 + (dx12 * dx34 + dy12 * dy34) / (dx12 * dx12 + dy12 * dy12);
                if (s1 > s2)
                {
                    Util.Swap(ref s1, ref s2);
                    Util.Swap(ref p3, ref p4);
                }

                if (Math.Abs(s1) < tol) s1 = 0;
                if (Math.Abs(s2) < tol) s2 = 0;
                if (Math.Abs(s1 - 1) < tol) s1 = 1;
                if (Math.Abs(s2 - 1) < tol) s2 = 1;

                if (s1 == 0 && s2 == 1)
                {
                    fusion = new gbSeg(p1, p2);
                    return segIntersectEnum.Coincident;
                }

                if (s1 > 1 || s2 < 0)
                    return segIntersectEnum.ColineDisjoint;

                if ((s1 >= 0 && s1 <= 1) || (s2 >= 0 && s2 <= 1))
                    if ((s1 >= 0 && s1 <= 1) && (s2 >= 0 && s2 <= 1))
                    {
                        fusion = new gbSeg(p1, p2);
                        return segIntersectEnum.ColineAContainB;
                    }
                    else
                    {
                        if (s1 == 1)
                        {
                            fusion = new gbSeg(p1, p4);
                            return segIntersectEnum.ColineJoint;
                        }
                        if (s1 == 0)
                        {
                            fusion = new gbSeg(p3, p4);
                            return segIntersectEnum.ColineBContainA;
                        }
                        if (s2 == 0)
                        {
                            fusion = new gbSeg(p3, p2);
                            return segIntersectEnum.ColineJoint;
                        }
                        if (s2 == 1)
                        {
                            fusion = new gbSeg(p3, p4);
                            return segIntersectEnum.ColineBContainA;
                        }
                        if (s1 > 0 && s1 < 1)
                        {
                            fusion = new gbSeg(p1, p4);
                            return segIntersectEnum.ColineOverlap;
                        }
                        if (s2 > 0 && s2 < 1)
                        {
                            fusion = new gbSeg(p3, p2);
                            return segIntersectEnum.ColineOverlap;
                        }
                    }
                else
                {
                    fusion = new gbSeg(p3, p4);
                    return segIntersectEnum.ColineBContainA;
                }
            }

            return segIntersectEnum.Intersect;
        }
        public static bool IsSegPolyIntersected(gbSeg a, List<gbXYZ> poly, double tol)
        {
            for (int i = 0; i < poly.Count - 1; i++)
            {
                gbSeg edge = new gbSeg(poly[i], poly[i + 1]);
                segIntersectEnum IntersectResult = SegIntersection(a, edge, tol, out gbXYZ intersection, out double t1, out double t2);
                if (IntersectResult == segIntersectEnum.IntersectOnBoth ||
                    IntersectResult == segIntersectEnum.ColineAContainB)
                    return true;
            }
            return false;
        }
#endregion seg relations

#region seg operations

        // further there will be gbLine method to modify line endpoints directly
        // for now, just regenerate one. always the longer one when trimming.
        public static gbSeg SegExtensionToSeg(gbSeg a, gbSeg b, double tolerance)
        {
            gbXYZ p1 = a.PointAt(0);
            gbXYZ p2 = a.PointAt(1);
            gbXYZ p3 = b.PointAt(0);
            gbXYZ p4 = b.PointAt(1);
            gbXYZ intersection = new gbXYZ();
            double dx12 = p2.X - p1.X;
            double dy12 = p2.Y - p1.Y;
            double dx34 = p4.X - p3.X;
            double dy34 = p4.Y - p3.Y;
            gbSeg extLine = a;
            double extensionA = 0;
            double extensionB = 0;

            double denominator = (dy12 * dx34 - dx12 * dy34);

            double t1 =
                ((p1.X - p3.X) * dy34 + (p3.Y - p1.Y) * dx34)
                    / denominator;
            if (double.IsInfinity(t1))
                return extLine;

            double t2 =
                ((p3.X - p1.X) * dy12 + (p1.Y - p3.Y) * dx12)
                    / -denominator;

            intersection = new gbXYZ(p1.X + dx12 * t1, p1.Y + dy12 * t1, 0);

            if (t2 < 0)
                extensionB = Math.Abs(t2) * b.Length;
            else if (t2 > 1)
                extensionB = (t2 - 1) * b.Length;

            if (t1 < 0)
            {
                extensionA = Math.Abs(t1) * a.Length;
                extLine = new gbSeg(intersection, p2);
            }
            else if (t1 > 1)
            {
                extensionA = (t1 - 1) * a.Length;
                extLine = new gbSeg(p1, intersection);
            }
            if (extensionA < tolerance && extensionB < tolerance)
                return extLine;
            else
                return a;
        }
        
        /// <summary>
        /// Extend the segment to another one if the extension is within the range "delta".
        /// This function change the original subject segment.
        /// </summary>
        public static void SegExtendToSeg(gbSeg subj, gbSeg obj, double tol, double delta)
        {
            segIntersectEnum sectType = SegIntersection(subj, obj, tol, out gbXYZ intersection, out double t1, out double t2);
            if (sectType == segIntersectEnum.ColineDisjoint)
            {
                double minDistance = double.PositiveInfinity;
                Tuple<int, int> substitute = new Tuple<int, int>(0, 0);
                for (int i = 0; i < 2; i++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        double d = subj.PointAt(i).DistanceTo(obj.PointAt(j));
                        if (d < minDistance)
                        {
                            minDistance = d;
                            substitute = new Tuple<int, int>(i, j);
                        }
                    }
                }
                if (minDistance < delta)
                    subj.AdjustEndPt(substitute.Item1, obj.PointAt(substitute.Item2));
                    //return new gbSeg(obj.PointAt(substitute.Item2), subj.PointAt(1 - substitute.Item1));
            }
            if (sectType == segIntersectEnum.IntersectOnLine)
            {
                double stretchA = t1 > 1 ? subj.Length * (t1 - 1) : subj.Length * (0 - t1);
                double stretchB = t2 > 1 ? obj.Length * (t2 - 1) : obj.Length * (0 - t2);
                if (stretchA < delta && stretchB < delta)
                    if (t1 > 1)
                        subj.AdjustEndPt(1, intersection);
                        //return new gbSeg(subj.PointAt(1), intersection);
                    else
                        subj.AdjustEndPt(0, intersection);
                        //return new gbSeg(subj.PointAt(0), intersection);
            }
            if (sectType == segIntersectEnum.IntersectOnB)
            {
                double stretchA = t1 > 1 ? subj.Length * (t1 - 1) : subj.Length * (0 - t1);
                if (stretchA < delta)
                    if (t1 > 1)
                        subj.AdjustEndPt(1, intersection);
                        //return new gbSeg(subj.PointAt(0), intersection);
                    else
                        subj.AdjustEndPt(0, intersection);
                        //return new gbSeg(subj.PointAt(0), intersection);
            }
            //return subj.Copy();
        }

        /// <summary>
        /// Align line segments if they are parallel and within a band with width smaller than the threshold.
        /// </summary>
        public static List<gbSeg> SegsAlignment(List<gbSeg> segs, double threshold)
        {
            List<gbSeg> _segs = new List<gbSeg>();
            foreach (gbSeg seg in segs)
                _segs.Add(new gbSeg(seg.Start, seg.End));
            List<List<gbSeg>> segGroups = new List<List<gbSeg>>();
            while (_segs.Count > 0)
            {
                List<gbSeg> segGroup = new List<gbSeg>() { _segs[0] };
                _segs.RemoveAt(0);
                for (int i = 0; i < segGroup.Count; i++)
                {
                    for (int j = _segs.Count - 1; j >= 0; j--)
                    {
                        if (segGroup[i] != _segs[j])
                        {
                            segIntersectEnum result = SegIntersection(_segs[j], segGroup[i], _eps,
                                out gbXYZ intersection, out double t1, out double t2);
                            double d1 = PtDistanceToSeg(_segs[j].Start, segGroup[i], out gbXYZ start, out double s1);
                            double d2 = PtDistanceToSeg(_segs[j].Start, segGroup[i], out gbXYZ end, out double s2);
                            double gap = d1 <= d2 ? d1 : d2;
                            if (result == segIntersectEnum.Parallel && gap < threshold)
                            {
                                segGroup.Add(_segs[j]);
                                _segs.RemoveAt(j);
                            }
                        }
                    }
                }
                segGroups.Add(segGroup);
                //Debug.Print($"GBMethod::SegsAlignment cluster with {segGroup.Count} added.");
            }
            List<gbSeg> collapse = new List<gbSeg>();
            foreach (List<gbSeg> segGroup in segGroups)
            {
                if (segGroup.Count == 0)
                    continue;
                if (segGroup.Count == 1)
                {
                    // PENDING
                    /*int checker = 0;
                    foreach (gbSeg axis in blueprint)
                    {
                        checker++;
                        double gap = SegDistanceToSeg(segGroup[0], axis, out double overlap, out gbSeg proj);
                        if (gap < 0.2 && overlap > 0)
                        {
                            double d1 = PtDistanceToSeg(segGroup[0].Start, axis, out gbXYZ start, out double t1);
                            double d2 = PtDistanceToSeg(segGroup[0].End, axis, out gbXYZ end, out double t2);
                            gbSeg aligned = new gbSeg(start, end);
                            if (aligned.length > 0.1)
                            {
                                collapse.Add(aligned);
                                break;
                            }
                        }
                    }*/
                    collapse.Add(segGroup[0]);
                    continue;
                }
                gbXYZ refPt = segGroup[0].Start;
                for (int i = 1; i < segGroup.Count; i++)
                {
                    double d1 = PtDistanceToSeg(refPt, segGroup[i], out gbXYZ plummet, out double t);
                    refPt += plummet;
                }
                refPt = refPt / segGroup.Count;
                gbSeg baseLine = new gbSeg(refPt, refPt + segGroup[0].Direction);
                foreach (gbSeg seg in segGroup)
                {
                    double d1 = PtDistanceToSeg(seg.Start, baseLine, out gbXYZ start, out double t1);
                    double d2 = PtDistanceToSeg(seg.End, baseLine, out gbXYZ end, out double t2);
                    gbSeg aligned = new gbSeg(start, end);
                    if (aligned.Length > 0.1)
                    {
                        collapse.Add(aligned);
                        //Debug.Print($"GBMethod::SegsAlignment aligned line added {aligned}");
                    }
                }
            }
            return collapse;
        }

        /// <summary>
        /// Fuse line segments if they are co-linear and overlapping.
        /// </summary>
        public static List<gbSeg> SegsFusion(List<gbSeg> segs, double threshold)
        {
            List<gbSeg> _segs = new List<gbSeg>();
            // 0 length segment that happens to be an intersection point
            // will be colined with two joining segments not parallel
            foreach (gbSeg seg in segs)
                if (seg.Length > _eps)
                    _segs.Add(new gbSeg(seg.Start, seg.End));
                
            for (int i = _segs.Count - 1; i >= 1; i--)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    segIntersectEnum result = SegFusion(_segs[i], _segs[j], _eps, out gbSeg fusion);
                    if (result == segIntersectEnum.ColineAContainB ||
                        result == segIntersectEnum.ColineBContainA ||
                        result == segIntersectEnum.ColineJoint ||
                        result == segIntersectEnum.ColineOverlap)
                    {
                        if (fusion != null)
                        {
                            _segs[j] = fusion;
                            _segs.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
            return _segs;
        }

        /// <summary>
        /// Calculate the distance between the point and the segment.
        /// Output the projected point and the ratio that the point is evaluated by the segment.
        /// </summary>
        public static double PtDistanceToSeg(gbXYZ pt, gbSeg line,
          out gbXYZ plummet, out double stretch)
        {
            double dx = line.PointAt(1).X - line.PointAt(0).X;
            double dy = line.PointAt(1).Y - line.PointAt(0).Y;
            gbXYZ origin = line.PointAt(0);

            if ((dx == 0) && (dy == 0)) // zero length segment
            {
                plummet = origin;
                stretch = 0;
                dx = pt.X - origin.X;
                dy = pt.Y - origin.Y;
                return Math.Sqrt(dx * dx + dy * dy);
            }

            // Calculate the t that minimizes the distance.
            stretch = ((pt.X - origin.X) * dx + (pt.Y - origin.Y) * dy) /
              (dx * dx + dy * dy);

            plummet = new gbXYZ(origin.X + stretch * dx, origin.Y + stretch * dy, 0);
            //plummet = new line.PointAt(stretch);
            dx = pt.X - plummet.X;
            dy = pt.Y - plummet.Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// Calculate the distance between two line segments if they are parallel (with 1 degree tolerance).
        /// Output the length of their overlapping region. Output the projected line segment as the overlapping region.
        /// </summary>
        public static double SegDistanceToSeg(gbSeg subj, gbSeg obj, out double overlap, out gbSeg proj)
        {
            gbXYZ start = subj.Start;
            gbXYZ end = subj.End;
            double angle = VectorAngle2D(subj.Direction, obj.Direction);
            //Debug.Print($"GBMethod::SegDistanceToSeg check angle {angle}");
            if (Math.Abs(angle - 90) > 89)
            {
                double d1 = PtDistanceToSeg(start, obj, out gbXYZ plummet1, out double t1);
                double d2 = PtDistanceToSeg(end, obj, out gbXYZ plummet2, out double t2);
                if (t1 > t2)
                    Util.Swap(ref t1, ref t2);
                if (t2 < 0 || t1 > 1)
                    overlap = 0;
                else if (t1 < 0 && t2 > 1)
                    overlap = 1;
                else if (t1 < 0)
                    overlap = 0 - t1;
                else if (t2 > 1)
                    overlap = 1 - t2;
                else
                    overlap = t2 - t1;
                proj = new gbSeg(plummet1, plummet2);
                return d1 <= d2 ? d1 : d2;
            }
            else
            {
                proj = null;
                overlap = 0;
                return double.PositiveInfinity;
            }
                
        }

        /// <summary>
        /// Return the projected segment of the subject segment (Entire projection regardless of overlapping).
        /// Output the mean distance of the two segments.
        /// </summary>
        public static gbSeg SegProjection(gbSeg a, gbSeg b, bool isProjectOnLine, out double distance)
        {
            double paramA, paramB;
            gbXYZ p1, p2;
            double d1 = PtDistanceToSeg(a.PointAt(0), b, out p1, out paramA);
            double d2 = PtDistanceToSeg(a.PointAt(1), b, out p2, out paramB);
            distance = (d1 + d2) / 2;

            if (isProjectOnLine)
                return new gbSeg(p1, p2);

            if (paramA < paramB)
            {
                Util.Swap(ref paramA, ref paramB);
                Util.Swap(ref p1, ref p2);
            }
            gbXYZ startPt;
            gbXYZ endPt;
            if (paramB < 0)
                startPt = FlattenPt(b.PointAt(0));
            else if (paramB > 1)
                startPt = FlattenPt(b.PointAt(1));
            else
                startPt = p2;
            if (paramA < 0)
                endPt = FlattenPt(b.PointAt(0));
            else if (paramA > 1)
                endPt = FlattenPt(b.PointAt(1));
            else
                endPt = p1;

            return new gbSeg(startPt, endPt);
        }

        /*public static gbSeg SegMerge(gbSeg a, gbSeg b, double tol)
        {
            gbXYZ p1 = a.PointAt(0);
            gbXYZ p2 = a.PointAt(1);
            gbXYZ p3 = b.PointAt(0);
            gbXYZ p4 = b.PointAt(1);

            // represents stretch vector of seg1 vec1 = (dx12, dy12)
            double dx12 = p2.X - p1.X;
            double dy12 = p2.Y - p1.Y;
            // represents stretch vector of seg2 vec2 = (dx34, dy34)
            double dx34 = p4.X - p3.X;
            double dy34 = p4.Y - p3.Y;

            // checker as cross product of vec1 and vec2
            double denominator = dy12 * dx34 - dx12 * dy34;
            // co-line checker as cross product of (p3 - p1) and vec1/vec
            double stretch = (p3.X - p1.X) * dy12 + (p1.Y - p3.Y) * dx12;

            if (denominator == 0 && stretch != 0)
                return new gbSeg();
            if (denominator == 0 && stretch == 0)
            {
                // express endpoints of seg2 in terms of seg1 parameter
                double s1 = ((p3.X - p1.X) * dx12 + (p3.Y - p1.Y) * dy12) / (dx12 * dx12 + dy12 * dy12);
                double s2 = s1 + (dx12 * dx34 + dy12 * dy34) / (dx12 * dx12 + dy12 * dy12);
                if (s1 > s2)
                {
                    Util.Swap(ref s1, ref s2);
                    Util.Swap(ref p3, ref p4);
                }
                if ((s1 > 0 && s1 < 1) || (s2 > 0 && s2 < 1))
                    if ((s1 > 0 && s1 < 1) && (s2 > 0 && s2 < 1))
                        return a.Copy();
                    else if (s1 > 0 && s1 < 1)
                        return new gbSeg(p1, p4);
                    else
                        return new gbSeg(p3, p2);
                if (s1 < 0 && s2 > 1)
                    return b.Copy();
                if (s1 >= 1)
                    if (s1 < 1 + tol)
                        return new gbSeg(p1, p4);
                    else
                        return new gbSeg();
                if (s2 <= 0)
                    if (s2 < 0 - tol)
                        return new gbSeg(p3, p2);
                    else
                        return new gbSeg();
            }
            return new gbSeg();
        }*/

        /// <summary>
        /// Create a expansion box by line segment offset
        /// </summary>
        public static List<gbXYZ> SegExpansionBox(gbXYZ p1, gbXYZ p2, double expansion)
        {
            List<gbXYZ> loop = new List<gbXYZ>();
            gbXYZ vec1;
            if (p1 == p2)
                vec1 = new gbXYZ(1, 0, 0);
            else
                vec1 = p2 - p1;
            vec1.Unitize();
            // no matter if true or false (clockwise or counter-clockwise)
            gbXYZ vec2 = GetPendicularVec(vec1, true);

            loop.Add(p2 + expansion * (vec1 + vec2));
            loop.Add(p2 + expansion * (vec1 - vec2));
            loop.Add(p1 + expansion * (-vec1 - vec2));
            loop.Add(p1 + expansion * (-vec1 + vec2));
            loop.Add(loop[0]);
            return loop;
        }
        public static List<gbXYZ> SegExpansionBox(gbSeg a, double expansion)
        {
            gbXYZ p1 = a.PointAt(0);
            gbXYZ p2 = a.PointAt(1);
            return SegExpansionBox(p1, p2, expansion);
        }

        /// <summary>
        /// Extend line segment at two ends by a specific length
        /// </summary>
        public static gbSeg SegExtensionByLength(gbSeg seg, double length)
        {
            double ratio = length / seg.Length;
            gbXYZ startPt = seg.PointAt(-ratio);
            gbXYZ endPt = seg.PointAt(1 + ratio);
            return new gbSeg(startPt, endPt);
        }
        public static List<gbSeg> SegsExtensionByLength(List<gbSeg> segs, double length)
        {
            List<gbSeg> extSegs = new List<gbSeg>();
            foreach (gbSeg seg in segs)
                extSegs.Add(SegExtensionByLength(seg, length));
            return extSegs;
        }


        /// <summary>
        /// Cluster a bunch of segments by checking their intersection relationship 
        /// </summary>
        public static List<List<gbSeg>> SegClusterByFuzzyIntersection(List<gbSeg> lines, double tolerance)
        {
            List<gbSeg> linePool = new List<gbSeg>();
            foreach (gbSeg line in lines)
            {
                linePool.Add(line);
            }
            List<List<gbSeg>> lineGroups = new List<List<gbSeg>>();
            while (linePool.Count > 0)
            {
                List<gbSeg> lineGroup = new List<gbSeg>() { linePool[0] };
                //Rhino.RhinoApp.Write("Initializing... "); displayCurve(crvPool[0]);
                linePool.RemoveAt(0);
                for (int i = 0; i < lineGroup.Count; i++)
                {
                    //Rhino.RhinoApp.WriteLine("Iteration... " + i.ToString());
                    //if (i >= crvGroup.Count - 1) { break; }
                    for (int j = linePool.Count - 1; j >= 0; j--)
                    {
                        if (lineGroup[i] != linePool[j])
                        {
                            if (IsSegFuzzyIntersected(lineGroup[i], linePool[j], tolerance))
                            {
                                lineGroup.Add(linePool[j]);
                                linePool.RemoveAt(j);
                            }
                        }
                    }
                }
                lineGroups.Add(lineGroup);
            }
            return lineGroups;
        }
        /// <summary>
        /// Check if two segments are intersected by their expanded box.
        /// </summary>
        public static bool IsSegFuzzyIntersected(gbSeg a, gbSeg b, double tolerance)
        {
            gbXYZ intersection; double t1, t2;
            if (SegIntersection(a, b, _eps, out intersection, out t1, out t2) == segIntersectEnum.IntersectOnBoth)
                return true;
            List<gbXYZ> expansionBox = SegExpansionBox(b, tolerance);
            for (int i = 0; i < expansionBox.Count - 1; i++)
                if (SegIntersection(a.PointAt(0), a.PointAt(1), 
                    expansionBox[i], expansionBox[i + 1], _eps, out intersection, out t1, out t2)
                    == segIntersectEnum.IntersectOnBoth)
                    return true;
            if (IsPtInPoly(a.PointAt(0), expansionBox, true)
                || IsPtInPoly(a.PointAt(1), expansionBox, true))
            {
                //Debug.Print($"GBMethod:: Containment intersection {a} in {b}");
                return true;
            }
                
            return false;
        }

        /// <summary>
        /// Shatter a list of segments by their intersection relationship
        /// </summary>
        public static List<gbSeg> ShatterSegs(List<gbSeg> crvs)
        {
            List<gbSeg> shatteredCrvs = new List<gbSeg>();

            for (int i = 0; i <= crvs.Count - 1; i++)
            {
                List<double> breakParams = new List<double>();
                for (int j = 0; j <= crvs.Count - 1; j++)
                {
                    if (i != j)
                        if (SegIntersection(crvs[i], crvs[j], 0.000001, 
                            out gbXYZ intersection, out double t1, out double t2) == segIntersectEnum.IntersectOnBoth)
                            breakParams.Add(t1);
                        else
                            continue;
                }
                shatteredCrvs.AddRange(crvs[i].Split(breakParams));
            }
            return shatteredCrvs;
        }
        
        /// <summary>
        /// Remove segments whose length is below the tolerance
        /// </summary>
        public static List<gbSeg> SkimOut(List<gbSeg> crvs, double tolerance)
        {
            for (int i = crvs.Count - 1; i >= 0; i--)
            {
                if (crvs[i].Length < tolerance)
                    crvs.RemoveAt(i);
            }
            return crvs;
        }
        /// <summary>
        /// Subtract a bunch of segments by a line segment. Debris whose length is 
        /// smaller than the tolerance will be erased.
        /// </summary>
        public static List<gbSeg> EtchSegs(List<gbSeg> segs, gbSeg clip, double tol)
        {
            List<gbSeg> debris = new List<gbSeg>();
            foreach (gbSeg seg in segs)
                debris.Add(seg);

            int counter = 0;

            while (counter < debris.Count)
            {
                gbSeg seg = debris[counter];
                if (seg.Length < tol)
                {
                    debris.RemoveAt(counter);
                    continue;
                }
                double tolRatio = tol / seg.Length;

                double d1 = PtDistanceToSeg(clip.Start, seg, out gbXYZ p1, out double s1);
                double d2 = PtDistanceToSeg(clip.End, seg, out gbXYZ p2, out double s2);
                double mod = clip.Direction.CrossProduct(seg.Direction).Norm();
                // PENDING
                if (d1 < 100 * _eps && d2 < 100 * _eps && Math.Abs(mod) < _eps)
                {
                    if (Math.Abs(s1 - s2) < _eps)
                    {
                        counter++;
                        continue;
                    }
                    if (s1 > s2)
                    {
                        Util.Swap(ref s1, ref s2);
                        Util.Swap(ref p1, ref p2);
                    }

                    if (Math.Abs(s1) < _eps) s1 = 0;
                    if (Math.Abs(s2) < _eps) s2 = 0;
                    if (Math.Abs(s1 - 1) < _eps) s1 = 1;
                    if (Math.Abs(s2 - 1) < _eps) s2 = 1;

                    if (s1 <= 0 && s2 >= 1)
                    {
                        debris.RemoveAt(counter);
                    }
                    else if (s1 < 0)
                    {
                        if (s2 <= 0)
                            counter++;
                        else
                        {
                            debris.Add(new gbSeg(p2, seg.End));
                            debris.RemoveAt(counter);
                        }

                    }
                    else if (s2 > 1)
                    {
                        if (s1 >= 1)
                            counter++;
                        else
                        {
                            debris.Add(new gbSeg(seg.Start, p1));
                            debris.RemoveAt(counter);
                        }
                    }
                    else
                    {
                        debris.Add(new gbSeg(seg.Start, p1));
                        debris.Add(new gbSeg(p2, seg.End));
                        debris.RemoveAt(counter);
                    }
                }
                else
                {
                    counter++;
                }
            }

            return debris;
        }

#endregion seg operations

#region poly relations

        /// <summary>
        /// Check if the 2D polygon is clockwise (z coordinate is omitted)
        /// </summary>
        public static bool IsClockwise(List<gbXYZ> pts)
        {
            var count = pts.Count;

            double area0 = 0;
            double area1 = 0;
            for (int i = 0; i < count; i++)
            {
                var x = pts[i].X;
                var y = i + 1 < count ? pts[i + 1].Y : pts[0].Y;
                area0 += x * y;

                var a = pts[i].Y;
                var b = i + 1 < count ? pts[i + 1].X : pts[0].X;
                area1 += a * b;
            }
            double ans = area0 - area1;
            if (ans < 0) return true;
            return false;
        }

        /// <summary>
        /// Check the convexity of a polygon. False as to concave.
        /// </summary>
        public static bool IsConvex(List<gbXYZ> pts)
        {
            List<gbXYZ> _pts = new List<gbXYZ>();
            for (int i = 0; i < pts.Count; i++)
            {
                int nextId = i + 1 < pts.Count ? i + 1 : 0;
                if (pts[i].DistanceTo(pts[nextId]) > 0.000001)
                    _pts.Add(pts[i]);
            }
            if (IsClockwise(_pts))
                _pts.Reverse();
            for (int i = 0; i < _pts.Count; i++)
            {
                int nextId = i + 1 < _pts.Count ? i + 1 : 0;
                int prevId = i - 1 < 0 ? _pts.Count - 1 : i - 1;
                if (VectorAngle(_pts[i] - _pts[prevId], _pts[i] - _pts[nextId]) > 180)
                {
                    Debug.Print("GBMethod:: a concave surface has been detected");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Point on the edge of a poly returns true. The poly includes the boundary
        /// </summary>
        public static bool IsPtInPoly(gbXYZ pt, List<gbXYZ> poly, bool includeOn)
        {
            int GetQuadrant(gbXYZ v, gbXYZ _pt)
            {
                return (v.X > _pt.X) ? ((v.Y > _pt.Y) ? 0 : 3) : ((v.Y > _pt.Y) ? 1 : 2);
            }

            double X_intercept(gbXYZ pt1, gbXYZ pt2, double y)
            {
                return pt2.X - ((pt2.Y - y) * ((pt1.X - pt2.X) / (pt1.Y - pt2.Y)));
            }

            void AdjustDelta(ref int _delta, gbXYZ v, gbXYZ next_v, gbXYZ _pt)
            {
                switch (_delta)
                {
                    case 3: _delta = -1; break;
                    case -3: _delta = 1; break;
                    case 2:
                    case -2:
                        if (X_intercept(v, next_v, _pt.Y) > _pt.X)
                            _delta = -_delta;
                        break;
                }
            }

            int quad = GetQuadrant(poly[0], pt);
            int angle = 0;
            int onEdgeCounter = 0;
            int next_quad, delta;
            for (int i = 0; i < poly.Count; i++)
            {
                gbXYZ v = poly[i];
                gbXYZ next_v = poly[(i + 1 < poly.Count) ? i + 1 : 0];
                next_quad = GetQuadrant(next_v, pt);
                delta = next_quad - quad;

                AdjustDelta(ref delta, v, next_v, pt);
                angle = angle + delta;
                quad = next_quad;

                // this is wrong when dealing with vertical edge, which will return 0 all the time
                //if (Math.Abs(X_intercept(v, next_v, pt.Y) - pt.X) < _eps)

                // more efficient methods are needed, do not be afraid of duplication
                double distance = PtDistanceToSeg(pt, new gbSeg(v, next_v), out gbXYZ plummet, out double stretch);
                if (distance < _eps && stretch >= 0 && stretch <= 1)
                    onEdgeCounter++;
            }
            if (includeOn)
                return onEdgeCounter > 0 || (angle == 4) || (angle == -4);
            else if (onEdgeCounter > 0)
                return false;
            else
                return (angle == 4) || (angle == -4);
        }
        public static bool IsPtOnPoly(gbXYZ pt, List<gbXYZ> poly)
        {
            for (int i = 0; i < poly.Count - 1; i++)
                if (Math.Abs(PtDistanceToSeg(pt, new gbSeg(poly[i], poly[i + 1]), out gbXYZ plummet, out double stretch)) < 0.000001)
                    return true;
            return false;
        }
        public static bool IsSegInPoly(gbSeg seg, List<gbXYZ> poly)
        {
            gbXYZ start = seg.Start;
            gbXYZ end = seg.End;
            //for (int i = 0; i < poly.Count - 1; i++)
            //{
            //    segIntersectEnum result = SegIntersection(seg, new gbSeg(poly[i], poly[i + 1]), 0.000001,
            //        out gbXYZ intersection, out double t1, out double t2);
            //    if ()
            //}
            //if (IsOn)
            //{
                if (IsPtInPoly(start, poly, true) && IsPtInPoly(end, poly, true))
                //!IsSegPolyIntersected(seg, poly, 0.000001))
                    return true;
                else
                    return false;
            //}
            //else
            //{
            //    if (IsPtInPoly(start, poly) && IsPtInPoly(end, poly) &&
            //        !(IsPtOnPoly(start, poly) && IsPtOnPoly(end, poly)) &&
            //        !IsSegPolyIntersected(seg, poly, 0.000001))
            //        return true;
            //    else
            //        return false;
            //}
        }

        /// <summary>
        /// Check if a polygon is totally inside another one.
        /// The method needs update.
        /// </summary>
        public static bool IsPolyInPoly(List<gbXYZ> polyA, List<gbXYZ> polyB)
        {
            // if any vertex of polygon A outside polygon B, deny it
            foreach (gbXYZ pt in polyA)
                if (!IsPtInPoly(pt, polyB, false))
                    return false;
            // if any edge of polygon A intersects with polygon B, deny it
            for (int i = 0; i < polyA.Count - 1; i++)
            {
                gbSeg edgeA = new gbSeg(polyA[i], polyA[i + 1]);
                for (int j = 0; j < polyB.Count - 1; j++)
                {
                    gbSeg edgeB = new gbSeg(polyB[j], polyB[j + 1]);
                    segIntersectEnum intersectEnum = SegIntersection(edgeA, edgeB, _eps, 
                        out gbXYZ sectPt, out double t1, out double t2);
                    if (intersectEnum == segIntersectEnum.IntersectOnBoth)
                        return false;
                }
            }
            return true;
        }

        public static bool IsPolyOutPoly(List<gbXYZ> polyA, List<gbXYZ> polyB)
        {
            // if any vertex of polygon A inside polygon B, deny it
            foreach (gbXYZ pt in polyA)
                if (IsPtInPoly(pt, polyB, false))
                    return false;
            // if any edge of polygon A intersects with polygon B, deny it
            for (int i = 0; i < polyA.Count - 1; i++)
            {
                gbSeg edgeA = new gbSeg(polyA[i], polyA[i + 1]);
                for (int j = 0; j < polyB.Count - 1; j++)
                {
                    gbSeg edgeB = new gbSeg(polyB[j], polyB[j + 1]);
                    segIntersectEnum intersectEnum = SegIntersection(edgeA, edgeB, _eps,
                        out gbXYZ sectPt, out double t1, out double t2);
                    if (intersectEnum == segIntersectEnum.IntersectOnBoth)
                        return false;
                }
            }
            return true;
        }

        // 
        /// <summary>
        /// Should this method include situation that two polys are adjacent on edge?
        /// </summary>
        public static bool IsPolyOverlap(List<gbXYZ> polyA, List<gbXYZ> polyB, bool includeOn)
        {
            int boolCounter = 0;
            foreach (gbXYZ pt in polyA)
                if (IsPtInPoly(pt, polyB, includeOn))
                    boolCounter++;
            foreach (gbXYZ pt in polyB)
                if (IsPtInPoly(pt, polyA, includeOn))
                    boolCounter++;
            if (boolCounter == 0)
                return false;
            else
                return true;
        }
        public static bool IsPolyIntersect(List<gbXYZ> polyA, List<gbXYZ> polyB)
        {
            int boolCounterA = 0;
            int boolCounterB = 0;
            foreach (gbXYZ pt in polyA)
                if (IsPtInPoly(pt, polyB, false))
                    boolCounterA++;
            foreach (gbXYZ pt in polyB)
                if (IsPtInPoly(pt, polyA, false))
                    boolCounterB++;
            if ((boolCounterA > 0 && boolCounterA < polyA.Count) || (boolCounterB > 0 && boolCounterB < polyB.Count))
                return true;
            else
                return false;
        }
        public static List<List<List<gbXYZ>>> PolyClusterByOverlap(List<List<gbXYZ>> loops, bool includeOn)
        {
            List<List<gbXYZ>> loopPool = new List<List<gbXYZ>>();
            foreach (List<gbXYZ> loop in loops)
            {
                loopPool.Add(loop);
            }
            List<List<List<gbXYZ>>> loopGroups = new List<List<List<gbXYZ>>>();
            while (loopPool.Count > 0)
            {
                List<List<gbXYZ>> loopGroup = new List<List<gbXYZ>>() { loopPool[0] };
                loopPool.RemoveAt(0);
                for (int i = 0; i < loopGroup.Count; i++)
                {
                    for (int j = loopPool.Count - 1; j >= 0; j--)
                    {
                        if (loopGroup[i] != loopPool[j])
                        {
                            if (IsPolyOverlap(loopGroup[i], loopPool[j], includeOn))
                            {
                                loopGroup.Add(loopPool[j]);
                                loopPool.RemoveAt(j);
                            }
                        }
                    }
                }
                loopGroups.Add(loopGroup);
            }
            return loopGroups;
        }

#endregion poly relations

#region poly operations

        /// <summary>
        /// Get the copy of the reversed polygon vertices loop.
        /// </summary>
        static public List<gbXYZ> GetReversedPoly(List<gbXYZ> loop)
        {
            List<gbXYZ> revLoop = new List<gbXYZ>();
            foreach (gbXYZ vertex in loop)
                revLoop.Add(vertex);
            revLoop.Reverse();
            return revLoop;
        }

        static public List<gbXYZ> GetDuplicatePoly(List<gbXYZ> loop)
        {
            List<gbXYZ> dupLoop = new List<gbXYZ>();
            foreach (gbXYZ vertex in loop)
                dupLoop.Add(vertex);
            return dupLoop;
        }

        static public List<gbXYZ> GetPolyLastPointRemoved(List<gbXYZ> loop)
        {
            List<gbXYZ> openLoop = new List<gbXYZ>();
            for (int i = 0; i < loop.Count - 1; i++)
                openLoop.Add(loop[i]);
            return openLoop;
        }
        static public List<gbXYZ> GetOpenPolyLoop(List<gbXYZ> loop)
        {
            List<gbXYZ> openLoop = new List<gbXYZ>() { loop[0] };
            for (int i = 1; i < loop.Count; i++)
            {
                if (loop[i].DistanceTo(loop[0]) > _eps)
                    openLoop.Add(loop[i]);
                else
                    break;
            }
            return openLoop;
        }

        /// <summary>
        /// Get the normal of a polygon by Left-hand order (normalized)
        /// </summary>
        public static gbXYZ GetPolyNormal(List<gbXYZ> pts)
        {
            /*
            double x1 = pts[1].X - pts[0].X;
            double y1 = pts[1].Y - pts[0].Y;
            double z1 = pts[1].Z - pts[0].Z;
            double x2 = pts[2].X - pts[0].X;
            double y2 = pts[2].Y - pts[0].Y;
            double z2 = pts[2].Z - pts[0].Z;

            double x = y2 * z1 - y1 * z2;
            double y = x2 * z1 - x1 * z2;
            double z = x2 * y1 - x1 * y2;
            double norm = Math.Sqrt(x * x + y * y + z * z);

            return new gbXYZ(x / norm, y / norm, z / norm);
            */
            gbXYZ vec1 = pts[1] - pts[0];
            gbXYZ vec2 = pts[2] - pts[0];
            gbXYZ norm = vec1.CrossProduct(vec2);
            norm.Unitize();

            return norm;
        }
        /// <summary>
        /// Only works for vertical walls (tilt == 90)
        /// </summary>
        public static List<gbXYZ> PolyToUV(List<gbXYZ> pts)
        {
            gbXYZ normal = GetPolyNormal(pts);
            gbXYZ u = GetPendicularVec(normal, true);
            gbXYZ v = new gbXYZ(0, 0, 1);
            List<gbXYZ> pts2D = new List<gbXYZ>();
            foreach (gbXYZ pt in pts)
            {
                gbXYZ vec = new gbXYZ(pt.X, pt.Y, pt.Z);
                pts2D.Add(new gbXYZ(vec.DotProduct(u), vec.DotProduct(v), 0));
            }
            return pts2D;
        }

        /// <summary>
        /// Get the area of a simple polygon by the X, Y coordinates of vertices. This is the 
        /// actually the z-plane projection of the original polygon.
        /// </summary>
        public static double GetPolyArea(List<gbXYZ> pts)
        {
            var count = pts.Count;

            double area0 = 0;
            double area1 = 0;
            for (int i = 0; i < count; i++)
            {
                var x = pts[i].X;
                var y = i + 1 < count ? pts[i + 1].Y : pts[0].Y;
                area0 += x * y;

                var a = pts[i].Y;
                var b = i + 1 < count ? pts[i + 1].X : pts[0].X;
                area1 += a * b;
            }
            return Math.Abs(0.5 * (area0 - area1));
        }
        /// <summary>
        /// Get the area of multiple multiply connected regions.
        /// Following one simple rule, + CCW region and - CW region
        /// </summary>
        public static double GetPolysArea(List<List<gbXYZ>> polys)
        {
            double area = 0;
            foreach (List<gbXYZ> poly in polys)
            {
                if (IsClockwise(poly))
                    area -= GetPolyArea(poly);
                else
                    area += GetPolyArea(poly);
            }
            return Math.Abs(area);
        }

        /// <summary>
        /// by Jeremy Tammik. This is a degenerated method that has some cases not applicable
        /// </summary>
        public static double GetPolyArea3d(List<gbXYZ> polygon)
        {
            gbXYZ normal = new gbXYZ();
            double area = 0.0;
            int n = (null == polygon) ? 0 : polygon.Count;
            bool rc = (2 < n);
            if (3 == n)
            {
                gbXYZ a = polygon[0];
                gbXYZ b = polygon[1];
                gbXYZ c = polygon[2];
                gbXYZ v = b - a;
                normal = v.CrossProduct(c - a);
            }
            else if (4 == n)
            {
                gbXYZ a = polygon[0];
                gbXYZ b = polygon[1];
                gbXYZ c = polygon[2];
                gbXYZ d = polygon[3];

                normal.X = (c.Y - a.Y) * (d.Z - b.Z)
                  + (c.Z - a.Z) * (b.Y - d.Y);
                normal.Y = (c.Z - a.Z) * (d.X - b.X)
                  + (c.X - a.X) * (b.Z - d.Z);
                normal.Z = (c.X - a.X) * (d.Y - b.Y)
                  + (c.Y - a.Y) * (b.X - d.X);
            }
            else if (4 < n)
            {
                gbXYZ a;
                gbXYZ b = polygon[n - 2];
                gbXYZ c = polygon[n - 1];

                for (int i = 0; i < n; ++i)
                {
                    a = b;
                    b = c;
                    c = polygon[i];

                    normal.X += b.Y * (c.Z - a.Z);
                    normal.Y += b.Z * (c.X - a.X);
                    normal.Z += b.X * (c.Y - a.Y);
                }
            }
            if (rc)
            {
                double length = normal.Norm();
                if (rc)
                    area = 0.5 * length;
            }
            return area;
        }
        /// <summary>
        /// Return the centroid of a convex polygon.
        /// Please turn to the pole of accessibility when facing concave polygons.
        /// </summary>
        public static gbXYZ GetPolyCentroid(List<gbXYZ> poly)
        {
            double[] ans = new double[2];

            int n = poly.Count;
            double signedArea = 0;

            // For all vertices
            for (int i = 0; i < n; i++)
            {
                double x0 = poly[i].X;
                double y0 = poly[i].Y;
                double x1 = poly[(i + 1) % n].X;
                double y1 = poly[(i + 1) % n].Y;

                // Calculate value of A
                // using shoelace formula
                double A = (x0 * y1) - (x1 * y0);
                signedArea += A;

                // Calculating coordinates of
                // centroid of polygon
                ans[0] += (x0 + x1) * A;
                ans[1] += (y0 + y1) * A;
            }
            signedArea *= 0.5;
            gbXYZ centroid = new gbXYZ(ans[0] / (6 * signedArea),
              ans[1] / (6 * signedArea), 0);
            return centroid;
        }
        /// <summary>
        /// Return the perimeter length of a polygon represented by a list of points.
        /// </summary>
        public static double GetPolyPerimeter(List<gbXYZ> poly)
        {
            double length = 0;
            for (int i = 0; i < poly.Count - 1; i++)
            {
                length += poly[i].DistanceTo(poly[i + 1]);
            }
            length += poly[poly.Count - 1].DistanceTo(poly[0]);
            return length;
        }
        /// <summary>
        /// Return the edges of a closed polygon by a list of points.
        /// It does not matter if the list of points is closed. The function 
        /// will erase edge with zero length and close the polygon.
        /// </summary>
        public static List<gbSeg> GetClosedPoly(List<gbXYZ> pts)
        {
            List<gbSeg> boundary = new List<gbSeg>();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                gbSeg edge = new gbSeg(pts[i], pts[i + 1]);
                if (edge.Length > _eps)
                    boundary.Add(edge);
            }
            if (pts[pts.Count - 1].DistanceTo(pts[0]) > _eps)
                boundary.Add(new gbSeg(pts[pts.Count - 1], pts[0]));
            return boundary;
        }

        /// <summary>
        /// Reorder a polyloop to make it start with the bottom-left point. 
        /// This may be a prerequisite of EnergyPlus and gbXML schema.
        /// Note that this will return a new, ordered list.
        /// The default input/output is a closed, XY-plane loop.
        /// </summary>
        public static List<gbXYZ> ReorderPoly(List<gbXYZ> pts)
        {
            if (pts.Count <= 2)
                return pts;
            // check if the polygon is closed
            int numPts = pts.Count;
            if (pts[0].DistanceTo(pts[pts.Count - 1]) < _eps)
                numPts = numPts - 1;
            int idStart = 0;
            List<int> idsPending = new List<int>();
            double min = double.PositiveInfinity;

            // ignore the last point
            for (int i = 0; i < numPts; i++)
                if (pts[i].Y < min)
                    min = pts[i].Y;
            for (int i = 0; i < numPts; i++)
                if (Math.Abs(pts[i].Y - min) < _eps)
                    idsPending.Add(i);
            if (idsPending.Count == 1)
                idStart = idsPending[0];
            else
            {
                min = double.PositiveInfinity;
                for (int j = 0; j < idsPending.Count; j++)
                    if (pts[idsPending[j]].X < min)
                    {
                        min = pts[idsPending[j]].X;
                        idStart = idsPending[j];
                    }
            }
            List<gbXYZ> ptsSorted = new List<gbXYZ>();
            for (int i = idStart; i < numPts; i++)
            {
                ptsSorted.Add(pts[i]);
            }
            for (int i = 0; i < idStart; i++)
            {
                ptsSorted.Add(pts[i]);
            }
            ptsSorted.Add(ptsSorted[0]);
            return ptsSorted;
        }

        /// <summary>
        /// Degenerated version. Please refer to Clipper for more robust and complex functions.
        /// </summary>
        public static List<gbXYZ> PolyOffset(List<gbXYZ> pts, double offset, bool isInward)
        {
            List<gbXYZ> offsetPts = new List<gbXYZ>();
            for (int i = 0; i < pts.Count; i++)
            {
                gbXYZ vec1, vec2;
                if (i == 0)
                    vec1 = pts[0] - pts[pts.Count - 1];
                else
                    vec1 = pts[i] - pts[i - 1];
                if (i == pts.Count - 1)
                    vec2 = pts[pts.Count - 1] - pts[0];
                else
                    vec2 = pts[i] - pts[i + 1];
                vec1.Unitize();
                vec2.Unitize();
                gbXYZ direction = vec1 + vec2;
                if (isInward)
                    offsetPts.Add(pts[i] - offset * direction);
                else
                    offsetPts.Add(pts[i] + offset * direction);
            }
            return offsetPts;
        }


        /// <summary>
        /// Do polygon boolean operation. Algorithm depends on Clipper.cs
        /// </summary>
        public static List<List<gbXYZ>> ClipPoly(List<gbXYZ> subjLoop, List<gbXYZ> clipLoop, ClipType operation)
        {
            //double zbase = subjLoop[0].Z;

            IntPoint PtToIntPt(gbXYZ pt)
            {
                //Util.LogPrint("PRECISION: " + Math.Round(pt.X * 10000000) + " / " + Math.Round(pt.Y * 10000000));
                return new IntPoint(Math.Round(pt.X * 10000000), Math.Round(pt.Y * 10000000));
            }
            gbXYZ IntPtToPt(IntPoint pt)
            {
                //Util.LogPrint("PRECISION: " + (pt.X * 0.0000001).ToString() + " / " + (pt.Y * 0.0000001).ToString());
                return new gbXYZ(pt.X * 0.0000001, pt.Y * 0.0000001, 0);
            }

            List<IntPoint> subj = new List<IntPoint>();
            List<IntPoint> clip = new List<IntPoint>();
            foreach (gbXYZ pt in subjLoop)
                subj.Add(PtToIntPt(pt));
            foreach (gbXYZ pt in clipLoop)
                clip.Add(PtToIntPt(pt));

            List<List<IntPoint>> solutions = new List<List<IntPoint>>();
            Clipper c = new Clipper();
            c.AddPath(subj, PolyType.ptSubject, true);
            c.AddPath(clip, PolyType.ptClip, true);
            c.Execute(operation, solutions);

            List<List<gbXYZ>> sectLoops = new List<List<gbXYZ>>();
            foreach (List<IntPoint> solution in solutions)
            {
                List<gbXYZ> sectLoop = new List<gbXYZ>();
                foreach (IntPoint pt in solution)
                    sectLoop.Add(IntPtToPt(pt));
                sectLoops.Add(sectLoop);
            }
            return sectLoops;
        }
        public static List<List<gbXYZ>> ClipPoly(List<List<gbXYZ>> subjLoops, List<gbXYZ> clipLoop, ClipType operation)
        {
            //double zbase = subjLoop[0].Z;

            IntPoint PtToIntPt(gbXYZ pt)
            {
                //Util.LogPrint("PRECISION: " + Math.Round(pt.X * 10000000) + " / " + Math.Round(pt.Y * 10000000));
                return new IntPoint(Math.Round(pt.X * 10000000), Math.Round(pt.Y * 10000000));
            }
            gbXYZ IntPtToPt(IntPoint pt)
            {
                //Util.LogPrint("PRECISION: " + (pt.X * 0.0000001).ToString() + " / " + (pt.Y * 0.0000001).ToString());
                return new gbXYZ(pt.X * 0.0000001, pt.Y * 0.0000001, 0);
            }

            List<List<IntPoint>> subj = new List<List<IntPoint>>();
            List<IntPoint> clip = new List<IntPoint>();
            foreach (List<gbXYZ> subjLoop in subjLoops)
            {
                List<IntPoint> _subj = new List<IntPoint>();
                foreach (gbXYZ pt in subjLoop)
                    _subj.Add(PtToIntPt(pt));
                subj.Add(_subj);
            }
            foreach (gbXYZ pt in clipLoop)
                clip.Add(PtToIntPt(pt));

            List<List<IntPoint>> solutions = new List<List<IntPoint>>();
            Clipper c = new Clipper();
            c.AddPaths(subj, PolyType.ptSubject, true);
            c.AddPath(clip, PolyType.ptClip, true);
            c.Execute(operation, solutions);

            List<List<gbXYZ>> sectLoops = new List<List<gbXYZ>>();
            foreach (List<IntPoint> solution in solutions)
            {
                List<gbXYZ> sectLoop = new List<gbXYZ>();
                foreach (IntPoint pt in solution)
                    sectLoop.Add(IntPtToPt(pt));
                sectLoops.Add(sectLoop);
            }
            return sectLoops;
        }

        public static List<List<gbXYZ>> ClipPoly(List<List<gbXYZ>> subjLoops, List<List<gbXYZ>> clipLoops, ClipType operation)
        {
            //double zbase = subjLoop[0].Z;

            IntPoint PtToIntPt(gbXYZ pt)
            {
                //Util.LogPrint("PRECISION: " + Math.Round(pt.X * 10000000) + " / " + Math.Round(pt.Y * 10000000));
                return new IntPoint(Math.Round(pt.X * 10000000), Math.Round(pt.Y * 10000000));
            }
            gbXYZ IntPtToPt(IntPoint pt)
            {
                //Util.LogPrint("PRECISION: " + (pt.X * 0.0000001).ToString() + " / " + (pt.Y * 0.0000001).ToString());
                return new gbXYZ(pt.X * 0.0000001, pt.Y * 0.0000001, 0);
            }

            List<List<IntPoint>> subj = new List<List<IntPoint>>();
            List<List<IntPoint>> clip = new List<List<IntPoint>>();
            foreach (List<gbXYZ> subjLoop in subjLoops)
            {
                List<IntPoint> _subj = new List<IntPoint>();
                foreach (gbXYZ pt in subjLoop)
                    _subj.Add(PtToIntPt(pt));
                subj.Add(_subj);
            }
            foreach (List<gbXYZ> clipLoop in clipLoops)
            {
                List<IntPoint> _clip = new List<IntPoint>();
                foreach (gbXYZ pt in clipLoop)
                    _clip.Add(PtToIntPt(pt));
                clip.Add(_clip);
            }

            List<List<IntPoint>> solutions = new List<List<IntPoint>>();
            Clipper c = new Clipper();
            c.AddPaths(subj, PolyType.ptSubject, true);
            c.AddPaths(clip, PolyType.ptClip, true);
            c.Execute(operation, solutions);

            List<List<gbXYZ>> sectLoops = new List<List<gbXYZ>>();
            foreach (List<IntPoint> solution in solutions)
            {
                List<gbXYZ> sectLoop = new List<gbXYZ>();
                foreach (IntPoint pt in solution)
                    sectLoop.Add(IntPtToPt(pt));
                sectLoops.Add(sectLoop);
            }
            return sectLoops;
        }

        /// <summary>
        /// Note that this function returns polygon not enclosed.
        /// </summary>
        public static List<List<gbXYZ>> OffsetPoly(List<gbXYZ> poly, double offset)
        {
            IntPoint PtToIntPt(gbXYZ pt)
            {
                return new IntPoint(Math.Round(pt.X * 10000000), Math.Round(pt.Y * 10000000));
            }
            gbXYZ IntPtToPt(IntPoint pt)
            {
                return new gbXYZ(pt.X * 0.0000001, pt.Y * 0.0000001, 0);
            }

            List<IntPoint> path = new List<IntPoint>();
            foreach (gbXYZ pt in poly)
                path.Add(PtToIntPt(pt));

            List<List<IntPoint>> solutions = new List<List<IntPoint>>();
            ClipperOffset co = new ClipperOffset();
            co.AddPath(path, JoinType.jtMiter, EndType.etClosedPolygon);
            co.Execute(ref solutions, Math.Round(offset * 10000000));

            List<List<gbXYZ>> offsetLoops = new List<List<gbXYZ>>();
            foreach (List<IntPoint> solution in solutions)
            {
                List<gbXYZ> offsetLoop = new List<gbXYZ>();
                foreach (IntPoint pt in solution)
                    offsetLoop.Add(IntPtToPt(pt));
                offsetLoops.Add(offsetLoop);
            }
            return offsetLoops;
        }

#endregion poly operations
    }
}