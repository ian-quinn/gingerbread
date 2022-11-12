#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Collections;
using System.Linq;
using System.IO;
using System.Reflection;
using WinForms = System.Windows.Forms;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

using Gingerbread.Core;
#endregion

namespace Gingerbread
{
    static public class Util
    {
        public const double _eps = 1.0e-9;

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
                Autodesk.Revit.DB.Reference r = uidoc.Selection.PickObject(ObjectType.Element, "Please select " + description); 

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

        public static IEnumerable<Document> GetLinkedDocuments(Document doc)
        {
            var linkedfiles = GetLinkedFileReferences(doc);
            var linkedFileNames = linkedfiles
                .Select(x => ModelPathUtils.ConvertModelPathToUserVisiblePath(x.GetAbsolutePath()))
                .ToList();

            return doc.Application.Documents
                .Cast<Document>()
                .Where(document => linkedFileNames
                    .Any(fileName => document.PathName.Equals(fileName)));
        }

        public static IEnumerable<ExternalFileReference> GetLinkedFileReferences(Document doc)
        {
            //ElementFilter categoryFilter = new ElementCategoryFilter(BuiltInCategory.OST_RvtLinks);
            //ElementFilter typeFilter = new ElementClassFilter(typeof(Instance));
            //ElementFilter logicalFilter = new LogicalAndFilter(categoryFilter, typeFilter);
            var collector = new FilteredElementCollector(doc);
            var linkedElements = collector
                .OfClass(typeof(RevitLinkType))
                //.OfCategory(BuiltInCategory.OST_RvtLinks)
                //.WherePasses(logicalFilter)
                .Select(x => x.GetExternalFileReference())
                .ToList();

            return linkedElements;
        }
        #endregion

        #region Conversion

        // Unit conversion

        /// <summary>
        /// Convert a given length in feet to milimeters.
        /// </summary>
        public static double FootToMm(double length) { return length * 304.8; }
        public static double FootToM(double length) { return length * 0.3048; }
        /// <summary>
        /// Convert a given length in milimeters to feet.
        /// </summary>
        public static double MmToFoot(double length) { return length / 304.8; }
        public static double MToFoot(double length) { return length / 0.3048; }

        /// <summary>
        /// Convert a given point or vector from milimeters to feet.
        /// </summary>
        public static XYZ MmToFoot(XYZ v) { return v.Divide(304.8); }


        // Geometry types conversion

        /// <summary>
        /// Convert List of lines to List of curves
        /// </summary>
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
        public static List<Line> CrvsToLines(List<Curve> crvs)
        {
            List<Line> lines = new List<Line>();
            foreach (Curve crv in crvs)
            {
                lines.Add(crv as Line);
            }
            return lines;
        }

        public static gbXYZ gbXYZConvert(XYZ pt)
        { return new gbXYZ(FootToM(pt.X), FootToM(pt.Y), FootToM(pt.Z)); }
        public static XYZ gbXYZConvert(gbXYZ pt)
        { return new XYZ(MToFoot(pt.X), MToFoot(pt.Y), MToFoot(pt.Z)); }
        public static XYZ gbXYZFlatten(gbXYZ pt)
        { return new XYZ(MToFoot(pt.X), MToFoot(pt.Y), 0); }
        public static List<gbXYZ> gbXYZsConvert(List<XYZ> pts)
        {
            List<gbXYZ> gbPts = new List<gbXYZ>();
            foreach (XYZ pt in pts)
                gbPts.Add(gbXYZConvert(pt));
            return gbPts;
        }
        public static List<XYZ> gbXYZsConvert(List<gbXYZ> gbPts)
        {
            List<XYZ> pts = new List<XYZ>();
            foreach (gbXYZ gbPt in gbPts)
                pts.Add(gbXYZConvert(gbPt));
            return pts;
        }
        public static List<JsonSchema.UV> gbXYZ2Json(List<gbXYZ> gbPts)
        {
            List<JsonSchema.UV> pts = new List<JsonSchema.UV>();
            foreach (gbXYZ gbPt in gbPts)
                pts.Add(new JsonSchema.UV { coordU = Math.Round(gbPt.X, 4), coordV = Math.Round(gbPt.Y, 4)});
            return pts;
        }
        public static gbSeg gbSegConvert(Line line)
        {
            return new gbSeg(
            gbXYZConvert(line.GetEndPoint(0)),
            gbXYZConvert(line.GetEndPoint(1)) ); 
        }
        public static Line gbSegConvert(gbSeg seg)
        {
            return Line.CreateBound(
            gbXYZFlatten(seg.PointAt(0)),
            gbXYZFlatten(seg.PointAt(1)));
        }
        public static List<Curve> gbSegsConvert(List<gbSeg> segs)
        {
            List<Curve> crvs = new List<Curve>();
            foreach (gbSeg seg in segs)
                crvs.Add(gbSegConvert(seg) as Curve);
            return crvs;
        }

        public static List<XYZ> PtsFlatten(List<XYZ> pts)
        {
            List<XYZ> ptsFlatten = new List<XYZ>();
            foreach (XYZ pt in pts)
                ptsFlatten.Add(new XYZ(pt.X, pt.Y, 0));
            return ptsFlatten;
        }

        #endregion

        #region Revit Geometry Operations

        /// <summary>
        /// Get the boundary of the section shape between a given plane and a solid.
        /// Return null if they are isolated.
        /// </summary>
        /// <returns></returns>
        public static List<CurveLoop> GetSolidPlaneIntersectionCurve(Plane plane, Solid solid)
        {
            if (solid == null)
                return null;
            Solid cast = BooleanOperationsUtils.CutWithHalfSpace(solid, plane);
            if (cast == null)
            {
                cast = BooleanOperationsUtils.CutWithHalfSpace(solid,
                    Plane.CreateByNormalAndOrigin(-plane.Normal, plane.Origin));
                if (cast == null)
                    return null;
            }
            PlanarFace cutFace = null;
            foreach (Face face in cast.Faces)
            {
                PlanarFace pf = face as PlanarFace;
                if (pf == null) continue;
                if (pf.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ.Negate()) &&
                    pf.Origin.Z == plane.Origin.Z)
                {
                    cutFace = pf;
                }
            }
            if (cutFace == null) return null;
            List<CurveLoop> boundary = cutFace.GetEdgesAsCurveLoops().ToList();
            return boundary;
        }

        /// <summary>
        /// A draft method for centerline extraction of a rectangle.
        /// </summary>
        /// <returns></returns>
        public static Curve GetCenterlineOfRectangle(CurveLoop bound)
        {
            if (bound == null)
                return null;
            List<XYZ> pts = new List<XYZ>() { };
            foreach (Curve crv in bound)
                pts.Add(crv.GetEndPoint(0));
            if (pts.Count != 4)
                return null;
            Curve crv1 = Line.CreateBound((pts[0] + pts[1]) / 2, (pts[2] + pts[3]) / 2);
            Curve crv2 = Line.CreateBound((pts[1] + pts[2]) / 2, (pts[3] + pts[0]) / 2);
            return crv1.Length > crv2.Length ? crv1 : crv2;
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
            Debug.Assert(null != e.Location, "Util:: " + "expected an element with a valid Location");
            LocationCurve lc = e.Location as LocationCurve;
            Debug.Assert(null != lc, "Util:: " + "expected an element with a valid LocationCurve");
            return lc.Curve;
        }

        /// <summary>
        /// Return the location point of a family instance or null.
        /// This null coalesces the location so you won't get an 
        /// error if the FamilyInstance is an invalid object.  
        /// Borrowed from Jeremy. Abandoned for now. 
        /// </summary>
        public static XYZ GetFamilyInstanceLocation(FamilyInstance fi)
        {
            return ((LocationPoint)fi?.Location)?.Point;
        }

        public static XYZ GetFamilyInstanceLocationPoint(FamilyInstance fi)
        {
            LocationPoint lp = fi.Location as LocationPoint;
            // LocationPoint.Point may not be XYZ?
            if (null != lp)
            {
                double x = lp.Point.X;
                double y = lp.Point.Y;
                double z = lp.Point.Z;
                return new XYZ(x, y, z);
            }
            else
            {
                //is it hosted?
                Element host = fi.Host;
                if (host == null) return null;
                LocationCurve locationCurve = (LocationCurve)host.Location;
                if (locationCurve == null) return null;
                XYZ point = locationCurve.Curve.Evaluate(fi.HostParameter, false);
                if (point == null)
                    return null;
                return point;
            }
        }

        public static Curve GetFamilyInstanceLocationCurve(FamilyInstance fi)
        {
            return ((LocationCurve)fi?.Location)?.Curve;
        }

        public static List<Line> GetCurtainGridVerticalLattice(Document doc, CurtainGrid cg)
        {
            List<Line> vCluster = new List<Line>();
            List<Line> uCluster = new List<Line>();
            List<XYZ> vStartCluster = new List<XYZ>();
            List<XYZ> vEndCluster = new List<XYZ>();
            List<XYZ> uStartCluster = new List<XYZ>();
            List<XYZ> uEndCluster = new List<XYZ>();

            List<ElementId> vIds = cg.GetVGridLineIds().ToList();
            List<ElementId> uIds = cg.GetUGridLineIds().ToList();
            for (int v = 0; v < vIds.Count; v++)
            {
                CurtainGridLine cgLine = doc.GetElement(vIds[v]) as CurtainGridLine;
                Curve gl = cgLine.FullCurve;
                vCluster.Add(Line.CreateBound(gl.GetEndPoint(0), gl.GetEndPoint(1)));
                vStartCluster.Add(gl.GetEndPoint(0));
                vEndCluster.Add(gl.GetEndPoint(1));
            }
            for (int u = 0; u < uIds.Count; u++)
            {
                CurtainGridLine cgLine = doc.GetElement(uIds[u]) as CurtainGridLine;
                Curve gl = cgLine.FullCurve;
                uCluster.Add(Line.CreateBound(gl.GetEndPoint(0), gl.GetEndPoint(1)));
                uStartCluster.Add(gl.GetEndPoint(0));
                uEndCluster.Add(gl.GetEndPoint(1));
            }
            // get the lower limit
            vStartCluster = vStartCluster.OrderBy(z => z.Z).ToList();
            vEndCluster = vEndCluster.OrderBy(z => z.Z).ToList();
            double upperBound = vEndCluster.Last().Z;
            double lowerBound = vStartCluster[0].Z;
            if (vCluster.Count == 0 || uCluster.Count == 0)
                return vCluster;
            if (uCluster.Count == 1)
            {
                double currentZ = uCluster[0].GetEndPoint(0).Z;
                XYZ basePt = Basic.LineIntersectPlane(vCluster[0].GetEndPoint(0), vCluster[0].GetEndPoint(1), currentZ);
                Transform tf1 = Transform.CreateTranslation(uCluster[0].GetEndPoint(0) - basePt);
                Transform tf2 = Transform.CreateTranslation(uCluster[0].GetEndPoint(1) - basePt);
                vCluster.Insert(0, vCluster[0].CreateTransformed(tf1) as Line);
                vCluster.Add(vCluster[0].CreateTransformed(tf2) as Line);
                return vCluster;
            }
            XYZ pt1 = Basic.LineIntersectPlane(uCluster[0].GetEndPoint(0), uCluster.Last().GetEndPoint(0), lowerBound);
            XYZ pt2 = Basic.LineIntersectPlane(uCluster[0].GetEndPoint(0), uCluster.Last().GetEndPoint(0), upperBound);
            XYZ pt3 = Basic.LineIntersectPlane(uCluster[0].GetEndPoint(1), uCluster.Last().GetEndPoint(1), lowerBound);
            XYZ pt4 = Basic.LineIntersectPlane(uCluster[0].GetEndPoint(1), uCluster.Last().GetEndPoint(1), upperBound);
            vCluster.Insert(0, Line.CreateBound(pt1, pt2));
            vCluster.Add(Line.CreateBound(pt3, pt4));
            return vCluster;
        }


        // Detailed line methods
        public static void GetListOfLinestyles(Document doc)
        {
            Category c = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
            CategoryNameMap subcats = c.SubCategories;
            foreach (Category lineStyle in subcats)
            {
                Debug.Print("Util:: " + "Line style", string.Format("Linestyle {0} id {1}", lineStyle.Name, lineStyle.Id.ToString()));
            }
        }

        /// <summary>
        /// Create a sketch plane with the given normal and origin.
        /// </summary>
        public static SketchPlane PlaneNormal(Document doc, XYZ normal, XYZ origin)
        {
            return SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(normal, origin));
        }
        /// <summary>
        /// Create a sketch plane based on the current view. If it is ViewPlan, return the 
        /// sketch plane based on the current level. If else, return the world base plane.
        /// </summary>
        public static SketchPlane PlaneView(Document doc)
        {
            View view = doc.ActiveView;
            if (view is ViewPlan)
            {
                double activeViewElevation = view.Origin.Z;
                IList<Element> eLevels = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Levels)
                    .WhereElementIsNotElementType()
                    .ToElements();
                foreach (Element eLevel in eLevels)
                {
                    if (eLevel is Level)
                    {
                        Level lv = eLevel as Level;
                        if (lv.Name == view.Name)
                        {
                            activeViewElevation = lv.Elevation;
                        }
                    }
                }
                return SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(
                    view.ViewDirection, new XYZ(0, 0, activeViewElevation)));
            }
            else
            {
                return SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(
                    view.ViewDirection, view.Origin));
            }
        }


        // ----------------------MODELLINE-------------------------

        /// <summary>
        /// Draw model curves based on co-plane curves. Note that the Model Curve are some 
        /// kind of a direct shape in the document. It is irrelvant to the view (visible in 
        /// 3D-view of course). If not shown, maybe it is off the current ViewPlan. Please 
        /// make proper transformation before you apply this function.
        /// </summary>
        public static void SketchCurves(Document doc, List<Curve> crvs)
        {
            SketchPlane sp = PlaneView(doc);
            double elevation = sp.GetPlane().Origin.Z;
            foreach(Curve crv in crvs)
            {
                XYZ pt = crv.GetEndPoint(0);
                XYZ moveVector = new XYZ(0, 0, elevation - pt.Z);
                Transform tf = Transform.CreateTranslation(moveVector);
                Curve crv_proj = crv.CreateTransformed(tf);
                doc.Create.NewModelCurve(crv_proj, sp);
            }
                
        }
        public static void SketchPtLoop(Document doc, List<XYZ> pts)
        {
            SketchPlane sp = PlaneView(doc);
            double elevation = sp.GetPlane().Origin.Z;
            List<XYZ> pts_proj = new List<XYZ>() { };
            foreach (XYZ pt in pts)
            {
                pts_proj.Add(new XYZ(0, 0, elevation - pt.Z));
            }
            for (int i = 0; i < pts_proj.Count - 1; i++)
            {
                Curve edge = Line.CreateBound(pts_proj[i], pts_proj[i + 1]);
                doc.Create.NewModelCurve(edge, sp);
            }
        }
        public static void SketchSegs(Document doc, List<gbSeg> segs)
        {
            SketchPlane sp = PlaneView(doc);
            double elevation = sp.GetPlane().Origin.Z;
            XYZ pt = gbXYZConvert(segs[0].Start);
            XYZ moveVector = new XYZ(pt.X, pt.Y, elevation - pt.Z);
            Transform tf = Transform.CreateTranslation(moveVector);
            foreach (gbSeg seg in segs)
            {
                // Curve does not accept zero length, not like Grasshopper
                // so check if the length satisfies the tolerance first
                if (seg.Length >= Properties.Settings.Default.ShortCurveTolerance)
                {
                    Curve crv = gbSegConvert(seg).CreateTransformed(tf);
                    doc.Create.NewModelCurve(crv, sp);
                }
            }
        }
        public static void SketchMarker(Document doc, XYZ pt, double size = 1, string style = "O")
        {
            SketchPlane sp = PlaneView(doc);
            double elevation = sp.GetPlane().Origin.Z;
            XYZ flatPt = new XYZ(MToFoot(pt.X), MToFoot(pt.Y), elevation);
            if (style == "O")
            {
                XYZ xAxis = new XYZ(1, 0, elevation);
                XYZ yAxis = new XYZ(0, 1, elevation);
                doc.Create.NewModelCurve(Arc.Create(flatPt, size, 0, 2 * Math.PI, xAxis, yAxis), sp);
            }
            if (style == "X")
            {
                XYZ v = new XYZ(size, size, elevation);
                doc.Create.NewModelCurve(Line.CreateBound(flatPt - v, flatPt + v), sp);
                v = new XYZ(size, -size, elevation);
                doc.Create.NewModelCurve(Line.CreateBound(flatPt - v, flatPt + v), sp);
            }
        }
        public static void SketchMarkers(Document doc, gbXYZ gbPt, double size = 1, string style = "O")
        {
            SketchMarker(doc, gbXYZConvert(gbPt), size, style);
        }
        public static void SketchMarkers(Document doc, List<XYZ> pts, double size = 1, string style = "O")
        {
            SketchPlane sp = PlaneView(doc);
            foreach (XYZ pt in pts)
            {
                SketchMarker(doc, pt, size, style);
            }
        }
        public static void SketchMarkers(Document doc, List<gbXYZ> pts,double size = 1, string style = "O")
        {
            SketchMarkers(doc, gbXYZsConvert(pts), size, style);
        }


        // ----------------------DETAILLINE-------------------------

        /// <summary>
        /// Draw detail curves on the ActiveView. Note that they must be planar curves 
        /// but not necessarily on the current ViewPlan. The Create.NewDetailCurve() will 
        /// project the curve on to the ActiveView automatically. (not working in 3D-view)
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
                    Debug.Print("Util:: " + "There's no matching pattern in the document");
                }
            }


            using (Transaction tx = new Transaction(doc))
            {
                tx.Start("Create Detail Curves");

                foreach (Curve crv in crvs)
                {
                    if (crv is null)
                        continue;
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
                    Debug.Print("Util:: " + "There's no matching pattern in the document");
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
                    //SketchMarkers(doc, vertices, 0.2, "O");
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
        //public static string UnitSymbolTypeString(UnitSymbolType u)
        //{
        //    string s = u.ToString();

        //    Debug.Assert(s.StartsWith("UST_"),
        //      "Util:: " + "expected UnitSymbolType enumeration value to begin with 'UST_'");

        //    s = s.Substring(4).Replace("_SUP_", "^").ToLower();

        //    return s;
        //}


        public static string ListString(List<bool> list)
        {
            string fusion = "";
            for (int index = 0; index < list.Count(); index++)
            {
                fusion = fusion + list[index].ToString() + " ";
            }
            return fusion;
        }

        public static string ListString(List<int> list)
        {
            string fusion = "";
            for (int index = 0; index < list.Count(); index++)
            {
                fusion = fusion + list[index].ToString() + " ";
            }
            return fusion;
        }

        public static string ListString(List<double> list)
        {
            string fusion = "";
            for (int index = 0; index < list.Count(); index++)
            {
                fusion = fusion + list[index].ToString() + " ";
            }
            return fusion;
        }

        public static string ListString(List<XYZ> list)
        {
            string fusion = "";
            for (int index = 0; index < list.Count(); index++)
            {
                fusion = fusion + PointString(list[index]) + " ";
            }
            return fusion;
        }

        public static double SumDoubles(List<double> nums)
        {
            double sum = 0;
            foreach (double num in nums)
                sum += num;
            return sum;
        }

        /// <summary>
        /// Serialize all outward vectors of each joint in point alignment
        /// </summary>
        public static List<string> HandString(List<List<gbXYZ>> hands)
        {
            List<string> handsList = new List<string>();
            foreach (List<gbXYZ> hand in hands)
            {
                string serialization = "";
                foreach (gbXYZ h in hand)
                {
                    serialization = serialization + "{" + h.ToString() + "}";
                    if (hand.IndexOf(h) != hand.Count - 1)
                        serialization += "#";
                }
                handsList.Add(serialization);
            }
            return handsList;
        }

        /// <summary>
        /// Serialize a list of regions for Rhino visualization
        /// </summary>
        public static string RegionString(List<gbRegion> regions)
        {
            string serialization = "";
            foreach (gbRegion region in regions)
            {
                string loopString = "";
                if (region.loop == null || region.loop.Count == 0)
                    continue;
                foreach (gbXYZ pt in region.loop)
                {
                    loopString += $"{{{pt.X}, {pt.Y}, {pt.Z}}}#";
                }
                serialization += loopString + "\n";
            }
            return serialization;
        }
        public static string LoopString(List<List<gbXYZ>> loops)
        {
            string serialization = "";
            foreach (List<gbXYZ> loop in loops)
            {
                string loopString = "";
                foreach (gbXYZ pt in loop)
                {
                    loopString += $"{{{pt.X}, {pt.Y}, {pt.Z}}}#";
                }
                serialization += loopString + "\n";
            }
            return serialization;
        }


        #endregion // Formatting

        #region aux
        public static bool IsZero(double a, double tolerance = _eps)
        {
            return tolerance > Math.Abs(a);
        }

        public static bool IsEqual(double a, double b, double tolerance = _eps)
        {
            return IsZero(b - a, tolerance);
        }

        // bubble sort two spans and return the length of their overlap
        public static double SpanOverlap(double a, double b, double c, double d)
        {
            if (a > b) Swap(ref a, ref b);
            if (c > d) Swap(ref c, ref d);
            if (b < c || a > d) return 0;

            double[] arr = new double[4] { a, b, c, d };
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 3 - i; j++)
                    if (arr[j] > arr[j + 1])
                        Swap(ref arr[j], ref arr[j + 1]);
            return arr[2] - arr[1];
        }

        /// <summary>
        /// Log the debug information
        /// </summary>
        public static void LogPrint(string msg)
        {
            string thisAssemblyFolderPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string logPath = thisAssemblyFolderPath + "/log.txt";
            using (var sw = File.AppendText(logPath))
            {
                sw.WriteLine($"[{DateTime.Now.ToLongTimeString()}] {msg}");
            }
        }

        public static void LogInitiate()
        {
            string thisAssemblyFolderPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string logPath = thisAssemblyFolderPath + "/log.txt";
            FileStream fs;
            if (File.Exists(logPath))
            {
                fs = new FileStream(logPath, FileMode.Truncate, FileAccess.Write);
            }
            else
            {
                fs = new FileStream(logPath, FileMode.Create, FileAccess.Write);
            }
            fs.Close();
        }

        public static void Swap<T>(ref T left, ref T right)
        {
            T temp;
            temp = left;
            left = right;
            right = temp;
        }

        public static List<T> FlattenList<T>(List<List<T>> nestedList)
        {
            List<T> flatList = new List<T>();
            foreach (List<T> list in nestedList)
                flatList.AddRange(list);
            return flatList;
        }
        #endregion
    }
}
