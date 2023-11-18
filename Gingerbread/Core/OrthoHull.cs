using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gingerbread.Core
{
    class OrthoHull
    {
        enum SearchQuadrant
        {
            // follows clockwise order
            // it is a fuzzy place for searching not the direction
            LeftUp = 1,
            UpRight = 0,
            RightDown = 3,
            DownLeft = 2
        }

        private static gbXYZ GetStartPoint(List<gbXYZ> pts, SearchQuadrant quadrant)
        {
            gbXYZ basePt = pts[0];
            switch (quadrant)
            {
                // find the leftmost point. pick the top one if duplicate
                case SearchQuadrant.LeftUp:
                    foreach (gbXYZ pt in pts)
                        if (basePt.X > pt.X ||
                            (basePt.X == pt.X && basePt.Y < pt.Y))
                            basePt = pt;
                    break;
                // find the top point. pick the rightmost one if duplicate
                case SearchQuadrant.UpRight:
                    foreach (gbXYZ pt in pts)
                        if (basePt.Y < pt.Y ||
                            (basePt.Y == pt.Y && basePt.X < pt.X))
                            basePt = pt;
                    break;
                // find the rightmost point. pick the bottom one if duplicate
                case SearchQuadrant.RightDown:
                    foreach (gbXYZ pt in pts)
                        if (basePt.X < pt.X ||
                            (basePt.X == pt.X && basePt.Y > pt.Y))
                            basePt = pt;
                    break;
                // find the bottom point. pick the leftmost one if duplicate
                case SearchQuadrant.DownLeft:
                    foreach (gbXYZ pt in pts)
                        if (basePt.Y > pt.Y ||
                            (basePt.Y == pt.Y && basePt.X > pt.X))
                            basePt = pt;
                    break;
            }
            return basePt;
        }

        // there will be some duplicated points in the chain
        // it does not matter to generate a polyloop (only add some lines with 0 length)
        // you may skim them out by RemoveDupPts if it bothers
        private static List<gbXYZ> GetChain(gbXYZ startPt, gbXYZ endPt,
            List<gbXYZ> sortedPts, SearchQuadrant quadrant)
        {
            List<gbXYZ> chain = new List<gbXYZ>();
            chain.Add(startPt);
            double baseZ = startPt.Z;
            switch (quadrant)
            {
                case SearchQuadrant.LeftUp:
                    for (int i = 1; i < sortedPts.Count; i++)
                    {
                        if (sortedPts[i].Y >= chain.Last().Y)
                        {
                            if (sortedPts[i].X != chain.Last().X
                                && sortedPts[i].Y != chain.Last().Y)
                                chain.Add(new gbXYZ(sortedPts[i].X, chain.Last().Y, baseZ));
                            chain.Add(sortedPts[i]);
                        }
                        if (sortedPts[i].Equals(endPt))
                            break;
                    }
                    break;
                case SearchQuadrant.UpRight:
                    for (int i = sortedPts.Count - 2; i >= 0; i--)
                    {
                        if (sortedPts[i].Y >= chain.Last().Y)
                        {
                            if (sortedPts[i].X != chain.Last().X
                                && sortedPts[i].Y != chain.Last().Y)
                                chain.Add(new gbXYZ(sortedPts[i].X, chain.Last().Y, baseZ));
                            chain.Add(sortedPts[i]);
                        }
                        if (sortedPts[i].Equals(endPt))
                            break;
                    }
                    break;
                case SearchQuadrant.RightDown:
                    for (int i = sortedPts.Count - 2; i >= 0; i--)
                    {
                        if (sortedPts[i].Y <= chain.Last().Y)
                        {
                            if (sortedPts[i].X != chain.Last().X
                                && sortedPts[i].Y != chain.Last().Y)
                                chain.Add(new gbXYZ(sortedPts[i].X, chain.Last().Y, baseZ));
                            chain.Add(sortedPts[i]);
                        }
                        if (sortedPts[i].Equals(endPt))
                            break;
                    }
                    break;
                case SearchQuadrant.DownLeft:
                    for (int i = 1; i < sortedPts.Count; i++)
                    {
                        if (sortedPts[i].Y <= chain.Last().Y)
                        {
                            if (sortedPts[i].X != chain.Last().X
                                && sortedPts[i].Y != chain.Last().Y)
                                chain.Add(new gbXYZ(sortedPts[i].X, chain.Last().Y, baseZ));
                            chain.Add(sortedPts[i]);
                        }
                        if (sortedPts[i].Equals(endPt))
                            break;
                    }
                    break;
            }
            return chain;
        }

        public static List<gbXYZ> GetOrthoHull(List<gbXYZ> pts)
        {
            List<gbXYZ> sortedPts = new List<gbXYZ>();
            foreach (gbXYZ pt in pts)
                sortedPts.Add(pt);
            int numPts = sortedPts.Count;
            gbXYZ tempPt;
            // LINQ is okay
            for (int i = 0; i < numPts; i++)
            {
                for (int j = 0; j < numPts - i - 1; j++)
                {
                    if (sortedPts[j].X > sortedPts[j + 1].X)
                    {
                        tempPt = sortedPts[j];
                        sortedPts[j] = sortedPts[j + 1];
                        sortedPts[j + 1] = tempPt;
                    }
                }
            }
            // locate identical points on two ends?


            gbXYZ leftPt = GetStartPoint(pts, SearchQuadrant.LeftUp);
            gbXYZ upPt = GetStartPoint(pts, SearchQuadrant.UpRight);
            gbXYZ rightPt = GetStartPoint(pts, SearchQuadrant.RightDown);
            gbXYZ downPt = GetStartPoint(pts, SearchQuadrant.DownLeft);

            List<gbXYZ> leftupChain = GetChain(leftPt, upPt, sortedPts, SearchQuadrant.LeftUp);
            leftupChain.RemoveAt(leftupChain.Count - 1);
            List<gbXYZ> uprightChain = GetChain(rightPt, upPt, sortedPts, SearchQuadrant.UpRight);
            uprightChain.Reverse(); uprightChain.RemoveAt(uprightChain.Count - 1);
            List<gbXYZ> rightdownChain = GetChain(rightPt, downPt, sortedPts, SearchQuadrant.RightDown);
            rightdownChain.RemoveAt(rightdownChain.Count - 1);
            List<gbXYZ> downleftChain = GetChain(leftPt, downPt, sortedPts, SearchQuadrant.DownLeft);
            downleftChain.Reverse(); //downleftChain.RemoveAt(downleftChain.Count - 1);

            //Util.LogPrint(Util.PtLoopToString(leftupChain));
            //Util.LogPrint(Util.PtLoopToString(uprightChain));
            //Util.LogPrint(Util.PtLoopToString(rightdownChain));
            //Util.LogPrint(Util.PtLoopToString(downleftChain));

            List<gbXYZ> loop = new List<gbXYZ>();
            loop.AddRange(leftupChain);
            loop.AddRange(uprightChain);
            loop.AddRange(rightdownChain);
            loop.AddRange(downleftChain);
            return loop;
        }

        public static List<gbXYZ> GetRectHull(List<gbXYZ> pts)
        {
            gbXYZ leftPt = GetStartPoint(pts, SearchQuadrant.LeftUp);
            gbXYZ upPt = GetStartPoint(pts, SearchQuadrant.UpRight);
            gbXYZ rightPt = GetStartPoint(pts, SearchQuadrant.RightDown);
            gbXYZ downPt = GetStartPoint(pts, SearchQuadrant.DownLeft);

            List<gbXYZ> loop = new List<gbXYZ>();
            loop.Add(new gbXYZ(leftPt.X, downPt.Y, 0));
            loop.Add(new gbXYZ(rightPt.X, downPt.Y, 0));
            loop.Add(new gbXYZ(rightPt.X, upPt.Y, 0));
            loop.Add(new gbXYZ(leftPt.X, upPt.Y, 0));
            loop.Add(loop[0]);
            return loop;
        }

        public static double Cross(gbXYZ O, gbXYZ A, gbXYZ B)
        {
            return (A.X - O.X) * (B.Y - O.Y) - (A.Y - O.Y) * (B.X - O.X);
        }

        /// <summary>
        /// Get the convex hull of a point set.
        /// </summary>
        // 20231118 original method alters the sequence of input points, terrible mistake
        public static List<gbXYZ> GetConvexHull(List<gbXYZ> pts)
        {
            if (pts == null)
                return null;

            if (pts.Count() <= 1)
                return pts;

            List<gbXYZ> points = pts.ToList();

            int n = points.Count(), k = 0;
            List<gbXYZ> H = new List<gbXYZ>(new gbXYZ[2 * n]);
            points.Sort((a, b) =>
                    a.X == b.X ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));

            // Build lower hull
            for (int i = 0; i < n; ++i)
            {
                while (k >= 2 && Cross(H[k - 2], H[k - 1], points[i]) <= 0)
                    k--;
                H[k++] = points[i];
            }

            // Build upper hull
            for (int i = n - 2, t = k + 1; i >= 0; i--)
            {
                while (k >= t && Cross(H[k - 2], H[k - 1], points[i]) <= 0)
                    k--;
                H[k++] = points[i];
            }

            return H.Take(k - 1).ToList();
        }

        static double Angle(gbXYZ p1, gbXYZ p2)
        {
            return Math.Atan2(p2.Y - p1.Y, p2.X - p1.X);
        }

        static public gbXYZ PtCoordTrans(gbXYZ pt, double theta)
        {
            return new gbXYZ(
                pt.X * Math.Cos(theta) - pt.Y * Math.Sin(theta),
                pt.X * Math.Sin(theta) + pt.Y * Math.Cos(theta),
                0
            );
        }

        /// <summary>
        /// Get the min-area rectangle / optimum bounding rectangle of a point set,
        /// iterating all possible directions by Rotating Calipers method
        /// </summary>
        static public List<gbXYZ> GetMinRectHull(List<gbXYZ> pts)
        {
            List<gbXYZ> vts = GetConvexHull(pts);

            double min_area = double.PositiveInfinity;
            List<gbXYZ> hull = new List<gbXYZ>() { };

            for (int i = 0; i < vts.Count; i++)
            {
                int j = (i + 1) % vts.Count;

                double theta = Angle(vts[i], vts[j]);

                List<double> coord_x = new List<double>() { };
                List<double> coord_y = new List<double>() { };
                foreach (gbXYZ vt in vts)
                {
                    gbXYZ vt_trans = PtCoordTrans(vt, theta);
                    coord_x.Add(vt_trans.X);
                    coord_y.Add(vt_trans.Y);
                }

                coord_x.Sort(); coord_y.Sort();

                double area = (coord_x.Last() - coord_x[0]) * (coord_y.Last() - coord_x[0]);

                if (area < min_area)
                {
                    min_area = area;
                    gbXYZ p1 = PtCoordTrans(new gbXYZ(coord_x[0], coord_y[0], 0), -theta);
                    gbXYZ p2 = PtCoordTrans(new gbXYZ(coord_x.Last(), coord_y[0], 0), -theta);
                    gbXYZ p3 = PtCoordTrans(new gbXYZ(coord_x.Last(), coord_y.Last(), 0), -theta);
                    gbXYZ p4 = PtCoordTrans(new gbXYZ(coord_x[0], coord_y.Last(), 0), -theta);
                    hull = new List<gbXYZ>() { p1, p2, p3, p4 };
                }
            }

            return hull;
        }
    }
}
