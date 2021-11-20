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
    class CurveSimplify
    {
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


        // -------------------- Douglas Peucker Reduction -------------------------

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


        // -------------------- Curve Adaptive Division -------------------------------

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
            //Debug.Print("CurveSimplify:: " + "solution reached in {0} steps.", c + 1);
            
            return PolyLine.Create(ptList);
        }


        // -------------------- Curve Adaptive Division -------------------------------
        // by courtesy of Radovan Grmusa
        // https://discourse.mcneel.com/t/best-polyline-from-curve/80417/9

        /// <summary>
        /// Reduce the number of vertices while trying to keep the original length
        /// </summary>
        /// <param name="crv"></param>
        /// <param name="noOfPoints"></param>
        public static PolyLine MaxLengthReduction(Curve crv, int noOfPoints)
        {
            int noOfPointsEnd = noOfPoints * 4 + 1;
            List<XYZ> points = new List<XYZ>();
            PolyLine plMax = null;

            for (int i = noOfPoints + 1; i < noOfPointsEnd; i++)
            {
                int noOfTestPoints = i;
                var crvPrms = Enumerable.Range(0, noOfTestPoints)
                    .Select(x => (double) x / (noOfTestPoints - 1)).ToList();
                //Debug.Print("CurveSimplify:: " + "### crvPrms ###" + Util.ListString(crvPrms));
                var crvPoints = crvPrms.Select(p => crv.Evaluate(p, true)).ToList();
                //Debug.Print("CurveSimplify:: " + "### crvPoints ###" + Util.ListString(crvPoints));


                List<double> FirstNeighborLengths = new List<double>();
                List<double> SecondNeighborLengths = new List<double>();

                double[] LengthDifferenceA = new double[noOfTestPoints - 2];
                var LengthDifferenceIds = Enumerable.Range(0, noOfTestPoints - 2).ToArray();

                FirstNeighborLengths.Add(crvPoints[0].DistanceTo(crvPoints[1]));
                for (int j = 1; j < noOfTestPoints - 1; j++)
                {
                    FirstNeighborLengths.Add(crvPoints[j].DistanceTo(crvPoints[j + 1]));
                    SecondNeighborLengths.Add(crvPoints[j - 1].DistanceTo(crvPoints[j + 1]));
                    LengthDifferenceA[j - 1] = (FirstNeighborLengths[j - 1] + FirstNeighborLengths[j] - SecondNeighborLengths[j - 1]);
                }
                double[] LD = new double[noOfTestPoints - 2];
                LengthDifferenceA.CopyTo(LD, 0);

                var LengthDifferences = LengthDifferenceA.ToList();
                Array.Sort(LengthDifferenceA, LengthDifferenceIds);

                List<double> LengthDifferenceSorted = LengthDifferenceA.ToList();
                List<int> LengthDifferenceSortedIds = LengthDifferenceIds.ToList();

                List<int> LengthDifferenceId_PosIn_LengthDifferenceSortedIds = Enumerable.Range(0, noOfTestPoints - 2).ToList();
                for (int j = 0; j < noOfTestPoints - 2; j++)
                {
                    LengthDifferenceId_PosIn_LengthDifferenceSortedIds[LengthDifferenceIds[j]] = j;
                }

                while (noOfPoints < crvPoints.Count)
                {
                    RemoveMinDifferenceSegment(crvPoints, FirstNeighborLengths, SecondNeighborLengths, LengthDifferences,
                        LengthDifferenceSorted, LengthDifferenceSortedIds, LengthDifferenceId_PosIn_LengthDifferenceSortedIds);
                }
                //Debug.Print("CurveSimplify:: " + "### crvPoints after ###" + Util.ListString(crvPoints));
                PolyLine ply = PolyLine.Create(crvPoints);
                if (Basic.PolyLineLength(ply) > Basic.PolyLineLength(plMax))
                {
                    plMax = ply;
                    points = crvPoints;
                }
            }

            return plMax;
        }

        private static void RemoveMinDifferenceSegment(List<XYZ> crvPoints, List<double> FirstNeighborLengths, List<double> SecondNeighborLengths,
    List<double> LengthDifference, List<double> LengthDifferenceSorted, List<int> LegnthDiferncesSortedIds,
    List<int> LengthDifferenceId_PosIn_LengthDifferenceSortedIds)
        {
            int i = LegnthDiferncesSortedIds.First();
            // renmove point at i+1 position
            crvPoints.RemoveAt(i + 1);
            FirstNeighborLengths.RemoveAt(i + 1);
            FirstNeighborLengths[i] = crvPoints[i].DistanceTo(crvPoints[i + 1]);

            SecondNeighborLengths.RemoveAt(i);
            RemoveAtId(i, LengthDifference, LengthDifferenceSorted, LegnthDiferncesSortedIds, LengthDifferenceId_PosIn_LengthDifferenceSortedIds);

            if (i < SecondNeighborLengths.Count)
            {
                SecondNeighborLengths[i] = crvPoints[i].DistanceTo(crvPoints[i + 2]);
                RemoveAtId(i, LengthDifference, LengthDifferenceSorted, LegnthDiferncesSortedIds, LengthDifferenceId_PosIn_LengthDifferenceSortedIds);
                var newLengthdifference = (FirstNeighborLengths[i] + FirstNeighborLengths[i + 1] - SecondNeighborLengths[i]);
                InsertAtId(newLengthdifference, i, LengthDifference, LengthDifferenceSorted, LegnthDiferncesSortedIds, LengthDifferenceId_PosIn_LengthDifferenceSortedIds);
            }

            if (i > 0)
            {
                SecondNeighborLengths[i - 1] = crvPoints[i - 1].DistanceTo(crvPoints[i + 1]);
                RemoveAtId(i - 1, LengthDifference, LengthDifferenceSorted, LegnthDiferncesSortedIds, LengthDifferenceId_PosIn_LengthDifferenceSortedIds);
                var newLengthdifference = (FirstNeighborLengths[i - 1] + FirstNeighborLengths[i] - SecondNeighborLengths[i - 1]);
                InsertAtId(newLengthdifference, i - 1, LengthDifference, LengthDifferenceSorted, LegnthDiferncesSortedIds, LengthDifferenceId_PosIn_LengthDifferenceSortedIds);
            }
        }

        private static void InsertAtId(double newElement, int id, List<double> nums, List<double> ordNums, List<int> ordNumsIds, List<int> pos_numsID_In_ordNumsIds)
        {
            if (id < 0 || id > (nums.Count)) return;
            var posInOrderedId = BinarySearchLargestLT(ordNums, newElement) + 1;
            //increase +1 all indices for indice>=id
            for (int i = id; i < pos_numsID_In_ordNumsIds.Count; i++)
            {
                ordNumsIds[pos_numsID_In_ordNumsIds[i]]++;
            }

            nums.Insert(id, newElement);
            ordNums.Insert(posInOrderedId, newElement);
            ordNumsIds.Insert(posInOrderedId, id);

            pos_numsID_In_ordNumsIds.Insert(id, posInOrderedId);

            for (int i = posInOrderedId; i < ordNumsIds.Count; i++)
            {
                pos_numsID_In_ordNumsIds[ordNumsIds[i]] = i;
            }
        }

        private static void RemoveAtId(int id, List<double> nums, List<double> ordNums, List<int> ordNumsIds, List<int> pos_numsID_In_ordNumsIds)
        {
            if (id < 0 || id > (nums.Count - 1))
            {
                return;
            }
            int posInOrderedId = pos_numsID_In_ordNumsIds[id];
            for (int i = id + 1; i < pos_numsID_In_ordNumsIds.Count; i++)
            {
                ordNumsIds[pos_numsID_In_ordNumsIds[i]]--;
            }

            nums.RemoveAt(id);
            pos_numsID_In_ordNumsIds.RemoveAt(id);
            ordNums.RemoveAt(posInOrderedId);
            ordNumsIds.RemoveAt(posInOrderedId);

            for (int i = posInOrderedId; i < ordNumsIds.Count; i++)
            {
                pos_numsID_In_ordNumsIds[ordNumsIds[i]] = i;
            }
        }
        static int BinarySearchLargestLT(List<double> arr, double val)
        {
            int low = 0, high = arr.Count - 1, mid;
            int indexLargestLT = -1;

            while (low <= high)
            {
                mid = (low + high) / 2;
                if (arr[mid] >= val)
                {
                    high = mid - 1;
                }
                else if (arr[mid] < val)
                {
                    low = mid + 1;
                    indexLargestLT = mid;
                }
            }
            return indexLargestLT;
        }

    }
}
