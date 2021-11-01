using System;
using System.IO;
using System.Xml;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CefSharp.Wpf;
using CefSharp;
using CefSharp.SchemeHandler;

namespace Gingerbread.Views
{
    /// <summary>
    /// Interaction logic for ExportSetting.xaml
    /// </summary>
    public partial class ExportXML : BaseWindow
    {
        // field
        public ExternalEvent ExEvent;
        public ExtExportXML extExportXML;
        private ChromiumWebBrowser Browser;

        // constructor
        public ExportXML(UIApplication uiapp)
        {
            InitializeComponent();


            var settings = new CefSettings
            {
                BrowserSubprocessPath = @"C:\gingerbread\Gingerbread\bin\x64\Debug\CefSharp.BrowserSubprocess.exe"
            };

            if (!Cef.IsInitialized)
            {
                MessageBox.Show("The cef has not been initialized!");
                Cef.Initialize(settings);
            }

            Browser = new ChromiumWebBrowser();
            if (Browser.IsBrowserInitialized)
            {
                MessageBox.Show("the browser has been initialized!");
                Browser.Load("https://www.baidu.com");
            }
            //RequestContext addinRequestContext = new RequestContext();
            //string addinDomain = "addinlocal";
            //addinRequestContext.RegisterSchemeHandlerFactory("https", addinDomain, 
            //    new FolderSchemeHandlerFactory(@"C:\Users\ianqu\AppData\Roaming\Autodesk\Revit\Addins\2020\Gingerbread"));
            //Browser.RequestContext = addinRequestContext;

            Browser.RegisterJsObject("dotNetMessage", new DotNetMessage());
            Browser.IsBrowserInitializedChanged += (sender, args) =>
            {
                if (Browser.IsBrowserInitialized)
                {
                    MessageBox.Show("the browser has been initialized!");
                    //Browser.LoadHtml(File.ReadAllText(@"C:\gingerbread\Gingerbread\Resources\spider\main.html"));
                    //Browser.Load($"https://{addinDomain}/index.html");
                    Browser.Load("https://www.baidu.com");

                }
                //ViewPort.Children.Add(Browser);
            };


            extExportXML = new ExtExportXML(uiapp);
            ExEvent = ExternalEvent.Create(extExportXML);
        }


        private void CloseCommandHandler(object sender, ExecutedRoutedEventArgs e)
        {
            // Properties.Settings.Default.Save();
            this.Close();
        }

        private void BtnGenerate(object sender, RoutedEventArgs e)
        {
            extExportXML.targetValue = "layerFrame";
            ExEvent.Raise();
        }

        public void BtnSaveAs(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = "Sample"; // Default file name
            dlg.DefaultExt = ".xml"; // Default file extension
            dlg.Filter = "Text documents (.xml)|*.xml"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                string XMLPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) +
                    "/Sample.xml";
                if (File.Exists(XMLPath))
                    File.Copy(XMLPath, dlg.FileName, true);
            }
        }

        public class DotNetMessage
        {
            public void Show(string message)
            {
                MessageBox.Show(message);
            }
        }
    }
}

