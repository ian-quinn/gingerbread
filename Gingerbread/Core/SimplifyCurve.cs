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
    class SimplifyCurve
    {
        /// <summary>
        /// Uses the Douglas Peucker algorithm to reduce the number of pts.
        /// </summary>
        /// <param name="pts">The pts.</param>
        /// <param name="Tolerance">The tolerance.</param>
        /// <returns></returns>
        public static List<XYZ> DouglasPeuckerReduction(List<XYZ> pts, double tolerance)
        {
            if (pts == null || pts.Count < 3)
                return pts;

            int firstPoint = 0;
            int lastPoint = pts.Count - 1;
            List<int> pointIndexsToKeep = new List<int>();

            //Add the first and last index to the keepers
            pointIndexsToKeep.Add(firstPoint);
            pointIndexsToKeep.Add(lastPoint);

            //The first and the last point cannot be the same
            while (pts[firstPoint].Equals(pts[lastPoint]))
            {
                lastPoint--;
            }

            DouglasPeuckerReduction(pts, firstPoint, lastPoint, tolerance, ref pointIndexsToKeep);

            List<XYZ> returnpts = new List<XYZ>();
            pointIndexsToKeep.Sort();
            foreach (int index in pointIndexsToKeep)
            {
                returnpts.Add(pts[index]);
            }

            return returnpts;
        }

        /// <summary>
        /// Douglases the peucker reduction.
        /// </summary>
        /// <param name="pts">The pts.</param>
        /// <param name="firstPoint">The first point.</param>
        /// <param name="lastPoint">The last point.</param>
        /// <param name="tolerance">The tolerance.</param>
        /// <param name="pointIndexsToKeep">The point index to keep.</param>
        private static void DouglasPeuckerReduction(List<XYZ> pts, int firstPoint, int lastPoint, 
            double tolerance, ref List<int> pointIndexsToKeep)
        {
            double maxDistance = 0;
            int indexFarthest = 0;

            for (int index = firstPoint; index < lastPoint; index++)
            {
                double distance = Basic.PerpendicularDistance
                    (pts[firstPoint], pts[lastPoint], pts[index]);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    indexFarthest = index;
                }
            }

            if (maxDistance > tolerance && indexFarthest != 0)
            {
                //Add the largest point that exceeds the tolerance
                pointIndexsToKeep.Add(indexFarthest);

                DouglasPeuckerReduction(pts, firstPoint,
                indexFarthest, tolerance, ref pointIndexsToKeep);
                DouglasPeuckerReduction(pts, indexFarthest,
                lastPoint, tolerance, ref pointIndexsToKeep);
            }
        }
    }
}
