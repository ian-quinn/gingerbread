using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using Gingerbread.Core;
using static Gingerbread.Core.JsonSchema;

namespace Gingerbread.Core
{
    public static class RegionDetect2
    {
        public static void GetRegion(List<gbSeg> lines, int levelId, int blockId, int groupId, 
            out List<gbRegion> regions)
        {
            regions = new List<gbRegion>() { };
            // assuming we have perfect trimmed line segment set.

            // perform self intersection and get all shatters
            // OUTPUT List<Line> shatters

            List<gbSeg> shatters = GBMethod.ShatterSegs(lines);

            // trim all orphan edges untill there is no vertice with 0 degree
            int[,] _adjMat = GetAdjMatBidirection(shatters, 0.000001, out gbXYZ[] _vts, out int[] _degrees);
            List<int> edge_remove = new List<int>() { };
            List<int> vt_remove = new List<int>() { };
            while (_degrees.Contains(1))
            {
                // remove vertex with 1 degree
                // deduct 1 degree from adjacent connected vertex
                // remove the edge, e.g. connection between
                for (int m = 0; m < _adjMat.GetLength(0); m++)
                {
                    if (vt_remove.Contains(m)) continue;
                    if (_degrees[m] == 1)
                    {
                        for (int n = 0; n < _adjMat.GetLength(1); n++)
                        {
                            if (vt_remove.Contains(n)) continue;
                            if (_adjMat[m, n] >= 0)
                            {
                                vt_remove.Add(m);
                                _degrees[m] = 0;
                                _degrees[n] = _degrees[n] - 1;
                                edge_remove.Add(_adjMat[m, n]);
                                _adjMat[m, n] = -1;
                                _adjMat[n, m] = -1;
                            }
                        }
                    }
                }
            }

            List<gbSeg> trimmed = new List<gbSeg>() { };
            for (int i = 0; i < _adjMat.GetLength(0); i++)
            {
                if (vt_remove.Contains(i)) continue;
                for (int j = i; j < _adjMat.GetLength(1); j++)
                {
                    if (vt_remove.Contains(j)) continue;
                    if (_adjMat[i, j] >= 0)
                    {
                        gbSeg newEdge = new gbSeg(_vts[i], _vts[j]);
                        if (newEdge.Length > 0)
                            trimmed.Add(newEdge);
                    }
                }
            }

            List<gbSeg> edges = trimmed;

            // build a directed graph representing all edges
            // double the shatters list then make them reversed "half curve"
            int[,] adjMat = GetAdjMatBidirection(edges, 0.000001, out gbXYZ[] vts, out int[] degrees);
            List<gbSeg> edge_reversed = new List<gbSeg>() { };
            for (int i = 0; i < edges.Count; i++)
            {
                edge_reversed.Add(new gbSeg(edges[i].PointAt(1), edges[i].PointAt(0)));
            }
            edges.AddRange(edge_reversed);

            // set a list marking those edges that have been traversed
            // traverse all edges by looking up the edge index in adjMat
            // from row index to column index to find the next vertice
            // search for the first edge rotating clockwise
            //int[] edge_traversed = new int[edges.Count];
            List<int> edge_remain = new List<int>() { };
            // a list marking the belonging of each edge
            // projecting each edge to its edgeLoop
            List<int> edge_belonging = new List<int>() { };

            for (int i = 0; i < edges.Count; i++)
            {
                //edge_traversed[i] = 0;
                edge_remain.Add(i);
                edge_belonging.Add(-1);
            }

            List<List<gbXYZ>> vtLoops = new List<List<gbXYZ>>() { };
            List<List<int>> edgeLoops = new List<List<int>>() { };
            List<List<string>> tagLoops = new List<List<string>>() { };
            int shellId = 0;

            int counter = 0;
            while (edge_remain.Count > 0)
            {
                counter++;

                // let's say starting from edge_remain[0]
                int edge_initiate = edge_remain[0];
                int vt_start = LookupEdgeVts(adjMat, edge_initiate)[0];
                int vt_current = vt_start;
                int vt_next = LookupEdgeVts(adjMat, edge_initiate)[1];

                // the list recording enclosed edges, which will be removed at the end of loop
                List<int> edgeLoop = new List<int>() { edge_initiate };

                // stop the traverse once the current vertex index is the starting vertex
                // or the vertex has degree 1, which means it connects to nothing, orphan point
                while (vt_next != vt_start)
                {
                    // this happens at the current vertex
                    gbXYZ baseDir = vts[vt_current] - vts[vt_next];
                    int[] out_edge_ids = LookupEdgeOut(adjMat, vt_next, vt_current);
                    double min_angle = double.PositiveInfinity;
                    int min_edge_id = -1;
                    for (int i = 0; i < out_edge_ids.Length; i++)
                    {
                        // right-hand rule counter-clockwise from vecA to vecB
                        double delta_angle = GBMethod.VectorAngle2PI(
                            edges[out_edge_ids[i]].Direction, baseDir);
                        if (delta_angle < min_angle)
                        {
                            min_angle = delta_angle;
                            min_edge_id = out_edge_ids[i];
                        }
                    }
                    if (min_edge_id >= 0)
                    {
                        edgeLoop.Add(min_edge_id);
                        vt_current = vt_next;
                        vt_next = LookupEdgeVts(adjMat, min_edge_id)[1];
                    }
                    else
                    {
                        break;
                    }
                }

                // create vertex list for this region
                // create the boundary condition tag for each edge
                List<gbXYZ> vtLoop = new List<gbXYZ>() { };
                List<string> tagLoop = new List<string>() { };
                
                foreach (int edge_id in edgeLoop)
                {
                    vtLoop.Add(edges[edge_id].PointAt(1));
                    edge_belonging[edge_id] = edgeLoops.Count;
                }
                edgeLoops.Add(edgeLoop);
                vtLoop.Insert(0, edges[edgeLoop[0]].PointAt(0));
                // mark the shell loop
                if (GBMethod.IsClockwise(vtLoop))
                {
                    shellId = vtLoops.Count;
                }
                vtLoops.Add(vtLoop);

                // remove traversed edges
                foreach (int index in edgeLoop)
                {
                    edge_remain.Remove(index);
                }
            }

            // iterate all recognized polylines
            // update boundary condition tags, create regions
            for (int i = 0; i < vtLoops.Count; i++)
            {
                List<string> tagLoop = new List<string>() { };

                // vtLoops[i] is closed, the edge number equals to vertex number - 1
                for (int j = 0; j < vtLoops[i].Count - 1; j++)
                {
                    int thisEdgeId = edgeLoops[i][j];
                    int adjEdgeId = thisEdgeId < edges.Count / 2 ? thisEdgeId + edges.Count / 2 : thisEdgeId - edges.Count / 2;
                    int adjPolyId = edge_belonging[adjEdgeId];
                    int adjEdgeSequence = edgeLoops[adjPolyId].IndexOf(adjEdgeId);
                    if (i == shellId)
                        // the shell region is a place holder for the entire process
                        // it will not appear in the final gbXML serialized. so, there must be 
                        // label gaps of spaces, like from ::Z1 jumping to Z3
                        // however, in this method, there can only be one invalid zone generated
                        // which means, the gapping happens only once, ideally

                        // thus, the adjacency label of the shell region is not that important
                        // however, marking their pairred edges of inner regions is a good convention
                        tagLoop.Add($"F{levelId}::B{blockId}::G{groupId}::Z{adjPolyId}::Wall_{adjEdgeSequence}");
                    else
                        if (adjPolyId == shellId)
                            tagLoop.Add($"F{levelId}::B{blockId}::G{groupId}::Z{adjPolyId}::Outside_{adjEdgeSequence}");
                        else
                            tagLoop.Add($"F{levelId}::B{blockId}::G{groupId}::Z{adjPolyId}::Wall_{adjEdgeSequence}");
                }

                gbRegion newRegion = new gbRegion(
                    $"F{levelId}::B{blockId}::G{groupId}::Z{i}", vtLoops[i], tagLoop);
                if (i == shellId)
                {
                    newRegion.isShell = true;
                    regions.Insert(0, newRegion);
                }
                else
                    regions.Add(newRegion);
            }

            return;
        }

        public static List<gbXYZ> GetShell(List<gbSeg> lines)
        {
            List<gbSeg> shatters = GBMethod.ShatterSegs(lines);

            // trim all orphan edges untill there is no vertice with 0 degree
            int[,] _adjMat = GetAdjMatBidirection(shatters, 0.000001, out gbXYZ[] _vts, out int[] _degrees);
            List<int> edge_remove = new List<int>() { };
            List<int> vt_remove = new List<int>() { };
            while (_degrees.Contains(1))
            {
                // remove vertex with 1 degree
                // deduct 1 degree from adjacent connected vertex
                // remove the edge, e.g. connection between
                for (int m = 0; m < _adjMat.GetLength(0); m++)
                {
                    if (vt_remove.Contains(m)) continue;
                    if (_degrees[m] == 1)
                    {
                        for (int n = 0; n < _adjMat.GetLength(1); n++)
                        {
                            if (vt_remove.Contains(n)) continue;
                            if (_adjMat[m, n] >= 0)
                            {
                                vt_remove.Add(m);
                                _degrees[m] = 0;
                                _degrees[n] = _degrees[n] - 1;
                                edge_remove.Add(_adjMat[m, n]);
                                _adjMat[m, n] = -1;
                                _adjMat[n, m] = -1;
                            }
                        }
                    }
                }
            }

            List<gbSeg> trimmed = new List<gbSeg>() { };
            for (int i = 0; i < _adjMat.GetLength(0); i++)
            {
                if (vt_remove.Contains(i)) continue;
                for (int j = i; j < _adjMat.GetLength(1); j++)
                {
                    if (vt_remove.Contains(j)) continue;
                    if (_adjMat[i, j] >= 0)
                    {
                        gbSeg newEdge = new gbSeg(_vts[i], _vts[j]);
                        if (newEdge.Length > 0)
                            trimmed.Add(newEdge);
                    }
                }
            }

            List<gbSeg> edges = trimmed;

            // build a directed graph representing all edges
            // double the shatters list then make them reversed "half curve"
            int[,] adjMat = GetAdjMatBidirection(edges, 0.000001, out gbXYZ[] vts, out int[] degrees);
            List<gbSeg> edge_reversed = new List<gbSeg>() { };
            for (int i = 0; i < edges.Count; i++)
            {
                edge_reversed.Add(new gbSeg(edges[i].PointAt(1), edges[i].PointAt(0)));
            }
            edges.AddRange(edge_reversed);

            // set a list marking those edges that have been traversed
            // traverse all edges by looking up the edge index in adjMat
            // from row index to column index to find the next vertice
            // search for the first edge rotating clockwise
            //int[] edge_traversed = new int[edges.Count];
            List<int> edge_remain = new List<int>() { };
            List<gbXYZ> shell = new List<gbXYZ>() { };
            for (int i = 0; i < edges.Count; i++)
            {
                //edge_traversed[i] = 0;
                edge_remain.Add(i);
            }

            int counter = 0;
            while (edge_remain.Count > 0)
            {
                counter++;

                // let's say starting from edge_remain[0]
                int edge_initiate = edge_remain[0];
                int vt_start = LookupEdgeVts(adjMat, edge_initiate)[0];
                int vt_current = vt_start;
                int vt_next = LookupEdgeVts(adjMat, edge_initiate)[1];

                // the list recording enclosed edges, which will be removed at the end of loop
                List<int> edge_polygon = new List<int>() { edge_initiate };

                // stop the traverse once the current vertex index is the starting vertex
                // or the vertex has degree 1, which means it connects to nothing, orphan point
                while (vt_next != vt_start)
                {
                    // this happens at the current vertex
                    gbXYZ baseDir = vts[vt_current] - vts[vt_next];
                    int[] out_edge_ids = LookupEdgeOut(adjMat, vt_next, vt_current);
                    double min_angle = double.PositiveInfinity;
                    int min_edge_id = -1;
                    for (int i = 0; i < out_edge_ids.Length; i++)
                    {
                        // right-hand rule counter-clockwise from vecA to vecB
                        double delta_angle = GBMethod.VectorAngle2PI(
                            edges[out_edge_ids[i]].Direction, baseDir);
                        if (delta_angle < min_angle)
                        {
                            min_angle = delta_angle;
                            min_edge_id = out_edge_ids[i];
                        }
                    }
                    if (min_edge_id >= 0)
                    {
                        edge_polygon.Add(min_edge_id);
                        vt_current = vt_next;
                        vt_next = LookupEdgeVts(adjMat, min_edge_id)[1];
                    }
                    else
                    {
                        break;
                    }
                }

                // create polyline
                List<gbXYZ> poly_vts = new List<gbXYZ>() { };
                foreach (int index in edge_polygon)
                {
                    poly_vts.Add(edges[index].PointAt(1));
                }
                poly_vts.Insert(0, edges[edge_polygon[0]].PointAt(0));

                // if this poly is clockwise, return it as the shell
                if (GBMethod.IsClockwise(poly_vts))
                    return poly_vts;

                // remove traversed edges
                foreach (int index in edge_polygon)
                {
                    edge_remain.Remove(index);
                }
            }

            return shell;
        }

        private static int[,] GetAdjMatBidirection(List<gbSeg> edges, double _eps,
            out gbXYZ[] vts, out int[] degrees)
        {
            List<gbXYZ> _vts = new List<gbXYZ>() { };
            List<int> _degrees = new List<int>() { };
            foreach (gbSeg line in edges)
            {
                _vts.Add(line.PointAt(0));
                _vts.Add(line.PointAt(1));
                _degrees.Add(1);
                _degrees.Add(1);
            }

            //Rhino.Geometry has available function to do this
            //Point3d[] vts_ = Point3d.CullDuplicates(vts, 0.0001);

            for (int i = _vts.Count - 1; i >= 1; i--)
            {
                for (int j = i - 1; j >= 0; j--)
                {
                    if ((_vts[i] - _vts[j]).Norm() < _eps)
                    {
                        _vts.RemoveAt(i);
                        _degrees[j] += _degrees[i];
                        _degrees.RemoveAt(i);
                        break;
                    }
                }
            }

            // array is just a reminder that this data cannot be revised
            vts = _vts.ToArray();
            degrees = _degrees.ToArray();

            // rule: all edges start from row index, and end at column index
            // all duplicated edges will append to the original edge list
            int[,] adjMat = new int[vts.Length, vts.Length];

            // initiation with -1, index referring to nothing, indicating disconnected
            for (int i = 0; i < adjMat.GetLength(0); i++)
            {
                for (int j = 0; j < adjMat.GetLength(1); j++)
                {
                    adjMat[i, j] = -1;
                }
            }

            for (int i = 0; i < edges.Count; i++)
            {
                int id_1 = -1;
                int id_2 = -1;
                // look up for the vertices in vts array
                // this is rather time consuming, only for test
                for (int j = 0; j < vts.Length; j++)
                {
                    if (edges[i].PointAt(0).DistanceTo(vts[j]) < _eps)
                        id_1 = j;
                    if (edges[i].PointAt(1).DistanceTo(vts[j]) < _eps)
                        id_2 = j;
                }
                if (id_1 >= 0 && id_2 >= 0)
                {
                    // at this step, duplicate the edge with its reversed version
                    // the direction is always from left to right, e.g. from row to column index
                    // remember to duplicate the list edges so that the index can match automatically
                    adjMat[id_1, id_2] = i;
                    adjMat[id_2, id_1] = i + edges.Count;
                }
            }
            return adjMat;
        }

        /// <summary>
        /// Return the pair of start/end vertex id of an edge
        /// </summary>
        private static int[] LookupEdgeVts(int[,] adjMat, int edge_id)
        {
            // the edge index can only appear once in this matrix
            for (int i = 0; i < adjMat.GetLength(0); i++)
            {
                for (int j = 0; j < adjMat.GetLength(1); j++)
                {
                    if (adjMat[i, j] == edge_id)
                    {
                        return new int[2] { i, j };
                    }
                }
            }
            return new int[2] { -1, -1 };
        }

        /// <summary>
        /// Return the ids of outgoing edges from a vertex
        /// </summary>
        private static int[] LookupEdgeOut(int[,] adjMat, int vt, int vt_in)
        {
            List<int> out_edge_id = new List<int>() { };
            for (int i = 0; i < adjMat.GetLength(0); i++)
            {
                if (i == vt)
                {
                    for (int j = 0; j < adjMat.GetLength(1); j++)
                    {
                        if (adjMat[i, j] != -1 && j != vt_in)
                            out_edge_id.Add(adjMat[i, j]);
                    }
                }
            }
            return out_edge_id.ToArray();
        }


        public static void GetMCR(List<List<gbRegion>> nestedRegion)
            // nest all loops
            //List<List<gbXYZ>> nestedShell // out List<List<List<gbXYZ>>> mcrs)
        {
            List<Tuple<int, int>> containRef = new List<Tuple<int, int>>(); // containment relations
            List<int> roots = new List<int>(); // shell index as root node
            List<int> branches = new List<int>(); // branches[0] encloses branches[1] encloses branches[2]

            // iterate to find all containment relations
            for (int i = 0; i < nestedRegion.Count; i++)
            {
                // nestedRegion[i].Count must > 1 to avoid [0] indexation incurs 'System.ArgumentOutOfRangeException'
                if (nestedRegion[i].Count == 0)
                    continue;

                // to prevent null shells exist
                if (nestedRegion[i][0].loop.Count == 0)
                    continue;
                for (int j = i + 1; j < nestedRegion.Count; j++)
                {
                    // nestedRegion[j][0] may trigger 'System.ArgumentOutOfRangeException'
                    if (nestedRegion[j].Count == 0)
                        continue;

                    if (GBMethod.IsPtInPoly(nestedRegion[i][0].loop[0], nestedRegion[j][0].loop, false) == true)
                    {
                        containRef.Add(Tuple.Create(j, i));
                        if (!branches.Contains(i))
                            branches.Add(i);
                        continue;
                    }
                    if (GBMethod.IsPtInPoly(nestedRegion[j][0].loop[0], nestedRegion[i][0].loop, false) == true)
                    {
                        containRef.Add(Tuple.Create(i, j));
                        if (!branches.Contains(j))
                            branches.Add(j);
                        continue;
                    }
                }
            }

            // locate root nodes
            for (int i = 0; i < nestedRegion.Count; i++)
            {
                if (!branches.Contains(i))
                    roots.Add(i);
            }

            // DEBUG
            //Debug.Write("SpaceDetect:: " + "Roots: ");
            //foreach (int num in roots)
            //    Debug.Write(num.ToString() + ", ");
            //Debug.Write("\n");
            //Debug.Write("SpaceDetect:: " + "Branches: ");
            //foreach (int num in branches)
            //    Debug.Write(num.ToString() + ", ");
            //Debug.Write("\n");
            //foreach (Tuple<int, int> idx in containRef)
            //{
            //    Debug.Print("SpaceDetect:: " + "Containment: ({0}, {1})", idx.Item1, idx.Item2);
            //}

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
            // not possible to have a nesting over 10 levels
            while (containRef.Count > 0 && safeLock < 10)
            {
                //Debug.Print("SpaceDetect:: " + "Iteration at: " + safeLock.ToString() + " with " + containRef.Count.ToString() + "chains.");
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
            //Debug.Print("SpaceDetect:: " + "Num of Chains " + chains.Count.ToString());
            //foreach (List<int> chain in chains)
            //{
            //    Debug.Write("SpaceDetect:: " + "Chain-" + chains.IndexOf(chain).ToString());
            //    Debug.Write(" ::Index-");
            //    foreach (int num in chain)
            //        Debug.Write(num.ToString() + ", ");
            //    Debug.Write("\n");
            //}

            int depth = 0;
            foreach (List<int> chain in chains)
            {
                if (nestedRegion[chain.Last()].Count == 2)
                    if (GBMethod.GetPolyArea(nestedRegion[chain.Last()][0].loop) < 2)
                    {
                        // the nestRegion with only one region
                        // one is the clockwise region and the other is the counter-clockwise shell
                        nestedRegion[chain.Last()][0].loop = null;
                        nestedRegion[chain.Last()][1].loop = null;
                        nestedRegion[chain.Last()][0].isShell = false;
                        chain.RemoveAt(chain.Count - 1);
                        //Debug.Print("SpaceDetect:: Remove one region with too small area");
                    }
                if (chain.Count > depth)
                {
                    depth = chain.Count;
                }
            }
            //Debug.Print("SpaceDetect:: Containment chain has depth: " + depth);
            // DEBUG
            // Rhino.RhinoApp.WriteLine("Note the depth of tree is: " + depth.ToString());
            // foreach (List<Point3d[]> item in groups)
            // {
            //   Rhino.RhinoApp.WriteLine("Length of the first " + item.Count.ToString());
            // }

            // Prepare regex for label decoding
            var pattern = "(.+)::(.+)";

            // generate Point Array pairs for multi-connected region
            // List<List<List<gbXYZ>>> mcrs = new List<List<List<gbXYZ>>>();
            List<string> mcrParentLabel = new List<string>();
            for (int i = 1; i < depth; i++)
            {
                foreach (List<int> chain in chains)
                {
                    if (i < chain.Count)
                    {
                        // loop through the parent group to find the right parent loop
                        foreach (gbRegion region in nestedRegion[chain[i - 1]])
                        {
                            if (region.isShell == true)
                                continue;
                            // to prevent there is null region with no data at all
                            if (region.loop.Count == 0)
                                continue;
                            // check if the shell at this level is enclosed by any loop of the parent level
                            // if true, generate MCR and switch the current shell's isShell attribute to false
                            if (GBMethod.IsPtInPoly(nestedRegion[chain[i]][0].loop[0], region.loop, false))
                            {
                                // mcr.Add(region.loop); // add the parent loop
                                if (region.innerLoops == null)
                                {
                                    region.innerLoops = new List<List<gbXYZ>>();
                                    region.innerMatchs = new List<List<string>>();
                                }
                                // add this shell loop to the parent region as innerLoops
                                region.innerLoops.Add(nestedRegion[chain[i]][0].loop);
                                // 20230613 retrospect
                                // in fact, innerMatchs does not affect the result
                                // during gbXML serialization, there is only one partition between 
                                // two spaces. the walls of subRegions will created instead of the 
                                // walls of such holes. 
                                region.innerMatchs.Add(nestedRegion[chain[i]][0].match);
                                region.InitializeMCR();

                                foreach (gbRegion subRegion in nestedRegion[chain[i]])
                                {
                                    if (subRegion.isShell)
                                        continue;
                                    for (int j = 0; j < subRegion.match.Count; j++)
                                    {
                                        if (subRegion.match[j].Contains("Outside"))
                                        {
                                            Match match = Regex.Match(subRegion.match[j], pattern);
                                            string appendix = match.Groups[2].Value.Split('_')[1];
                                            // 20230613 retrospect
                                            // the label of matches are not inportant. this is only a mark
                                            // used for finding the adjacency relation between two spaces
                                            // after binding them together, this label will be abandoned
                                            subRegion.match[j] = region.label + "::Wall" + (region.innerLoops.Count - 1) + "_" + appendix;
                                        }
                                    }
                                }
                                nestedRegion[chain[i]][0].isShell = false; // or just delete it

                                string parentLabel = chain[i - 1].ToString() + ":" +
                                  nestedRegion[chain[i - 1]].IndexOf(region).ToString(); // which loop in which group
                                mcrParentLabel.Add(parentLabel);
                                //mcrs.Add(mcr);
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

            // me embed following belonging relations in the gbRegion.innerLoops

            // merge mcr for those share the same parent polyline
            // this creates mcr with multiple holes
            //for (int i = mcrs.Count - 1; i >= 0; i--)
            //{
            //    for (int j = i - 1; j >= 0; j--)
            //    {
            //        if (mcrParentLabel[j] == mcrParentLabel[i])
            //        {
            //            mcrs[i].RemoveAt(0);
            //            mcrs[j].AddRange(mcrs[i]);
            //            mcrs.RemoveAt(i);
            //        }
            //    }
            //}

            return;
        }

    }
}
