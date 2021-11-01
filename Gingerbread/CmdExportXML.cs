#region Namespaces
using System;
using System.Diagnostics;
using System.IO;

using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
#endregion

namespace Gingerbread
{
    [Transaction(TransactionMode.Manual)]
    public class CmdExportXML : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;

            //Properties.Settings.Default.url_install = UtilGetInstallPath.Execute(app);
            Properties.Settings.Default.SpiderPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\Spider\main.html";

            Views.ExportXML generator = new Views.ExportXML(uiapp);

            System.Windows.Interop.WindowInteropHelper mainUI = new System.Windows.Interop.WindowInteropHelper(generator);
            mainUI.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

            generator.Show();
            return Result.Succeeded;
        }
    }
}