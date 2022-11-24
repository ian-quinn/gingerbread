using System;
using System.IO;
using System.Xml;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Controls;
using System.Diagnostics;
using System.Text.Json;
using System.Collections.Generic;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using Gingerbread.Core;

namespace Gingerbread.Views
{
    /// <summary>
    /// Interaction logic for ExportSetting.xaml
    /// </summary>
    public partial class ViewPreview : BaseWindow
    {
        // temperal data for preview
        Dictionary<string, List<DirectShape>> prevSpaces = new Dictionary<string, List<DirectShape>>() { };

        public ExtPreview extPreview;
        public ExtPreview extClear;
        public ExtPickSpace extPickSpace;
        public ExternalEvent ExEventPreview;
        public ExternalEvent ExEventClear;
        public ExternalEvent ExEventPickSpace;

        // constructor
        public ViewPreview()
        {
            InitializeComponent();

            extPreview = new ExtPreview();
            extPreview.runMode = PrevSwitch.Depict;
            extPreview.CurrentUI = this;

            extClear = new ExtPreview();
            extClear.runMode = PrevSwitch.Erase;
            extClear.CurrentUI = this;

            extPickSpace = new ExtPickSpace();
            extPickSpace.CurrentUI = this;

            ExEventPreview = ExternalEvent.Create(extPreview);
            ExEventClear = ExternalEvent.Create(extClear);
            ExEventPickSpace = ExternalEvent.Create(extPickSpace);

            this.Closed += new EventHandler(ViewPreview_Closed);

            if (string.IsNullOrEmpty(Properties.Settings.Default.geomInfo))
            {
                statusBar.Text = "Please run gbXML export first.";
                btnPick.IsEnabled = false;
            }

            try
            {
                JsonSchema.Building jsBuildingCheck = JsonSerializer.
                    Deserialize<JsonSchema.Building>(Properties.Settings.Default.geomInfo);
                foreach (JsonSchema.Level jsLevel in jsBuildingCheck.levels)
                {
                    layerList.Items.Add(jsLevel.name);
                }
            }
            catch
            {
                statusBar.Text = "Please run gbXML export first.";
                btnPick.IsEnabled = false;
            }
        }


        private void CloseCommandHandler(object sender, ExecutedRoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnPick(object sender, RoutedEventArgs e)
        {
            ExEventPickSpace.Raise();
        }

        private void layerList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Properties.Settings.Default.prevSpaceIds != "")
                ExEventClear.Raise();

            var item = ItemsControl.ContainerFromElement(layerList, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null)
            {
                Properties.Settings.Default.prevFloorId = layerList.ItemContainerGenerator.IndexFromContainer(item);
                Debug.Print("The selected layer is " + item.Content);
                ExEventPreview.Raise();
            }
        }

        // erase all drawings when closing the window
        void ViewPreview_Closed(object sender, EventArgs e)
        {
            if (Properties.Settings.Default.prevSpaceIds != "")
                ExEventClear.Raise();
            this.Close();
        }
    }
}

