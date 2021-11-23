using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Globalization;
using System.ComponentModel;
using System.Runtime.Remoting.Metadata.W3cXsd2001;


namespace Gingerbread.Core
{
    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class Absorptance
    {
        public Absorptance()
        {
            //empty constructor
        }

        [XmlAttribute]
        public unitlessUnitEnum unit { get; set; }
        [XmlAttribute]
        public absorptanceUnitEnum type { get; set; }
        [XmlText]
        public double value { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum absorptanceUnitEnum
    {
        IntIr,
        IntSolar,
        IntVisible,
        IntTotal,
        ExtIR,
        ExtSolar,
        ExtVisible,
        ExtTotal,
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class AdjacentSpaceId
    {
        [XmlAttribute]
        public string spaceIdRef { get; set; }
        public surfaceTypeEnum surfaceTypeField { get; set; }
        [XmlIgnore]
        public bool surfaceTypeFieldSpecified { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class Albedo
    {
        [XmlAttribute]
        public unitlessUnitEnum unit { get; set; }
        [XmlText]
        public double value { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class Area
    {
        [XmlText]
        public string val { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class BeginDate
    {
        [XmlElement(DataType = "date")]
        public DateTime val { get; set; }

    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class Building
    {
        [XmlAttribute]
        public string id { get; set; }
        [XmlAttribute]
        public buildingTypeEnum buildingType { get; set; }
        public double Area { get; set; }
        [XmlElement("BuildingStorey")]
        public BuildingStorey[] bldgStories { get; set; }
        [XmlElement("Space")]
        public Space[] Spaces { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class BuildingStorey
    {
        [XmlAttribute]
        public string id { get; set; }
        public string Name { get; set; }
        public string Level { get; set; }
        [XmlElement("PlanarGeometry")]
        public PlanarGeometry PlanarGeo { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum buildingTypeEnum
    {
        AutomotiveFacility,
        ConventionCenter,
        Courthouse,
        DataCenter,
        DiningBarLoungeOrLeisure,
        DiningCafeteriaFastFood,
        DiningFamily,
        Dormitory,
        ExerciseCenter,
        FireStation,
        Gymnasium,
        HospitalOrHealthcare,
        Hotel,
        Library,
        Manufacturing,
        Motel,
        MotionPictureTheatre,
        MultiFamily,
        Museum,
        Office,
        ParkingGarage,
        Penitentiary,
        PerformingArtsTheater,
        PoliceStation,
        PostOffice,
        ReligiousBuilding,
        Retail,
        SchoolOrUniversity,
        SingleFamily,
        SportsArena,
        TownHall,
        Transportation,
        Unknown
    }


    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class CADMaterialId
    {
        [XmlAttribute]
        public string CADMaterialIdRef { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class CADObjectId
    {
        [XmlText]
        public string id { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class Campus
    {
        [XmlAttribute]
        public string id;
        public Location Location;
        [XmlElement(ElementName = "Building", IsNullable = false)]
        public Building[] Buildings { get; set; }
        [XmlElement(ElementName = "Surface", IsNullable = false)]
        public Surface[] Surface { get; set; }

        // custom class for BEMBots
        [XmlElement(ElementName = "Column", IsNullable = true)]
        public Column[] Column { get; set; }
        [XmlElement(ElementName = "Beam", IsNullable = true)]
        public Beam[] Beam { get; set; }
        [XmlElement(ElementName = "Shaft", IsNullable = true)]
        public Shaft[] Shaft { get; set; }

    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class CartesianPoint
    {
        [XmlElement(ElementName = "Coordinate", IsNullable = false)]
        public string[] Coordinate;
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class ClosedShell
    {
        [XmlElement(IsNullable = false, ElementName = "PolyLoop")]
        public PolyLoop[] PolyLoops { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class Conductivity
    {
        [XmlAttribute]
        public conductivityUnitEnum unit { get; set; }
        [XmlText]
        public double value { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum conductivityUnitEnum
    {
        WPerCmC,
        WPerMeterK,
        BtuPerHourFtF,
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class Construction
    {

        public Construction()
        {
            //an empty constructor
        }

        [XmlAttribute]
        public string id { get; set; }
        [XmlAttribute]
        public string DOELibIdRef { get; set; }

        [XmlElement(IsNullable = false)]
        public Absorptance[] Absorptance { get; set; }
        [XmlElement(IsNullable = false)]
        public Albedo Albedo { get; set; }
        [XmlArray(IsNullable = false)]
        public Cost[] Cost { get; set; }
        [XmlElement(IsNullable = false)]
        public string Description;
        [XmlElement(IsNullable = false)]
        public Emittance[] Emittance { get; set; }
        [XmlElement(IsNullable = false)]
        public ExtEquipId ExtEquipId { get; set; }
        [XmlElement(IsNullable = false)]
        public FireFace[] FireFace { get; set; }


        [XmlElement(IsNullable = false)]
        public LayerId[] LayerId { get; set; }
        //need loadcalc parameters class defined and added
        [XmlElement(IsNullable = false)]
        public string Name;
        [XmlElement(IsNullable = false)]
        public PercentExisting PercentExisting { get; set; }
        [XmlElement(IsNullable = false)]
        public Reflectance[] Reflectance { get; set; }
        [XmlElement(IsNullable = false)]
        public Roughness Roughness { get; set; }
        [XmlElement(IsNullable = false)]
        public Transmittance[] Transmittance { get; set; }
        [XmlElement(IsNullable = false, ElementName = "U-Value")]
        public UValue Uvalue { get; set; }


    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class Cost
    {
        [XmlAttribute]
        public costTypeEnum costType { get; set; }
        [XmlElement(IsNullable = false)]
        public CostValue CostValue { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum costTypeEnum
    {
        CO2,
        SOx,
        NOx,
        EmbodiedEnergyTransportation,
        EmbodiedEnergyManufacture,
        EmbodiedEnergyInstallation,
        EmbodiedEnergyTotal,
        PurchaseCost,
        DeliveryCost,
        FirstCost,
        MonthlyCost,
        AnnualCost,
        MaintenanceCost,
        OverhaulCost,
        InstallationCost,
    }


    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class CostValue
    {
        [XmlAttribute]
        public currencyTypeEnum currency;
        [XmlAttribute]
        public string unit;
        [XmlText]
        public double value;
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum currencyTypeEnum
    {
        USDollars,
        CanadianDollars,
        Pesos,
        Euros,
        Yen,
        IndianRupee,
        Yuan,
        TaiwanDollar,
        EnglishPound,
        Mark,
        Franc,
        Ruble,
        Real,
        NewZealandDollar,
        AustralianDollar,
    }


    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class CreatedBy
    {
        [XmlAttribute]
        public string personId { get; set; }
        [XmlAttribute]
        public string programId { get; set; }
        [XmlAttribute]
        public DateTime date { get; set; }
        [XmlElement]
        public string CADModelId { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum dayTypeEnum
    {
        Weekday,
        Weekend,
        Holiday,
        WeekendOrHoliday,
        HeatingDesignDay,
        CoolingDesignDay,
        Sun,
        Mon,
        Tue,
        Wed,
        Thu,
        Fri,
        Sat,
        All
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class Day
    {
        [XmlAttribute]
        public dayTypeEnum dayType { get; set; }
        [XmlAttribute]
        public string dayScheduleIdRef { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class DaySchedule
    {
        [XmlElement]
        public string Name { get; set; }
        [XmlElement]
        public string Description { get; set; }
        [XmlElement]
        public ScheduleValue[] ScheduleValue { get; set; }
        [XmlAttribute]
        public string id { get; set; }
        [XmlAttribute]
        public scheduleTypeEnum type { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class Density
    {
        [XmlAttribute]
        public densityUnitEnum unit { get; set; }
        [XmlText]
        public double value { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum densityUnitEnum
    {
        GramsPerCubicCm,
        LbsPerCubicIn,
        LbsPerCubicFt,
        KgPerCubicM,
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class DocumentHistory
    {
        [XmlAttribute]
        public string id { get; set; }
        [XmlElement]
        public ProgramInfo ProgramInfo { get; set; }
        [XmlElement]
        public PersonInfo PersonInfo { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class EmissionRate
    {
        [XmlAttribute]
        public emissionUnitEnum unit { get; set; }
        [XmlText]
        public double value { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum emissionUnitEnum
    {
        PoundsPerHour,
        KilogramsPerHour,
        GramsPerHour,
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class Emittance
    {
        [XmlAttribute]
        public unitlessUnitEnum unit;
        [XmlAttribute]
        public emittanceTypeEnum type;
        [XmlText]
        public double value { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum emittanceTypeEnum
    {
        IntIR,
        ExtIR,
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class EndDate
    {
        [XmlText]
        public DateTime val { get; set; }

    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class EquipPowerPerArea
    {
        [XmlAttribute]
        public powerPerAreaUnitEnum unit { get; set; }
        [XmlText]
        public string epd { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class ExtEquipId
    {
        [XmlAttribute]
        public string extEquipIdRef { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class Fire
    {
        [XmlElement]
        public Thickness Thickness { get; set; }
        [XmlElement]
        public Conductivity Conductivity { get; set; }
        [XmlElement]
        public Density Density { get; set; }
        [XmlElement]
        public SpecificHeat SpecificHeat { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class FireFace
    {
        public Absorptance[] Absorbtance { get; set; }
        public Reflectance[] Reflectance { get; set; }
        public Emittance[] Emittance { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum gasTypeEnum
    {
        Air,
        Argon,
        Krypton,
        CO2,
        SF6,
        Custom
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class Gap
    {
        [XmlAttribute]
        public string id { get; set; }
        [XmlAttribute]
        public gasTypeEnum gas { get; set; }
        [XmlElement]
        public string Name { get; set; }
        [XmlElement]
        public string Description { get; set; }
        [XmlElement]
        public Thickness Thickness { get; set; }
        [XmlElement]
        public Density Density { get; set; }
        [XmlElement]
        public Conductivity Conductivity { get; set; }

    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class gbXML
    {
        [XmlAttribute]
        public temperatureUnitEnum temperatureUnit { get; set; }
        [XmlAttribute]
        public lengthUnitEnum lengthUnit { get; set; }
        [XmlAttribute]
        public areaUnitEnum areaUnit { get; set; }
        [XmlAttribute]
        public volumeUnitEnum volumeUnit { get; set; }
        [XmlAttribute]
        public versionEnum version { get; set; }
        [XmlAttribute]
        public string useSIUnitsForResults { get; set; }

        [XmlElement(IsNullable = false)]
        public Campus Campus;
        [XmlElement(IsNullable = false, ElementName = "Construction")]
        public Construction[] Constructions;
        [XmlElement(IsNullable = false, ElementName = "Layer")]
        public Layer[] Layers;
        [XmlElement(IsNullable = false, ElementName = "Material")]
        public Material[] Materials;
        [XmlElement(IsNullable = false, ElementName = "WindowType")]
        public WindowType[] WindowTypes;
        [XmlElement(IsNullable = false, ElementName = "Glaze")]
        public Glaze[] Glazing;
        [XmlElement(IsNullable = false, ElementName = "Gap")]
        public Gap[] Gaps;
        [XmlElement(IsNullable = false, ElementName = "Schedule")]
        public Schedule[] Schedule;
        [XmlElement(IsNullable = false, ElementName = "WeekSchedule")]
        public WeekSchedule[] WeekSchedule;
        [XmlElement(IsNullable = false, ElementName = "DaySchedule")]
        public DaySchedule[] DaySchedule;
        //[XmlElement(IsNullable = false, ElementName = "Layer")]
        //public Layer[] Layers;
        //[XmlElement(IsNullable = false, ElementName = "Material")]
        //public Material[] Materials;

    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class Glaze
    {
        [XmlAttribute]
        public string id { get; set; }
        [XmlAttribute]
        public string DOELibIdRef { get; set; }
        [XmlElement]
        public string Name { get; set; }
        [XmlElement]
        public string Description { get; set; }
        [XmlElement]
        public Transmittance[] Transmittance { get; set; }
        [XmlElement]
        public Reflectance[] Reflectance { get; set; }
        [XmlElement]
        public Emittance[] Emittance { get; set; }
        [XmlElement]
        public Thickness Thickness { get; set; }
        [XmlElement]
        public Conductivity Conductivity { get; set; }
        [XmlElement]
        public Reference Reference { get; set; }

    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class HighLimit
    {
        [XmlAttribute]
        public unitlessSmallUnitEnum unit { get; set; }
        [XmlText]
        public double value { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class IndoorAirQuality
    {
        [XmlAttribute]
        public string id { get; set; }
        [XmlAttribute]
        public string scheduleIdRef { get; set; }
        [XmlElement]
        public EmissionRate EmissionRate { get; set; }
        [XmlElement]
        public SinkRate SinkRate { get; set; }
        [XmlElement]
        public HighLimit HighLimit { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class InsideAirFilmResistance
    {
        [XmlAttribute]
        public resistanceUnitEnum unit { get; set; }
        [XmlText]
        public double value { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class Layer
    {
        [XmlAttribute]
        public string id { get; set; }
        [XmlAttribute]
        public string DOELibIdRef { get; set; }

        [XmlElement(IsNullable = false)]
        public string Name { get; set; }
        [XmlElement(IsNullable = false)]
        public string Description { get; set; }
        [XmlElement(IsNullable = false)]
        public Cost[] Cost { get; set; }
        [XmlElement(IsNullable = false)]
        public InsideAirFilmResistance InsideAirFilmResistance { get; set; }
        [XmlElement(IsNullable = true)]
        public MaterialId[] MaterialId { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class LayerId
    {
        [XmlAttribute]
        public string layerIdRef { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class Length
    {

        public lengthUnitEnum unitField { get; set; }

        public bool unitFieldSpecified { get; set; }

        public decimal valueField { get; set; }

        /// <remarks/>
        [System.Xml.Serialization.XmlAttributeAttribute()]
        public lengthUnitEnum unit
        {
            get
            {
                return this.unitField;
            }
            set
            {
                this.unitField = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlIgnoreAttribute()]
        public bool unitSpecified
        {
            get
            {
                return this.unitFieldSpecified;
            }
            set
            {
                this.unitFieldSpecified = value;
            }
        }

        /// <remarks/>
        [System.Xml.Serialization.XmlTextAttribute()]
        public decimal Value
        {
            get
            {
                return this.valueField;
            }
            set
            {
                this.valueField = value;
            }
        }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum lengthUnitEnum
    {
        Kilometers,
        Meters,
        Centimeters,
        Millimeters,
        Miles,
        Yards,
        Feet,
        Inches,
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum areaUnitEnum
    {
        SqaureKilometers,
        SquareMeters,
        SquareCentimeters,
        SquareMillimeters,
        SquareMiles,
        SquareYards,
        SquareFeet,
        SquareInches,
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum volumeUnitEnum
    {
        CubicKilometers,
        CubicMeters,
        CubicCentimeters,
        CubicMillimeters,
        CubicMiles,
        CubicYards,
        CubicFeet,
        CubicInches,
    }


    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class LightPowerPerArea
    {
        [XmlAttribute]
        public powerPerAreaUnitEnum unit { get; set; }
        [XmlText]
        public string lpd { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class Location
    {
        public string Name { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public double CADModelAzimuth { get; set; }
        public string ZipcodeOrPostalCode { get; set; }

    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class Material
    {
        [XmlAttribute]
        public string id { get; set; }
        [XmlAttribute]
        public string DOELibIdRef { get; set; }

        [XmlElement(IsNullable = false)]
        public string Name { get; set; }
        [XmlElement(IsNullable = false)]
        public string Description { get; set; }
        [XmlElement(IsNullable = false)]
        public Thickness Thickness { get; set; }
        [XmlElement(IsNullable = false)]
        public Conductivity Conductivity { get; set; }
        [XmlElement(IsNullable = false)]
        public Density Density { get; set; }
        [XmlElement(IsNullable = false)]
        public SpecificHeat SpecificHeat { get; set; }
        [XmlElement(IsNullable = false)]
        public Permeance Permeance { get; set; }
        [XmlElement(IsNullable = false)]
        public Porosity Porosity { get; set; }
        [XmlElement(IsNullable = false, ElementName = "R-value")]
        public RValue RValue { get; set; }
        [XmlElement(IsNullable = false)]
        public RecycledContent RecycledContent { get; set; }
        [XmlElement(IsNullable = false)]
        public Fire Fire { get; set; }
        [XmlElement(IsNullable = false)]
        public Cost[] Cost { get; set; }
        [XmlElement(IsNullable = false)]
        public IndoorAirQuality IndoorAirQuality { get; set; }
        [XmlElement(IsNullable = false)]
        public CADMaterialId[] CADMaterialId { get; set; }
        [XmlElement(IsNullable = false)]
        public Reference[] Reference { get; set; }

    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class MaterialId
    {
        [XmlAttribute]
        public string materialIdRef { get; set; }
        [XmlAttribute]
        public double percentOfLayer { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class Opening
    {
        [XmlAttribute]
        public string id { get; set; }

        [XmlAttribute]
        public string constructionIdRef { get; set; }

        [XmlAttribute]
        public string ifcGUID { get; set; }

        [XmlAttribute]
        public string windowTypeIdRef { get; set; }

        [XmlElement(IsNullable = false)]
        public string Description { get; set; }

        [XmlElement(IsNullable = true)]
        public string Name { get; set; }

        [XmlAttribute]
        public openingTypeEnum openingType { get; set; }

        [XmlElement(IsNullable = false, ElementName = "RectangularGeometry")]
        public RectangularGeometry rg { get; set; }

        [XmlElement(ElementName = "PlanarGeometry")]
        public PlanarGeometry pg { get; set; }

    }

    public enum openingTypeEnum
    {
        FixedWindow,
        OperableWindow,
        FixedSkylight,
        OperableSkylight,
        SlidingDoor,
        NonSlidingDoor,
        Air
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class PersonInfo
    {
        [XmlAttribute]
        public string id { get; set; }
        [XmlElement]
        public string LastName { get; set; }
        [XmlElement]
        public string FirstName { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class PeopleHeatGain
    {
        [XmlAttribute]
        public peopleHeatGainUnitEnum unit { get; set; }
        [XmlAttribute]
        public peopleHeatGainTypeEnum heatGainType { get; set; }
        public string programidfield { get; set; }
        [XmlText]
        public string value { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum peopleHeatGainTypeEnum
    {
        Total,
        Sensible,
        Latent,
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum peopleHeatGainUnitEnum
    {
        WattPerPerson,
        BtuPerHourPerson,
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class PeopleNumber
    {
        [XmlAttribute]
        public peopleNumberUnitEnum unit { get; set; }
        public string progidfield { get; set; }
        [XmlText]
        public string valuefield { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum peopleNumberUnitEnum
    {
        NumberOfPeople,
        SquareFtPerPerson,
        SquareMPerPerson,
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class PercentExisting
    {
        [XmlAttribute]
        public unitlessUnitEnum unit { get; set; }
        [XmlText]
        public double value { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class Permeance
    {
        [XmlAttribute]
        public permUnitEnum unit { get; set; }
        [XmlText]
        public double value { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum permUnitEnum
    {
        PermSI,
        PermIP,
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class PlanarGeometry
    {
        public PolyLoop PolyLoop { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class Porosity
    {
        [XmlAttribute]
        public unitlessUnitEnum unit { get; set; }
        [XmlText]
        public double value { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class ProgramInfo
    {
        [XmlAttribute]
        public string id { get; set; }
        [XmlElement("CompanyName")]
        public string CompanyName { get; set; }
        [XmlElement("ProductName")]
        public string ProductName { get; set; }
        [XmlElement("Version")]
        public string Version { get; set; }
        [XmlElement]
        public string Platform { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class PolyLoop
    {
        [XmlElement(IsNullable = false, ElementName = "CartesianPoint")]
        public CartesianPoint[] Points { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum powerPerAreaUnitEnum
    {
        WattPerSquareMeter,
        WattPerSquareFoot,
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum radiationWavelengthTypeEnum
    {
        Solar,
        IR,
        Visible,
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class RValue
    {
        [XmlAttribute]
        public resistanceUnitEnum unit { get; set; }
        [XmlText]
        public double value { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class RectangularGeometry
    {
        public string Azimuth { get; set; }
        public CartesianPoint CartesianPoint { get; set; }
        public string Tilt { get; set; }
        public string Height { get; set; }
        public string Width { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class RecycledContent
    {
        [XmlAttribute]
        public unitlessUnitEnum unit { get; set; }
        [XmlText]
        public double value { get; set; }

    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class Reference
    {
        [XmlText]
        public string value { get; set; }
    }
    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class Reflectance
    {
        [XmlAttribute]
        public unitlessUnitEnum unit { get; set; }
        [XmlAttribute]
        public reflectanceTypeEnum type { get; set; }
        [XmlText]
        public double value;

    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum reflectanceTypeEnum
    {
        Ground,
        IntSolar,
        IntIR,
        IntVisible,
        ExtSolar,
        ExtVisible,
        ExtIR,
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum resistanceUnitEnum
    {
        SquareMeterKPerW,
        HrSquareFtFPerBTU,
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class Roughness
    {
        [XmlAttribute]
        public roughnessValueEnum value { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum roughnessValueEnum
    {
        VeryRough,
        MediumRough,
        Rough,
        Smooth,
        MediumSmooth,
        VerySmooth,
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class Schedule
    {
        [XmlAttribute]
        public string id { get; set; }
        [XmlAttribute]
        public scheduleTypeEnum type { get; set; }
        [XmlAttribute]
        public string programID { get; set; }
        [XmlElement]
        public string Name { get; set; }
        [XmlElement]
        public string Description { get; set; }
        [XmlElement]
        public YearSchedule[] YearSchedule { get; set; }

    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class ScheduleValue
    {
        [XmlText]
        public double value { get; set; }
    }


    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum scheduleTypeEnum
    {
        Temp,
        Fraction,
        OnOff

    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class ShadingCoeff
    {
        [XmlAttribute]
        public unitlessUnitEnum unit { get; set; }
        [XmlText]
        public double value { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class ShellGeometry
    {
        [XmlAttribute]
        public string id { get; set; }
        [XmlAttribute]
        public lengthUnitEnum unit { get; set; }
        public ClosedShell ClosedShell;
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class SinkRate
    {
        [XmlAttribute]
        public sinkRateUnitEnum unit { get; set; }
        [XmlText]
        public double value { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum sinkRateUnitEnum
    {
        PoundsPerHour,
        KilogramsPerHour,
        GramsPerHour,
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class SolarHeatGainCoeff
    {
        [XmlAttribute]
        public unitlessUnitEnum unit { get; set; }
        [XmlAttribute]
        public double solarIncidentAngle { get; set; }
        [XmlText]
        public double value { get; set; }
    }


    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class Space
    {
        [XmlAttribute]
        public string id;
        [XmlAttribute]
        public string zoneIdRef;
        [XmlAttribute]
        public string lightScheduleIdRef;
        [XmlAttribute]
        public string equipmentScheduleIdRef;
        [XmlAttribute]
        public string peopleScheduleIdRef;
        [XmlAttribute]
        public string conditionType;
        [XmlAttribute]
        public string buildingStoreyIdRef;

        public string Name { get; set; }
        public PeopleNumber PeopleNumber { get; set; }
        [XmlElement("PeopleHeatGain")]
        public PeopleHeatGain[] PeopleHeatGains { get; set; }
        [XmlIgnore]
        public double peoplenum { get; set; }
        [XmlIgnore]
        public double totalpeoplegain { get; set; }
        [XmlIgnore]
        public double senspeoplegain { get; set; }
        [XmlIgnore]
        public double latpeoplegain { get; set; }

        public LightPowerPerArea LightPowerPerArea { get; set; }
        [XmlIgnore]
        public double lpd { get; set; }
        public EquipPowerPerArea EquipPowerPerArea { get; set; }
        [XmlIgnore]
        public double epd { get; set; }

        [XmlIgnore]
        public double Area { get; set; }
        [XmlElement("Area")]
        public Area spacearea { get; set; }

        [XmlIgnore]
        public double Volume { get; set; }
        [XmlElement("Volume")]
        public Volume spacevol { get; set; }

        [XmlElement("PlanarGeometry")]
        public PlanarGeometry PlanarGeo { get; set; }
        [XmlElement("ShellGeometry")]
        public ShellGeometry ShellGeo { get; set; }
        [XmlElement("CADObjectId")]
        public CADObjectId cadid { get; set; }
        [XmlElement("SpaceBoundary")]
        public SpaceBoundary[] spbound { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class SpaceBoundary
    {
        [XmlAttribute]
        public string surfaceIdRef { get; set; }
        public PlanarGeometry PlanarGeometry { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class SpecificHeat
    {
        [XmlAttribute]
        public specificHeatEnum unit { get; set; }
        [XmlText]
        public double value { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum specificHeatEnum
    {
        JPerKgK,
        BTUPerLbF,
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class Surface
    {
        [XmlAttribute]
        public string id { get; set; }
        [XmlAttribute]
        public surfaceTypeEnum surfaceType { get; set; }
        [XmlAttribute]
        public string constructionIdRef { get; set; }
        public string Name { get; set; }
        [XmlElement(IsNullable = false, ElementName = "AdjacentSpaceId")]
        public AdjacentSpaceId[] AdjacentSpaceId { get; set; }
        public RectangularGeometry RectangularGeometry { get; set; }
        [XmlElement]
        public PlanarGeometry PlanarGeometry { get; set; }
        [XmlElement(ElementName = "Opening", IsNullable = false)]
        public Opening[] Opening { get; set; }
        [XmlIgnore]
        public bool exposedToSunField;
        [XmlElement(IsNullable = false)]
        public CADObjectId CADObjectId { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum surfaceTypeEnum
    {
        InteriorWall,
        ExteriorWall,
        Roof,
        InteriorFloor,
        ExposedFloor,
        Shade,
        UndergroundWall,
        UndergroundSlab,
        Ceiling,
        Air,
        UndergroundCeiling,
        RaisedFloor,
        SlabOnGrade,
        FreestandingColumn,
        EmbeddedColumn,
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum temperatureUnitEnum
    {
        F,
        C,
        K,
        R,
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class Thickness
    {
        [XmlAttribute]
        public lengthUnitEnum unit { get; set; }
        [XmlText]
        public double value { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class Transmittance
    {
        [XmlAttribute]
        public unitlessUnitEnum unit { get; set; }
        [XmlAttribute]
        public radiationWavelengthTypeEnum type { get; set; }
        [XmlText]
        public double value { get; set; }
    }



    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class UValue
    {
        [XmlAttribute]
        public uValueUnitEnum unit { get; set; }
        [XmlText]
        public double value { get; set; }
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum uValueUnitEnum
    {
        WPerSquareMeterK,
        BtuPerHourSquareFtF,
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum unitlessUnitEnum
    {
        Percent,
        Fraction,
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum unitlessSmallUnitEnum
    {
        Percent,
        PartPerMillionByVolumeaction,
        milliGramsPerCubicMeter,
    }

    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.1")]
    [System.SerializableAttribute()]
    [System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.gbxml.org/schema")]
    public enum versionEnum
    {
        [Description("0.35")]
        OhThreeFive = 0,
        [Description("0.36")]
        OhThreeSix = 1,
        [Description("0.37")]
        OhThreeSeven = 2,
        [Description("5.00")]
        FiveOhOh = 3,
        [Description("5.01")]
        FiveOhOne = 4,
        [Description("5.10")]
        FiveOneOh = 5,
        [Description("5.11")]
        FiveOneOne = 6,
        [Description("5.12")]
        FiveOneTwo = 7,
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class Volume
    {
        [XmlText]
        public string val { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class WeekSchedule
    {
        [XmlElement]
        public string Name { get; set; }
        [XmlElement]
        public string Description { get; set; }
        [XmlElement]
        public Day[] Day { get; set; }
        [XmlAttribute]
        public string id { get; set; }
        [XmlAttribute]
        public scheduleTypeEnum type { get; set; }

    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = false, Namespace = "http://www.gbxml.org/schema")]
    public class WeekScheduleId
    {
        [XmlAttribute]
        public string weekScheduleIdRef { get; set; }
        [XmlIgnore]
        public bool weekIdSpecified { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class YearSchedule
    {
        [XmlElement]
        public string Name { get; set; }
        [XmlElement]
        public string Description { get; set; }
        [XmlElement(DataType = "date")]
        public DateTime BeginDate { get; set; }
        [XmlElement(DataType = "date")]
        public DateTime EndDate { get; set; }
        [XmlElement]
        public WeekScheduleId WeekScheduleId { get; set; }

        [XmlAttribute]
        public string id { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class WindowType
    {
        [XmlAttribute]
        public string id { get; set; }
        [XmlAttribute]
        public string DOELibIdRef { get; set; }
        [XmlElement]
        public string Name { get; set; }
        [XmlElement]
        public string Description { get; set; }
        [XmlElement]
        public UValue UValue { get; set; }
        [XmlElement]
        public ShadingCoeff ShadingCoeff { get; set; }
        [XmlElement]
        public SolarHeatGainCoeff[] SolarHeatGainCoeff { get; set; }
        [XmlElement]
        public Transmittance Transmittance { get; set; }
        [XmlElement]
        public Reflectance Reflectance { get; set; }
        [XmlElement]
        public Emittance Emittance { get; set; }
        //Gaps and Glazing from outside to Inside
        [XmlElement]
        public Gap[] Gap { get; set; }
        [XmlElement]
        public Glaze[] Glaze { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class Column
    {
        [XmlAttribute]
        public string id { get; set; }
        [XmlAttribute]
        public int level { get; set; }
        [XmlElement]
        public PlanarGeometry PlanarGeometry { get; set; }
        public string Width { get; set; }
        public string Height { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class Beam
    {
        [XmlAttribute]
        public string id { get; set; }
        [XmlAttribute]
        public int level { get; set; }
        [XmlElement]
        public PlanarGeometry PlanarGeometry { get; set; }
        public string Width { get; set; }
        public string Height { get; set; }
    }

    [Serializable]
    [XmlRootAttribute(ElementName = "gbXML", IsNullable = true, Namespace = "http://www.gbxml.org/schema")]
    public class Shaft
    {
        [XmlAttribute]
        public string id { get; set; }
        [XmlAttribute]
        public int level { get; set; }
        [XmlElement]
        public PlanarGeometry PlanarGeometry { get; set; }
    }

    public static class gb
    {
        public static CultureInfo gbci;
        //this should not be serializable
        public static string FormatDoubleToString(double number)
        {

            string ret;
            ret = string.Format(gb.gbci, "{0:0.000000}", number);
            return ret;
        }
    }
}
