using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
namespace DesktopNotes {
    abstract class StepDialogWindow : Window {
        protected readonly ThemePalette P=new ThemePalette();
        protected readonly Grid Body=new Grid();
        protected StepDialogWindow(MainWindow main,string title,double width,double height) {
            Title=title;Width=width;Height=Math.Min(height,SystemParameters.WorkArea.Height-30);WindowStartupLocation=WindowStartupLocation.CenterScreen;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=13;
            P.Apply(main.Service.State.Settings,true);P.Resources(this);Foreground=P.Text;
            EventHandler changed=delegate {P.Apply(main.Service.State.Settings,true);};main.AppearanceChanged+=changed;Closed+=delegate {main.AppearanceChanged-=changed;};
            var shell=new Border {Margin=new Thickness(8),Padding=new Thickness(22,14,22,20),CornerRadius=new CornerRadius(16),Background=P.Surface,Effect=new DropShadowEffect {Color=Colors.Black,Opacity=.22,BlurRadius=16,ShadowDepth=2}};Content=shell;
            var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition {Height=new GridLength(44)});layout.RowDefinitions.Add(new RowDefinition());shell.Child=layout;
            var header=new Grid {Background=Brushes.Transparent,Margin=new Thickness(0,0,-8,8)};header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});layout.Children.Add(header);
            header.Children.Add(new TextBlock {Text=title,FontSize=17,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
            var close=UiDesign.Button("","关闭"+title);close.Width=30;close.Height=30;close.Padding=new Thickness(8);close.Content=UiDesign.Icon("close",P.Icon,14);close.Click+=delegate {Close();};Grid.SetColumn(close,1);header.Children.Add(close);
            header.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e) {var node=e.OriginalSource as DependencyObject;while(node!=null && node!=header) {if(node is ButtonBase)return;node=node is Visual ? VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node);}if(e.LeftButton==MouseButtonState.Pressed){DragMove();e.Handled=true;}};
            Grid.SetRow(Body,1);layout.Children.Add(Body);
        }
        protected Button ActionButton(string text,bool accent) {var b=UiDesign.Button(text,text);b.Padding=new Thickness(17,9,17,9);if(accent){b.Background=P.Selected;b.Foreground=P.AccentText;}return b;}
    }
    sealed class CompleteStepsWindow : StepDialogWindow {
        public CompleteStepsWindow(MainWindow main,int remaining) : base(main,"完成整个事项",388,236) {
            Body.RowDefinitions.Add(new RowDefinition());Body.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            Body.Children.Add(new TextBlock {Text="还有 "+remaining+" 个步骤未完成。\n是否一并勾选，并将事项移入已完成？",TextWrapping=TextWrapping.Wrap,LineHeight=24,Margin=new Thickness(0,6,0,16)});
            var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};Grid.SetRow(buttons,1);Body.Children.Add(buttons);var cancel=ActionButton("取消",false);cancel.IsCancel=true;cancel.Click+=delegate {DialogResult=false;};buttons.Children.Add(cancel);var complete=ActionButton("全部完成",true);complete.Click+=delegate {DialogResult=true;};buttons.Children.Add(complete);
        }
    }
}
