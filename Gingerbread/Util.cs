#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Collections;
using System.Linq;
using System.Reflection;
using WinForms = System.Windows.Forms;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
#endregion

namespace Gingerbread
{
    static public class Util
    {
        #region Selection
        public static Element SelectSingleElement(UIDocument uidoc, string description)
        {
            if (ViewType.Internal == uidoc.ActiveView.ViewType)
            {
                TaskDialog.Show("Error", "Cannot pick element in this view: " + uidoc.ActiveView.Name);

                return null;
            }
            try
            {
                Reference r = uidoc.Selection.PickObject(ObjectType.Element, "Please select " + description); 

                return uidoc.Document.GetElement(r);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        /// Return the first element of the given type and name.
        /// </summary>
        public static Element GetFirstElementOfTypeNamed(Document doc, Type type, string name)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc).OfClass(type);

            #if EXPLICIT_CODE

              // explicit iteration and manual checking of a property:

              Element ret = null;
              foreach( Element e in collector )
              {
                if( e.Name.Equals( name ) )
                {
                  ret = e;
                  break;
                }
              }
              return ret;
            #endif // EXPLICIT_CODE

            #if USE_LINQ
            // using LINQ:

              IEnumerable<Element> elementsByName =
                from e in collector
                where e.Name.Equals( name )
                select e;

              return elementsByName.First<Element>();
            #endif // USE_LINQ

            // using an anonymous method:

            // if no matching elements exist, First<> throws an exception.

            //return collector.Any<Element>( e => e.Name.Equals( name ) )
            //  ? collector.First<Element>( e => e.Name.Equals( name ) )
            //  : null;

            // using an anonymous method to define a named method:

            Func<Element, bool> nameEquals = e => e.Name.Equals(name);

            return collector.Any<Element>(nameEquals) ? collector.First<Element>(nameEquals) : null;
        }


        public static Element GetSingleSelectedElement(UIDocument uidoc)
        {
            ICollection<ElementId> ids = uidoc.Selection.GetElementIds();

            Element e = null;

            if (1 == ids.Count)
            {
                foreach (ElementId id in ids)
                {
                    e = uidoc.Document.GetElement(id);
                }
            }
            return e;
        }


        static bool HasRequestedType( Element e, Type t,bool acceptDerivedClass)
        {
            bool rc = null != e;

            if (rc)
            {
                Type t2 = e.GetType();

                rc = t2.Equals(t);

                if (!rc && acceptDerivedClass)
                {
                    rc = t2.IsSubclassOf(t);
                }
            }
            return rc;
        }


        public static Element SelectSingleElementOfType(UIDocument uidoc, Type t, string description, bool acceptDerivedClass)
        {
            Element e = GetSingleSelectedElement(uidoc);

            if (!HasRequestedType(e, t, acceptDerivedClass))
            {
                e = Util.SelectSingleElement(
                  uidoc, description);
            }
            return HasRequestedType(e, t, acceptDerivedClass)
              ? e
              : null;
        }

        #endregion

        #region Conversion
        /// <summary>
        /// Convert a given length in feet to milimeters.
        /// </summary>
        public static double FootToMm(double length) { return length * 304.8; }

        /// <summary>
        /// Convert a given length in milimeters to feet.
        /// </summary>
        public static double MmToFoot(double length) { return length / 304.8; }

        /// <summary>
        /// Convert a given point or vector from milimeters to feet.
        /// </summary>
        public static XYZ MmToFoot(XYZ v) { return v.Divide(304.8); }

        /// <summary>
        /// Convert List of lines to List of curves
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        public static List<Curve> LinesToCrvs(List<Line> lines)
        {
            List<Curve> crvs = new List<Curve>();
            foreach (Line line in lines)
            {
                crvs.Add(line as Curve);
            }
            return crvs;
        }

        /// <summary>
        /// Convert List of curves to List of lines
        /// </summary>
        /// <param name="crvs"></param>
        /// <returns></returns>
        public static List<Line> CrvsToLines(List<Curve> crvs)
        {
            List<Line> lines = new List<Line>();
            foreach (Curve crv in crvs)
            {
                lines.Add(crv as Line);
            }
            return lines;
        }

        #endregion

        #region Sketch Modelline

        /// <summary>
        /// Create a plane perpendicular to the given factor.
        /// </summary>
        public static SketchPlane PlaneNormal(Document doc, XYZ normal, XYZ origin)
        {
            return SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(normal, origin));
        }

        public static SketchPlane PlaneWorld(Document doc)
        {
            return SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero));
        }


        // Marker methods

        /// <summary>
        /// Draw an X at the given position.
        /// </summary>
        public static void DrawMarkerX(XYZ p, double size, SketchPlane sketchPlane)
        {
            size *= 0.5;
            XYZ v = new XYZ(size, size, 0);
            Document doc = sketchPlane.Document;
            doc.Create.NewModelCurve(Line.CreateBound(p - v, p + v), sketchPlane);
            v = new XYZ(size, -size, 0);
            doc.Create.NewModelCurve(Line.CreateBound(p - v, p + v), sketchPlane);
        }

        /// <summary>
        /// Draw an O at the given position.
        /// </summary>
        public static void DrawMarkerO(XYZ p, double radius, SketchPlane sketchPlane)
        {
            Document doc = sketchPlane.Document;
            XYZ xAxis = new XYZ(1, 0, 0);
            XYZ yAxis = new XYZ(0, 1, 0);
            doc.Create.NewModelCurve(Arc.Create(p, radius, 0, 2 * Math.PI, xAxis, yAxis), sketchPlane);
        }

        #endregion

        #region Doc geometry methods
        /// <summary>
        /// Return the curve from a Revit database Element 
        /// location curve, if it has one.
        /// </summary>
        public static Curve GetLocationCurve(this Element e)
        {
            Debug.Assert(null != e.Location, "expected an element with a valid Location");

            LocationCurve lc = e.Location as LocationCurve;

            Debug.Assert(null != lc, "expected an element with a valid LocationCurve");

            return lc.Curve;
        }

        /// <summary>
        /// Return the location point of a family instance or null.
        /// This null coalesces the location so you won't get an 
        /// error if the FamilyInstance is an invalid object.  
        /// </summary>
        public static XYZ GetFamilyInstanceLocation(FamilyInstance fi)
        {
            return ((LocationPoint)fi?.Location)?.Point;
        }


        // Detailed line methods
        public static void GetListOfLinestyles(Document doc)
        {
            Category c = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);

            CategoryNameMap subcats = c.SubCategories;

            foreach (Category lineStyle in subcats)
            {
                Debug.Print("Line style", string.Format("Linestyle {0} id {1}", lineStyle.Name, lineStyle.Id.ToString()));
            }
        }

        #endregion

        #region Sketch DetailLines
        /// <summary>
        /// Draw detail curves based on List<Curve>
        /// </summary>
        public static void DrawDetailLines(Document doc, List<Curve> crvs, int weight = 2, string color = "red", string pattern = "")
        {
            GetListOfLinestyles(doc);

            View view = doc.ActiveView;
            Color palette = new Color(0, 0, 0);
            switch (color)
            {
                case "red": palette = new Color(200, 50, 80); break;
                case "blue": palette = new Color(100, 149, 237); break;
                case "orange": palette = new Color(255, 140, 0); break;
            }

            FilteredElementCollector fec = new FilteredElementCollector(doc)
                .OfClass(typeof(LinePatternElement));

            LinePatternElement linePatternElem = null;
            if (pattern != "")
            {
                try
                {
                    linePatternElem = fec
                        .Cast<LinePatternElement>()
                        .First<LinePatternElement>(linePattern => linePattern.Name == pattern);
                }
                catch
                {
                    Debug.Print("There's no matching pattern in the document");
                }
            }


            using (Transaction tx = new Transaction(doc))
            {
                tx.Start("Create Detail Curves");
                foreach (Curve crv in crvs)
                {
                    // Should do style setting here or...?
                    DetailCurve detailCrv = doc.Create.NewDetailCurve(view, crv);
                    GraphicsStyle gs = detailCrv.LineStyle as GraphicsStyle;
                    gs.GraphicsStyleCategory.LineColor = palette;
                    gs.GraphicsStyleCategory.SetLineWeight(weight, gs.GraphicsStyleType);
                    if (linePatternElem != null)
                    {
                        gs.GraphicsStyleCategory.SetLinePatternId(linePatternElem.Id, GraphicsStyleType.Projection);
                    }
                }
                tx.Commit();
            }
        }

        /// <summary>
        /// Draw point marker with detail circles.
        /// Optional colors are "red" "blue" "orange"
        /// </summary>
        public static void DrawDetailMarkers(Document doc, List<XYZ> pts, int weight = 2, string color = "red", string pattern = "")
        {
            GetListOfLinestyles(doc);

            View view = doc.ActiveView;
            Color palette = new Color(0, 0, 0);
            switch (color)
            {
                case "red": palette = new Color(200, 50, 80); break;
                case "blue": palette = new Color(100, 149, 237); break;
                case "orange": palette = new Color(255, 140, 0); break;
            }

            FilteredElementCollector fec = new FilteredElementCollector(doc)
                .OfClass(typeof(LinePatternElement));

            LinePatternElement linePatternElem = null;
            if (pattern != "")
            {
                try
                {
                    linePatternElem = fec
                        .Cast<LinePatternElement>()
                        .First<LinePatternElement>(linePattern => linePattern.Name == pattern);
                }
                catch
                {
                    Debug.Print("There's no matching pattern in the document");
                }
            }

            XYZ xAxis = new XYZ(1, 0, 0);
            XYZ yAxis = new XYZ(0, 1, 0);

            using (Transaction tx = new Transaction(doc))
            {
                tx.Start("Create Detail Markers");
                foreach (XYZ pt in pts)
                {
                    double radius = 0.3;
                    Arc marker = Arc.Create(pt, radius, 0, 2 * Math.PI, xAxis, yAxis);
                    DetailCurve detailCrv = doc.Create.NewDetailCurve(view, marker);
                    GraphicsStyle gs = detailCrv.LineStyle as GraphicsStyle;
                    gs.GraphicsStyleCategory.LineColor = palette;
                    gs.GraphicsStyleCategory.SetLineWeight(weight, gs.GraphicsStyleType);
                    if (linePatternElem != null)
                    {
                        gs.GraphicsStyleCategory.SetLinePatternId(linePatternElem.Id, GraphicsStyleType.Projection);
                    }
                }
                tx.Commit();
            }
        }

        #endregion

        #region Geometry
        public const double _eps = 1.0e-9;

        public static bool IsZero(double a, double tolerance = _eps)
        {
            return tolerance > Math.Abs(a);
        }

        public static bool IsVertical(XYZ v)
        {
            return IsZero(v.X) && IsZero(v.Y);
        }
        public static bool IsVertical(XYZ v, double tolerance)
        {
            return IsZero(v.X, tolerance)
              && IsZero(v.Y, tolerance);
        }

        #endregion


        #region DEBUG
        /// <summary>
        /// Return the coorinate of XYZ as a string
        /// </summary>
        /// <param name="pt"></param>
        /// <returns></returns>
        public static string PrintXYZ(XYZ pt)
        {
            return string.Format(" ({0}, {1}, {2}) ", pt.X, pt.Y, pt.Z);
        }

        /// <summary>
        /// Return the content of List(int) as a string
        /// </summary>
        /// <param name="seq"></param>
        /// <returns></returns>
        public static string PrintSeq(List<int> seq)
        {
            string result = "";
            foreach (int e in seq)
            {
                result = result + e.ToString() + " ";
            }
            return result;
        }
        #endregion

    }
}
