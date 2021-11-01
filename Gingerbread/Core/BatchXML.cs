using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gingerbread.Core
{
    public class BatchXML
    {
        public static void Execute(
            Dictionary<int, Tuple<string, double>> dictElevation, 
            Dictionary<int, List<gbSeg>> dictWall, 
            Dictionary<int, List<Tuple<gbXYZ, string>>> dictWindow, 
            Dictionary<int, List<Tuple<gbXYZ, string>>> dictDoor, 
            Dictionary<int, List<gbSeg>> dictCurtain, 
            out List<gbXYZ> testPts, 
            out List<List<gbXYZ>> testVecs)
        {
            int levelNum = dictElevation.Count;
            testPts = new List<gbXYZ>();
            testVecs = new List<List<gbXYZ>>();

            // info check
            if (dictWall.Count != levelNum ||
                dictWindow.Count != levelNum ||
                dictDoor.Count != levelNum ||
                dictCurtain.Count != levelNum)
                return;

            // process wall lines at each level
            // process space boundary and matching relation at each level
            Dictionary<int, List<List<gbXYZ>>> dictLoop = new Dictionary<int, List<List<gbXYZ>>>();
            Dictionary<int, List<gbXYZ>> dictShell = new Dictionary<int, List<gbXYZ>>();
            Dictionary<int, List<List<string>>> dictMatch = new Dictionary<int, List<List<string>>>();
            for (int z = 0; z < levelNum; z++)
            {
                List<gbSeg> flatLines = GBMethod.FlattenLines(dictWall[z]);

                for (int i = 0; i < flatLines.Count; i++)
                    for (int j = 0; j < flatLines.Count; j++)
                        if (i != j)
                            flatLines[i] = GBMethod.SegExtension(flatLines[i], flatLines[j], 
                                Properties.Settings.Default.expandTolerance);
                            //Debug.Print(flatLines[i].Start.Serialize() + " / " + flatLines[i].End.Serialize());

                List<List<gbSeg>> lineGroups = GBMethod.SegClusterByFuzzyIntersection(flatLines, 
                    Properties.Settings.Default.groupTolerance);
                List<gbSeg> orphans = new List<gbSeg>();
                // dump some orphan segments that will be processed later
                for (int i = lineGroups.Count - 1; i >= 0; i--)
                {
                    if (lineGroups[i].Count <= 3)
                    {
                        orphans.AddRange(lineGroups[i]);
                        lineGroups.RemoveAt(i);
                    }
                }

                // enter point alignment and space detection of each segment group
                foreach (List<gbSeg> lineGroup in lineGroups)
                {

                    List<gbSeg> lineShatters = GBMethod.SkimOut(GBMethod.ShatterSegs(lineGroup), 0.00001);


                    List<gbXYZ> joints = PointAlign.GetJoints(lineShatters, out List<List<gbXYZ>> hands);


                    // deepcopy hands for debugging
                    List<List<gbXYZ>> handsCopy = new List<List<gbXYZ>>();
                    foreach (List<gbXYZ> hand in hands)
                    {
                        List<gbXYZ> handCopy = new List<gbXYZ>();
                        foreach (gbXYZ h in hand)
                            handCopy.Add(h);
                        handsCopy.Add(handCopy);
                    }
                    
                    //joints = joints.OrderBy(pt => (pt.X + pt.Y)).ToList();
                    //for (int i = 0; i < joints.Count; i++)
                    //{
                    //    Debug.Print("F-{0} JOINTS-{1} ({2}, {3})", z, i, joints[i].X, joints[i].Y);
                    //    string handcollector = "";
                    //    foreach (gbXYZ hand in hands[i])
                    //        handcollector += string.Format("({0:F2},{1:F2})-", hand.X, hand.Y);
                    //    Debug.Print("F-{0} HANDS-{1} {2}", z, i, handcollector);
                    //}

                    List<List<gbXYZ>> anchorInfo_temp;
                    List<List<gbXYZ>> anchorInfo;
                    List<gbXYZ> ptAlign_temp = PointAlign.AlignPts(joints, hands,
                        Properties.Settings.Default.latticeTheta,
                        Properties.Settings.Default.latticeDelta,
                        Properties.Settings.Default.doubleTolerance,
                        out anchorInfo_temp);
                    List<gbXYZ> ptAlign = PointAlign.AlignPts(ptAlign_temp, anchorInfo_temp,
                        Properties.Settings.Default.latticeTheta - Math.PI / 2,
                        Properties.Settings.Default.latticeDelta,
                        Properties.Settings.Default.doubleTolerance,
                        out anchorInfo);


                    List<gbSeg> strays; // abandoned for now
                    List<List<gbSeg>> nestedLattice = PointAlign.GetLattice(ptAlign, anchorInfo,
                        Properties.Settings.Default.doubleTolerance, out strays);
                    List<gbSeg> lattice = Util.FlattenList(nestedLattice);


                    // DEBUG
                    //System.Windows.MessageBox.Show("Point Alignment at F-" + z + "\n" +
                    //    "Shattered lines: " + lineShatters.Count + "\n" +
                    //    "Joints: " + joints.Count + "\n" +
                    //    "Hands: " + handsCopy.Count + "\n" +
                    //    "Anchors: " + ptAlign.Count + "\n" + 
                    //    "Lattice lines: " + lattice.Count + "\n" + 
                    //    "Strays: " + strays.Count, "Report");


                    List<List<gbXYZ>> nestedSpace;
                    List<gbXYZ> nestedShell;
                    List<List<string>> nestedMatch;
                    List<List<gbSeg>> nestedOrphans;


                    SpaceDetection.GetBoundary(lattice, z, out nestedSpace, out nestedShell, out nestedMatch, out nestedOrphans);


                    // left for some MCR coupling work


                    // DEBUG
                    //Debug.Print("F-{0} LOOPS-{1}", z, nestedSpace.Count);
                    //Debug.Print("F-{0} SHELLS-{1}", z, nestedShell.Count);
                    //System.Windows.MessageBox.Show("Space Detection at F-" + z + "\n" +
                    //    "Loops: " + nestedSpace.Count + "\n" + 
                    //    "Shells: " + nestedShell.Count, "Report");

                    dictLoop.Add(z, nestedSpace);
                    dictShell.Add(z, nestedShell);
                    dictMatch.Add(z, nestedMatch);

                    break;
                }
            }

            if (dictLoop.Count == 0 || dictShell.Count == 0)
            {
                System.Windows.MessageBox.Show("Something wrong with the space detection. \n" +
                    "The process will be terminated.", "Warning");
                return;
            }

            XMLGeometry.Generate(dictElevation,
                dictLoop, dictShell, dictMatch,
                dictWindow, dictDoor, dictCurtain,
                out List<gbZone> zones,
                out List<gbFloor> floors,
                out List<gbSurface> surfaces);


            string fileName = "Sample.xml";
            string thisAssemblyFolderPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            XMLSerialization.Generate(thisAssemblyFolderPath + "/" + fileName, zones, floors, surfaces);

            System.Windows.MessageBox.Show("gbXML exported to " + thisAssemblyFolderPath);
            return;
        }
    }
}
