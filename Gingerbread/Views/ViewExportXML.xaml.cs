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
using System.Threading.Tasks;

namespace Gingerbread.Views
{
    /// <summary>
    /// Interaction logic for ExportSetting.xaml
    /// </summary>
    public partial class ViewExportXML : BaseWindow
    {
        // field
        public ExternalEvent ExEvent;
        public ExtExportXML extExportXML;
        private ChromiumWebBrowser Browser;

        // constructor
        public ViewExportXML()
        {
            InitializeComponent();

            //var settings = new CefSettings
            //{
            //    BrowserSubprocessPath = @"C:\gingerbread\Gingerbread\bin\x64\Debug\CefSharp.BrowserSubprocess.exe"
            //};
            //if (!Cef.IsInitialized)
            //{
            //    MessageBox.Show("The cef has not been initialized!");
            //    Cef.Initialize(settings);
            //}
            //Browser = new ChromiumWebBrowser();
            //if (Browser.IsBrowserInitialized)
            //{
            //    MessageBox.Show("the browser has been initialized!");
            //    Browser.Load("https://www.baidu.com");
            //}

            extExportXML = new ExtExportXML();
            extExportXML.CurrentUI = this;
            extExportXML.CurrentControl = new ProgressBarControl();
            extExportXML.CurrentControl.CurrentContext = "Stand by";
            ExEvent = ExternalEvent.Create(extExportXML);
        }


        private void CloseCommandHandler(object sender, ExecutedRoutedEventArgs e)
        {
            Properties.Settings.Default.Save();
            this.Close();
        }

        private void BtnGenerate(object sender, RoutedEventArgs e)
        {
            btnGenerate.Visibility = System.Windows.Visibility.Collapsed;
            btnCancel.Visibility = System.Windows.Visibility.Visible;
            ExEvent.Raise();
        }
        private void BtnCancel(object sender, RoutedEventArgs e)
        {
            btnGenerate.Visibility = System.Windows.Visibility.Visible;
            btnCancel.Visibility = System.Windows.Visibility.Collapsed;
        }

        public void BtnSaveAs(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = "GingerbreadXML"; // Default file name
            dlg.DefaultExt = ".xml"; // Default file extension
            dlg.Filter = "Text documents (.xml)|*.xml"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                string XMLPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) +
                    "/GingerbreadXML.xml";
                if (File.Exists(XMLPath))
                    File.Copy(XMLPath, dlg.FileName, true);
            }
        }


    }
}

