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
    public class CmdPreview : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;

            Views.ViewPreview viewer = new Views.ViewPreview();

            System.Windows.Interop.WindowInteropHelper mainUI = new System.Windows.Interop.WindowInteropHelper(viewer);
            mainUI.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

            viewer.Show();
            return Result.Succeeded;
        }
    }
}