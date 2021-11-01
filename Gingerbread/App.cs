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
            //string thisAssemblyPath = AssemblyLoadEventArgs.getExecutingAssembly().Location;
            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;


            ////////////
            // 0 Panel
            RibbonPanel modelReduction = ribbonPanel(a, "Gingerbread", "Preprocess");
            PushButton prefix = modelReduction.AddItem(new PushButtonData("mesh", "Model\n  Emendo!  ",
                thisAssemblyPath, "Gingerbread.CmdEmendo")) as PushButton;
            prefix.ToolTip = "Check and grab the information from revit model";
            BitmapImage prefixImg = new BitmapImage(new Uri("pack://application:,,,/Gingerbread;component/Resources/ico/Prefix.ico", UriKind.Absolute));
            prefix.LargeImage = prefixImg;

            PushButtonData sketchLocation = new PushButtonData("sketchLocation", "Location",
                thisAssemblyPath, "Gingerbread.CmdSketchLocation");
            sketchLocation.ToolTip = "Draw location curve/point of the component";
            sketchLocation.AvailabilityClassName = "Gingerbread.UtilButtonSwitch";
            BitmapImage sketchLocImg = new BitmapImage(new Uri("pack://application:,,,/Gingerbread;component/Resources/ico/SketchLocation.ico", UriKind.Absolute));
            sketchLocation.Image = sketchLocImg;

            PushButtonData sketchFootprint = new PushButtonData("sketchFootprint", "Footprint",
                thisAssemblyPath, "Gingerbread.CmdSketchFootprint");
            sketchFootprint.ToolTip = "Draw bottom face of the component";
            sketchFootprint.AvailabilityClassName = "Gingerbread.UtilButtonSwitch";
            BitmapImage sketchFootprintImg = new BitmapImage(new Uri("pack://application:,,,/Gingerbread;component/Resources/ico/SketchFootprint.ico", UriKind.Absolute));
            sketchFootprint.Image = sketchFootprintImg;

            PushButtonData sketchBoundingbox = new PushButtonData("sketchBounding", "BoundingBox",
                thisAssemblyPath, "Gingerbread.CmdSketchBoundingbox");
            sketchBoundingbox.ToolTip = "Draw bounding box of the component";
            sketchBoundingbox.AvailabilityClassName = "Gingerbread.UtilButtonSwitch";
            BitmapImage sketchBoxImg = new BitmapImage(new Uri("pack://application:,,,/Gingerbread;component/Resources/ico/SketchBoundingbox.ico", UriKind.Absolute));
            sketchBoundingbox.Image = sketchBoxImg;

            IList<RibbonItem> stackedSketch = modelReduction.AddStackedItems(sketchLocation, sketchFootprint, sketchBoundingbox);
            stackedSketch[2].Enabled = false;

            modelReduction.Enabled = true;


            ////////////
            // 1 Panel
            RibbonPanel modelExport = ribbonPanel(a, "Gingerbread", "Energy Modeling");

            PushButtonData runShading = new PushButtonData("runShading", "Shading  ",
                thisAssemblyPath, "Gingerbread.CmdZippo");
            runShading.ToolTip = "Pickup custom surfaces as shades in gbXML";
            BitmapImage runShadingImg = new BitmapImage(new Uri("pack://application:,,,/Gingerbread;component/Resources/ico/RunShading.ico", UriKind.Absolute));
            runShading.Image = runShadingImg;

            PushButtonData runViewer = new PushButtonData("runViewer", "Viewer    ",
                thisAssemblyPath, "Gingerbread.CmdZippo");
            runViewer.ToolTip = "Call up the Spider to check the gbXML";
            BitmapImage runViewerImg = new BitmapImage(new Uri("pack://application:,,,/Gingerbread;component/Resources/ico/RunViewer.ico", UriKind.Absolute));
            runViewer.Image = runViewerImg;

            PushButtonData runMaterial = new PushButtonData("runConstruction", "Material ",
                thisAssemblyPath, "Gingerbread.CmdZippo");
            runMaterial.ToolTip = "Default material & construction settings for gbXML export";
            BitmapImage runExtrusionImg = new BitmapImage(new Uri("pack://application:,,,/Gingerbread;component/Resources/ico/RunMaterial.ico", UriKind.Absolute));
            runMaterial.Image = runExtrusionImg;

            IList<RibbonItem> stackedPreRun = modelExport.AddStackedItems(runShading, runViewer, runMaterial);


            PushButton runExport = modelExport.AddItem(new PushButtonData("runExport", "gbXML\n  Export!  ",
                thisAssemblyPath, "Gingerbread.CmdExportXML")) as PushButton;
            runExport.ToolTip = "Export a lightweight gbXML model for energy analysis";
            BitmapImage runExportImg = new BitmapImage(new Uri("pack://application:,,,/Gingerbread;component/Resources/ico/RunExport.ico", UriKind.Absolute));
            runExport.LargeImage = runExportImg;

            modelExport.Enabled = true;


            ////////////
            // 2 Panel
            RibbonPanel modelServer = ribbonPanel(a, "Gingerbread", "Cloud");

            PushButtonData runServer = new PushButtonData("runSever", "Authentication",
                thisAssemblyPath, "Gingerbread.CmdZippo");
            runServer.ToolTip = "Do settings and run simulation";
            BitmapImage runServerImg = new BitmapImage(new Uri("pack://application:,,,/Gingerbread;component/Resources/ico/Authentication.ico", UriKind.Absolute));
            runServer.Image = runServerImg;

            PushButtonData runSimulation = new PushButtonData("runSimulation", "Run Simulation",
                thisAssemblyPath, "Gingerbread.CmdZippo");
            runSimulation.ToolTip = "Do settings and run simulation";
            BitmapImage runSimulationImg = new BitmapImage(new Uri("pack://application:,,,/Gingerbread;component/Resources/ico/RunSimulation.ico", UriKind.Absolute));
            runSimulation.Image = runSimulationImg;

            PushButtonData runReport = new PushButtonData("runReport", "Generate Reports",
                thisAssemblyPath, "Gingerbread.CmdZippo");
            runReport.ToolTip = "Output simulation reports";
            BitmapImage runReportImg = new BitmapImage(new Uri("pack://application:,,,/Gingerbread;component/Resources/ico/RunReport.ico", UriKind.Absolute));
            runReport.Image = runReportImg;

            IList<RibbonItem> stackedServer = modelServer.AddStackedItems(runServer, runSimulation, runReport);

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
