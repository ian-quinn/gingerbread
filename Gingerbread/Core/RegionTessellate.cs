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
    public static class RegionTessellate
    {
        /// <summary>
        /// Tessellate one multi-connected region by rectangular tiles. Clip the MCR step by step util nothing left.
        /// mcr[0] must be the outer shell represented by counter-clockwise loop of points that are closed.
        /// Rest of list in mcr must be the inner holes represented by clockwise loops of points that are closed.
        /// Output tiles are a list of rectangles during each step. Output remains are a list of regions remaining during each step.
        /// </summary>
        public static List<List<gbXYZ>> Rectangle(List<List<gbXYZ>> mcr)
        {
            List<List<gbXYZ>> tiles = new List<List<gbXYZ>>();
            List<List<List<gbXYZ>>> remains = new List<List<List<gbXYZ>>>();
            if (mcr.Count == 0)
                return tiles;
            //if (mcr[0].Count <= 5)
            if (mcr.Count == 1 && mcr[0].Count <= 5)
                return tiles;

            // a safe lock
            int counter = 0;
            while (counter < 50)
            {
                Debug.Print("RegionTessellate:: Tesselation initializing.. MCR with {0} loops", mcr.Count);

                if (counter == 0)
                {
                    foreach (gbXYZ pt in mcr[0])
                        Debug.Print($"{{{pt.X}, {pt.Y}, {pt.Z}}}");
                    Debug.Print("Now is the other");
                    foreach (gbXYZ pt in mcr[1])
                        Debug.Print($"{{{pt.X}, {pt.Y}, {pt.Z}}}");
                }

                Tessellate(mcr, out List<List<gbXYZ>> panel, out List<gbXYZ> tile);
                tiles.Add(tile);
                remains.Add(panel);
                Debug.Print("RegionTessellate:: Tile generated: " + tiles.Count);
                counter += 1;
                if (panel.Count == 1 && panel[0].Count <= 5)
                {
                    tiles.Add(panel[0]);
                    break;
                }
                mcr = panel;
            }

            return tiles;
        }

        public static void Tessellate(List<List<gbXYZ>> mcr, out List<List<gbXYZ>> panel, out List<gbXYZ> tile)
        {
            tile = new List<gbXYZ>();
            panel = new List<List<gbXYZ>>();
            if (mcr.Count == 0)
                return;
            //if (mcr[0].Count <= 5)
            if (mcr.Count == 1 && mcr[0].Count <= 5)
                return;

            // find out which loop is the outer shell
            int shellId = -1;
            for (int i = 0; i < mcr.Count; i++)
            {
                if (!GBMethod.IsClockwise(mcr[i]))
                {
                    shellId = i;
                    break;
                }
            }
            if (shellId == -1)
            {
                Debug.Print("There is no counter-clockwise shell in the MCR loops");
                return;
            }

            CircularLinkedList<gbXYZ> shell = new CircularLinkedList<gbXYZ>();
            for (int i = 0; i < mcr[shellId].Count - 1; i++)
            {
                LinkedListNode<gbXYZ> ptNode = new LinkedListNode<gbXYZ>(mcr[shellId][i]);
                shell.AddLast(ptNode);
            }

            double minLength = double.PositiveInfinity;
            int counter = 0;
            LinkedListNode<gbXYZ> currentNode = shell.First;
            LinkedListNode<gbXYZ> targetNode = shell.First;
            gbXYZ cutPt = shell.NextOrFirst(currentNode).Value;

            while (counter < shell.Count)
            {
                gbXYZ left, mid, right;
                double thisLength;
                left = currentNode.Value - shell.PreviousOrLast(currentNode).Value;
                mid = shell.NextOrFirst(currentNode).Value - currentNode.Value;
                right = shell.NextOrFirst(shell.NextOrFirst(currentNode)).Value - shell.NextOrFirst(currentNode).Value;
                thisLength = currentNode.Value.DistanceTo(shell.NextOrFirst(currentNode).Value);

                double angleLeft = GBMethod.VectorAngle(left, mid);
                double angleRight = GBMethod.VectorAngle(mid, right);

                if (thisLength < minLength && angleLeft > 180 && angleRight > 180)
                {
                    minLength = thisLength;
                    targetNode = currentNode;
                    cutPt = left.Norm() < right.Norm() ?
                        shell.PreviousOrLast(currentNode).Value :
                        shell.NextOrFirst(shell.NextOrFirst(currentNode)).Value;
                }

                // tick the pointer to the next
                counter++;
                currentNode = shell.NextOrFirst(currentNode);
                //Rhino.RhinoApp.WriteLine("Tick to the next: " + counter);
            }

            List<gbXYZ> vertices = new List<gbXYZ>()
            {
                targetNode.Value,
                shell.NextOrFirst(targetNode).Value,
                cutPt
            };

            List<gbXYZ> clipper = OrthoHull.GetRectHull(vertices);
            int cutterId = 0;
            for (int i = 0; i < clipper.Count - 1; i++)
            {
                gbXYZ midChecker = (clipper[i] + clipper[i + 1]) / 2;
                if (GBMethod.IsPtInPoly(midChecker, mcr[shellId], false))
                    cutterId = i;
            }
            gbXYZ basePt1 = clipper[cutterId - 2 > 0 ? cutterId - 2 : cutterId + 2];
            gbXYZ basePt2 = clipper[cutterId - 1 > 0 ? cutterId - 1 : cutterId + 3];

            //Rhino.RhinoApp.WriteLine("Movable index: " + verticeId.ToString());
            double minDistance = double.PositiveInfinity;
            gbXYZ nearestPt = null;
            for (int i = 0; i < mcr.Count; i++)
            {
                if (i == shellId)
                    continue;
                // make sure an intersection calculation is necessary
                if (GBMethod.IsPolyOverlap(mcr[i], clipper, false) || GBMethod.IsPolyInPoly(mcr[i], clipper))
                {
                    List<List<gbXYZ>> debris = GBMethod.ClipPoly(mcr[i], clipper, ClipperLib.ClipType.ctIntersection);
                    foreach (List<gbXYZ> loop in debris)
                    {
                        for (int j = 0; j < loop.Count - 1; j++)
                        {
                            double thisDistance = GBMethod.PtDistanceToSeg(loop[j], new gbSeg(basePt1, basePt2),
                                out gbXYZ plummet, out double stretch);
                            if (thisDistance < minDistance)
                            {
                                nearestPt = loop[j];
                                minDistance = thisDistance;
                            }
                        }
                    }
                }
            }

            //Rhino.RhinoApp.WriteLine($"Offset check {offset} vs. {minDistance}");
            if (minDistance < 1000)
            {
                clipper = OrthoHull.GetRectHull(new List<gbXYZ>() { basePt1, basePt2, nearestPt });
            }

            List<List<gbXYZ>> clipResult = GBMethod.ClipPoly(mcr, clipper, ClipperLib.ClipType.ctDifference);

            foreach (List<gbXYZ> loop in clipResult)
            {
                SimplifyPoly(loop);
                loop.Add(loop[0]);
            }

            panel = clipResult;
            tile = GBMethod.ClipPoly(mcr[shellId], clipper, ClipperLib.ClipType.ctIntersection)[0];
            return;
        }

        /// <summary>
        /// Remove the redundant point of a polygon or polyline. The input must not be a closed polyloop, 
        /// or else the duplicate point will be removed as well. Here we check if the two outgoing vectors 
        /// of a vertice are co-lined. 
        /// </summary>
        public static void SimplifyPoly(List<gbXYZ> poly)
        {
            if (poly[poly.Count - 1].Equals(poly[0]))
            {
                poly.RemoveAt(poly.Count - 1);
                Debug.Print("RegionTesselate:: Prune the tail of a polyloop");
            }

            for (int i = poly.Count - 1; i >= 0; i--)
            {
                gbXYZ vec1, vec2;
                if (i == poly.Count - 1)
                    vec1 = poly[i] - poly[0];
                else
                    vec1 = poly[i] - poly[i + 1];
                if (i == 0)
                    vec2 = poly[i] - poly[poly.Count - 1];
                else
                    vec2 = poly[i] - poly[i - 1];
                // The function VectorAngle will not check the minor value
                // must make sure the input vectors are not residules extremely small
                if (vec1.Norm() < 0.001 || vec2.Norm() < 0.001)
                {
                    //Rhino.RhinoApp.WriteLine("Zero norm: " + vec1.Norm().ToString() + " | " + vec2.Norm().ToString());
                    poly.RemoveAt(i);
                    continue;
                }
                double angle = GBMethod.VectorAngle(vec1, vec2);
                //Rhino.RhinoApp.WriteLine("Check the redundant point: " + angle.ToString());

                if (Math.Abs(180 - angle) < 0.001 || Math.Abs(0 - angle) < 0.001 || Math.Abs(360 - angle) < 0.001)
                {
                    //Rhino.RhinoApp.WriteLine("Remove point: " + poly[i].ToString());
                    poly.RemoveAt(i);
                }

            }
        }
    }

    class CircularLinkedList<T> : LinkedList<T>
    {
        public LinkedListNode<T> NextOrFirst(LinkedListNode<T> current)
        {
            if (current.Next == null)
                return current.List.First;
            return current.Next;
        }

        public LinkedListNode<T> PreviousOrLast(LinkedListNode<T> current)
        {
            if (current.Previous == null)
                return current.List.Last;
            return current.Previous;
        }

        public CircularLinkedList<T> Reverse()
        {
            CircularLinkedList<T> temp = new CircularLinkedList<T>();
            foreach (var current in this)
                temp.AddFirst(current);

            return temp;
        }
    }
}
