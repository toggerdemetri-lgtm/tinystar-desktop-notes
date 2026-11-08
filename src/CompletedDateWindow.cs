using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
namespace DesktopNotes {
    public sealed class CompletedDateWindow : Window {
        public DateTime? From {get;private set;}
        public DateTime? To {get;private set;}
        DateTime? pendingFrom,pendingTo;bool choosingEnd;
        public CompletedDateWindow(MainWindow main,DateTime? from,DateTime? to) {
            pendingFrom=from;pendingTo=to;
            Title="筛选完成日期";Width=400;Height=548;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;WindowStartupLocation=WindowStartupLocation.CenterScreen;ShowInTaskbar=false;FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=13;
            var palette=new ThemePalette();palette.Apply(main.Service.State.Settings,true);palette.Resources(this);Foreground=palette.Text;
            EventHandler changed=delegate {palette.Apply(main.Service.State.Settings,true);};main.AppearanceChanged+=changed;Closed+=delegate {main.AppearanceChanged-=changed;};
            var shell=new Border {Margin=new Thickness(8),Padding=new Thickness(20,15,20,18),CornerRadius=new CornerRadius(16),Background=palette.Surface,Effect=new DropShadowEffect {Color=Colors.Black,Opacity=.22,BlurRadius=16,ShadowDepth=2}};Content=shell;
            var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition());layout.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});shell.Child=layout;
            var header=new Grid {Background=Brushes.Transparent,Margin=new Thickness(0,0,-8,12)};header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});layout.Children.Add(header);
            header.Children.Add(new TextBlock {Text="完成日期",FontSize=17,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center});
            var close=UiDesign.Button("","关闭日期筛选");close.Width=30;close.Height=30;close.Padding=new Thickness(8);close.Content=UiDesign.Icon("close",palette.Icon,14);close.Click+=delegate {Close();};Grid.SetColumn(close,1);header.Children.Add(close);
            header.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){var node=e.OriginalSource as DependencyObject;while(node!=null && node!=header){if(node is ButtonBase)return;node=node is Visual ? VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node);}if(e.LeftButton==MouseButtonState.Pressed){DragMove();e.Handled=true;}};
            var fields=new StackPanel();Grid.SetRow(fields,1);layout.Children.Add(fields);
            var dates=new Grid {Margin=new Thickness(0,0,0,10)};dates.ColumnDefinitions.Add(new ColumnDefinition());dates.ColumnDefinitions.Add(new ColumnDefinition());fields.Children.Add(dates);
            var start=UiDesign.Button("","选择开始日期");var end=UiDesign.Button("","选择结束日期");start.HorizontalContentAlignment=end.HorizontalContentAlignment=HorizontalAlignment.Left;start.Padding=end.Padding=new Thickness(12,9,12,9);dates.Children.Add(start);Grid.SetColumn(end,1);dates.Children.Add(end);
            var calendar=new CompletedDateCalendar(palette,from??to??DateTime.Today);fields.Children.Add(calendar);
            var shortcuts=new Grid {Margin=new Thickness(0,8,0,0)};shortcuts.ColumnDefinitions.Add(new ColumnDefinition());shortcuts.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});fields.Children.Add(shortcuts);
            var today=UiDesign.Button("今天","选择今天");today.Foreground=palette.AccentText;today.HorizontalAlignment=HorizontalAlignment.Left;shortcuts.Children.Add(today);
            var unlimited=UiDesign.Button("不限日期","当前端点不限日期");unlimited.Foreground=palette.Secondary;Grid.SetColumn(unlimited,1);shortcuts.Children.Add(unlimited);
            var hint=new TextBlock {FontSize=11,Foreground=palette.Secondary,Text="先选开始日期，再选结束日期；包含当天全天。",Margin=new Thickness(3,6,0,0)};fields.Children.Add(hint);
            var error=new TextBlock {FontSize=11,Foreground=new SolidColorBrush(Color.FromRgb(244,128,128)),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(3,5,0,0)};fields.Children.Add(error);
            Action refresh=delegate {
                start.Content=DateLabel("开始日期",pendingFrom,palette);end.Content=DateLabel("结束日期",pendingTo,palette);
                start.Background=choosingEnd ? palette.Input:palette.Selected;end.Background=choosingEnd ? palette.Selected:palette.Input;
                start.BorderBrush=choosingEnd ? palette.Line:palette.Accent;end.BorderBrush=choosingEnd ? palette.Accent:palette.Line;start.BorderThickness=end.BorderThickness=new Thickness(1);
                calendar.SetSelection(choosingEnd ? pendingTo:pendingFrom,pendingFrom,pendingTo);error.Text="";
            };
            start.Click+=delegate {choosingEnd=false;calendar.ShowMonth(pendingFrom??pendingTo??DateTime.Today);refresh();};end.Click+=delegate {choosingEnd=true;calendar.ShowMonth(pendingTo??pendingFrom??DateTime.Today);refresh();};
            Action<DateTime> select=delegate(DateTime date){if(choosingEnd)pendingTo=date.Date;else{pendingFrom=date.Date;choosingEnd=true;}refresh();};
            calendar.DateSelected+=select;today.Click+=delegate {calendar.ShowMonth(DateTime.Today);select(DateTime.Today);};unlimited.Click+=delegate {if(choosingEnd)pendingTo=null;else pendingFrom=null;refresh();};
            var buttons=new Grid();buttons.ColumnDefinitions.Add(new ColumnDefinition());buttons.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});Grid.SetRow(buttons,2);layout.Children.Add(buttons);
            var clear=UiDesign.Button("清除筛选","显示全部已完成事项");clear.HorizontalAlignment=HorizontalAlignment.Left;clear.Padding=new Thickness(0,9,8,9);clear.Click+=delegate {From=null;To=null;DialogResult=true;};buttons.Children.Add(clear);
            var right=new StackPanel {Orientation=Orientation.Horizontal};Grid.SetColumn(right,1);buttons.Children.Add(right);
            var cancel=UiDesign.Button("取消","取消");cancel.IsCancel=true;cancel.Click+=delegate {DialogResult=false;};right.Children.Add(cancel);
            var apply=UiDesign.Button("应用","应用日期范围");apply.IsDefault=true;apply.Background=palette.Selected;apply.Foreground=palette.AccentText;apply.Padding=new Thickness(18,9,18,9);right.Children.Add(apply);
            apply.Click+=delegate {if(pendingFrom.HasValue && pendingTo.HasValue && pendingFrom.Value>pendingTo.Value){error.Text="开始日期不能晚于结束日期";return;}From=pendingFrom;To=pendingTo;DialogResult=true;};
            refresh();Loaded+=delegate {start.Focus();};
        }
        static UIElement DateLabel(string label,DateTime? date,ThemePalette palette){var content=new StackPanel();content.Children.Add(new TextBlock {Text=label,FontSize=11,Foreground=palette.Secondary});content.Children.Add(new TextBlock {Text=date.HasValue ? date.Value.ToString("yyyy年M月d日",CultureInfo.InvariantCulture):"不限",FontSize=13,Foreground=palette.Text,Margin=new Thickness(0,3,0,0)});return content;}
    }
}
