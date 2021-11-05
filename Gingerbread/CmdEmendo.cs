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
    public class CmdEmendo : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;

            //Properties.Settings.Default.url_install = UtilGetInstallPath.Execute(app);
            Properties.Settings.Default.checkInfo = "";

            Views.ViewEmendo fixer = new Views.ViewEmendo();

            System.Windows.Interop.WindowInteropHelper mainUI = new System.Windows.Interop.WindowInteropHelper(fixer);
            mainUI.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

            fixer.Show();
            return Result.Succeeded;
        }
    }
}