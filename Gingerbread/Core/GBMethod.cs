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
        Parallel
    }
    // Seg - Segment, not line
    // Poly - Polyline, including polygon, represented by a loop of points
    public class GBMethod
    {
        public static gbXYZ GetPendicularVec(gbXYZ vec, bool isClockwise)
        {
            if (isClockwise)
                return new gbXYZ(vec.Y, -vec.X, 0);
            return new gbXYZ(-vec.Y, vec.X, 0);
        }

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

        // for pre-process of the wall centerlines
        // futher this function will be used to cast XYZ class to gbPoint class
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


        public static List<gbSeg> PtsLoopToPoly(List<gbXYZ> pts)
        {
            List<gbSeg> segs = new List<gbSeg>();
            for (int i = 0; i < pts.Count - 1; i++)
                segs.Add(new gbSeg(pts[i], pts[i + 1]));
            if (!pts[0].Equals(pts.Last()))
                segs.Add(new gbSeg(pts.Last(), pts[0]));
            return segs;
        }

        public static List<gbXYZ> ElevatePtsLoop(List<gbXYZ> pts, double elevation)
        {
            List<gbXYZ> elevatedPts = new List<gbXYZ>();
            for (int i = 0; i < pts.Count; i++)
                elevatedPts.Add(pts[i] + new gbXYZ(0, 0, elevation));
            return elevatedPts;
        }
        public static gbXYZ FlattenPt(gbXYZ pt)
        {
            return new gbXYZ(pt.X, pt.Y, 0);
        }

        public static gbXYZ RelativePt(gbXYZ pt, gbXYZ origin)
        {
            return new gbXYZ(
                Math.Abs(pt.X - origin.X),
                Math.Abs(pt.Y - origin.Y),
                Math.Abs(pt.Z - origin.Z));
        }


        // futher there will be gbLine method to modify line endpoints directly
        // for now, just regenerate one. always the longer one when trimming.
        public static gbSeg SegExtension(gbSeg a, gbSeg b, double tolerance)
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
        // borrowed from Jeremy Tammik
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
        /// Return the intersectEnum, output the intersection point and the ratio if the point falls on the first segment.
        /// </summary>
        public static segIntersectEnum SegIntersection(gbXYZ p1, gbXYZ p2, gbXYZ p3, gbXYZ p4, double tol, 
            out gbXYZ intersection, out double fractile)
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
            // co-line checker as cross product of (p3 - p1) and vec1/vec
            double stretch = (p3.X - p1.X) * dy12 + (p1.Y - p3.Y) * dx12;
            fractile = 0;

            if (denominator == 0 && stretch != 0)
                return segIntersectEnum.Parallel;
            if (denominator == 0 && stretch == 0)
            {
                // express endpoints of seg2 in terms of seg1 parameter
                double s1 = ((p3.X - p1.X) * dx12 + (p3.Y - p1.Y) * dy12) / (dx12 * dx12 + dy12 * dy12);
                double s2 = s1 + (dx12 * dx34 + dy12 * dy34) / (dx12 * dx12 + dy12 * dy12);
                double swap;
                if (s1 > s2)
                {
                    swap = s2;
                    s2 = s1;
                    s1 = swap;
                }
                if ((s1 > 0 && s1 < 1) || (s2 > 0 && s2 < 1))
                    if ((s1 > 0 && s1 < 1) && (s2 > 0 && s2 < 1))
                        return segIntersectEnum.ColineAContainB;
                    else
                        return segIntersectEnum.ColineOverlap;
                if (s1 < 0 && s2 > 1)
                    return segIntersectEnum.ColineBContainA;
                if (s1 > 1 || s2 < 0)
                    return segIntersectEnum.ColineDisjoint;
                if (s1 == 1)
                {
                    intersection = p2;
                    return segIntersectEnum.ColineJoint;
                }
                if (s2 == 0)
                {
                    intersection = p1;
                    return segIntersectEnum.ColineJoint;
                }
            }

            intersect = segIntersectEnum.IntersectOnLine;

            double t1 = ((p1.X - p3.X) * dy34 + (p3.Y - p1.Y) * dx34) / denominator;
            double t2 = ((p3.X - p1.X) * dy12 + (p1.Y - p3.Y) * dx12) / -denominator;
            fractile = t1;

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
            out gbXYZ intersection, out double fractile)
        {
            gbXYZ p1 = a.PointAt(0);
            gbXYZ p2 = a.PointAt(1);
            gbXYZ p3 = b.PointAt(0);
            gbXYZ p4 = b.PointAt(1);
            intersection = new gbXYZ();
            return SegIntersection(p1, p2, p3, p4, tol, out intersection, out fractile);
        }
        public static bool IsSegPolyIntersected(gbSeg a, List<gbXYZ> poly, double tol)
        {
            for (int i = 0; i < poly.Count - 1; i++)
            {
                gbSeg edge = new gbSeg(poly[i], poly[i + 1]);
                segIntersectEnum IntersectResult = SegIntersection(a, edge, tol, out gbXYZ intersection, out double fractile);
                if (IntersectResult == segIntersectEnum.IntersectOnBoth ||
                    IntersectResult == segIntersectEnum.ColineAContainB)
                    return true;
            }
            return false;
        }

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
            dx = pt.X - plummet.X;
            dy = pt.Y - plummet.Y;

            return Math.Sqrt(dx * dx + dy * dy);
        }

        public static gbSeg SegProjection(gbSeg a, gbSeg b, out double distance)
        {
            double paramA, paramB;
            gbXYZ p1, p2;
            double d1 = PtDistanceToSeg(a.PointAt(0), b, out p1, out paramA);
            double d2 = PtDistanceToSeg(a.PointAt(1), b, out p2, out paramB);
            distance = (d1 + d2) / 2;

            if (paramA < paramB)
            {
                Util.Swap<double>(ref paramA, ref paramB);
                Util.Swap<gbXYZ>(ref p1, ref p2);
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

        // switch to more robust polygon offset function in future
        // note that this is not a closed vertice loop
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
        public static bool IsSegFuzzyIntersected(gbSeg a, gbSeg b, double tolerance)
        {
            gbXYZ intersection; double fractile;
            if (SegIntersection(a, b, 0, out intersection, out fractile) == segIntersectEnum.IntersectOnBoth)
                return true;
            List<gbXYZ> expansionBox = SegExpansionBox(b, tolerance);
            for (int i = 0; i < expansionBox.Count - 1; i++)
                if (SegIntersection(a.PointAt(0), a.PointAt(1), 
                    expansionBox[i], expansionBox[i + 1], 0, out intersection, out fractile)
                    == segIntersectEnum.IntersectOnBoth)
                    return true;
            if (IsPtInPoly(a.PointAt(0), expansionBox)
                || IsPtInPoly(a.PointAt(1), expansionBox))
                return true;
            return false;
        }

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
                            out gbXYZ intersection, out double fractile) == segIntersectEnum.IntersectOnBoth)
                            breakParams.Add(fractile);
                        else
                            continue;
                }
                shatteredCrvs.AddRange(crvs[i].Split(breakParams));
            }
            return shatteredCrvs;
        }

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
        /// Algorithm to test if a point is inside a polygon. Borrowed from Jeremy Tammik
        /// </summary>
        public static bool IsPtInPoly(gbXYZ pt, List<gbXYZ> poly)
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
            }
            return (angle == 4) || (angle == -4);
        }
        public static bool IsPolyInPoly(List<gbXYZ> polyA, List<gbXYZ> polyB)
        {
            foreach (gbXYZ pt in polyA)
                if (!IsPtInPoly(pt, polyB))
                    return false;
            return true;
        }
        public static bool IsPolyOverlap(List<gbXYZ> polyA, List<gbXYZ> polyB)
        {
            int boolCounter = 0;
            foreach (gbXYZ pt in polyA)
                if (IsPtInPoly(pt, polyB))
                    boolCounter++;
            foreach (gbXYZ pt in polyB)
                if (IsPtInPoly(pt, polyA))
                    boolCounter++;
            if (boolCounter == 0)
                return false;
            else
                return true;
        }
        public static List<List<List<gbXYZ>>> PolyClusterByOverlap(List<List<gbXYZ>> loops)
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
                            if (IsPolyOverlap(loopGroup[i], loopPool[j]))
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

        /// <summary>
        /// Get the normal of a polygon by Left-hand order
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
        public static List<gbXYZ> PolyToPoly2D(List<gbXYZ> pts)
        {
            gbXYZ normal = GetPolyNormal(pts);
            gbXYZ u = GetPendicularVec(normal, true);
            gbXYZ v = new gbXYZ(0, 0, 1);
            List<gbXYZ> flattenPts = new List<gbXYZ>();
            foreach (gbXYZ pt in pts)
            {
                gbXYZ vec = new gbXYZ(pt.X, pt.Y, pt.Z);
                flattenPts.Add(new gbXYZ(vec.DotProduct(u), vec.DotProduct(v), 0));
            }
            return flattenPts;
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

        #endregion
    }
}

