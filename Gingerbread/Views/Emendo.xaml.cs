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
    public partial class Emendo : BaseWindow
    {
        // field
        public ExternalEvent ExEvent;
        public ExtEmendo extEmendo;

        // constructor
        public Emendo(UIApplication uiapp)
        {
            InitializeComponent();

            extEmendo = new ExtEmendo(uiapp);
            ExEvent = ExternalEvent.Create(extEmendo);
        }


        private void CloseCommandHandler(object sender, ExecutedRoutedEventArgs e)
        {
            Properties.Settings.Default.checkInfo = "";
            Properties.Settings.Default.Save();
            this.Close();
        }

        private void BtnCheck(object sender, RoutedEventArgs e)
        {
            //extEmendo.targetValue = "layerFrame";
            ExEvent.Raise();
        }
    }
}

