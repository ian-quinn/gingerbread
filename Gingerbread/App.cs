#region Namespaces
using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
#endregion

namespace Gingerbread
{
    class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication a)
        {
            // INITIATIONS
            // clear previous shading selections for ViewPickShade.xaml
            Properties.Settings.Default.shadeIds = "";
            Properties.Settings.Default.shadeNames = "";
            Properties.Settings.Default.shadeCurrentId = -1;
            Properties.Settings.Default.shadeCurrent = "";
            // clear previous geometry cache for ViewExportXML.xaml
            Properties.Settings.Default.geomInfo = "";

            //string thisAssemblyPath = AssemblyLoadEventArgs.getExecutingAssembly().Location;
            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;
            string thisResourcePath = "pack://application:,,,/Gingerbread;component/Resources";
            // the format is "pack://application:,,,/{ASSEMBLY NAME};component/{RESOURCE FOLDER NAME}"

            ////////////
            // 0 Panel
            RibbonPanel modelReduction = ribbonPanel(a, "Gingerbread", "Preprocess");
            PushButton prefix = modelReduction.AddItem(new PushButtonData("mesh", "Model\n  Emendo  ",
                thisAssemblyPath, "Gingerbread.CmdEmendo")) as PushButton;
            prefix.ToolTip = "Check and grab the information from revit model";
            BitmapImage prefixImg = new BitmapImage(new Uri(thisResourcePath + @"/ico/Prefix.ico", UriKind.Absolute));
            prefix.LargeImage = prefixImg;

            PushButtonData sketchLocation = new PushButtonData("sketchLocation", "Location",
                thisAssemblyPath, "Gingerbread.CmdSketchLocation");
            sketchLocation.ToolTip = "Draw location curve/point of certain components. A test module.";
            sketchLocation.AvailabilityClassName = "Gingerbread.UtilButton3DGrayed";
            BitmapImage sketchLocImg = new BitmapImage(new Uri(thisResourcePath + @"/ico/SketchLocation.ico", UriKind.Absolute));
            sketchLocation.Image = sketchLocImg;

            PushButtonData sketchFootprint = new PushButtonData("sketchFootprint", "Footprint",
                thisAssemblyPath, "Gingerbread.CmdSketchFootprint");
            sketchFootprint.ToolTip = "Draw bottom face of a component. A test module.";
            sketchFootprint.AvailabilityClassName = "Gingerbread.UtilButton3DGrayed";
            BitmapImage sketchFootprintImg = new BitmapImage(new Uri(thisResourcePath + @"/ico/SketchFootprint.ico", UriKind.Absolute));
            sketchFootprint.Image = sketchFootprintImg;

            PushButtonData sketchBoundingbox = new PushButtonData("sketchBounding", "BoundingBox",
                thisAssemblyPath, "Gingerbread.CmdSketchBoundingbox");
            sketchBoundingbox.ToolTip = "Draw bounding box of certain components (wall/column/curtain system). Only available in 3D view.";
            sketchBoundingbox.AvailabilityClassName = "Gingerbread.UtilButton3DActive";
            BitmapImage sketchBoxImg = new BitmapImage(new Uri(thisResourcePath + @"/ico/SketchBoundingbox.ico", UriKind.Absolute));
            sketchBoundingbox.Image = sketchBoxImg;

            IList<RibbonItem> stackedSketch = modelReduction.AddStackedItems(sketchLocation, sketchFootprint, sketchBoundingbox);
            //tackedSketch[2].Enabled = false;

            modelReduction.Enabled = true;
            prefix.Enabled = false;


            ////////////
            // 1 Panel
            RibbonPanel modelExport = ribbonPanel(a, "Gingerbread", "Energy Modeling");

            PushButtonData runShading = new PushButtonData("runShading", "Shading  ",
                thisAssemblyPath, "Gingerbread.CmdPickShade");
            runShading.ToolTip = "Pick surfaces as custom shades in gbXML. Only available in 3D view.";
            runShading.AvailabilityClassName = "Gingerbread.UtilButton3DActive";
            BitmapImage runShadingImg = new BitmapImage(new Uri(thisResourcePath + @"/ico/RunShading.ico", UriKind.Absolute));
            runShading.Image = runShadingImg;

            PushButtonData runViewer = new PushButtonData("runViewer", "Preview  ",
                thisAssemblyPath, "Gingerbread.CmdPreview");
            runViewer.ToolTip = "Create DirectShape according to the recognized space volumes. " +
                "Only available after gbXML export.";
            runViewer.AvailabilityClassName = "Gingerbread.UtilButton3DActive";
            BitmapImage runViewerImg = new BitmapImage(new Uri(thisResourcePath + @"/ico/RunViewer.ico", UriKind.Absolute));
            runViewer.Image = runViewerImg;

            PushButtonData runMaterial = new PushButtonData("runConstruction", "Material ",
                thisAssemblyPath, "Gingerbread.CmdZippo");
            runMaterial.ToolTip = "Default material & construction settings for gbXML export";
            BitmapImage runExtrusionImg = new BitmapImage(new Uri(thisResourcePath + @"/ico/RunMaterial.ico", UriKind.Absolute));
            runMaterial.Image = runExtrusionImg;

            IList<RibbonItem> stackedPreRun = modelExport.AddStackedItems(runShading, runViewer, runMaterial);
            stackedPreRun[0].Enabled = true;
            stackedPreRun[1].Enabled = true;
            stackedPreRun[2].Enabled = true;


            PushButton runExport = modelExport.AddItem(new PushButtonData("runExport", "gbXML\n  Export  ",
                thisAssemblyPath, "Gingerbread.CmdExportXML")) as PushButton;
            runExport.ToolTip = "Export a lightweight gbXML model for energy analysis";
            BitmapImage runExportImg = new BitmapImage(new Uri(thisResourcePath + @"/ico/RunExport.ico", UriKind.Absolute));
            runExport.LargeImage = runExportImg;

            modelExport.Enabled = true;


            ////////////
            // 2 Panel
            RibbonPanel modelServer = ribbonPanel(a, "Gingerbread", "Cloud");

            PushButtonData runServer = new PushButtonData("runSever", "Authentication",
                thisAssemblyPath, "Gingerbread.CmdZippo");
            runServer.ToolTip = "Do settings and run simulation";
            BitmapImage runServerImg = new BitmapImage(new Uri(thisResourcePath + @"/ico/Authentication.ico", UriKind.Absolute));
            runServer.Image = runServerImg;

            PushButtonData runSimulation = new PushButtonData("runSimulation", "Run Simulation",
                thisAssemblyPath, "Gingerbread.CmdZippo");
            runSimulation.ToolTip = "Do settings and run simulation";
            BitmapImage runSimulationImg = new BitmapImage(new Uri(thisResourcePath + @"/ico/RunSimulation.ico", UriKind.Absolute));
            runSimulation.Image = runSimulationImg;

            PushButtonData runReport = new PushButtonData("runReport", "Generate Reports",
                thisAssemblyPath, "Gingerbread.CmdZippo");
            runReport.ToolTip = "Output simulation reports";
            BitmapImage runReportImg = new BitmapImage(new Uri(thisResourcePath + @"/ico/RunReport.ico", UriKind.Absolute));
            runReport.Image = runReportImg;

            IList<RibbonItem> stackedServer = modelServer.AddStackedItems(runServer, runSimulation, runReport);
            stackedServer[0].Enabled = false;
            stackedServer[1].Enabled = false;
            stackedServer[2].Enabled = false;

            modelServer.Enabled = true;

            
            a.ApplicationClosing += a_ApplicationClosing;

            // backup buttons
            //PushButtonData simplifyCurve = new PushButtonData("simplifyCurve", "Simplify Curve",
            //    thisAssemblyPath, "Gingerbread.CmdCoreSimplifyCurve");
            //simplifyCurve.ToolTip = "Simplify curve/polylines by Douglas-Peucker Algorithm. Please pre-select some Modellines/Detaillines";
            //simplifyCurve.AvailabilityClassName = "Gingerbread.UtilButtonSwitch";
            //BitmapImage simplifyCurveImg = new BitmapImage(new Uri("pack://application:,,,/Gingerbread;component/Resources/ico/CoreSimplifyCurve.ico", UriKind.Absolute));
            //simplifyCurve.Image = simplifyCurveImg;

            return Result.Succeeded;
        }

        void a_Idling(object sender, Autodesk.Revit.UI.Events.IdlingEventArgs e)
        {
        }

        private void a_ApplicationClosing(object sender, Autodesk.Revit.UI.Events.ApplicationClosingEventArgs e)
        {
            throw new NotImplementedException();
        }

        public RibbonPanel ribbonPanel(UIControlledApplication a, String tabName, String panelName)
        {
            RibbonPanel ribbonPanel = null;
            try
            {
                a.CreateRibbonTab(tabName);
            }
            catch { }
            try
            {
                RibbonPanel panel = a.CreateRibbonPanel(tabName, panelName);
            }
            catch { }

            List<RibbonPanel> panels = a.GetRibbonPanels(tabName);
            foreach (RibbonPanel p in panels)
            {
                if (p.Name == panelName)
                {
                    ribbonPanel = p;
                }
            }

            return ribbonPanel;
        }

        public Result OnShutdown(UIControlledApplication a)
        {
            return Result.Succeeded;
        }

    }
}
