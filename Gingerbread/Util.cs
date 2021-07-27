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

        #region Sketch
        // USE WITHIN TRANSACTIONS

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


        // ----------------------MODELLINE-------------------------

        public static void SketchCurves(Document doc, List<Curve> crvs)
        {
            foreach(Curve crv in crvs)
            {
                doc.Create.NewModelCurve(crv, PlaneWorld(doc));
            }
        }

        public static void SketchMarkers(Document doc, List<XYZ> pts, double size = 1, string style = "O")
        {
            SketchPlane sketchPlane = PlaneWorld(doc);
            foreach(XYZ pt in pts)
            {
                if (style == "O")
                {
                    XYZ xAxis = new XYZ(1, 0, 0);
                    XYZ yAxis = new XYZ(0, 1, 0);
                    doc.Create.NewModelCurve(Arc.Create(pt, size, 0, 2 * Math.PI, xAxis, yAxis), sketchPlane);
                }
                if (style == "X")
                {
                    XYZ v = new XYZ(size, size, 0);
                    doc.Create.NewModelCurve(Line.CreateBound(pt - v, pt + v), sketchPlane);
                    v = new XYZ(size, -size, 0);
                    doc.Create.NewModelCurve(Line.CreateBound(pt - v, pt + v), sketchPlane);
                }
            }
        }
        

        // ----------------------DETAILLINE-------------------------

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

        /// <summary>
        /// Draw a polyline for testing
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="ply"></param>
        /// <param name="ptVisible"></param>
        public static void DrawPolyLine(Document doc, PolyLine ply, Boolean ptVisible = true)
        {
            if (null != ply)
            {
                View active_view = doc.ActiveView;

                // we'll come back to the alignment of IList and List
                List<XYZ> vertices = new List<XYZ>(ply.GetCoordinates());

                for (int i = 0; i < vertices.Count - 1; i++)
                {
                    // It seems that the PolyLine can store point array where each point is very closed to another
                    // but when you create a curve it is forbidden. right?
                    if (vertices[i].DistanceTo(vertices[i + 1]) < Properties.Settings.Default.ShortCurveTolerance)
                    {
                        continue;
                    }
                    Curve stroke = Line.CreateBound(vertices[i], vertices[i + 1]);

                    DetailCurve detailCrv = doc.Create.NewDetailCurve(active_view, stroke);
                }
                if (ptVisible)
                {
                    SketchMarkers(doc, vertices, 0.2, "O");
                }
                
                TextNoteType tnt = new FilteredElementCollector(doc)
                        .OfClass(typeof(TextNoteType)).First() as TextNoteType;
                TextNote startFlag = TextNote.Create(doc, active_view.Id, vertices[0], "START", tnt.Id);
                TextNote endFlag = TextNote.Create(doc, active_view.Id, vertices.Last(), "END", tnt.Id);
            }
        }

        #endregion


        // Borrowed from BuildingCoder by JeremyTammick
        #region Formatting
        /// <summary>
        /// Return an English plural suffix for the given
        /// number of items, i.e. 's' for zero or more
        /// than one, and nothing for exactly one.
        /// </summary>
        public static string PluralSuffix(int n)
        {
            return 1 == n ? "" : "s";
        }

        /// <summary>
        /// Return an English plural suffix 'ies' or
        /// 'y' for the given number of items.
        /// </summary>
        public static string PluralSuffixY(int n)
        {
            return 1 == n ? "y" : "ies";
        }

        /// <summary>
        /// Return a dot (full stop) for zero
        /// or a colon for more than zero.
        /// </summary>
        public static string DotOrColon(int n)
        {
            return 0 < n ? ":" : ".";
        }

        /// <summary>
        /// Return a string for a real number
        /// formatted to two decimal places.
        /// </summary>
        public static string RealString(double a)
        {
            return a.ToString("0.##");
        }

        /// <summary>
        /// Return a hash string for a real number
        /// formatted to nine decimal places.
        /// </summary>
        public static string HashString(double a)
        {
            return a.ToString("0.#########");
        }

        /// <summary>
        /// Return a string representation in degrees
        /// for an angle given in radians.
        /// </summary>
        public static string AngleString(double angle)
        {
            return RealString(angle * 180 / Math.PI) + " degrees";
        }

        /// <summary>
        /// Return a string for a length in millimetres
        /// formatted as an integer value.
        /// </summary>
        public static string MmString(double length)
        {
            //return RealString( FootToMm( length ) ) + " mm";
            return Math.Round(FootToMm(length)).ToString() + " mm";
        }

        /// <summary>
        /// Return a string for a UV point
        /// or vector with its coordinates
        /// formatted to two decimal places.
        /// </summary>
        public static string PointString(UV p, bool onlySpaceSeparator = false)
        {
            string format_string = onlySpaceSeparator ? "{0} {1}" : "({0},{1})";
            return string.Format(format_string, RealString(p.U), RealString(p.V));
        }

        /// <summary>
        /// Return a string for an XYZ point
        /// or vector with its coordinates
        /// formatted to two decimal places.
        /// </summary>
        public static string PointString(XYZ p, bool onlySpaceSeparator = false)
        {
            string format_string = onlySpaceSeparator ? "{0} {1} {2}" : "({0},{1},{2})";
            return string.Format(format_string, RealString(p.X), RealString(p.Y), RealString(p.Z));
        }

        /// <summary>
        /// Return a hash string for an XYZ point
        /// or vector with its coordinates
        /// formatted to nine decimal places.
        /// </summary>
        public static string HashString(XYZ p)
        {
            return string.Format("({0},{1},{2})", HashString(p.X), HashString(p.Y), HashString(p.Z));
        }

        /// <summary>
        /// Return a string for this bounding box
        /// with its coordinates formatted to two
        /// decimal places.
        /// </summary>
        public static string BoundingBoxString( BoundingBoxUV bb, bool onlySpaceSeparator = false)
        {
            string format_string = onlySpaceSeparator ? "{0} {1}" : "({0},{1})";

            return string.Format(format_string, PointString(bb.Min, onlySpaceSeparator), PointString(bb.Max, onlySpaceSeparator));
        }

        /// <summary>
        /// Return a string for this bounding box
        /// with its coordinates formatted to two
        /// decimal places.
        /// </summary>
        public static string BoundingBoxString( BoundingBoxXYZ bb, bool onlySpaceSeparator = false)
        {
            string format_string = onlySpaceSeparator ? "{0} {1}" : "({0},{1})";

            return string.Format(format_string, PointString(bb.Min, onlySpaceSeparator), PointString(bb.Max, onlySpaceSeparator));
        }

        /// <summary>
        /// Return a string for this plane
        /// with its coordinates formatted to two
        /// decimal places.
        /// </summary>
        public static string PlaneString(Plane p)
        {
            return string.Format("plane origin {0}, plane normal {1}", PointString(p.Origin), PointString(p.Normal));
        }

        /// <summary>
        /// Return a string for this transformation
        /// with its coordinates formatted to two
        /// decimal places.
        /// </summary>
        public static string TransformString(Transform t)
        {
            return string.Format("({0},{1},{2},{3})", PointString(t.Origin),
              PointString(t.BasisX), PointString(t.BasisY), PointString(t.BasisZ));
        }

        /// <summary>
        /// Return a string for a list of doubles 
        /// formatted to two decimal places.
        /// </summary>
        public static string DoubleArrayString( IEnumerable<double> a, bool onlySpaceSeparator = false)
        {
            string separator = onlySpaceSeparator ? " " : ", ";

            return string.Join(separator, a.Select<double, string>(x => RealString(x)));
        }

        /// <summary>
        /// Return a string for this point array
        /// with its coordinates formatted to two
        /// decimal places.
        /// </summary>
        public static string PointArrayString( IEnumerable<UV> pts, bool onlySpaceSeparator = false)
        {
            string separator = onlySpaceSeparator ? " " : ", ";

            return string.Join(separator, pts.Select<UV, string>(p => PointString(p, onlySpaceSeparator)));
        }

        /// <summary>
        /// Return a string for this point array
        /// with its coordinates formatted to two
        /// decimal places.
        /// </summary>
        public static string PointArrayString( IEnumerable<XYZ> pts, bool onlySpaceSeparator = false)
        {
            string separator = onlySpaceSeparator ? " " : ", ";

            return string.Join(separator, pts.Select<XYZ, string>(p => PointString(p, onlySpaceSeparator)));
        }

        /// <summary>
        /// Return a string representing the data of a
        /// curve. Currently includes detailed data of
        /// line and arc elements only.
        /// </summary>
        public static string CurveString(Curve c)
        {
            string s = c.GetType().Name.ToLower();

            XYZ p = c.GetEndPoint(0);
            XYZ q = c.GetEndPoint(1);

            s += string.Format(" {0} --> {1}", PointString(p), PointString(q));

            // To list intermediate points or draw an
            // approximation using straight line segments,
            // we can access the curve tesselation, cf.
            // CurveTessellateString:

            //foreach( XYZ r in lc.Curve.Tessellate() )
            //{
            //}

            // List arc data:

            Arc arc = c as Arc;

            if (null != arc)
            {
                s += string.Format(" center {0} radius {1}", PointString(arc.Center), arc.Radius);
            }

            // Todo: add support for other curve types
            // besides line and arc.

            return s;
        }

        /// <summary>
        /// Return a string for this curve with its
        /// tessellated point coordinates formatted
        /// to two decimal places.
        /// </summary>
        public static string CurveTessellateString(Curve curve)
        {
            return "curve tessellation " + PointArrayString(curve.Tessellate());
        }

        /// <summary>
        /// Convert a UnitSymbolType enumeration value
        /// to a brief human readable abbreviation string.
        /// </summary>
        public static string UnitSymbolTypeString( UnitSymbolType u)
        {
            string s = u.ToString();

            Debug.Assert(s.StartsWith("UST_"),
              "expected UnitSymbolType enumeration value to begin with 'UST_'");

            s = s.Substring(4).Replace("_SUP_", "^").ToLower();

            return s;
        }


        public static string JoinListString(List<bool> list)
        {
            string fusion = "";
            for (int index = 0; index < list.Count(); index++)
            {
                fusion = fusion + list[index].ToString() + " ";
            }
            return fusion;
        }

        public static string JoinListString(List<int> list)
        {
            string fusion = "";
            for (int index = 0; index < list.Count(); index++)
            {
                fusion = fusion + list[index].ToString() + " ";
            }
            return fusion;
        }


        #endregion // Formatting

    }
}
