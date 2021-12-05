using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gingerbread.Core
{
    class PointAlign
    {
        /// <summary>
        /// Alignment step 1: get the joints and their reaching hands
        /// </summary>
        /// <param name="lines"></param>
        /// <param name="hands"></param>
        /// <returns></returns>
        public static List<gbXYZ> GetJoints(List<gbSeg> lines, double tolerance, out List<List<gbXYZ>> hands)
        {
            List<gbXYZ> Vtc = new List<gbXYZ>(); // all unique vertices
            List<gbSeg> HC = new List<gbSeg>(); // list of all shattered half-curves
            List<int> HCI = new List<int>(); // half curve indices
            List<int> HCO = new List<int>(); // half curve reversed
            List<int> HCV = new List<int>(); // vertex representing this half-curve
                                             // (if it either starts or ends hanging, but does not exclude redundant curves that not exclosing a room)
            Dictionary<int, List<int>> VOut = new Dictionary<int, List<int>>(); // data tree of outgoing half-curves from each vertex
            Dictionary<int, List<gbXYZ>> VJoint = new Dictionary<int, List<gbXYZ>>();

            foreach (gbSeg crv in lines) // cycle through each curve
            {
                for (int CRun = 0; CRun <= 2; CRun += 2) // create two half-curves: first in one direction, and then the other...
                {
                    gbXYZ testedPt = crv.PointAt(0);
                    HC.Add(crv.Copy());
                    HCI.Add(HCI.Count); // count this iteration
                    HCO.Add(HCI.Count - CRun); // a little index trick

                    int VtcSet = -1;

                    for (int VtxCheck = 0; VtxCheck <= Vtc.Count - 1; VtxCheck++)
                    {
                        if (Vtc[VtxCheck].DistanceTo(testedPt) < tolerance)
                        {
                            VtcSet = VtxCheck; // get the vertex index, if it already exists
                            break;
                        }
                    }

                    gbXYZ paleVec = RoundVec(HC[HCI.Last()].Direction, tolerance);
                    if (VtcSet > -1)
                    {
                        HCV.Add(VtcSet); // If the vertex already exists, set the half-curve vertex
                        VOut[VtcSet].Add(HCI.Last());
                        if (!IsIncluded(VJoint[VtcSet], paleVec, tolerance))
                            VJoint[VtcSet].Add(paleVec);
                    }
                    else
                    {
                        HCV.Add(Vtc.Count); // if the vertex doesn't already exist, add a new vertex index
                        VOut.Add(Vtc.Count, new List<int>() { HCI.Last() });
                        VJoint.Add(Vtc.Count, new List<gbXYZ>() { paleVec });
                        // add the new half-curve index to the list of outgoing half-curves associated with the vertex
                        Vtc.Add(testedPt);
                        // add the new vertex to the vertex list
                    }
                    crv.Reverse(); // reverse the curve for creating the opposite half-curve in the second part of the loop
                                   //Debug.Print("PointAlign:: " + "Tested point is (" + testedPt.X.ToString() + ", " + testedPt.Y.ToString() + ")");
                }
            }
            List<List<gbXYZ>> vecList = new List<List<gbXYZ>>();
            foreach (List<gbXYZ> vecs in VJoint.Values)
            {
                vecList.Add(vecs);
            }

            // OUTPUT
            hands = vecList;
            return Vtc;
        }

        /// <summary>
        /// Main function of this component to align points on certain direction
        /// </summary>
        /// <param name="pts"></param>
        /// <param name="vecList"></param>
        /// <param name="theta"></param>
        /// <param name="delta"></param>
        /// <param name="anchorInfo"></param>
        /// <returns></returns>
        public static List<gbXYZ> AlignPts(
          List<gbXYZ> pts, List<List<gbXYZ>> vecList, List<gbSeg> preBlueprint, 
          double theta, double delta, double tolerance,
          out List<List<gbXYZ>> anchorInfo, out List<gbSeg> nextBlueprint)
        //out List<gbSeg> axes, out List<List<gbXYZ>> ptGroups)
        {
            // Copy the original points for iteration
            List<gbXYZ> ptPool = new List<gbXYZ>();
            List<int> ptIdPool = new List<int>();
            for (int i = 0; i < pts.Count; i++)
            {
                ptPool.Add(pts[i]);
                ptIdPool.Add(i);
            }


            // Results declearation
            List<gbXYZ> ptRastered = new List<gbXYZ>();
            List<int> ptIdRastered = new List<int>();
            //axes = new List<gbSeg>();
            //ptGroups = new List<List<gbXYZ>>();


            // Define exact alignment direction
            gbXYZ scanRay = new gbXYZ(Math.Cos(theta), Math.Sin(theta), 0);
            nextBlueprint = new List<gbSeg>();

            while (ptPool.Count > 0)
            {
                List<gbXYZ> ptGroup = new List<gbXYZ>() { ptPool[0] };
                List<int> ptIdGroup = new List<int>() { ptIdPool[0] };
                ptPool.RemoveAt(0);
                ptIdPool.RemoveAt(0);

                // Generate the group of points almost in line
                for (int i = 0; i < ptGroup.Count; i++)
                {
                    for (int j = ptPool.Count - 1; j >= 0; j--)
                    {
                        if (ptGroup[i] != ptPool[j])
                        {
                            double distance = DistanceToRay(ptGroup[i], ptPool[j], scanRay);
                            if (distance < delta)
                            {
                                ptGroup.Add(ptPool[j]);
                                ptIdGroup.Add(ptIdPool[j]);
                                ptPool.RemoveAt(j);
                                ptIdPool.RemoveAt(j);
                            }
                        }
                    }
                }
                //ptGroups.Add(ptGroup);

                List<int> escapeIdx = new List<int>();

                // as to orphan point just ignore it
                if (ptGroup.Count == 1)
                {
                    ptRastered.Add(ptGroup[0]);
                    ptIdRastered.Add(ptIdGroup[0]);
                }

                // if more than one generate the axis
                if (ptGroup.Count > 1)
                {
                    // rotate the world plane so the alignment happens on Y axis
                    List<gbXYZ> transPts = new List<gbXYZ>();
                    List<int> transIdx = new List<int>();
                    for (int i = 0; i < ptGroup.Count; i++)
                    {
                        transPts.Add(new gbXYZ(
                          ptGroup[i].X * Math.Cos(theta) + ptGroup[i].Y * Math.Sin(theta),
                          ptGroup[i].Y * Math.Cos(theta) - ptGroup[i].X * Math.Sin(theta),
                          0));
                        transIdx.Add(i);
                    }

                    // these can be recoded to something with LINQ
                    List<List<int>> tempIdGroups = new List<List<int>>();
                    while (transIdx.Count > 0)
                    {
                        List<int> tempIdGroup = new List<int>() { transIdx[0] };
                        transIdx.RemoveAt(0);
                        for (int i = 0; i < tempIdGroup.Count; i++)
                        {
                            for (int j = transIdx.Count - 1; j >= 0; j--)
                            {
                                if (transPts[tempIdGroup[i]].Y == transPts[transIdx[j]].Y)
                                {
                                    tempIdGroup.Add(transIdx[j]);
                                    transIdx.RemoveAt(j);
                                }
                            }
                        }
                        tempIdGroups.Add(tempIdGroup);
                    }

                    int axisIdx = 0;
                    int maxMember = 0;
                    for (int i = 0; i < tempIdGroups.Count; i++)
                    {
                        if (tempIdGroups[i].Count > maxMember)
                        {
                            maxMember = tempIdGroups[i].Count;
                            axisIdx = i;
                        }
                    }


                    List<int> axisIdPts = tempIdGroups[axisIdx];
                    foreach (int idx in axisIdPts)
                        escapeIdx.Add(ptIdGroup[idx]);

                    List<gbXYZ> axisPts = new List<gbXYZ>();
                    foreach (int idx in axisIdPts)
                        axisPts.Add(transPts[idx]);
                    BubbleSortPtsOnX(axisPts, axisIdPts);
                    double axisY = axisPts[0].Y;

                    // DEBUG REGION
                    /*
                    Rhino.RhinoApp.WriteLine("-------------------within one group-------------------");
                    Rhino.RhinoApp.Write("SeqAxisPts: ");
                    foreach (int idx in tempIdGroups[axisIdx])
                    {
                    Rhino.RhinoApp.Write(ptIdGroup[idx].ToString() + ", ");
                    }
                    Rhino.RhinoApp.Write("\n");
                    */

                    // transform reversal and get the axis
                    // no digit rounding here
                    gbXYZ p1 = new gbXYZ(
                      axisPts[0].X * Math.Cos(theta) - axisY * Math.Sin(theta),
                      axisPts[0].X * Math.Sin(theta) + axisY * Math.Cos(theta),
                      0);
                    gbXYZ p2 = new gbXYZ(
                      axisPts.Last().X * Math.Cos(theta) - axisY * Math.Sin(theta),
                      axisPts.Last().X * Math.Sin(theta) + axisY * Math.Cos(theta),
                      0);

                    List<double> splits = new List<double>(); // section point position
                    List<bool> isConnected = new List<bool>(); // connectivity at each section
                    gbSeg axis = new gbSeg(p1, p2);

                    if (maxMember == 1) // one point as axis
                    {
                        p2 = p1 + 10 * scanRay;
                        axis = new gbSeg(p1, p2);
                        splits.Add(0);
                        isConnected.Add(false);
                    }
                    else if (maxMember > 1) // multiple points on the same axis
                    {
                        axis = new gbSeg(p1, p2);
                        for (int i = 0; i < axisIdPts.Count; i++)
                        {
                            double split = (axisPts[i].X - axisPts[0].X) / (axisPts.Last().X - axisPts[0].X);
                            splits.Add(split);
                            if (IsIncluded(vecList[ptIdGroup[axisIdPts[i]]], scanRay, tolerance))
                            {
                                isConnected.Add(true);
                            }
                            else
                            {
                                isConnected.Add(false);
                            }
                        }
                    }

                    // DEBUG REGION
                    /*
                    Rhino.RhinoApp.WriteLine("CHECK ESCAPING IDX: " + escapeIdx.Count.ToString());
                    Rhino.RhinoApp.WriteLine("ANCHOR POINT ({0}, {1})", axis.PointAt(0).X, axis.PointAt(0).Y);
                    Rhino.RhinoApp.WriteLine("ANCHOR POINT NUM: " + axisPts.Count.ToString());
                    DisplayList(splits);
                    DisplayBoolList(isConnected);
                    */

                    //AXES.Add(axis);

                    // modify this axis inline with ther former blueprint
                    //double maxOverlap = 0;
                    double minDistance = Properties.Settings.Default.tolDelta;
                    gbSeg offsetAxis = axis;
                    foreach (gbSeg preAxis in preBlueprint)
                    {
                        double distance = GBMethod.SegDistanceToSeg(axis, preAxis, out double overlap, out gbSeg proj);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            offsetAxis = proj;
                            Debug.Print($"PointAlign:: offsetAxis updated with overlap {overlap} distance {distance}");
                        }
                    }
                    if (minDistance < Properties.Settings.Default.tolDelta)
                    {
                        axis = offsetAxis;
                        Debug.Print($"Axis moves to {axis}");
                    }

                    nextBlueprint.Add(axis);

                    // travers each point group and align the joints to anchors
                    for (int i = 0; i < ptGroup.Count; i++)
                    {
                        if (tempIdGroups[axisIdx].Contains(i))
                        {
                            //Rhino.RhinoApp.WriteLine("One point belongs to the axis");
                            ptRastered.Add(ptGroup[i]);
                            ptIdRastered.Add(ptIdGroup[i]);
                            continue;
                        }

                        double t;
                        gbXYZ plummet;
                        double distance = DistanceToLine(ptGroup[i], axis, out plummet, out t);
                        //Rhino.RhinoApp.WriteLine("I GOT T! " + t.ToString());

                        int insertIdx = -1;
                        for (int j = 0; j < splits.Count; j++)
                        {
                            if (t >= splits[j])
                                insertIdx++;
                        }

                        //Rhino.RhinoApp.WriteLine("Insert point location: " + insertIdx.ToString());

                        // here the Rhino global tolerance should be used, very tiny
                        gbXYZ moveDirection = RoundVec(new gbXYZ(
                          plummet.X - ptGroup[i].X,
                          plummet.Y - ptGroup[i].Y,
                          0), 0.0000001);
                        moveDirection.Unitize();
                        //Rhino.RhinoApp.WriteLine("EVIL VEC IS ({0}, {1})", moveDirection.X, moveDirection.Y);

                        // t value between 0 - 1 means the point moves onto the axis
                        // these may happen at the same time
                        if (t >= 0 && t <= 0)
                        {
                            //
                            if (IsIncluded(vecList[ptIdGroup[i]], moveDirection, tolerance))
                            {
                                //Rhino.RhinoApp.WriteLine("HERE WE DELETE AT: " + ptIdGroup[i].ToString());
                                vecList[ptIdGroup[i]].Remove(moveDirection);
                            }
                        }

                        if (t > 0 && t < 1)
                        {
                            //Rhino.RhinoApp.WriteLine("More vectors are added!");
                            if (isConnected[insertIdx])
                            {
                                if (!IsIncluded(vecList[ptIdGroup[i]], scanRay, tolerance))
                                {
                                    vecList[ptIdGroup[i]].Add(scanRay);
                                }
                                if (!IsIncluded(vecList[ptIdGroup[i]], -scanRay, tolerance))
                                {
                                    vecList[ptIdGroup[i]].Add(-scanRay);
                                }
                            }
                        }

                        ptRastered.Add(plummet);
                        ptIdRastered.Add(ptIdGroup[i]);
                    }
                }
            }

            // Future LINQ here
            //ptRastered = ptRastered.OrderBy(d => ptIdRastered.IndexOf(d.Id)).ToList();
            List<gbXYZ> ptAfter = new List<gbXYZ>();
            for (int i = 0; i < ptRastered.Count; i++)
            {
                ptAfter.Add(ptRastered[ptIdRastered.IndexOf(i)]);
            }

            // generate a vector list for all transform directions
            List<gbXYZ> vecTrans = new List<gbXYZ>();
            for (int i = 0; i < pts.Count; i++)
            {
                gbXYZ vec = new gbXYZ(ptAfter[i].X - pts[i].X,
                  ptAfter[i].Y - pts[i].Y, 0);
                vec = RoundVec(vec, 0.0000001);
                vec.Unitize();
                vecTrans.Add(vec);
            }

            // DEBUG REGION
            //Rhino.RhinoApp.WriteLine("Alignement check: {0} - {1} - {2}",
            //  ptAfter.Count, vecList.Count, vecTrans.Count);

            // merge coincident points to an anchor
            List<gbXYZ> anchors = new List<gbXYZ>();
            anchorInfo = new List<List<gbXYZ>>();
            while (ptAfter.Count > 0)
            {
                //Rhino.RhinoApp.Write("Loop {0}: ", counter);
                anchors.Add(ptAfter[0]);
                List<gbXYZ> pileVec = vecList[0];
                for (int i = ptAfter.Count - 1; i > 0; i--)
                {
                    // Here the coincident point event happens
                    if (PtAlmostTheSame(ptAfter[0], ptAfter[i], tolerance))
                    {
                        for (int j = pileVec.Count - 1; j >= 0; j--)
                        {
                            // if a point receive another point,
                            // it should delete the corresponding vector of that direction
                            if (VecAlmostTheSame(pileVec[j], -vecTrans[i], tolerance))
                            {
                                pileVec.RemoveAt(j);
                            }
                        }
                        // pile vectors of merged points if they are different
                        foreach (gbXYZ vec in vecList[i])
                        {
                            if (!IsIncluded(pileVec, vec, tolerance))
                                pileVec.Add(vec);
                        }
                        ptAfter.RemoveAt(i);
                        vecList.RemoveAt(i);
                        vecTrans.RemoveAt(i);
                    }
                }
                anchorInfo.Add(pileVec);

                ptAfter.RemoveAt(0);
                vecList.RemoveAt(0);
                vecTrans.RemoveAt(0);
            }


            // DEBUG REGION
            //Rhino.RhinoApp.WriteLine("After merging check: {0} - {1} - {2}",
            //  ptAfter.Count, vecList.Count, vecTrans.Count);


            // after fixation
            for (int i = anchors.Count - 1; i >= 0; i--)
            {
                // skim out points on the line that are redundant
                if (anchorInfo[i].Count == 2)
                {
                    if (VecAlmostTheSame(anchorInfo[i][0], -anchorInfo[i][1], tolerance))
                    {
                        anchors.RemoveAt(i);
                        anchorInfo.RemoveAt(i);
                        //vecTrans.RemoveAt(i);
                    }
                }
                // skim out points with no outgoing vectors
                // highly not possible
                // add these lines will cause termination sometimes
                // still don't know how this function malfunctions
                /*
                if (anchorInfo[i].Count == 0)
                {
                    anchors.RemoveAt(i);
                    anchorInfo.RemoveAt(i);
                }
                */
            }

            // DEBUG REGION
            //Rhino.RhinoApp.WriteLine("Final check: anchor/{0} - info/{1}",
            //  anchors.Count, anchorInfo.Count, vecTrans.Count);

            return anchors;
        }


        /// <summary>
        /// Alignment step 3: Recreate lattice from the aligned anchors
        /// </summary>
        public static List<gbSeg> GetLattice(List<gbXYZ> anchors, List<List<gbXYZ>> anchorInfo,
            double tolerance, out List<gbSeg> strays)
        {
            // tolerance - apply when compare two values to avoid double precision failure
            // delta - the farthest walk a point can make during alignment

            // loop to get all stray lines
            List<int> anchorVecCount = new List<int>(); // cache the out-reaching vector of each anchor
            strays = new List<gbSeg>(); // cache all stray lines
            foreach (List<gbXYZ> vecs in anchorInfo)
            {
                anchorVecCount.Add(vecs.Count);
            }
            int counter = 0;

            // iterate if there still exists an anchor with only one outgoing vector
            while (anchorVecCount.IndexOf(1) != -1)
            {
                counter++;
                //Rhino.RhinoApp.WriteLine(anchorVecCount.IndexOf(1).ToString());
                // ramdomly pick an anchor with single vector
                int pointer = anchorVecCount.IndexOf(1);
                //Rhino.RhinoApp.WriteLine($"This orphan point {anchors[pointer]} | {anchorInfo[pointer].Count} | {anchorInfo[pointer][0]}");
                double max = double.PositiveInfinity;
                int endId = -1;
                for (int i = anchors.Count - 1; i >= 0; i--)
                {
                    // these new added checkings follow the lattice generation down below
                    // not passed debugging right now, pending
                    double thetaBetween = Math.Abs(GBMethod.VectorAngle2D(anchors[pointer] - anchors[i], 
                        anchorInfo[pointer][0]) - 90);
                    if (thetaBetween < 85)
                        continue;

                    // check if the receiving anchor has the opposite projection vector
                    if (i != pointer && IsJointPaired(anchorInfo[i], -anchorInfo[pointer][0], 2))
                    {
                        double distance = DistanceToRay(anchors[i], anchors[pointer],
                          anchorInfo[pointer][0], out double stretch);
                        if (distance < tolerance && stretch > 0)
                        {
                            if (stretch < max)
                            {
                                max = stretch;
                                endId = i;
                            }
                        }
                    }
                }

                // when deleting a stray line
                // one end of the line that is type-I joint must be removed (projection point)
                // the other end of the line must remove the corresponding vector in anchorInfo (receiving point)
                // if it fails to locate the receiving point, we still remove the projection point anyway
                if (endId >= 0)
                {
                    strays.Add(new gbSeg(anchors[pointer], anchors[endId]));
                    for (int k = anchorInfo[endId].Count - 1; k >= 0; k--)
                        if (VecAlmostTheSame(anchorInfo[endId][k], -anchorInfo[pointer][0], tolerance))
                            anchorInfo[endId].RemoveAt(k);
                    // there must be a tolerance to avoid the double precision
                    // the following line will not work as you intend
                    // anchorInfo[endId].Remove(-anchorInfo[pointer][0]);
                    anchorVecCount[endId] = anchorVecCount[endId] - 1;
                }
                anchorVecCount.RemoveAt(pointer);
                anchors.RemoveAt(pointer);
                anchorInfo.RemoveAt(pointer);
                // a safe lock
                // if there is always an anchor with single outgoing vector
                // and it cannot find a corresponding point to form a segment with 
                // there will be infinite loops. working on how to solve this
                if (counter > 200)
                    break;
            }

            // There is no need to keep the tree structure
            List<gbSeg> grids = new List<gbSeg>();

            for (int i = 0; i < anchors.Count; i++)
            {
                foreach (gbXYZ vec in anchorInfo[i])
                {
                    //Debug.Print($"Begin {anchors[i]} No.{i} at vec-{anchorInfo[i].IndexOf(vec)} {vec}");
                    double max = double.PositiveInfinity;
                    int endId = -1;
                    for (int j = 0; j < anchors.Count; j++)
                    {
                        
                        double thetaBetween = Math.Abs(GBMethod.VectorAngle2D(anchors[j] - anchors[i], vec) - 90);
                        if (thetaBetween < 85)
                        {
                            //Debug.Print($"Point {anchors[j]} fails term 1 with {GBMethod.VectorAngle2D(anchors[j] - anchors[i], vec)}");
                            continue;
                        }
                        if (i != j && IsJointPaired(anchorInfo[j], -vec, 2))
                        {
                            double distance = DistanceToRay(anchors[j], anchors[i], vec, out double stretch);
                            //Debug.Print($"----[j] {anchors[j]} with d: {distance} s: {stretch}");
                            if (distance < 0.2 && stretch > 0)
                            {
                                if (stretch < max)
                                {
                                    max = stretch;
                                    endId = j;
                                }
                                //Debug.Print("Accepted");
                            }
                        }
                        //else
                        //{
                        //    Debug.Write($"Point {anchors[j]} No.{j} fails term 2 with");
                        //    foreach (gbXYZ dd in anchorInfo[j])
                        //        Debug.Write($"{dd}-{GBMethod.VectorAngle2D(dd, -vec)}-");
                        //    Debug.Write("\n");
                        //}
                    }

                    if (endId >= 0)
                    {
                        grids.Add(new gbSeg(anchors[i], anchors[endId]));

                        // if not removing the retrospective vector, there will be duplicated segments
                        // which means the original lattice grid will be doubled
                        // then we only need to sort out the double segments as lattice grid
                        // this may prevent a situation that if joint A found a wrong B, it will not affect
                        // the joint B to find the right A. Therefore, a retrospective connection is established

                        //for (int j = anchorInfo[endId].Count - 1; j >= 0; j--)
                        //{
                        //    if (VecAlmostTheSame(anchorInfo[endId][j], -vec, tolerance))
                        //        anchorInfo[endId].RemoveAt(j);
                        //}
                    }
                    //else
                    //{
                    //    Debug.Print("One lost anchor vector has been fount");
                    //}
                }
            }

            List<gbSeg> lattice = new List<gbSeg>();

            for (int i = grids.Count - 1; i >= 0; i--)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    if (IsSegReciprocal(grids[i], grids[j], tolerance))
                    {
                        //lattice.RemoveAt(i);
                        lattice.Add(grids[i]);
                        break;
                    }
                }
            }
            Debug.Print("PointAlign:: lattice count after skim: " + lattice.Count);


            return lattice;
        }

        #region support functions
        
        public static gbXYZ RoundVec(gbXYZ vec, double tolerance)
        {
            if (Math.Abs(vec.X) < tolerance)
                vec.X = 0;
            if (Math.Abs(vec.Y) < tolerance)
                vec.Y = 0;
            if (Math.Abs(vec.Z) < tolerance)
                vec.Z = 0;
            return vec;
        }



        public static double DistanceToRay(gbXYZ pt, gbXYZ origin, gbXYZ vec)
        {
            double dx = vec.X;
            double dy = vec.Y;

            // Calculate the t that minimizes the distance.
            double t = ((pt.X - origin.X) * dx + (pt.Y - origin.Y) * dy) /
              (dx * dx + dy * dy);

            dx = pt.X - (origin.X + t * dx);
            dy = pt.Y - (origin.Y + t * dy);
            return Math.Sqrt(dx * dx + dy * dy);
        }


        public static double DistanceToLine(gbXYZ pt, gbSeg line,
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


        public static void BubbleSortPtsOnX(List<gbXYZ> pts, List<int> idx)
        {
            gbXYZ tempPt;
            int tempId;
            for (int i = 0; i < pts.Count; i++)
            {
                for (int j = 0; j < pts.Count - i - 1; j++)
                {
                    if (pts[j].X > pts[j + 1].X)
                    {
                        tempPt = pts[j + 1];
                        pts[j + 1] = pts[j];
                        pts[j] = tempPt;
                        tempId = idx[j + 1];
                        idx[j + 1] = idx[j];
                        idx[j] = tempId;
                    }
                }
            }
        }

        public static bool PtAlmostTheSame(gbXYZ pt1, gbXYZ pt2, double tolerance)
        {
            if (Math.Abs(pt1.X - pt2.X) < tolerance &&
              Math.Abs(pt1.Y - pt2.Y) < tolerance &&
              Math.Abs(pt1.Z - pt2.Z) < tolerance)
                return true;
            else
                return false;
        }

        public static bool VecAlmostTheSame(gbXYZ vec1, gbXYZ vec2, double tolerance)
        {
            if (Math.Abs(vec1.X - vec2.X) < tolerance &&
              Math.Abs(vec1.Y - vec2.Y) < tolerance &&
              Math.Abs(vec1.Z - vec2.Z) < tolerance)
                return true;
            else
                return false;
        }

        //public static bool IsLoadable(List<gbXYZ> vecs, gbXYZ newVec, double tolerance)
        //{
        //    int counter = 0;
        //    foreach (gbXYZ vec in vecs)
        //    {
        //        if (VecAlmostTheSame(vec, newVec, tolerance))
        //            counter++;
        //    }
        //    if (counter > 0)
        //        return false;
        //    else
        //        return true;
        //}

        public static bool IsIncluded(List<gbXYZ> vecs, gbXYZ newVec, double tolerance)
        {
            foreach (gbXYZ vec in vecs)
                if (VecAlmostTheSame(vec, newVec, tolerance))
                    return true;
            return false;
        }

        public static bool IsJointPaired(List<gbXYZ> vecs, gbXYZ newVec, double angle)
        {
            foreach (gbXYZ vec in vecs)
                if (GBMethod.VectorAngle2D(vec, newVec) < angle)
                    return true;
            return false;
        }

        public static bool IsSegReciprocal(gbSeg a, gbSeg b, double tolerance)
        {
            if (PtAlmostTheSame(a.PointAt(0), b.PointAt(1), tolerance) &&
                PtAlmostTheSame(a.PointAt(1), b.PointAt(0), tolerance))
                return true;
            return false;
        }

        public static double DistanceToRay(
            gbXYZ pt, gbXYZ origin, gbXYZ vec, out double stretch)
        {
            double dx = vec.X;
            double dy = vec.Y;

            // Calculate the t that minimizes the distance.
            double t = ((pt.X - origin.X) * dx + (pt.Y - origin.Y) * dy) /
              (dx * dx + dy * dy);

            gbXYZ closest = new gbXYZ(origin.X + t * dx, origin.Y + t * dy, 0);
            dx = pt.X - (origin.X + t * dx);
            dy = pt.Y - (origin.Y + t * dy);
            //stretch = t * Math.Sqrt(dx * dx + dy * dy);
            stretch = closest.DistanceTo(origin);
            if (t < 0)
                stretch = -stretch;
            //Rhino.RhinoApp.WriteLine("this distance is: " + stretch.ToString());
            return Math.Sqrt(dx * dx + dy * dy);
        }

        #endregion
    }
}
