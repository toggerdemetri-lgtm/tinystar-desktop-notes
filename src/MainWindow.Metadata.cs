using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace DesktopNotes {
    public partial class MainWindow {
        TextBlock TimeLabel(string text,string tag) {return new TextBlock {Text=text,Tag=tag,FontSize=10,Foreground=Palette.Text,Opacity=.5,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};}
        static double NaturalTimeWidth(TextBlock text) {return new FormattedText(text.Text,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(text.FontFamily,text.FontStyle,text.FontWeight,text.FontStretch),text.FontSize,Brushes.Black,VisualTreeHelper.GetDpi(text).PixelsPerDip).WidthIncludingTrailingWhitespace;}
        FrameworkElement NoteMetadata(Note note) {
            bool compact=note.Steps.Count==0 && !note.Done && !note.Deleted;
            var metadata=new Grid {Tag="note-metadata",Margin=new Thickness(0,compact ? 1:5,0,0)};metadata.ColumnDefinitions.Add(new ColumnDefinition());metadata.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});metadata.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});metadata.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            if(note.Done) {var category=TimeLabel("分类 · "+Service.CategoryName(note.CategoryId),"note-source");category.Margin=new Thickness(0,0,0,4);Grid.SetColumnSpan(category,2);metadata.Children.Add(category);}
            var clocks=new Grid();clocks.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});clocks.ColumnDefinitions.Add(new ColumnDefinition());clocks.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});clocks.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});Grid.SetRow(clocks,1);metadata.Children.Add(clocks);
            var created=TimeLabel((note.Done ? "创建 ":"")+DateTimeOffset.Parse(note.Created).ToLocalTime().ToString("yyyy-MM-dd HH:mm"),"created-time");clocks.Children.Add(created);
            if(note.Done) {
                var completed=TimeLabel("完成 "+DateTimeOffset.Parse(note.Completed).ToLocalTime().ToString("yyyy-MM-dd HH:mm"),"completed-time");completed.HorizontalAlignment=HorizontalAlignment.Right;Grid.SetColumn(completed,1);clocks.Children.Add(completed);
                bool? stacked=null;
                clocks.SizeChanged+=delegate {
                    bool next=clocks.ActualWidth<NaturalTimeWidth(created)+NaturalTimeWidth(completed)+12;if(stacked==next)return;stacked=next;
                    Grid.SetColumnSpan(created,next ? 2:1);Grid.SetColumn(completed,next ? 0:1);Grid.SetColumnSpan(completed,next ? 2:1);Grid.SetRow(completed,next ? 1:0);completed.HorizontalAlignment=next ? HorizontalAlignment.Left:HorizontalAlignment.Right;completed.Margin=next ? new Thickness(0,4,0,0):new Thickness(12,0,0,0);
                };
            } else Grid.SetColumnSpan(created,2);
            return metadata;
        }
        Button AddStepAction(Note note) {
                var add=Button("＋步骤","新增步骤");add.FontSize=10;add.ClearValue(Control.ForegroundProperty);var style=new Style(typeof(Button));style.Setters.Add(new Setter(Control.ForegroundProperty,Palette.Text));style.Setters.Add(new Setter(UIElement.OpacityProperty,.5));
                foreach(var property in new[]{UIElement.IsMouseOverProperty,UIElement.IsKeyboardFocusWithinProperty}) {var trigger=new Trigger {Property=property,Value=true};trigger.Setters.Add(new Setter(Control.ForegroundProperty,Palette.AccentText));trigger.Setters.Add(new Setter(UIElement.OpacityProperty,1.0));style.Triggers.Add(trigger);}
                add.Style=style;add.Padding=new Thickness(4,5,0,5);add.Margin=new Thickness(7,0,8,0);add.VerticalAlignment=VerticalAlignment.Center;add.Click+=delegate {BeginStepEdit(note,null,null,true);};return add;
        }
    }
}
