using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gingerbread.Core;

namespace Gingerbread.Core
{
    public static class LayoutPatch
    {
        /// <summary>
        /// </summary>
        public static void PerimeterPatch(List<gbSeg> walls, List<gbXYZ> boundary, double threshold)
        {
            // the input boundary must be cleaned and simplified
            List<gbSeg> edges = new List<gbSeg>();
            for (int i = 0; i < boundary.Count - 1; i++)
            {
                gbSeg edge = new gbSeg(boundary[i], boundary[i + 1]);
                if (edge.Length != 0)
                    edges.Add(edge);
            }

            List<gbXYZ> offsetBoundary = GBMethod.OffsetPoly(boundary, threshold)[0];
            for (int i = walls.Count - 1; i >= 0; i--)
            {
                //gbXYZ start = walls[i].PointAt(0);
                //gbXYZ end = walls[i].PointAt(1);
                //if (GBMethod.IsPtInPoly(start, boundary) &&
                //    GBMethod.IsPtInPoly(end, boundary) &&
                //    !GBMethod.IsPtInPoly(start, offsetBoundary) &&
                //    !GBMethod.IsPtInPoly(end, offsetBoundary))
                //{
                //    walls.RemoveAt(i);
                //}
                for (int ptIndex = 0; ptIndex < 2; ptIndex++)
                {
                    gbXYZ ptMove = walls[i].PointAt(ptIndex);
                    if (GBMethod.IsPtInPoly(ptMove, boundary) && !GBMethod.IsPtInPoly(ptMove, offsetBoundary))
                    {
                        foreach (gbSeg edge in edges)
                        {
                            if (edge.Length == 0) // be cautious if the input edge is of zero length
                                continue;
                            double distance = GBMethod.PtDistanceToSeg(ptMove, edge, out gbXYZ plummet, out double stretch);
                            if (distance < Math.Abs(threshold))
                            {
                                Debug.Print($"LayoutPatch:: baseline: {edges.IndexOf(edge)}");
                                Debug.Print($"LayoutPatch:: {ptMove} -> {plummet}");
                                walls[i].AdjustEndPt(ptIndex, plummet);
                                Debug.Print($"LayoutPatch:: {walls[i].PointAt(ptIndex)}");
                            }
                        }
                    }
                }
            }
            return;
        }

        private static List<gbXYZ> SortPtLoop(List<gbSeg> lines)
        {
            if (lines.Count == 0)
            {
                //Rhino.RhinoApp.WriteLine("NO CURVE AS INPUT");
                return new List<gbXYZ>();
            }
            List<gbXYZ> ptLoop = new List<gbXYZ>();

            // first define the start point
            // all curve in crvs are shuffled up with random directions
            // but the start point has to be the joint of the first and last curve
            gbXYZ startPt = new gbXYZ();
            if (lines[0].PointAt(0) == lines.Last().PointAt(0) ||
              lines[0].PointAt(0) == lines.Last().PointAt(1))
                startPt = lines[0].PointAt(0);
            else
                startPt = lines[0].PointAt(1);

            ptLoop.Add(startPt);

            for (int i = 0; i < lines.Count; i++)
                if (lines[i].PointAt(0) == ptLoop[i])
                    ptLoop.Add(lines[i].PointAt(1));
                else
                    ptLoop.Add(lines[i].PointAt(0));

            return ptLoop;
        }                     

        /// <summary>
        /// </summary>
        public static void ColumnPatch(List<gbSeg> walls, List<List<List<gbXYZ>>> floors)
        {
            return;
        }
    }
}
