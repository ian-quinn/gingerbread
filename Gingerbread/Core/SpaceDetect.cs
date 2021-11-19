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
    public static class SpaceDetect
    {
        public static object Rhino { get; private set; }

        // this function is only for the sorted, grouped, fixed wall centerlines
        // which will be generalized to the entire floorplan. For floorplan, there will be
        // nested lists of points representing boundaries of each space
        // nested lists of points representing boundaries of each floor slab (there may be multiple isolated slabs)
        // nested lists of strings representing the surface matching relationships.
        // the surface matching across different levels will not be covered here
        public static void GetRegion(List<gbSeg> lines, int levelId, int groupId, out List<gbRegion> regions,
            out List<gbXYZ> shell, out List<List<gbSeg>> orphans)
        {

            List<gbXYZ> Vtc = new List<gbXYZ>(); // all unique vertices
            List<gbSeg> HC = new List<gbSeg>(); // list of all shattered half-curves
            List<int> HCI = new List<int>(); // half curve indices
            List<int> HCO = new List<int>(); // half curve reversed
            List<int> HCN = new List<int>(); // next index for each half-curve (conter-clockwise)
            List<int> HCV = new List<int>(); // vertex representing this half-curve
            List<int> HCF = new List<int>(); // half-curve face
            List<gbXYZ> HCPln = new List<gbXYZ>();
            List<bool> HCK = new List<bool>(); // mark if a half-curve needs to be killed
                                               // (if it either starts or ends hanging, but does not exclude redundant curves that not exclosing a room)
            Dictionary<int, List<gbSeg>> F = new Dictionary<int, List<gbSeg>>(); // data tree for faces
            Dictionary<int, List<int>> FIdx = new Dictionary<int, List<int>>();
            Dictionary<int, List<int>> VOut = new Dictionary<int, List<int>>(); // data tree of outgoing half-curves from each vertex

            foreach (gbSeg line in lines) // cycle through each curve
            {
                for (int CRun = 0; CRun <= 2; CRun += 2) // create two half-curves: first in one direction, and then the other...
                {
                    gbXYZ testedPt = line.PointAt(0);

                    HC.Add(line);
                    HCI.Add(HCI.Count); // count this iteration
                    HCO.Add(HCI.Count - CRun); // a little index trick
                    HCN.Add(-1);
                    HCF.Add(-1);
                    HCK.Add(false);

                    int VtcSet = -1;

                    for (int VtxCheck = 0; VtxCheck <= Vtc.Count - 1; VtxCheck++)
                    {
                        if (Vtc[VtxCheck].DistanceTo(testedPt) < 1e-6) // set to a global value!!
                        {
                            VtcSet = VtxCheck; // get the vertex index, if it already exists
                            break;
                        }
                    }

                    if (VtcSet > -1)
                    {
                        HCV.Add(VtcSet); // If the vertex already exists, set the half-curve vertex
                        VOut[VtcSet].Add(HCI.Last());
                    }
                    else
                    {
                        HCV.Add(Vtc.Count); // if the vertex doesn't already exist, add a new vertex index
                        VOut.Add(Vtc.Count, new List<int>() { HCI.Last() });
                        // add the new half-curve index to the list of outgoing half-curves associated with the vertex
                        Vtc.Add(testedPt);
                        // add the new vertex to the vertex list
                    }
                    HCPln.Add(line.Direction);

                    // reverse the curve for creating the opposite
                    // half - curve in the second part of the loop
                    line.Reverse();
                }
            }

            // For each Vertex that has only one outgoing half-curve, kill the half-curve and its opposite
            foreach (KeyValuePair<int, List<int>> path in VOut)
            {
                //Debug.Print("This point has been connected to " + path.Value.Count.ToString() + " curves");
                if (path.Value.Count == 1)
                {
                    HCK[path.Value[0]] = true;
                    HCK[HCO[path.Value[0]]] = true;
                }
            }


            // Find the "next" half-curve for each starting half curve by
            // identifying the outgoing half-curve from the end vertex
            // that presents the smallest angle by calculating its plane's x-axis angle
            // from x-axis of the starting half-curve's opposite plane
            foreach (int HCIdx in HCI)
            {
                int minIdx = -1;
                double minAngle = 2 * Math.PI;
                //Debug.Print(VOut[HCV[HCO[HCIdx]]].Count().ToString());
                foreach (int HCOut in VOut[HCV[HCO[HCIdx]]])
                {
                    if (HCOut != HCO[HCIdx] & HCK[HCIdx] == false & HCK[HCOut] == false)
                    {
                        double testAngle = 2 * Math.PI - GBMethod.VectorAngle(HCPln[HCOut], HCPln[HCO[HCIdx]]);

                        //Rhino.RhinoApp.Write(testAngle.ToString() + "\n");
                        // The comparing order is important to ensure a right-hand angle under z-axis
                        if (testAngle < minAngle)
                        {
                            minIdx = HCOut;
                            minAngle = testAngle;
                        }
                    }
                }
                HCN[HCIdx] = minIdx;
            }


            // Sequence half-curves into faces by running along "next" half-curves in order
            // until the starting half-curve is returned to

            // this list contain the generated face with stray edge or polys not enclosed
            // which will be deleted. typically, a well trimmed and documented half line input
            // will produce no orhpan faces. the F dictionary only contains a counter-clockwise
            // outer shell and the rest clockwise enclosed regions.
            List<int> orphanId = new List<int>();
            // cycle through each half-curve
            foreach (int HCIdx in HCI)
            {
                int emExit = 0;
                if (HCF[HCIdx] == -1)
                {
                    int faceIdx = F.Count();
                    int currentIdx = HCIdx;
                    F.Add(faceIdx, new List<gbSeg>() { HC[currentIdx] });
                    FIdx.Add(faceIdx, new List<int>() { currentIdx });
                    HCF[currentIdx] = faceIdx;
                    do
                    {
                        // this denotes a half-curve 
                        if (HCN[currentIdx] == -1)
                        {
                            orphanId.Add(faceIdx);
                            //Util.LogPrint("Log 1 orphan for missing next HC " + orphanId.Count);
                            break;
                        }

                        currentIdx = HCN[currentIdx];
                        F[faceIdx].Add(HC[currentIdx]);
                        FIdx[faceIdx].Add(currentIdx);
                        HCF[currentIdx] = faceIdx;
                        if (HCN[currentIdx] == HCIdx)
                            break;
                        // emergency exit prevents infinite loops
                        emExit += 1;
                        if (emExit == lines.Count - 1)
                            break;
                    }
                    while (true);
                    // exit once the starting half-curve is reached again
                }
            }


            // this list cache the outer shell face id
            List<int> shellId = new List<int>();
            foreach (KeyValuePair<int, List<gbSeg>> kvp in F)
            {
                // if the face edges are not enclosed, regard them as orphans
                segIntersectEnum intersectionCheck = GBMethod.SegIntersection(
                    kvp.Value[0], kvp.Value.Last(), 0, out gbXYZ intersection, out double stretch);
                if (!(intersectionCheck == segIntersectEnum.IntersectOnBoth ||
                    intersectionCheck == segIntersectEnum.ColineJoint))
                {
                    orphanId.Add(kvp.Key);
                    //Util.LogPrint("Log 1 orphan for not enclosed " + orphanId.Count);
                    continue;
                }
                // if the loop is clockwise, regard it as outer shell
                // typically there's only one outer shell
                List<gbXYZ> ptLoop = SortPtLoop(kvp.Value);
                if (GBMethod.IsClockwise(ptLoop))
                    shellId.Add(kvp.Key);
            }

            //Util.LogPrint(Util.IntListToString(orphanId));
            //Util.LogPrint(Util.IntListToString(shellId));

            // crvLoops to cache all space boundaries. The lines are following counter-clockwise order around the space,
            // but the direction of each is random.
            List<List<gbSeg>> edgeLoops = new List<List<gbSeg>>();  // for debugging
            List<List<gbXYZ>> ptLoops = new List<List<gbXYZ>>();
            regions = new List<gbRegion>();
            // for debugging. considering to generate gbZone/gbSurface directly
            List<List<string>> infoLoops = new List<List<string>>();

            //int renumberOffset = 0;
            // only output those faces that haven't been identified as either the perimeter or open
            // note that the region loop should be counter-clockwise and closed and that's how we sort them out

            foreach (KeyValuePair<int, List<gbSeg>> kvp in F)
            {
                // if the face is orphan or outer shell skip the matching process
                if (orphanId.Contains(kvp.Key) || shellId.Contains(kvp.Key))
                {
                    //renumberOffset++;
                    ptLoops.Add(new List<gbXYZ>());
                    infoLoops.Add(new List<string>());
                    regions.Add(new gbRegion("F" + levelId + "::G" + groupId + "::Z" + kvp.Key.ToString(),
                        new List<gbXYZ>(), new List<string>()));
                    continue;
                }
                List<gbSeg> edgeLoop = new List<gbSeg>();
                List<string> infoLoop = new List<string>();
                for (int j = 0; j < kvp.Value.Count; j++)
                {
                    int adjCrvIdx = GetMatchIdx(FIdx[kvp.Key][j]);
                    FIdx.TryGetValue(HCF[adjCrvIdx], out List<int> adjFace);

                    string boundaryCondition;
                    // the matching code should follow the definition in gbZone and gbSurface
                    if (orphanId.Contains(HCF[adjCrvIdx]) || shellId.Contains(HCF[adjCrvIdx]))
                        boundaryCondition = "Outside";
                    else
                        //boundaryCondition = "Level_" + levelId + "::Zone_" + (HCF[adjCrvIdx] - renumberOffset).ToString() +
                        //    "::Wall_" + adjFace.IndexOf(adjCrvIdx).ToString();
                        boundaryCondition = "F" + levelId + "::G" + groupId + "::Z" + HCF[adjCrvIdx].ToString() +
                            "::Wall_" + adjFace.IndexOf(adjCrvIdx).ToString();

                    edgeLoop.Add(kvp.Value[j]);
                    infoLoop.Add(boundaryCondition);
                }
                edgeLoops.Add(edgeLoop);

                List<gbXYZ> ptLoop = SortPtLoop(edgeLoop);
                ptLoops.Add(ptLoop);

                infoLoops.Add(infoLoop);

                // additional for region test
                regions.Add(new gbRegion("F" + levelId + "::G" + groupId + "::Z" + kvp.Key.ToString(), ptLoop, infoLoop));
            }

            // the first item in shellId will be the output
            // ideally, this shell is the one and only
            List<gbXYZ> ptShell = new List<gbXYZ>();
            if (shellId.Count != 0)
                ptShell = SortPtLoop(F[shellId[0]]);
            List<List<gbSeg>> orphanLoops = new List<List<gbSeg>>();
            foreach (int id in orphanId)
                orphanLoops.Add(F[id]);

            // outputs
            //loops = ptLoops;
            orphans = orphanLoops;
            shell = ptShell;
            //match = infoLoops;

            //Util.LogPrint("Region detection result: " + loops.Count + " " + shell.Count + " " + match.Count);
        }

        public static void GetMCR(List<List<gbRegion>> nestedRegion, List<List<gbXYZ>> nestedShell,
            out List<List<List<gbXYZ>>> mcrs)
        {
            mcrs = new List<List<List<gbXYZ>>>();

            if (nestedRegion.Count != nestedShell.Count)
            {
                Debug.Print("The number of space groups and shells are not equal. This may due to the space detection failure");
                return;
            }

            List<Tuple<int, int>> containRef = new List<Tuple<int, int>>(); // containment relations
            List<int> roots = new List<int>(); // shell index as root node
            List<int> branches = new List<int>(); // shell being included

            // iterate to find all containment relations
            for (int i = 0; i < nestedShell.Count; i++)
            {
                for (int j = i + 1; j < nestedShell.Count; j++)
                {
                    if (GBMethod.IsPtInPoly(nestedShell[i][0], nestedShell[j]) == true)
                    {
                        containRef.Add(Tuple.Create(j, i));
                        if (!branches.Contains(i))
                            branches.Add(i);
                        continue;
                    }
                    if (GBMethod.IsPtInPoly(nestedShell[j][0], nestedShell[i]) == true)
                    {
                        containRef.Add(Tuple.Create(i, j));
                        if (!branches.Contains(j))
                            branches.Add(j);
                        continue;
                    }
                }
            }

            // locate root nodes
            for (int i = 0; i < nestedShell.Count; i++)
            {
                if (!branches.Contains(i))
                    roots.Add(i);
            }

            // DEBUG
            Debug.Write("Roots: ");
            foreach (int num in roots)
                Debug.Write(num.ToString() + ", ");
            Debug.Write("\n");
            Debug.Write("Branches: ");
            foreach (int num in branches)
                Debug.Write(num.ToString() + ", ");
            Debug.Write("\n");
            foreach (Tuple<int, int> idx in containRef)
            {
                Debug.Print("Containment: ({0}, {1})", idx.Item1, idx.Item2);
            }

            // create root nodes (creating containment tree)
            List<List<int>> chains = new List<List<int>>();
            for (int i = containRef.Count - 1; i >= 0; i--)
            {
                if (roots.Contains(containRef[i].Item1))
                {
                    List<int> chain = new List<int>() { containRef[i].Item1, containRef[i].Item2 };
                    chains.Add(chain);
                    containRef.RemoveAt(i);
                }
            }
            // create branch nodes (creating containment tree)
            int safeLock = 0;
            while (containRef.Count > 0 && safeLock < 10)
            {
                Debug.Print("Iteration at: " + safeLock.ToString() +
                  " with " + containRef.Count.ToString() + "chains.");
                int delChainIdx = -1;
                int delCoupleIdx = -1;
                foreach (var couple in containRef)
                {
                    foreach (List<int> chain in chains)
                    {
                        if (couple.Item2 == chain[chain.Count - 1])
                            delChainIdx = chains.IndexOf(chain);
                    }
                    foreach (List<int> chain in chains)
                    {
                        if (couple.Item1 == chain[chain.Count - 1])
                        {
                            delCoupleIdx = containRef.IndexOf(couple);
                            chain.Add(couple.Item2);
                        }
                    }
                }
                if (delChainIdx >= 0 && delCoupleIdx >= 0)
                {
                    chains.RemoveAt(delChainIdx);
                    containRef.RemoveAt(delCoupleIdx);
                }
                safeLock++;
            }

            // DEBUG
            Debug.Print("Num of Chains " + chains.Count.ToString());
            foreach (List<int> chain in chains)
            {
                Debug.Print("Chain-" + chains.IndexOf(chain).ToString());
                Debug.Write("Index: ");
                foreach (int num in chain)
                    Debug.Write(num.ToString() + ", ");
                Debug.Write("\n");
            }

            int depth = 0;
            foreach (List<int> chain in chains)
            {
                if (chain.Count > depth)
                {
                    depth = chain.Count;
                }
            }

            // DEBUG
            // Rhino.RhinoApp.WriteLine("Note the depth of tree is: " + depth.ToString());
            // foreach (List<Point3d[]> item in groups)
            // {
            //   Rhino.RhinoApp.WriteLine("Length of the first " + item.Count.ToString());
            // }

            // generate Point Array pairs for multi-connected region
            // List<List<List<gbXYZ>>> mcrs = new List<List<List<gbXYZ>>>();
            List<string> mcrParentLabel = new List<string>();
            for (int i = 1; i < depth; i++)
            {
                foreach (List<int> chain in chains)
                {
                    if (i < chain.Count)
                    {
                        List<List<gbXYZ>> mcr = new List<List<gbXYZ>>();
                        foreach (gbRegion region in nestedRegion[chain[i - 1]])
                        {
                            if (GBMethod.IsPtInPoly(nestedShell[chain[i]][0], region.loop))
                            {
                                mcr.Add(region.loop);
                                mcr.Add(nestedShell[chain[i]]);
                                string parentLabel = chain[i - 1].ToString() + ":" +
                                  nestedRegion[chain[i - 1]].IndexOf(region).ToString();
                                mcrParentLabel.Add(parentLabel);
                                mcrs.Add(mcr);
                            }
                        }
                    }
                }
            }
            // DEBUG
            // foreach (string label in mcrParentLabel)
            // {
            //   Rhino.RhinoApp.WriteLine("MCR label: " + label);
            // }

            // merge mcr for those share the same parent polyline
            // this creates mcr with multiple holes
            for (int i = mcrs.Count - 1; i >= 0; i--)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    if (mcrParentLabel[j] == mcrParentLabel[i])
                    {
                        mcrs[i].RemoveAt(0);
                        mcrs[j].AddRange(mcrs[i]);
                        mcrs.RemoveAt(i);
                    }
                }
            }

            return;
        }

        /// <summary>
        /// ONLY used after the region detect function
        /// the input crvs must follows the right order (counter-clockwise)
        /// </summary>
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
        /// Each half curve is stored with its reversed pair
        /// to get its pair you only need to find the opposite in the bundled two
        /// </summary>
        /// <param name="idx"></param>
        /// <returns></returns>
        private static int GetMatchIdx(int idx)
        {
            if (idx < 0)
                return -1;
            else if (idx % 2 == 0)
                return idx + 1;
            else
                return idx - 1;
        }

    }
}
