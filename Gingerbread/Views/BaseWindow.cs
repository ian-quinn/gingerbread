#region Namespaces
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
#endregion

namespace Gingerbread.Views
{
    public class BaseWindow : Window
    {
        public BaseWindow()
        {
            //this.ApplyTemplate();
            InitializeStyle();
            
            this.Loaded += delegate
            {
                InitializeEvent();
            };
        }
        private void InitializeEvent()
        {
            var resourceDict = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Gingerbread;component/Views/BaseWindowStyle.xaml", UriKind.Absolute)
            };
            ControlTemplate baseTemplate = resourceDict["BaseWindowControlTemplate"] as ControlTemplate;

            Button infoBtn = this.Template.FindName("InfoButton", this) as Button;
            Style style = new Style(typeof(ToolTip));
            style.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed));
            style.Seal();
            bool _isToolTipVisible = false;
            this.Resources.Add(typeof(ToolTip), style);
            infoBtn.Click += delegate
            {
                if (_isToolTipVisible)
                {
                    this.Resources.Add(typeof(ToolTip), style); //hide
                    _isToolTipVisible = false;
                }
                else
                {
                    this.Resources.Remove(typeof(ToolTip)); //show
                    _isToolTipVisible = true;
                }
            };

            Button minBtn = this.Template.FindName("MinimizeButton", this) as Button;
            minBtn.Click += delegate
            {
                this.WindowState = WindowState.Minimized;
            };

            //Button maxBtn = this.Template.FindName("MaximizeButton", this) as Button;
            //maxBtn.Click += delegate
            //{
            //    this.WindowState = (this.WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal);
            //};

            Button closeBtn = this.Template.FindName("CloseButton", this) as Button;
            closeBtn.Click += delegate
            {
                this.Close();
            };

            Border mainHeader = this.Template.FindName("MainWindowBorder", this) as Border;
            mainHeader.MouseMove += delegate (object sender, MouseEventArgs e)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    this.DragMove();
                }
            };

            //mainHeader.MouseLeftButtonDown += delegate (object sender, MouseButtonEventArgs e)
            //{
            //    if (e.ClickCount >= 2)
            //    {
            //        maxBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            //    }
            //};

            TextBlock titleBar = this.Template.FindName("txtTitle", this) as TextBlock;
            titleBar.Text = "Gingerbread";
        }

        private void InitializeStyle()
        {
            var resourceDict = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Gingerbread;component/Views/BaseWindowStyle.xaml", UriKind.Absolute)
            };
            this.Style = resourceDict["BaseWindowStyle"] as Style;
        }

    }
}
