using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
namespace DesktopNotes {
    public sealed class CategoryPromptWindow : Window {
        readonly TextBox input;
        public string Value {get {return input.Text;}}
        public CategoryPromptWindow(MainWindow main,string title,string hint,string initial) {
            Title=title;Width=380;Height=238;WindowStartupLocation=WindowStartupLocation.CenterScreen;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=13;
            var palette=new ThemePalette();palette.Apply(main.Service.State.Settings,true);palette.Resources(this);Foreground=palette.Text;
            EventHandler changed=delegate {palette.Apply(main.Service.State.Settings,true);};main.AppearanceChanged+=changed;Closed+=delegate {main.AppearanceChanged-=changed;};
            var shell=new Border {Margin=new Thickness(8),CornerRadius=new CornerRadius(16),Background=palette.Surface,Effect=new DropShadowEffect {Color=Colors.Black,Opacity=.22,BlurRadius=16,ShadowDepth=2}};Content=shell;
            var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition {Height=new GridLength(48)});layout.RowDefinitions.Add(new RowDefinition());shell.Child=layout;
            var header=new Grid {Margin=new Thickness(22,4,12,0),Background=Brushes.Transparent};header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});layout.Children.Add(header);
            header.Children.Add(new TextBlock {Text=title,FontSize=16,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
            var close=UiDesign.Button("","关闭分类窗口");close.Width=30;close.Height=30;close.Margin=new Thickness(0);close.Padding=new Thickness(8);close.Content=UiDesign.Icon("close",palette.Icon,14);close.Click+=delegate {Close();};Grid.SetColumn(close,1);header.Children.Add(close);
            header.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) {
                var node=e.OriginalSource as DependencyObject;while(node!=null && node!=header) {if(node is ButtonBase)return;node=node is Visual ? VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node);}
                if(e.LeftButton==MouseButtonState.Pressed) {DragMove();e.Handled=true;}
            };
            var panel=new Grid {Margin=new Thickness(22,6,22,20)};panel.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});panel.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});panel.RowDefinitions.Add(new RowDefinition());Grid.SetRow(panel,1);layout.Children.Add(panel);
            panel.Children.Add(new TextBlock {Text=hint,Foreground=palette.Secondary,FontSize=12,Margin=new Thickness(0,0,0,8)});
            input=new TextBox {Text=initial ?? "",MaxLength=30,FontSize=14,Foreground=palette.Text,CaretBrush=palette.Accent,BorderThickness=new Thickness(0),Background=Brushes.Transparent,Padding=new Thickness(12,10,12,10),FocusVisualStyle=null};System.Windows.Automation.AutomationProperties.SetName(input,hint);
            var field=new Border {CornerRadius=new CornerRadius(10),Background=palette.Input,BorderBrush=palette.Line,BorderThickness=new Thickness(1),Child=input};Grid.SetRow(field,1);panel.Children.Add(field);
            input.GotKeyboardFocus+=delegate {field.BorderBrush=palette.Accent;};input.LostKeyboardFocus+=delegate {field.BorderBrush=palette.Line;};
            var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom};Grid.SetRow(buttons,2);panel.Children.Add(buttons);
            var cancel=UiDesign.Button("取消","取消");cancel.IsCancel=true;cancel.MinWidth=74;cancel.Padding=new Thickness(18,8,18,8);cancel.Click+=delegate {DialogResult=false;};buttons.Children.Add(cancel);
            var ok=UiDesign.Button("确定","确定");ok.IsDefault=true;ok.MinWidth=74;ok.Padding=new Thickness(18,8,18,8);ok.Background=palette.Selected;ok.Foreground=palette.AccentText;ok.Click+=delegate {DialogResult=true;};buttons.Children.Add(ok);
            Loaded+=delegate {input.Focus();input.SelectAll();};
        }
    }
}
