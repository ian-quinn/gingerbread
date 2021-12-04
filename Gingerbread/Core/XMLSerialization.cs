using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Gingerbread.Core
{
    class XMLSerialization
    {
        public static void Generate(string path, List<gbZone> zones, List<gbLoop> floors, List<gbSurface> faces,
            List<gbLoop> columns, List<gbLoop> beams, List<gbLoop> shafts)
        //    Dictionary<string, string> adjDict)
        {
            gb.gbci = new CultureInfo(String.Empty);

            //the basics
            //constructor to define the basics
            gbXML gbx = new gbXML();
            gbx.lengthUnit = lengthUnitEnum.Feet;
            gbx.temperatureUnit = temperatureUnitEnum.F;

            Campus cmp = CreateCampus("sample_0");

            cmp.Buildings = new Building[10000];
            gbx.Campus = cmp; // backward mapping

            //where does this location information from?  it could be smartly inferred somehow, but otherwise specified by the user/programmer
            Location zeloc = new Location();
            zeloc.Name = "???";
            zeloc.Latitude = "00.00";
            zeloc.Longitude = "00.00";
            cmp.Location = zeloc; // backward mapping

            // set an array as big as possible, revise here
            cmp.Buildings[0] = MakeBuilding(10000, "bldg_0", buildingTypeEnum.AutomotiveFacility);

            // STOREY
            for (int i = 0; i < floors.Count; i++)
                cmp.Buildings[0].bldgStories[i] = MakeStorey(floors[i].level, floors[i].loop);

            // SPACE
            for (int i = 0; i < zones.Count; i++)
                cmp.Buildings[0].Spaces[i] = MakeSpace(zones[i], i);

            // SURFACE
            List<gbSurface> uniqueSrfs = new List<gbSurface>();
            // either one of the two doppelganger surfaces is okay to be added
            // on condition that they are exactly the same!
            cmp.Surface = new Surface[faces.Count];
            int srfCounter = 0;
            for (int i = 0; i < faces.Count; i++)
            {
                if (faces[i].loop.Count < 4)
                    Debug.Print("Degenerated surface detected at: " + i);
                //Util.LogPrint(faces[i].id + "-" + faces[i].adjSrfId);
                if (IsDuplicateSrf(faces[i], uniqueSrfs))
                    continue;
                uniqueSrfs.Add(faces[i]);

                cmp.Surface[i] = MakeSurface(faces[i], srfCounter);
                srfCounter++;
            }

            //Appendix, columns, beams, shafts
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
            string zformat = string.Format(ci, "{0:0.000000}", pt.Z);
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

        public static Building MakeBuilding(double bldarea, string bldgname, buildingTypeEnum bldgType)
        {
            Building zeb = new Building();
            zeb.Area = bldarea;
            zeb.id = bldgname;
            zeb.buildingType = bldgType;
            //this has been arbitrarily defined and could be changed
            zeb.bldgStories = new BuildingStorey[1000];
            zeb.Spaces = new Space[10000];
            return zeb;
        }

        public static BuildingStorey MakeStorey(gbLevel level, List<gbXYZ> ptsLoop)
        {
            BuildingStorey bs = new BuildingStorey();
            bs.id = level.label;
            bs.Name = "Story-" + level.id;
            bs.Level = level.elevation.ToString();

            //there is only one plane per storey
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
            space.Name = "Space-" + GUID + "-" + zone.function;
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
            spaceplpoly.PolyLoop = PtsToPolyLoop(zone.loop);
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
                face.type == surfaceTypeEnum.Roof)
                surface.exposedToSunField = true;
            else
                surface.exposedToSunField = false;

            //surface.constructionIdRef = face.id; // back projection to some construction dict

            // there can only be two adjacent spaces for an interior wall
            // this second boudnary split is mandantory for energy simulation
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

            RectangularGeometry rg = new RectangularGeometry();
            rg.Azimuth = face.azimuth.ToString();
            rg.CartesianPoint = PtToCartesianPoint(face.loop[0]);
            rg.Tilt = face.tilt.ToString();

            rg.Width = string.Format("{0:0.000000}", face.width);
            rg.Height = string.Format("{0:0.000000}", face.height);
            surface.RectangularGeometry = rg;

            PlanarGeometry pg = new PlanarGeometry();
            pg.PolyLoop = PtsToPolyLoop(face.loop);
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

        public static bool IsDuplicateSrf(gbSurface target, List<gbSurface> faces)
        {
            if (faces.Count == 0)
                return false;
            foreach (gbSurface face in faces)
                if (target.adjSrfId == face.id)
                    return true;
            return false;
        }
    }
}
