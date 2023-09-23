using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Linq;
using System.Diagnostics;

namespace Gingerbread.Core
{
    class XMLSerialize
    {
        public static void Generate(string path, List<gbZone> zones, List<gbLoop> floors, List<gbSurface> faces,
            List<gbLoop> columns, List<gbLoop> beams, List<gbLoop> shafts)
        //    Dictionary<string, string> adjDict)
        {
            gb.gbci = new CultureInfo(String.Empty);

            //the basics
            //constructor to define the basics
            gbXML gbx = new gbXML();
            gbx.lengthUnit = lengthUnitEnum.Meters;
            gbx.areaUnit = areaUnitEnum.SquareMeters;
            gbx.volumeUnit = volumeUnitEnum.CubicMeters;
            gbx.temperatureUnit = temperatureUnitEnum.C;

            Campus cmp = CreateCampus("DXI_Conf_Center");

            cmp.Buildings = new Building[10];
            gbx.Campus = cmp; // backward mapping

            //where does this location information from?  it could be smartly inferred somehow, but otherwise specified by the user/programmer
            Location zeloc = new Location();
            zeloc.Name = Properties.Settings.Default.projAddress;
            zeloc.Latitude = Properties.Settings.Default.projLatitude;
            zeloc.Longitude = Properties.Settings.Default.projLongitude;
            zeloc.Elevation = Properties.Settings.Default.projElevation;
            zeloc.CADModelAzimuth = Properties.Settings.Default.projAzimuth;
            cmp.Location = zeloc; // backward mapping

            // set default building area then revise it
            // by default, a building class will allow 100 stories and 10000 spaces
            // you may change this setting in the function MakeBuilding()
            cmp.Buildings[0] = MakeBuilding(10000, Properties.Settings.Default.projName, buildingTypeEnum.Office);

            int offsetLv = 0;
            // STOREY
            for (int i = 0; i < floors.Count; i++)
            {
                cmp.Buildings[0].bldgStories[i] = MakeStorey(floors[i].level, floors[i].loop);
                if (floors[i].level.elevation < 0)
                    offsetLv++;
            }
                

            // SPACE
            // a list for space that is replaceable by multipliers during energy simulation
            List<string> spaceRemovableID = new List<string>();
            List<string> spaceVoidID = new List<string>();
            // PENDING use another way to generate the label
            int currentLevel = 0;
            int counter = 0;
            double sumArea = 0;

            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i].level.id != currentLevel)
                {
                    counter = 0;
                    currentLevel = zones[i].level.id;
                }
                cmp.Buildings[0].Spaces[i] = MakeSpace(zones[i], counter);
                sumArea += zones[i].area;
                counter++;

                if (zones[i].level.isShadowing == true)
                    spaceRemovableID.Add(zones[i].id);
                if (zones[i].function == "Void")
                    spaceVoidID.Add(zones[i].id);
            }
            // summarize the total indoor area
            cmp.Buildings[0].Area = string.Format("{0:0.000000}", sumArea);


            // SURFACE
            List<gbSurface> uniqueSrfs = new List<gbSurface>();
            // either one of the two doppelganger surfaces is okay to be added
            // on condition that they are exactly the same!
            cmp.Surface = new Surface[faces.Count];
            int srfCounter = 0;
            for (int i = 0; i < faces.Count; i++)
            {
                if (faces[i].loop.Count < 3)
                {
                    // the degenerate surface excludes triangles
                    //Debug.Print("XMLSerialization:: Degenerated surface detected at: " + i);
                    Util.LogPrint($"Serialization: Degenerated surface removed at Surface_{i}");
                    continue;
                }
                if (faces[i].area < 0.001)
                {
                    //Debug.Print("XMLSerialization:: 0 area warning at surface: " + i);
                    //continue;
                }

                //Util.LogPrint(faces[i].id + "-" + faces[i].adjSrfId);
                // if the current face has already been an adjacent face 
                // of one of those modeled surfaces (uniqueSrfs), skip it
                // 20230923
                // such a process randomly take one of the paired (matched) adjacent surfaces as the partition
                // to make it always select the floor surface, a short cut is to only add Floor of every partition
                string matchedSrfId = RetrieveMatchedSrf(faces[i].id, uniqueSrfs);
                // if none of the paired surface has been recorded...
                if (matchedSrfId == null)
                {
                    // check if this is the Ceiling -> Floor pair
                    if (faces[i].id.Contains("Ceil") && faces[i].adjSrfId.Contains("Floor"))
                        continue;
                    // if not, the surface can be safely added
                    else
                        uniqueSrfs.Add(faces[i]);
                } 
                else
                    // if continue, only one surface will be added representing the partition
                    // if commented out, all surfaces will be added
                    continue;


                Surface newSurface = MakeSurface(faces[i], srfCounter);
                //Debug.Print($"Generating surface-{srfCounter}");

                // check if its adjacent spaces are all shadowing spaces, and removable
                // by null, the surface is shading surface, skip it
                if (newSurface.AdjacentSpaceId != null)
                {
                    if (newSurface.AdjacentSpaceId.Length == 1)
                        if (spaceRemovableID.Contains(newSurface.AdjacentSpaceId[0].spaceIdRef))
                            newSurface.isShadowing = "true";
                    if (newSurface.AdjacentSpaceId.Length == 2)
                    {
                        if (spaceVoidID.Contains(newSurface.AdjacentSpaceId[0].spaceIdRef) ||
                            spaceVoidID.Contains(newSurface.AdjacentSpaceId[1].spaceIdRef))
                        {
                            // return value is like ["F1", "", "B0", "", "G2", "", "Z10", ""]
                            string[] lableChain1 = newSurface.AdjacentSpaceId[0].spaceIdRef.Split(':');
                            // or you should split by a string
                            //string[] lableChain1 = newSurface.AdjacentSpaceId[0].spaceIdRef.Split(new string[] { "::" },
                            //StringSplitOptions.RemoveEmptyEntries);
                            int levelId1 = Convert.ToInt32(lableChain1[0].Substring(1, lableChain1[0].Length - 1));
                            string[] lableChain2 = newSurface.AdjacentSpaceId[1].spaceIdRef.Split(':');
                            int levelId2 = Convert.ToInt32(lableChain2[0].Substring(1, lableChain2[0].Length - 1));
                            if (spaceVoidID.Contains(newSurface.AdjacentSpaceId[1].spaceIdRef))
                                Util.Swap(ref levelId1, ref levelId2);
                            if (levelId1 == levelId2 + 1)
                                newSurface.surfaceType = surfaceTypeEnum.Air;
                        }
                        if (spaceRemovableID.Contains(newSurface.AdjacentSpaceId[0].spaceIdRef) &&
                            spaceRemovableID.Contains(newSurface.AdjacentSpaceId[1].spaceIdRef))
                            newSurface.isShadowing = "true";
                    }
                }
                cmp.Surface[i] = newSurface;
                srfCounter++;
            }

            // the SpaceBoundary may point to a surface not modeled
            // the reference must be in the unique surface list
            foreach (Space zone in cmp.Buildings[0].Spaces)
            {
                if (zone == null) continue;
                foreach (SpaceBoundary sb in zone.spbound)
                {
                    string matchedSrfId = RetrieveMatchedSrf(sb.surfaceIdRef, uniqueSrfs);
                    if (matchedSrfId != null)
                    {
                        sb.surfaceIdRef = matchedSrfId;
                    }
                }
            }

            // Appendix, columns, beams, shafts
            cmp.Column = new Column[columns.Count];
            for (int i = 0; i < columns.Count; i++)
            {
                Column column = new Column();
                column.id = columns[i].id;
                column.level = columns[i].level.id;
                column.Width = string.Format("{0:0.000000}", columns[i].dimension1);
                column.Height = string.Format("{0:0.000000}", columns[i].dimension2);
                PlanarGeometry pg = new PlanarGeometry();
                pg.PolyLoop = PtsToPolyLoop(columns[i].loop);
                column.PlanarGeometry = pg;
                cmp.Column[i] = column;
            }
            cmp.Beam = new Beam[beams.Count];
            for (int i = 0; i < beams.Count; i++)
            {
                Beam beam = new Beam();
                beam.id = beams[i].id;
                beam.level = beams[i].level.id;
                beam.Width = string.Format("{0:0.000000}", beams[i].dimension1);
                beam.Height = string.Format("{0:0.000000}", beams[i].dimension2);
                PlanarGeometry pg = new PlanarGeometry();
                pg.PolyLoop = PtsToPolyLoop(beams[i].loop);
                beam.PlanarGeometry = pg;
                cmp.Beam[i] = beam;
            }
            cmp.Shaft = new Shaft[shafts.Count];
            for (int i = 0; i < shafts.Count; i++)
            {
                Shaft shaft = new Shaft();
                shaft.id = shafts[i].id;
                shaft.level = shafts[i].level.id;
                PlanarGeometry pg = new PlanarGeometry();
                pg.PolyLoop = PtsToPolyLoop(shafts[i].loop);
                shaft.PlanarGeometry = pg;
                cmp.Shaft[i] = shaft;
            }

            // try to update all space id, surface id and spaceIdRef
            foreach (Surface srf in cmp.Surface)
            {
                if (srf is null) continue;
                srf.id = UpdateElementId(srf.id, offsetLv);
                if (srf.AdjacentSpaceId is null)
                {
                    continue;
                }
                else
                {
                    foreach (var refId in srf.AdjacentSpaceId)
                    {
                        refId.spaceIdRef = UpdateElementId(refId.spaceIdRef, offsetLv);
                    }
                }
            }
            foreach (Space sp in cmp.Buildings[0].Spaces)
            {
                if (sp is null) continue;
                sp.id = UpdateElementId(sp.id, offsetLv);
            }

            //write xml to the file

            XmlSerializer writer = new XmlSerializer(typeof(gbXML));
            FileStream file = File.Create(path);
            writer.Serialize(file, gbx);
            file.Close();
        }

        #region geometric info translate
        public static CartesianPoint PtToCartesianPoint(gbXYZ pt)
        {
            CartesianPoint cpt = new CartesianPoint();
            cpt.Coordinate = new string[3];
            CultureInfo ci = new CultureInfo(String.Empty);
            string xformat = string.Format(ci, "{0:0.000000}", pt.X - Properties.Settings.Default.originX);
            string yformat = string.Format(ci, "{0:0.000000}", pt.Y - Properties.Settings.Default.originY);
            string zformat = string.Format(ci, "{0:0.000000}", pt.Z - Properties.Settings.Default.originZ);
            cpt.Coordinate[0] = xformat;
            cpt.Coordinate[1] = yformat;
            cpt.Coordinate[2] = zformat;
            return cpt;
        }

        // note that all polyloops are not enclosed
        // also the input ptsLoop here is not closed
        public static PolyLoop PtsToPolyLoop(List<gbXYZ> ptsLoop)
        {
            PolyLoop pl = new PolyLoop();
            pl.Points = new CartesianPoint[ptsLoop.Count];
            for (int i = 0; i < ptsLoop.Count; i++)
            {
                CartesianPoint cpt = PtToCartesianPoint(ptsLoop[i]);
                pl.Points[i] = cpt;
            }
            return pl;
        }
        #endregion

        #region XML class translate
        public static Campus CreateCampus(string id)
        {
            Campus cmp = new Campus();
            cmp.id = id;
            return cmp;
        }

        public static Building MakeBuilding(double bldgArea, string bldgName, buildingTypeEnum bldgType)
        {
            Building zeb = new Building();
            zeb.Area = string.Format("{0:0.000000}", bldgArea);
            zeb.id = bldgName;
            zeb.buildingType = bldgType;
            //this has been arbitrarily defined and could be changed
            zeb.bldgStories = new BuildingStorey[100];
            zeb.Spaces = new Space[10000];
            return zeb;
        }

        public static BuildingStorey MakeStorey(gbLevel level, List<gbXYZ> ptsLoop)
        {
            BuildingStorey bs = new BuildingStorey();
            bs.id = level.label;
            bs.Name = "Story-" + level.id;
            bs.Level = level.elevation.ToString();

            //there is only one plane per story
            PlanarGeometry pg = new PlanarGeometry();
            pg.PolyLoop = PtsToPolyLoop(ptsLoop);
            bs.PlanarGeo = pg;
            return bs;
        }


        // currently only the default settings added to the space
        public static Space AddSpaceProgram(Space space)
        {
            space.lightScheduleIdRef = "lightSchedule-1";
            space.equipmentScheduleIdRef = "equipmentSchedule-1";
            space.peopleScheduleIdRef = "peopleSchedule-1";
            space.conditionType = "HeatedAndCooled";
            space.buildingStoreyIdRef = "bldg-story-1";
            space.peoplenum = 12;
            space.totalpeoplegain = 450;
            space.senspeoplegain = 250;
            space.latpeoplegain = 200;
            space.PeopleHeatGains = new PeopleHeatGain[3];
            space.lpd = 1.2;
            space.epd = 1.5;

            PeopleNumber pn = new PeopleNumber();
            pn.unit = peopleNumberUnitEnum.NumberOfPeople;

            string people = gb.FormatDoubleToString(space.peoplenum);
            pn.valuefield = people;
            space.PeopleNumber = pn;

            PeopleHeatGain phg = new PeopleHeatGain();
            phg.unit = peopleHeatGainUnitEnum.BtuPerHourPerson;
            phg.heatGainType = peopleHeatGainTypeEnum.Total;
            string totalpopload = gb.FormatDoubleToString(space.totalpeoplegain);
            phg.value = totalpopload;
            space.PeopleHeatGains[0] = phg;

            PeopleHeatGain shg = new PeopleHeatGain();
            shg.unit = peopleHeatGainUnitEnum.BtuPerHourPerson;
            shg.heatGainType = peopleHeatGainTypeEnum.Sensible;
            string senspopload = gb.FormatDoubleToString(space.senspeoplegain);
            shg.value = senspopload;
            space.PeopleHeatGains[1] = shg;

            PeopleHeatGain lhg = new PeopleHeatGain();
            lhg.unit = peopleHeatGainUnitEnum.BtuPerHourPerson;
            lhg.heatGainType = peopleHeatGainTypeEnum.Latent;
            string latpopload = gb.FormatDoubleToString(space.latpeoplegain);
            lhg.value = latpopload;
            space.PeopleHeatGains[2] = lhg;

            LightPowerPerArea lpd = new LightPowerPerArea();
            lpd.unit = powerPerAreaUnitEnum.WattPerSquareFoot;
            lpd.lpd = gb.FormatDoubleToString(space.lpd);
            space.LightPowerPerArea = lpd;

            EquipPowerPerArea epd = new EquipPowerPerArea();
            epd.unit = powerPerAreaUnitEnum.WattPerSquareFoot;
            epd.epd = gb.FormatDoubleToString(space.epd);
            space.EquipPowerPerArea = epd;

            return space;
        }

        public static Space MakeSpace(gbZone zone, int GUID)
        {
            Space space = new Space();

            // SEMANTIC SETTINGS
            space = AddSpaceProgram(space);

            space.id = zone.id;
            space.Name = $"F{zone.level.id}_{GUID}_{zone.function}";
            space.buildingStoreyIdRef = zone.level.label;
            space.Area = zone.area;
            space.Volume = zone.volume;
            space.PlanarGeo = new PlanarGeometry();
            space.ShellGeo = new ShellGeometry();
            space.cadid = new CADObjectId();
            space.cadid.id = "???????";

            Area spacearea = new Area();
            spacearea.val = gb.FormatDoubleToString(space.Area);
            space.spacearea = spacearea;

            Volume spacevol = new Volume();
            spacevol.val = gb.FormatDoubleToString(space.Volume);
            space.spacevol = spacevol;

            // /PLANARGEOMETRY
            PlanarGeometry spaceplpoly = new PlanarGeometry();
            spaceplpoly.PolyLoop = PtsToPolyLoop(GBMethod.GetPolyLastPointRemoved(zone.loop));
            space.PlanarGeo = spaceplpoly;

            // /SHELLGEOMETRY
            ShellGeometry sg = new ShellGeometry();
            sg.unit = lengthUnitEnum.Meters;
            sg.id = "sg_" + space.Name;

            // /SHELLGEOMETRY /CLOSEDSHELL
            sg.ClosedShell = new ClosedShell();
            sg.ClosedShell.PolyLoops = new PolyLoop[zone.numFaces];
            for (int i = 0; i < zone.numFaces; i++)
            {
                sg.ClosedShell.PolyLoops[i] = PtsToPolyLoop(zone.faces[i].loop);
            }
            space.ShellGeo = sg;

            // SPACEBOUNDARY
            space.spbound = new SpaceBoundary[zone.numFaces];
            for (int i = 0; i < zone.numFaces; i++)
            {
                SpaceBoundary sb = new SpaceBoundary();
                sb.surfaceIdRef = zone.faces[i].id;
                PlanarGeometry pg = new PlanarGeometry();
                pg.PolyLoop = PtsToPolyLoop(zone.faces[i].loop);
                sb.PlanarGeometry = pg;
                space.spbound[i] = sb;
            }

            if (zone.level.isShadowing == true)
                space.isShadowing = "true";

            return space;
        }

        public static Surface MakeSurface(gbSurface face, int GUID)
        {
            Surface surface = new Surface();


            // SEMANTIC
            surface.id = face.id;
            surface.Name = "Surface-" + GUID; // false
            surface.surfaceType = face.type;
            if (face.type == surfaceTypeEnum.ExteriorWall ||
                face.type == surfaceTypeEnum.Roof || 
                face.type == surfaceTypeEnum.Shade)
                surface.exposedToSunField = true;
            else
                surface.exposedToSunField = false;

            // 20230329 see XMLGeometry.cs Appendix for Firewall
            surface.isFirewall = face.isFirewall;

            //surface.constructionIdRef = face.id; // back projection to some construction dict

            // there can only be two adjacent spaces for an interior wall
            // this second boundary split is mandatory for energy simulation
            if (face.type != surfaceTypeEnum.Shade)
            {
                AdjacentSpaceId adjspace1 = new AdjacentSpaceId();
                adjspace1.spaceIdRef = face.parentId;
                if (face.adjSrfId.Contains("Outside"))
                {
                    AdjacentSpaceId[] adjspaces = { adjspace1 };
                    surface.AdjacentSpaceId = adjspaces;
                }
                else
                {
                    // the adjacent space is decoded from the label of adjacent surface
                    // it is crucial about how you code the name
                    AdjacentSpaceId adjspace2 = new AdjacentSpaceId();
                    Match match = Regex.Match(face.adjSrfId, "(.+)::(.+)");
                    adjspace2.spaceIdRef = match.Groups[1].Value;
                    AdjacentSpaceId[] adjspaces = { adjspace1, adjspace2 };
                    surface.AdjacentSpaceId = adjspaces;
                }
            }

            RectangularGeometry rg = new RectangularGeometry();
            rg.Azimuth = face.azimuth.ToString();
            rg.CartesianPoint = PtToCartesianPoint(face.loop[0]);
            rg.Tilt = face.tilt.ToString();

            rg.Width = string.Format("{0:0.000000}", face.width);
            rg.Height = string.Format("{0:0.000000}", face.height);
            surface.RectangularGeometry = rg;

            PlanarGeometry pg = new PlanarGeometry();
            pg.PolyLoop = PtsToPolyLoop(GBMethod.GetOpenPolyLoop(face.loop));
            surface.PlanarGeometry = pg;

            // openings
            if (face.openings.Count > 0)
            {
                surface.Opening = new Opening[face.openings.Count];
                int winCounter = 0;
                int doorCounter = 0;
                int nullCounter = 0;
                for (int i = 0; i < face.openings.Count; i++)
                {
                    Opening op = new Opening();
                    
                    op.id = face.openings[i].id;
                    op.openingType = face.openings[i].type;
                    if (op.openingType == openingTypeEnum.FixedWindow)
                    {
                        op.Name = surface.Name + "_Window_" + winCounter;
                        winCounter++;
                    }
                    else if (op.openingType == openingTypeEnum.NonSlidingDoor)
                    {
                        op.Name = surface.Name + "_Door_" + doorCounter;
                        doorCounter++;
                    }
                    else
                    {
                        op.Name = surface.Name + "_Null_" + nullCounter;
                        nullCounter++;
                    }
                        
                    RectangularGeometry op_rg = new RectangularGeometry();
                    op_rg.Azimuth = face.azimuth.ToString();
                    op_rg.Tilt = face.tilt.ToString();
                    // in gbXML schema, the point here represents the relative position
                    // of the opening and its parent surface. It is calculated by the points
                    // at the left down corner
                    op_rg.CartesianPoint = PtToCartesianPoint(
                        GBMethod.RelativePt(face.openings[i].loop[0], face.loop[0]));
                    op_rg.Width = string.Format("{0:0.000000}", face.openings[i].width);
                    op_rg.Height = string.Format("{0:0.000000}", face.openings[i].height);
                    op.rg = op_rg;

                    PlanarGeometry op_pg = new PlanarGeometry();
                    op_pg.PolyLoop = PtsToPolyLoop(face.openings[i].loop);
                    op.pg = op_pg;

                    surface.Opening[i] = op;
                }
            }

            return surface;
        }
        #endregion


        /// <summary>
        /// Return the id string of the adjacent surface of the current gbSurface
        /// by looking up in the unique surface list
        /// </summary>
        private static string RetrieveMatchedSrf(string id, List<gbSurface> faces)
        {
            if (faces.Count == 0)
                return null;
            foreach (gbSurface face in faces)
                if (id == face.adjSrfId)
                    return face.id;
            return null;
        }

        /// <summary>
        /// Rename element id by certain floor level offset. For example, "F0" offsets 2 levels as "U2"
        /// </summary>
        private static string UpdateElementId(string id, int offset)
        {
            string[] labelLv = id.Split(new string[] {"::"}, 2, StringSplitOptions.RemoveEmptyEntries);
            List<string> digits = Regex.Split(labelLv[0], @"\D+").Where(s => s != string.Empty).ToList();
            int offsetLv = Convert.ToInt32(digits[0]) - offset;
            if (offsetLv >= 0)
                return $"F{offsetLv}::{labelLv[1]}";
            else
                return $"U{-offsetLv}::{labelLv[1]}";
        }
    }
}
