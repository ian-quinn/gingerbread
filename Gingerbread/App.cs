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
            RibbonPanel modelReduction = ribbonPanel(a, "Gingerbread", "Prefix");
            PushButton prefix = modelReduction.AddItem(new PushButtonData("mesh", "Simplify\nthis Document",
                thisAssemblyPath, "Gingerbread.CmdZippo")) as PushButton;
            prefix.ToolTip = "Make a lightweight Revit model";
            BitmapImage prefixImg = new BitmapImage(new Uri("pack://application:,,,/Gingerbread;component/Resources/ico/Prefix.ico", UriKind.Absolute));
            prefix.LargeImage = prefixImg;

            modelReduction.Enabled = false;


            ////////////
            // 1st Panel
            RibbonPanel modelFix = ribbonPanel(a, "Gingerbread", "Algorithm");
            PushButtonData meshButtonData = new PushButtonData("mesh", "Detect Region\nby Selections",
                thisAssemblyPath, "Gingerbread.CmdPatchBoundary");
            meshButtonData.AvailabilityClassName = "Gingerbread.UtilButtonSwitch";
            PushButton mesh = modelFix.AddItem(meshButtonData) as PushButton;
            mesh.ToolTip = "WIP. A fuzzy enclosure detection by selected walls & columns.";
            BitmapImage meshImg = new BitmapImage(new Uri("pack://application:,,,/Gingerbread;component/Resources/ico/RegionDetect.ico", UriKind.Absolute));
            mesh.LargeImage = meshImg;


            PushButtonData simplifyCurve = new PushButtonData("simplifyCurve", "Simplify curve",
                thisAssemblyPath, "Gingerbread.CmdCoreSimplifyCurve");
            simplifyCurve.ToolTip = "Simplify a curve or polyline by Douglas-Peucker Algorithm";
            simplifyCurve.AvailabilityClassName = "Gingerbread.UtilButtonSwitch";
            BitmapImage simplifyCurveImg = new BitmapImage(new Uri("pack://application:,,,/Gingerbread;component/Resources/ico/SimplifyCurve.ico", UriKind.Absolute));
            simplifyCurve.Image = simplifyCurveImg;

            PushButtonData splitHole = new PushButtonData("splitHole", "Split hole",
                thisAssemblyPath, "Gingerbread.CmdCoreSplitHole");
            splitHole.ToolTip = "Split a multi-connect region";
            splitHole.AvailabilityClassName = "Gingerbread.UtilButtonSwitch";
            BitmapImage splitHoleImg = new BitmapImage(new Uri("pack://application:,,,/Gingerbread;component/Resources/ico/SplitHole.ico", UriKind.Absolute));
            splitHole.Image = splitHoleImg;

            PushButtonData alignPoint = new PushButtonData("alignPoint", "Align anchors",
                thisAssemblyPath, "Gingerbread.CmdCoreAlignPoint");
            alignPoint.ToolTip = "Align and merge adjacent points";
            alignPoint.AvailabilityClassName = "Gingerbread.UtilButtonSwitch";
            BitmapImage alignPointImg = new BitmapImage(new Uri("pack://application:,,,/Gingerbread;component/Resources/ico/AlignPoint.ico", UriKind.Absolute));
            alignPoint.Image = alignPointImg;

            IList<RibbonItem> stackedCore = modelFix.AddStackedItems(simplifyCurve, splitHole, alignPoint);
            stackedCore[0].Enabled = false;
            stackedCore[1].Enabled = false;
            stackedCore[2].Enabled = false;


            ////////////
            // 2ed Panel
            RibbonPanel modelSketch = ribbonPanel(a, "Gingerbread", "Sketch");

            PushButtonData sketchLocation = new PushButtonData("sketchLocation", "Location",
                thisAssemblyPath, "Gingerbread.CmdSketchLocation");
            sketchLocation.ToolTip = "Draw axis of the component";
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

            IList<RibbonItem> stackedSketch = modelSketch.AddStackedItems(sketchLocation, sketchFootprint, sketchBoundingbox);
            stackedSketch[2].Enabled = false;


            ////////////
            // 3rd Panel
            RibbonPanel modelRun = ribbonPanel(a, "Gingerbread", "Energy Analysis");

            PushButton runExport = modelRun.AddItem(new PushButtonData("runExport", "Export\ngbXML Model",
                thisAssemblyPath, "Gingerbread.CmdZippo")) as PushButton;
            runExport.ToolTip = "Export a lightweight gbXML model for energy analysis";
            BitmapImage runExportImg = new BitmapImage(new Uri("pack://application:,,,/Gingerbread;component/Resources/ico/RunExport.ico", UriKind.Absolute));
            runExport.LargeImage = runExportImg;

            PushButtonData runViewer = new PushButtonData("runViewer", "Open 3.js Viewer",
                thisAssemblyPath, "Gingerbread.CmdZippo");
            runViewer.ToolTip = "Check the gbXML model in three.js viewer";
            BitmapImage runViewerImg = new BitmapImage(new Uri("pack://application:,,,/Gingerbread;component/Resources/ico/RunViewer.ico", UriKind.Absolute));
            runViewer.Image = runViewerImg;

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

            IList<RibbonItem> stackedRun = modelRun.AddStackedItems(runViewer, runSimulation, runReport);

            modelRun.Enabled = false;


            ////////////
            // 4th Panel
            RibbonPanel modelServer = ribbonPanel(a, "Gingerbread", "Cloud Service");

            PushButton authServer = modelServer.AddItem(new PushButtonData("authServer", "Authentication",
                thisAssemblyPath, "Gingerbread.CmdZippo")) as PushButton;
            authServer.ToolTip = "Connect to the cloud service";
            BitmapImage authServerImg = new BitmapImage(new Uri("pack://application:,,,/Gingerbread;component/Resources/ico/Authentication.ico", UriKind.Absolute));
            authServer.LargeImage = authServerImg;

            modelServer.Enabled = false;


            a.ApplicationClosing += a_ApplicationClosing;

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
