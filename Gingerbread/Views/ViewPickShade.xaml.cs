using System;
using System.IO;
using System.Xml;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Diagnostics;
using System.Windows.Controls;
using System.Collections.Generic;
using System.ComponentModel;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CefSharp.Wpf;
using CefSharp;
using CefSharp.SchemeHandler;


namespace Gingerbread.Views
{
    /// <summary>
    /// Interaction logic for ViewPickShade.xaml
    /// </summary>
    public partial class ViewPickShade : BaseWindow
    {
        // field
        public ExternalEvent PickEvent;
        public ExternalEvent DrawEvent;
        public ExternalEvent EraseEvent;
        public ExtPickShade extPickShade;
        public ExtPickShade extViewShade;
        public ExtPickShade extEraseShade;

        public ViewPickShade()
        {
            InitializeComponent();

            if (Properties.Settings.Default.shadeNames != "")
            {
                string[] elementIds = Properties.Settings.Default.shadeNames.Split('#');
                shadeList.ItemsSource = new List<string>(elementIds);
            }

            extPickShade = new ExtPickShade();
            extPickShade.CurrentUI = this;
            extPickShade.runMode = FuncSwitch.Select;
            PickEvent = ExternalEvent.Create(extPickShade);

            extViewShade = new ExtPickShade();
            extViewShade.runMode = FuncSwitch.Depict;
            DrawEvent = ExternalEvent.Create(extViewShade);

            extEraseShade = new ExtPickShade();
            extEraseShade.runMode = FuncSwitch.Erase;
            EraseEvent = ExternalEvent.Create(extEraseShade);

            this.Closed += new EventHandler(ViewPickShade_Closed);
        }

        private void shadeList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Properties.Settings.Default.shadeCurrentId != -1)
                EraseEvent.Raise();

            var item = ItemsControl.ContainerFromElement(shadeList, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null)
            {
                string key = item.Content.ToString();
                if (key == "") return;
                List<string> names = new List<string>(Properties.Settings.Default.shadeNames.Split('#'));
                string[] ids = Properties.Settings.Default.shadeIds.Split('#');
                Properties.Settings.Default.shadeCurrent = ids[names.IndexOf(key)];

                DrawEvent.Raise();
            }
        }
        private void BtnClear(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.shadeIds = "";
            Properties.Settings.Default.shadeNames = "";
            shadeList.ItemsSource = new List<string>() { };
            if (Properties.Settings.Default.shadeCurrentId != -1)
                EraseEvent.Raise();
        }
        private void BtnPick(object sender, RoutedEventArgs e)
        {
            PickEvent.Raise();
        }

        // erase all drawings when closing the window
        void ViewPickShade_Closed(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.shadeCurrentId != -1)
                EraseEvent.Raise();
            this.Close();
        }
    }
}
