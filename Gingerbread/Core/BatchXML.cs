using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gingerbread.Core
{
    // this file is abandoned, only for reference
    /*
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
            Dictionary<int, List<gbRegion>> dictRegion = new Dictionary<int, List<gbRegion>>();

            for (int z = 0; z < levelNum; z++)
            {
                List<gbSeg> flatLines = GBMethod.FlattenLines(dictWall[z]);

                for (int i = 0; i < flatLines.Count; i++)
                    for (int j = 0; j < flatLines.Count; j++)
                        if (i != j)
                            flatLines[i] = GBMethod.SegExtension(flatLines[i], flatLines[j],
                                Properties.Settings.Default.tolExpand);
                //Debug.Print("BatchXML:: " + flatLines[i].Start.Serialize() + " / " + flatLines[i].End.Serialize());

                List<List<gbSeg>> lineGroups = GBMethod.SegClusterByFuzzyIntersection(flatLines,
                    Properties.Settings.Default.tolGroup);

                // a trush bin for stray lines that are processed after space detection
                // three steps are dumping debris to this trush bin
                // 1st cluster. assume that segments less than 4 are not likely to compose a region
                // 2nd alignment and lattice regeneration. 
                // 3rd after region detection. (not likely produces stray lines here if the former process done well)
                List<gbSeg> strays = new List<gbSeg>();
                for (int i = lineGroups.Count - 1; i >= 0; i--)
                {
                    if (lineGroups[i].Count <= 3)
                    {
                        strays.AddRange(lineGroups[i]);
                        lineGroups.RemoveAt(i);
                    }
                }

                // enter point alignment and space detection of each segment group
                List<List<List<gbXYZ>>> nestedSpace = new List<List<List<gbXYZ>>>();
                List<List<gbXYZ>> nestedShell = new List<List<gbXYZ>>();
                List<List<List<string>>> nestedMatch = new List<List<List<string>>>();
                List<List<gbRegion>> nestedRegion = new List<List<gbRegion>>();

                for (int g = 0; g < lineGroups.Count; g++)
                {
                    // Use a global short curve length tolerance here
                    List<gbSeg> lineShatters = GBMethod.SkimOut(GBMethod.ShatterSegs(lineGroups[g]), 0.00001);


                    List<gbXYZ> joints = PointAlign.GetJoints(lineShatters, Properties.Settings.Default.tolDouble, out List<List<gbXYZ>> hands);


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
                    //    Debug.Print("BatchXML:: " + "F-{0} JOINTS-{1} ({2}, {3})", z, i, joints[i].X, joints[i].Y);
                    //    string handcollector = "";
                    //    foreach (gbXYZ hand in hands[i])
                    //        handcollector += string.Format("({0:F2},{1:F2})-", hand.X, hand.Y);
                    //    Debug.Print("BatchXML:: " + "F-{0} HANDS-{1} {2}", z, i, handcollector);
                    //}

                    List<List<gbXYZ>> anchorInfo_temp;
                    List<List<gbXYZ>> anchorInfo;
                    List<gbXYZ> ptAlign_temp = PointAlign.AlignPts(joints, hands,
                        Properties.Settings.Default.tolTheta,
                        Properties.Settings.Default.tolDelta,
                        Properties.Settings.Default.tolDouble,
                        out anchorInfo_temp);
                    List<gbXYZ> ptAlign = PointAlign.AlignPts(ptAlign_temp, anchorInfo_temp,
                        Properties.Settings.Default.tolTheta - Math.PI / 2,
                        Properties.Settings.Default.tolDelta,
                        Properties.Settings.Default.tolDouble,
                        out anchorInfo);


                    List<gbSeg> latticeDebries; // abandoned for now
                    List<List<gbSeg>> nestedLattice = PointAlign.GetLattice(ptAlign, anchorInfo,
                        Properties.Settings.Default.tolDouble, out latticeDebries);
                    List<gbSeg> lattice = Util.FlattenList(nestedLattice);
                    strays.AddRange(latticeDebries);


                    // DEBUG
                    //System.Windows.MessageBox.Show("Point Alignment at F-" + z + "\n" +
                    //    "Shattered lines: " + lineShatters.Count + "\n" +
                    //    "Joints: " + joints.Count + "\n" +
                    //    "Hands: " + handsCopy.Count + "\n" +
                    //    "Anchors: " + ptAlign.Count + "\n" + 
                    //    "Lattice lines: " + lattice.Count + "\n" + 
                    //    "Strays: " + strays.Count, "Report");


                    //List<List<gbXYZ>> regionLoops;
                    List<gbXYZ> regionShell;
                    //List<List<string>> regionRefs;
                    List<List<gbSeg>> regionDebris;
                    List<gbRegion> regions;


                    RegionDetect.GetRegion(lattice, z, g, out regions, out regionDebris);
                    strays.AddRange(Util.FlattenList(regionDebris));


                    // DEBUG
                    //Debug.Print("BatchXML:: " + "F-{0} LOOPS-{1}", z, nestedSpace.Count);
                    //Debug.Print(""BatchXML:: " + F-{0} SHELLS-{1}", z, nestedShell.Count);
                    //System.Windows.MessageBox.Show("Space Detection at F-" + z + "\n" +
                    //    "Loops: " + nestedSpace.Count + "\n" + 
                    //    "Shells: " + nestedShell.Count, "Report");

                    //nestedSpace.Add(regionLoops);
                    //nestedShell.Add(regionShell);
                    //nestedMatch.Add(regionRefs);
                    nestedRegion.Add(regions);
                }

                // left for some MCR coupling work
                // only a placeholder that solves nothing
                //SpaceDetect.GetMCR(nestedSpace, nestedShell, out List<List<List<gbXYZ>>> mcrs);

                // summarization after solving the MCR issues
                dictLoop.Add(z, Util.FlattenList(nestedSpace));
                dictShell.Add(z, Util.FlattenList(nestedShell));
                dictMatch.Add(z, Util.FlattenList(nestedMatch));
                dictRegion.Add(z, Util.FlattenList(nestedRegion));
            }


            if (dictLoop.Count == 0 || dictShell.Count == 0)
            {
                System.Windows.MessageBox.Show("Something wrong with the space detection. \n" +
                    "The process will be terminated.", "Warning");
                return;
            }

            XMLGeometry.Generate(dictElevation, dictRegion, dictShell,
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
    */
}
