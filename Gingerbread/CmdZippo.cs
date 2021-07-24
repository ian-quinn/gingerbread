#region Namespaces
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Diagnostics;

#endregion

namespace Gingerbread
{
    [Transaction(TransactionMode.Manual)]
    public class CmdZippo : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            System.Windows.MessageBox.Show("This button is still under construction.", "Hi there");

            return Result.Succeeded;
        }
    }
}