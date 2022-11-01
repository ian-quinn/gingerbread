using System;
using System.IO;
using System.Xml;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Media;
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
    public partial class ViewEmendo : BaseWindow
    {
        // field
        public ExternalEvent ExEvent;
        public ExtEmendo extEmendo;

        // constructor
        public ViewEmendo()
        {
            InitializeComponent();

            extEmendo = new ExtEmendo();
            extEmendo.CurrentUI = this;
            extEmendo.CurrentControl = new ProgressBarControl();
            extEmendo.CurrentControl.CurrentContext = "Ready";
            ExEvent = ExternalEvent.Create(extEmendo);

        }


        private void CloseCommandHandler(object sender, ExecutedRoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnCheck(object sender, RoutedEventArgs e)
        {
            ExEvent.Raise();
        }

        private void BtnShow(object sender, RoutedEventArgs e)
        {
            Polygon p = new Polygon();
            p.Stroke = Brushes.Black;
            p.Fill = Brushes.LightBlue;
            p.StrokeThickness = 1;
            p.HorizontalAlignment = HorizontalAlignment.Left;
            p.VerticalAlignment = VerticalAlignment.Center;
            p.Points = new PointCollection() {
                new System.Windows.Point(10, 10),
                new System.Windows.Point(50, 50),
                new System.Windows.Point(100, 100)};
            friPan.Children.Add(p);
        }
    }
}

