using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Gingerbread.Core
{
    // class definition reserved by gingerbread with prefix gb-
    #region Gingerbread class
    /// <summary>
    /// Degenerated version of point exclusive for Gingerbread
    /// </summary>
    public class gbXYZ
    {
        // private info
        private double x;
        private double y;
        private double z;
        private static double eps = 1e-6;
        public double X { get { return x; } set { x = value; } }
        public double Y { get { return y; } set { y = value; } }
        public double Z { get { return z; } set { z = value; } }

        // constructor
        public gbXYZ()
        {
            x = 0; y = 0; z = 0;
        }

        public gbXYZ(double x, double y, double z)
        {
            //this.x = x;
            //this.y = y;
            //this.z = z;
            this.x = Math.Abs(x) > eps ? x : 0;
            this.y = Math.Abs(y) > eps ? y : 0;
            this.z = Math.Abs(z) > eps ? z : 0;
        }

        // public methods
        public void Unitize()
        {
            double norm = Math.Sqrt(x * x + y * y + z * z);
            if (norm > 0)
            {
                x /= norm; y /= norm; z /= norm;
            }
        }
        public double Norm()
        {
            return Math.Sqrt(x * x + y * y + z * z);
        }
        public gbXYZ Copy()
        {
            return new gbXYZ(X, Y, Z);
        }
        public gbXYZ CrossProduct(gbXYZ b)
        {
            return new gbXYZ(
                Y * b.Z - Z * b.Y,
                Z * b.X - X * b.Z,
                X * b.Y - Y * b.X);
        }
        public double DotProduct(gbXYZ b)
        {
            return X * b.X + Y * b.Y + Z * b.Z;
        }
        public double DistanceTo(gbXYZ b)
        {
            return Math.Sqrt(
                (b.X - X) * (b.X - X) +
                (b.Y - Y) * (b.Y - Y) +
                (b.Z - Z) * (b.Z - Z));
        }
        public gbXYZ SwapPlaneZY()
        {
            return new gbXYZ(X, 0, Y);
        }
        public gbXYZ RotateOnPlaneZ(gbXYZ vec)
        {
            return new gbXYZ(X * vec.X / vec.Norm(), X * vec.Y / vec.Norm(), Z);
        }
        public gbXYZ Move(gbXYZ vec)
        {
            return new gbXYZ(X + vec.X, Y + vec.Y, Z + vec.Z);
        }
        // Define addition operation
        public static gbXYZ operator +(gbXYZ A, gbXYZ B)
        {
            return new gbXYZ(A.X + B.X, A.Y + B.Y, A.Z + B.Z);
        }
        public static gbXYZ operator -(gbXYZ A, gbXYZ B)
        {
            return new gbXYZ(A.X - B.X, A.Y - B.Y, A.Z - B.Z);
        }
        public static gbXYZ operator -(gbXYZ A)
        {
            return new gbXYZ(-A.X, -A.Y, -A.Z);
        }
        public static gbXYZ operator /(gbXYZ P, double a)
        {
            if (a == 0)
                return P;
            else
                return new gbXYZ(P.X / a, P.Y / a, P.Z / a);

        }
        public static gbXYZ operator *(double a, gbXYZ P)
        {
            return new gbXYZ(P.X * a, P.Y * a, P.Z * a);
        }
        public static bool operator ==(gbXYZ A, gbXYZ B)
        {
            if (Math.Abs(A.X - B.X) <= eps &&
                Math.Abs(A.Y - B.Y) <= eps &&
                Math.Abs(A.Z - B.Z) <= eps)
                return true;
            return false;
        }
        public static bool operator !=(gbXYZ A, gbXYZ B)
        {
            if (Math.Abs(A.X - B.X) <= eps &&
                Math.Abs(A.Y - B.Y) <= eps &&
                Math.Abs(A.Z - B.Z) <= eps)
                return false;
            return true;
        }
        // Define function to display a point
        public override string ToString()
        {
            return string.Format("{0:F4}, {1:F4}, {2:F4}", X, Y, Z);
        }
    }
    
    public class gbUV
    {
        // private info
        private double u;
        private double v;
        private static double eps = 1e-6;
        public double U { get { return u; } set { u = value; } }
        public double V { get { return v; } set { v = value; } }

        public gbUV()
        {
            u = 0; v = 0;
        }
        public gbUV(double u, double v)
        {
            this.u = u; this.v = v;
        }
        // Define addition operation
        public static gbUV operator +(gbUV A, gbUV B)
        {
            return new gbUV(A.U + B.U, A.V + B.V);
        }
        public static gbUV operator -(gbUV A, gbUV B)
        {
            return new gbUV(A.U - B.U, A.V - B.V);
        }
        public static bool operator ==(gbUV A, gbUV B)
        {
            if (Math.Abs(A.U - B.U) <= eps && Math.Abs(A.V - B.V) <= eps)
            {
                return true;
            }
            return false;
        }
        public static bool operator !=(gbUV A, gbUV B)
        {
            if (Math.Abs(A.U - B.U) <= eps && Math.Abs(A.V - B.V) <= eps)
            {
                return false;
            }
            return true;
        }
        // Define function to display a point
        public override string ToString()
        {
            return string.Format("({0:F2}, {1:F2})", U, V);
        }
    }

    public class gbSeg
    {
        private gbXYZ start;
        private gbXYZ end;
        private double length;
        private gbXYZ direction;
        public gbXYZ Start { get { return start; } }
        public gbXYZ End { get { return end; } }
        public double Length { get { return length; } }
        public gbXYZ Direction { get { return direction; } }
        
        public gbSeg()
        {
            start = new gbXYZ();
            end = new gbXYZ();
            length = 0;
            direction = new gbXYZ();
        }
        public gbSeg(gbXYZ start, gbXYZ end)
        {
            this.start = start;
            this.end = end;
            length = Math.Sqrt(
                (end.X - start.X) * (end.X - start.X) +
                (end.Y - start.Y) * (end.Y - start.Y) +
                (end.Z - start.Z) * (end.Z - start.Z));
            direction = (end - start) / length;
        }

        public gbXYZ PointAt(double ratio)
        {
            return start + ratio * length * direction;
        }
        public void Reverse()
        {
            gbXYZ temp = end;
            end = start;
            start = temp;
            direction = -direction;
        }
        public void AdjustEndPt(int i, gbXYZ pt)
        {
            if (i == 0)
                start = pt;
            if (i == 1)
                end = pt;
            length = Math.Sqrt(
                (end.X - start.X) * (end.X - start.X) +
                (end.Y - start.Y) * (end.Y - start.Y) +
                (end.Z - start.Z) * (end.Z - start.Z));
            return;
        }
        public gbSeg Copy()
        {
            return new gbSeg(start, end);
        }
        public List<gbSeg> Split(List<double> intervals)
        {
            foreach (double interval in intervals)
                if (Math.Round(interval, 6) > 1 || Math.Round(interval, 6) < 0)
                {
                    //Debug.Print("GBClass:: " + "Trigger malfunction:");
                    return new List<gbSeg>() { new gbSeg(start, end) };
                }
            List<gbSeg> segments = new List<gbSeg>();
            intervals.Sort();
            intervals.Insert(0, 0);
            intervals.Add(1);
            //Debug.Print("GBClass:: " + "Fractiles: " + Util.ListString(intervals));
            //Debug.Print("GBClass:: " + "Host line: ({0:f3}, {1:f3})-({2:f3}, {3:f3})", Util.MToFoot(start.X),
            //    Util.MToFoot(start.Y), Util.MToFoot(end.X), Util.MToFoot(end.Y));

            for (int i = 0; i < intervals.Count - 1; i++)
                if (intervals[i] != intervals[i + 1])
                {
                    gbSeg debris = new gbSeg(PointAt(intervals[i]), PointAt(intervals[i + 1]));
                    if (debris.Length > 0.0000001)
                        segments.Add(debris);
                }
            //Debug.Print("GBClass:: " + "Generated: " + segments.Count);

            return segments;
        }
        public override string ToString()
        {
            return string.Format("{0}}}#{{{1}", Start.ToString(), End.ToString());
        }
    }
     
    // just have a try...
    public class gbRegion
    {
        public string label; // label of current region
        public List<gbXYZ> loop; // vertice loop of this region
        public List<string> match; // label of the adjacent edge
        public bool isShell = false; // reconsider this
        //public bool isMCR = false; // reconsider this

        // null by default
        public List<List<gbXYZ>> innerLoops;
        public List<List<string>> innerMatchs;
        public List<List<gbXYZ>> tiles;

        public gbRegion(string label, List<gbXYZ> loop, List<string> match)
        {
            this.label = label;
            this.loop = loop;
            this.match = match;
        }
        public void InitializeMCR()
        {
            if (innerLoops != null && innerMatchs != null)
            {
                List<List<gbXYZ>> mcr = new List<List<gbXYZ>>();
                mcr.Add(loop);
                mcr.AddRange(innerLoops);
                tiles = RegionTessellate.Rectangle(mcr);
                //isMCR = true;
                return;
            }
            return;
        }
    }

    // note these classes aim for Energyplus IDF structures
    // in gbXML there must be no coincident surface, but in IDF there must be two identical 
    // surfaces as the adjacent interior wall
    // for debugging we still use Point3d from Rhino.Geometry
    public class gbLevel
    {
        public int id;
        public int prevId;
        public int nextId;
        public string label;
        public double elevation;
        public double height;
        // in here there permits no gap between spaces and floors
        // so usually the space height equals the level capacity

        public bool isTop = false;
        public bool isBottom = false;
        public bool isBasement = false;
        public bool isGround = false;
        public bool isShadowing = false;
        public gbLevel(int id, string label, double elevation, int numAllLevels)
        {
            this.id = id;
            this.label = label;
            this.elevation = elevation;
            if (this.elevation < 0) isBasement = true;
            if (this.elevation == 0) isGround = true;
            if (id == 0) isBottom = true; else prevId = id - 1;
            if (id == numAllLevels - 1) isTop = true; else nextId = id + 1;
        }
    }

    public class gbLoop
    {
        public string id;
        public gbLevel level;
        public List<gbXYZ> loop;
        public double dimension1;
        public double dimension2;
        // convert the 2D loop to 3D floor geometry
        public gbLoop(string id, gbLevel level, List<gbXYZ> loop, double delta)
        {
            this.id = id;
            this.level = level;
            this.loop = GBMethod.ElevatePts(loop, level.elevation + delta);
        }
    }

    public class gbSurface
    {
        // initialization attributes
        public string id;
        public string parentId;
        public gbLevel level;

        public double tilt;
        public double azimuth;  // pending right now
        public double area;
        public double width;
        public double height;

        public List<gbXYZ> loop;
        public gbSeg locationLine; // when the tilt is 90
        public List<gbOpening> openings;
        public List<List<gbXYZ>> subLoops;
        public double openingArea;

        // modification attributes
        public string adjSrfId;
        public surfaceTypeEnum type;

        // customized attribute to mark a firewall
        public bool isFirewall;

        public gbSurface(string id, string parentId, List<gbXYZ> loop, double tilt)
        {
            this.id = id;
            this.loop = loop;
            subLoops = new List<List<gbXYZ>>();
            this.parentId = parentId;
            this.tilt = tilt;
            // the azimuth is the angle (0-360) from the north axis (0, 1, 0) to the normal vector
            // the azimuth should follow the clockwise sequence. north-0, east-90, south-180, west-270
            gbXYZ normal = GBMethod.GetPolyNormal(loop);
            azimuth = GBMethod.VectorAngle(new gbXYZ(0, 1, 0), normal);
            // no specific regulations on the azimuth of horizontal plane?
            // simply assign them all with 0 azimuth, for now
            if (Math.Abs(Math.Abs(normal.Z) - 1) < 1.0e-9)
                azimuth = 0;
                    
                
            area = GBMethod.GetPolyArea3d(loop);
            openings = new List<gbOpening>();
            openingArea = 0;
            

            if (tilt == 90)
            {
                locationLine = new gbSeg(loop[0], loop[1]);
                // according to gbXML schema, the width is the length between
                // the left most and right most points of the polygon
                // the height is an equivalent value by area / width
                width = locationLine.Length;
                height = area / width;
            }
            // the GetRectHull function only works for 2D points
            if (tilt == 0 || tilt == 180)
            {
                List<gbXYZ> corners = OrthoHull.GetRectHull(loop);
                width = corners[1].X - corners[0].X;
                height = area / width;
            }
        }
    }

    /// <summary>
    /// Only including vertical openings: window/door/curtain wall
    /// </summary>
    public class gbOpening
    {
        public string id;
        public List<gbXYZ> loop;
        public int levelId;
        public double area;
        public double width;
        public double height;
        public openingTypeEnum type;

        public gbOpening(string id, List<gbXYZ> loop)
        {
            this.id = id;
            this.loop = loop;
            area = GBMethod.GetPolyArea3d(loop);
            width = loop[0].DistanceTo(loop[1]);
            height = area / width;
        }
    }

    public class gbZone
    {
        public string id; // structured relationships F0::Z0::Srf_1
        public List<gbXYZ> loop;
        public List<List<gbXYZ>> tiles;

        public gbLevel level;

        public double area;
        public double volume;
        public double height;
        public bool isFuzzySeperated = false; // goto isovist division if true
        public bool isMultiConnected = false; // goto MCR separation if true
        public bool isExposedAbove = false;   // the zone lacks top ceiling if true

        // connect to program presets or space label
        public string function;

        // all need to be validated before faces are generated
        public List<gbSurface> faces;
        public int numFaces;
        public List<gbSurface> walls = new List<gbSurface>();
        public List<gbSurface> ceilings = new List<gbSurface>();
        public List<gbSurface> floors = new List<gbSurface>();

        // the input loop of points must be closed
        public gbZone(string id, gbLevel level, gbRegion region)
        {
            this.id = id;
            this.loop = GBMethod.ElevatePts(region.loop, level.elevation);
            if (region.tiles != null)
            {
                List<List<gbXYZ>> elevatedTiles = new List<List<gbXYZ>>();
                foreach (List<gbXYZ> tile in region.tiles)
                    elevatedTiles.Add(GBMethod.ElevatePts(tile, level.elevation));
                tiles = elevatedTiles;
            }
            else
            {
                tiles = new List<List<gbXYZ>>() { GBMethod.ElevatePts(region.loop, level.elevation) };
            }

                

            this.level = level;
            this.height = level.height;

            // PENDING // use net area 22-04-16
            area = GBMethod.GetPolyArea(region.loop);
            if (region.innerLoops != null)
                foreach (List<gbXYZ> hole in region.innerLoops)
                    area -= GBMethod.GetPolyArea(hole);
            volume = area * height;

            walls = new List<gbSurface>();
            for (int i = 0; i < loop.Count - 1; i++)
            {
                List<gbXYZ> subLoop = new List<gbXYZ>
                {
                    this.loop[i],
                    this.loop[i + 1],
                    this.loop[i + 1] + new gbXYZ(0, 0, height),
                    this.loop[i] + new gbXYZ(0, 0, height)
                };
                // only extrude on axis-z
                gbSurface wall = new gbSurface(id + "::Wall_" + i, id, subLoop, 90);
                wall.adjSrfId = region.match[i];

                // 20230329 CAUTION
                // the floor/ceiling only has null level attribute, for now
                wall.level = level;

                if (region.match[i].Contains("Outside"))
                    if (this.level.isBasement)
                        wall.type = surfaceTypeEnum.UndergroundWall;
                    else
                        wall.type = surfaceTypeEnum.ExteriorWall;
                else
                    wall.type = surfaceTypeEnum.InteriorWall;
                walls.Add(wall);
            }

            if (region.innerLoops != null && region.innerMatchs != null)
            {
                for (int i = 0; i < region.innerLoops.Count; i++)
                {
                    List<gbSurface> innerWalls = new List<gbSurface>();
                    for (int j = 0; j < region.innerLoops[i].Count - 1; j++)
                    {
                        List<gbXYZ> elevatedInnerLoop = GBMethod.ElevatePts(region.innerLoops[i], level.elevation);
                        List<gbXYZ> subLoop = new List<gbXYZ>() {
                            elevatedInnerLoop[j],
                            elevatedInnerLoop[j + 1],
                            elevatedInnerLoop[j + 1] + new gbXYZ(0, 0, height),
                            elevatedInnerLoop[j] + new gbXYZ(0, 0, height)
                        };
                        gbSurface wall = new gbSurface(id + "::Wall" + i + "_" + j, id, subLoop, 90);
                        wall.adjSrfId = region.innerMatchs[i][j];
                        wall.type = surfaceTypeEnum.InteriorWall;
                        innerWalls.Add(wall);
                    }
                    walls.AddRange(innerWalls);
                }
            }
        }
        /// <summary>
        /// Add up all surfaces of this zone
        /// </summary>
        public void Summarize()
        {
            faces = new List<gbSurface>();
            if (walls.Count != 0)
                faces.AddRange(walls);
            if (ceilings.Count != 0)
                faces.AddRange(ceilings);
            if (floors.Count != 0)
                faces.AddRange(floors);
            numFaces = faces.Count;
        }
    }
    #endregion
}
