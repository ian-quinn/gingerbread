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

            List<XYZ> returnPts = new List<XYZ>();
            pointIndexsToKeep.Sort();
            foreach (int index in pointIndexsToKeep)
            {
                returnPts.Add(pts[index]);
            }

            return returnPts;
        }

        public static PolyLine DouglasPeuckerReduction(Curve crv, double tolerance)
        {
            List<XYZ> pts = new List<XYZ>(crv.Tessellate());
            if (pts == null)
            {
                return null;
            }
            return PolyLine.Create(DouglasPeuckerReduction(pts, tolerance));
        }
        public static PolyLine DouglasPeuckerReduction(PolyLine ply, double tolerance)
        {
            List<XYZ> pts = new List<XYZ>(ply.GetCoordinates());
            if (pts == null)
            {
                return null;
            }
            return PolyLine.Create(DouglasPeuckerReduction(pts, tolerance));
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


        // -------------------- alternatives -------------------------------

        /// <summary>
        /// Reduce a curve to polyline adaptively. Not an ideal method.
        /// </summary>
        /// <param name="crv"></param>
        /// <param name="divisions"></param>
        /// <returns></returns>
        public static PolyLine AdaptiveReduction(Curve crv, double divisions)
        {
            XYZ startPt = crv.GetEndPoint(0);
            XYZ endPt = crv.GetEndPoint(1);
            XYZ mPt;
            double tolerance = crv.Length / divisions;
            bool keepOn = true;
            int c = 0;
            List<XYZ> ptList = new List<XYZ>();
            ptList.Add(startPt);
            if (startPt.IsAlmostEqualTo(endPt))
            {
                XYZ p1 = crv.Evaluate(0.25, true);
                XYZ p2 = crv.Evaluate(0.5, true);
                XYZ p3 = crv.Evaluate(0.75, true);
                ptList.Add(p1);
                ptList.Add(p2);
                ptList.Add(p3);
            }
            ptList.Add(endPt);

            double distance = 0;
            double edgeLength = 0;
            
            while ((keepOn) && (c < 100))
            {
                keepOn = false;
                for (int i = 0; i < ptList.Count - 1; i++)
                {
                    mPt = (ptList[i] + ptList[i + 1]) / 2;
                    XYZ pPt = null;
                    // Here can be dangerous situations
                    try
                    {
                        pPt = crv.Project(mPt).XYZPoint;
                    }
                    catch { }

                    if (null != pPt)
                    {
                        distance = pPt.DistanceTo(mPt);
                        edgeLength = pPt.DistanceTo(ptList[i]);
                        if (distance > tolerance &&
                            edgeLength > Properties.Settings.Default.ShortCurveTolerance)
                        {
                            ptList.Insert(i + 1, pPt);
                            keepOn = true;
                            i++;
                        }
                    }
                }

                c++;
            }
            //Debug.Print("solution reached in {0} steps.", c + 1);
            
            return PolyLine.Create(ptList);
        }

        /// <summary>
        /// Coonvert curve to polyline by curvature
        /// </summary>
        /// <param name="crv"></param>
        /// <returns></returns>
        public static PolyLine TessellateCurve(Curve crv)
        {
            List<XYZ> pts = new List<XYZ>(crv.Tessellate());
            return PolyLine.Create(pts);
        }
    }
}
