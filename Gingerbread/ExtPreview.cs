#region Namespaces
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

using Gingerbread.Core;
using Gingerbread.Views;
#endregion

namespace Gingerbread
{
    public enum PrevSwitch { Depict, Erase }

    [Transaction(TransactionMode.Manual)]
    public class ExtPreview : IExternalEventHandler
    {
        public PrevSwitch runMode { get; set; } = PrevSwitch.Depict;
        public ViewPreview CurrentUI { get; set; }

        public ExtPreview() { }

        public void Execute(UIApplication uiapp)
        {
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            switch (runMode)
            {
                case PrevSwitch.Depict:

                    // clear previous drawing directshape ids
                    Properties.Settings.Default.prevSpaceIds = "";

                    if (Properties.Settings.Default.prevFloorId == -1)
                    {
                        CurrentUI.statusBar.Text = "Fail to get your selection of the floor.";
                        return;
                    }
                    try
                    {
                        JsonSchema.Building jsBuildingCheck = JsonSerializer.
                            Deserialize<JsonSchema.Building>(Properties.Settings.Default.geomInfo);
                    }
                    catch
                    {
                        CurrentUI.statusBar.Text = "Cannot decode the cached gbXML geometry.";
                        return;
                    }

                    View view = doc.ActiveView;
                    if (!(view is View3D))
                    {
                        CurrentUI.statusBar.Text = "Please switch to 3D View first.";
                        return;
                    }
                    View3D view3D = view as View3D;

                    JsonSchema.Building jsBuilding = JsonSerializer.
                            Deserialize<JsonSchema.Building>(Properties.Settings.Default.geomInfo);
                    // get the XY boundary of the section box
                    IList<JsonSchema.UV> corners = jsBuilding.canvas.vertice;
                    double minX = Util.MToFoot(Math.Min(corners[0].coordU, corners[2].coordU));
                    double maxX = Util.MToFoot(Math.Max(corners[0].coordU, corners[2].coordU));
                    double minY = Util.MToFoot(Math.Min(corners[0].coordV, corners[2].coordV));
                    double maxY = Util.MToFoot(Math.Max(corners[0].coordV, corners[2].coordV));

                    Color color = new Color(150, 30, 70);
                    OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                    ogs.SetSurfaceTransparency(90);
                    // change the line style on the section plane
                    ogs.SetCutLineColor(color);
                    ogs.SetCutLineWeight(8);
                    ogs.SetProjectionLineColor(color);
                    ogs.SetProjectionLineWeight(8);

                    //OverrideGraphicSettings ogs_halftone = new OverrideGraphicSettings();
                    //ogs_halftone.SetHalftone(true);

                    JsonSchema.Level thisLevel = jsBuilding.levels[Properties.Settings.Default.prevFloorId];
                    // create directshapes representing spaces of the target floor
                    string cacheIdsStr = "";
                    ICollection<ElementId> cacheIds = new List<ElementId>() { };
                    using (Transaction tx = new Transaction(doc, "Create spaces in sectioned 3D view"))
                    {
                        tx.Start();
                        foreach (JsonSchema.Poly jsPoly in thisLevel.rooms)
                        {
                            List<XYZ> vertice = new List<XYZ>() { };
                            foreach (JsonSchema.UV jsPt in jsPoly.vertice)
                            {
                                vertice.Add(new XYZ(
                                    Util.MToFoot(jsPt.coordU),
                                    Util.MToFoot(jsPt.coordV),
                                    Util.MToFoot(thisLevel.elevation)));
                            }
                            // vertice is a closed loop, always keep the vertice loop closed
                            // why sometimes CreateDirectShapeSpace() would fail?
                            DirectShape ds = CreateDirectShapeSpace(doc,
                                vertice, Util.MToFoot(thisLevel.height) * 4 / 5);
                            if (ds is null)
                                continue;
                            doc.ActiveView.SetElementOverrides(ds.Id, ogs);
                            cacheIds.Add(ds.Id);
                            cacheIdsStr += ds.Id.ToString() + "#";
                        }
                        BoundingBoxXYZ sectionBox = new BoundingBoxXYZ();
                        double minZ = Util.MToFoot(thisLevel.elevation);
                        double maxZ = minZ + Util.MToFoot(thisLevel.height) / 2;
                        sectionBox.Min = new XYZ(minX, minY, minZ);
                        sectionBox.Max = new XYZ(maxX, maxY, maxZ);
                        view3D.SetSectionBox(sectionBox);
                        view.HideElements(cacheIds);
                        view.EnableRevealHiddenMode();
                        tx.Commit();
                    }
                    Properties.Settings.Default.prevSpaceIds = cacheIdsStr;
                    break;

                case PrevSwitch.Erase:
                    IList<string> delIds = Properties.Settings.Default.prevSpaceIds.Split('#');
                    using (Transaction tx = new Transaction(doc, "Delete previous drawing"))
                    {
                        tx.Start();
                        foreach (string delId in delIds)
                        {
                            if (delId == "")
                                continue;
                            int idx = int.Parse(delId);
                            ElementId delDsId = new ElementId(idx);
                            ICollection<ElementId> deletedIdSet = doc.Delete(delDsId);
                        }
                        tx.Commit();
                    }
                    Properties.Settings.Default.prevSpaceIds = "";
                    break;
            }
            return;
        }

        public string GetName()
        {
            return "Pick Shade";
        }

        DirectShape CreateDirectShapeSpace(Document doc, List<XYZ> loop, double height)
        {
            List<Curve> edges = new List<Curve>() { };
            for (int i = 0; i < loop.Count - 1; i++)
            {
                edges.Add(Line.CreateBound(loop[i], loop[i + 1]));
            }
            IList<CurveLoop> edgeCrvLoops = new List<CurveLoop>();
            edgeCrvLoops.Add(CurveLoop.Create(edges));

            Solid extrudedSpace = null;
            try
            {
                extrudedSpace = GeometryCreationUtilities
                    .CreateExtrusionGeometry(edgeCrvLoops, XYZ.BasisZ, height);
            }
            catch
            {
                return null;
            }
            if (extrudedSpace is null)
            {
                return null;
            }
            List<GeometryObject> objs = new List<GeometryObject>() { extrudedSpace };

            DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
            ds.SetShape(objs);
            
            return ds;
        }
    }
}