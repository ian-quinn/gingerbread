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
            Document doc = uiapp.ActiveUIDocument.Document;

            //Properties.Settings.Default.url_install = UtilGetInstallPath.Execute(app);

            ProjectInfo projectInfo = doc.ProjectInformation;
            //dictproinfo.Add("OrganizationDescription", projectInfo.OrganizationName);
            //dictproinfo.Add("OrganizationName", projectInfo.OrganizationName);
            //dictproinfo.Add("BuildingName", projectInfo.BuildingName);
            //dictproinfo.Add("Author", projectInfo.Author);
            //dictproinfo.Add("Number", projectInfo.Number);
            //dictproinfo.Add("Name", projectInfo.Name);
            //dictproinfo.Add("Address", projectInfo.Address);
            //dictproinfo.Add("ClientName", projectInfo.ClientName);
            //dictproinfo.Add("Status", projectInfo.Status);
            //dictproinfo.Add("IssueDate", projectInfo.IssueDate);

            Properties.Settings.Default.projName = projectInfo.BuildingName;
            Properties.Settings.Default.projAddress = projectInfo.Address;
            Properties.Settings.Default.projDate = projectInfo.IssueDate;
            Properties.Settings.Default.projAuthor = projectInfo.Author;
            Properties.Settings.Default.projNumber = projectInfo.Number;
            Properties.Settings.Default.Save();

            Properties.Settings.Default.spiderPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + @"\Spider\main.html";

            Views.ViewExportXML generator = new Views.ViewExportXML();

            System.Windows.Interop.WindowInteropHelper mainUI = new System.Windows.Interop.WindowInteropHelper(generator);
            mainUI.Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;

            generator.Show();
            return Result.Succeeded;
        }
    }
}