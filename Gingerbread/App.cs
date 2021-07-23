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
            // 1st Panel
            RibbonPanel modelFix = ribbonPanel(a, "Gingerbread", "Algorithm");
            PushButtonData meshButtonData = new PushButtonData("mesh", "Detect Region\nby Selection",
                thisAssemblyPath, "Gingerbread.CmdPatchBoundary");
            meshButtonData.AvailabilityClassName = "Gingerbread.UtilButtonSwitch";
            PushButton mesh = modelFix.AddItem(meshButtonData) as PushButton;
            mesh.ToolTip = "WIP. A fuzzy enclosure detection by selected walls & columns.";
            BitmapImage meshImg = new BitmapImage(new Uri("pack://application:,,,/Gingerbread;component/Resources/ico/RegionDetect.ico", UriKind.Absolute));
            mesh.LargeImage = meshImg;


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
