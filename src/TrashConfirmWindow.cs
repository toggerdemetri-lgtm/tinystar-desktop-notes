using System.Windows;
using System.Windows.Controls;
namespace DesktopNotes {
    sealed class TrashConfirmWindow : StepDialogWindow {
        public TrashConfirmWindow(MainWindow main,int count,bool retainsLockedTrash) : base(main,"清空回收站",410,retainsLockedTrash ? 310:280) {
            Body.RowDefinitions.Add(new RowDefinition());Body.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            var content=new Grid {Margin=new Thickness(0,8,0,14)};content.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(50)});content.ColumnDefinitions.Add(new ColumnDefinition());Body.Children.Add(content);
            var icon=new Border {Width=36,Height=36,CornerRadius=new CornerRadius(11),Background=P.Selected,VerticalAlignment=VerticalAlignment.Top,HorizontalAlignment=HorizontalAlignment.Left,Child=UiDesign.Icon("trash",P.Accent,18)};content.Children.Add(icon);
            var text=new StackPanel();Grid.SetColumn(text,1);content.Children.Add(text);
            text.Children.Add(new TextBlock {Text="要永久删除这 "+count+" 项已删除事项吗？",Foreground=P.Text,FontSize=14,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,LineHeight=23,Margin=new Thickness(0,0,0,9)});
            text.Children.Add(new TextBlock {Text="清空后无法恢复。",Foreground=P.Secondary,FontSize=12,TextWrapping=TextWrapping.Wrap,LineHeight=20});
            if(retainsLockedTrash)text.Children.Add(new TextBlock {Text="已上锁分类中的内容会保留。",Foreground=P.Secondary,FontSize=12,TextWrapping=TextWrapping.Wrap,LineHeight=20,Margin=new Thickness(0,10,0,0)});
            var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};Grid.SetRow(buttons,1);Body.Children.Add(buttons);
            var cancel=ActionButton("取消",false);cancel.IsDefault=true;cancel.IsCancel=true;cancel.Click+=delegate {DialogResult=false;};buttons.Children.Add(cancel);
            var clear=ActionButton("清空",true);clear.Click+=delegate {DialogResult=true;};buttons.Children.Add(clear);
            Loaded+=delegate {cancel.Focus();};
        }
    }
}
