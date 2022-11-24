using System;
using System.IO;
using System.Xml;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Collections.Generic;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using CefSharp.Wpf;
using CefSharp;
using CefSharp.SchemeHandler;

using Gingerbread.Core;


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

        // temperal data for preview
        Dictionary<string, List<Polygon>> prevFloorplans = new Dictionary<string, List<Polygon>>() { };
        Dictionary<string, List<System.Windows.Shapes.Line>> prevPartitions = new Dictionary<string, List<System.Windows.Shapes.Line>>() { };

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
            txtState.Text = "Stand by";

            extExportXML = new ExtExportXML();
            extExportXML.CurrentUI = this;
            extExportXML.CurrentControl = new ProgressBarControl();
            ExEvent = ExternalEvent.Create(extExportXML);
        }

        private void BtnApply(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.originX = double.Parse(Xcoord.Text);
            Properties.Settings.Default.originY = double.Parse(Ycoord.Text);
            Properties.Settings.Default.originZ = double.Parse(Zcoord.Text);
            Properties.Settings.Default.tolGrouping = double.Parse(grouping.Text);
            Properties.Settings.Default.tolPerimeter = double.Parse(perimeter.Text);
            Properties.Settings.Default.tolAlignment = double.Parse(alignment.Text);
            Properties.Settings.Default.projName = projName.Text;
            Properties.Settings.Default.projAddress = projAddress.Text;
            Properties.Settings.Default.projNumber = projNumber.Text;
            Properties.Settings.Default.projLatitude = double.Parse(projLatitude.Text);
            Properties.Settings.Default.projLongitude = double.Parse(projLongitude.Text);
            Properties.Settings.Default.projElevation = double.Parse(projElevation.Text);
            Properties.Settings.Default.projAzimuth = double.Parse(projAzimuth.Text);
            Properties.Settings.Default.Save();
            txtUpdate.Visibility = System.Windows.Visibility.Collapsed;
            txtState.Visibility = System.Windows.Visibility.Visible;
            txtState.Text = "Settings updated.";
        }

        private void BtnReset(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.originX = 0;
            Properties.Settings.Default.originY = 0;
            Properties.Settings.Default.originZ = 0;
            Properties.Settings.Default.tolGrouping = 0.5;
            Properties.Settings.Default.tolPerimeter = 0.5;
            Properties.Settings.Default.tolAlignment = 0.11; 
            Properties.Settings.Default.projName = "GingerbreadHouse";
            Properties.Settings.Default.projAddress = "Shanghai, China";
            Properties.Settings.Default.projNumber = projNumber.Text;
            Properties.Settings.Default.projLatitude = 31.4;
            Properties.Settings.Default.projLongitude = 121.45;
            Properties.Settings.Default.projElevation = 5.5;
            Properties.Settings.Default.projAzimuth = 0;
            txtUpdate.Visibility = System.Windows.Visibility.Collapsed;
            txtState.Visibility = System.Windows.Visibility.Visible;
            txtState.Text = "Reset to default value.";
        }

        private void IncludeRef_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            Properties.Settings.Default.includeRef = true;
        }
        private void IncludeRef_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            Properties.Settings.Default.includeRef = false;
        }
        private void ExportStruct_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            Properties.Settings.Default.exportStruct = true;
        }
        private void ExportStruct_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            Properties.Settings.Default.exportStruct = false;
        }
        private void ExportShade_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            Properties.Settings.Default.exportShade = true;
        }
        private void ExportShade_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            Properties.Settings.Default.exportShade = false;
        }
        
        private void ShadowPrev_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            Properties.Settings.Default.shadowPrev = false;
        }
        private void ShadowPrev_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            Properties.Settings.Default.shadowPrev = true;
        }
        private void PunchMass_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            Properties.Settings.Default.punchMass = false;
        }
        private void PunchMass_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            Properties.Settings.Default.punchMass = true;
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
            txtUpdate.Visibility = System.Windows.Visibility.Visible;
            txtState.Visibility = System.Windows.Visibility.Collapsed;
            prevFloorplans = new Dictionary<string, List<Polygon>>();
            prevPartitions = new Dictionary<string, List<System.Windows.Shapes.Line>>();
            layerList.Items.Clear();
            ExEvent.Raise();
        }

        private void BtnPreview(object sender, RoutedEventArgs e)
        {
            //System.Windows.Point CoordTransition(double ptX, double ptY, double factor, 
            //    double originX, double originY, double sizeX, double sizeY)
            //{
            //    double xcoord = (ptX - originX) * factor + sizeX / 2;
            //    double ycoord = (ptX - originY) * factor + sizeY / 2;
            //    return new System.Windows.Point(xcoord, ycoord);
            //}

            try
            {
                JsonSchema.Building jsBuildingCheck = JsonSerializer.
                    Deserialize<JsonSchema.Building>(Properties.Settings.Default.geomInfo);
            }
            catch
            {
                return;
            }

            JsonSchema.Building jsBuilding = JsonSerializer.
                    Deserialize<JsonSchema.Building>(Properties.Settings.Default.geomInfo);
            // clear the preview floorplan cahce
            prevFloorplans = new Dictionary<string, List<Polygon>>() { };

            IList<JsonSchema.UV> corners = jsBuilding.canvas.vertice;
            double centroidX = (corners[0].coordU + corners[1].coordU + corners[2].coordU + corners[3].coordU) / 4;
            double centroidY = (corners[0].coordV + corners[1].coordV + corners[2].coordV + corners[3].coordV) / 4;
            double width = Math.Abs(corners[0].coordU - corners[2].coordU);
            double length = Math.Abs(corners[1].coordV - corners[3].coordV);
            double minDimBox = Math.Min(prevBox.ActualWidth, prevBox.ActualHeight);
            double maxDimDrawing = Math.Max(width, length);
            double scaler = minDimBox / maxDimDrawing * 0.85;
            //Debug.Print($"width {width}, length {length}, boxWidth{prevBox.ActualWidth}, boxHeight{prevBox.ActualHeight}");

            foreach (JsonSchema.Level jsLevel in jsBuilding.levels)
            {
                // recognized space boundary list generation
                List<Polygon> polys = new List<Polygon>() { };
                foreach (JsonSchema.Poly jsPoly in jsLevel.rooms)
                {
                    List<System.Windows.Point> vertice = new List<System.Windows.Point>() { };
                    foreach (JsonSchema.UV jsPt in jsPoly.vertice)
                    {
                        vertice.Add(new System.Windows.Point(
                            (jsPt.coordU - centroidX) * scaler + prevBox.ActualWidth / 2,
                            -(jsPt.coordV - centroidY) * scaler + prevBox.ActualHeight / 2
                            ));
                    }
                    Polygon p = new Polygon();
                    p.Stroke = Brushes.Salmon;
                    p.StrokeThickness = 4;
                    p.Points = new PointCollection(vertice);
                    polys.Add(p);
                }
                prevFloorplans.Add(jsLevel.name, polys);

                // wall centerlines that fed to the algorithm
                List<System.Windows.Shapes.Line> lines = new List<System.Windows.Shapes.Line>() { };
                foreach (JsonSchema.Seg jsSeg in jsLevel.walls)
                {
                    System.Windows.Shapes.Line l = new System.Windows.Shapes.Line();
                    l.Stroke = Brushes.Black;
                    l.StrokeThickness = 1;
                    l.X1 = (jsSeg.start.coordU - centroidX) * scaler + prevBox.ActualWidth / 2; 
                    l.Y1 = -(jsSeg.start.coordV - centroidY) * scaler + prevBox.ActualHeight / 2;
                    l.X2= (jsSeg.end.coordU - centroidX) * scaler + prevBox.ActualWidth / 2;
                    l.Y2 = -(jsSeg.end.coordV - centroidY) * scaler + prevBox.ActualHeight / 2;
                    lines.Add(l);
                }
                prevPartitions.Add(jsLevel.name, lines);
            }

            foreach (JsonSchema.Level jsLevel in jsBuilding.levels)
            {
                layerList.Items.Add(jsLevel.name);
            }

            prevNote.Text = "Select the floorplan view to preview.";
        }

        private void layerList_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var item = ItemsControl.ContainerFromElement(layerList, e.OriginalSource as DependencyObject) as ListBoxItem;
            if (item != null)
            {
                string key = item.Content.ToString();
                if (key == "") return;

                prevCanvas.Children.Clear();

                List<string> keyList = new List<string>(prevFloorplans.Keys);
                if (keyList.IndexOf(key) != 0 && Properties.Settings.Default.drawPrev)
                {
                    List<Polygon> shadowPolys = prevFloorplans[keyList[keyList.IndexOf(key) - 1]];
                    foreach (Polygon poly in shadowPolys)
                    {
                        prevCanvas.Children.Add(poly);
                    }
                    Polygon overlay = new Polygon();
                    overlay.Points = new PointCollection() { 
                        new System.Windows.Point(0, 0), 
                        new System.Windows.Point(0, prevBox.ActualWidth), 
                        new System.Windows.Point(prevBox.ActualHeight, prevBox.ActualWidth), 
                        new System.Windows.Point(prevBox.ActualHeight, 0)};
                    SolidColorBrush strokeBrush = new SolidColorBrush(Colors.White);
                    strokeBrush.Opacity = .85d;
                    overlay.Fill = strokeBrush;
                    prevCanvas.Children.Add(overlay);
                }
                List<Polygon> polys = prevFloorplans[key];
                
                foreach (Polygon poly in polys)
                {
                    prevCanvas.Children.Add(poly);
                }

                if (Properties.Settings.Default.drawWall)
                {
                    List<System.Windows.Shapes.Line> lines = prevPartitions[key];
                    foreach (System.Windows.Shapes.Line line in lines)
                    {
                        prevCanvas.Children.Add(line);
                    }
                }

                // update the legends
                TextBlock legend1 = new TextBlock();
                legend1.Text = "Space boundary";
                Canvas.SetRight(legend1, 50);
                Canvas.SetTop(legend1, 5);
                prevCanvas.Children.Add(legend1);

                TextBlock legend2 = new TextBlock();
                legend2.Text = "Partitions";
                Canvas.SetRight(legend2, 50);
                Canvas.SetTop(legend2, 25);
                prevCanvas.Children.Add(legend2);

                System.Windows.Shapes.Line mark1 = new System.Windows.Shapes.Line();
                mark1.Stroke = Brushes.Salmon;
                mark1.StrokeThickness = 4;
                mark1.X1 = prevBox.ActualWidth - 40;
                mark1.X2 = prevBox.ActualWidth - 10;
                mark1.Y1 = 13; mark1.Y2 = 13;
                prevCanvas.Children.Add(mark1);

                System.Windows.Shapes.Line mark2 = new System.Windows.Shapes.Line();
                mark2.Stroke = Brushes.Black;
                mark2.StrokeThickness = 1;
                mark2.X1 = prevBox.ActualWidth - 40;
                mark2.X2 = prevBox.ActualWidth - 10;
                mark2.Y1 = 34; mark2.Y2 = 34;
                prevCanvas.Children.Add(mark2);
            }
        }

        private void DrawPrev_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            Properties.Settings.Default.drawPrev = true;
        }
        private void DrawPrev_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            Properties.Settings.Default.drawPrev = false;
        }
        private void DrawWall_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            Properties.Settings.Default.drawWall = true;
        }
        private void DrawWall_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            Properties.Settings.Default.drawWall = false;
        }

        private void BtnCancel(object sender, RoutedEventArgs e)
        {
            btnGenerate.Visibility = System.Windows.Visibility.Visible;
            btnCancel.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void BtnOpenAragog(object sender, RoutedEventArgs e)
        {
            string urlToOpen = @"https://www.ladybug.tools/spider/gbxml-viewer/r14/gv-cor-core/gv-cor.html";
            System.Diagnostics.Process.Start(urlToOpen);
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
                string XMLPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) +
                    "/GingerbreadXML.xml";
                if (File.Exists(XMLPath))
                    File.Copy(XMLPath, dlg.FileName, true);
            }
        }
    }
}

