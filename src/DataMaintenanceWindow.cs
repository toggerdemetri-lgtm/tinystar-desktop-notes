using System;
using System.Windows;
using System.Windows.Controls;
namespace DesktopNotes {
    enum MaintenanceDialogKind {ClearChoice,ClearDirect,ResetDefaults}
    enum ClearDataChoice {None,Backup,Direct}
    sealed class DataMaintenanceWindow : StepDialogWindow {
        internal ClearDataChoice Choice {get;private set;}
        internal DataMaintenanceWindow(MainWindow main,MaintenanceDialogKind kind):base(main,kind==MaintenanceDialogKind.ResetDefaults ? "恢复默认设置":kind==MaintenanceDialogKind.ClearDirect ? "确认直接清除":"清除数据",460,kind==MaintenanceDialogKind.ClearChoice ? 360:330) {
            Body.RowDefinitions.Add(new RowDefinition());Body.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            var content=new StackPanel {Margin=new Thickness(0,8,0,15)};Body.Children.Add(content);
            bool reset=kind==MaintenanceDialogKind.ResetDefaults;
            content.Children.Add(new TextBlock {Text=reset ? "恢复安装时自带的设置？":kind==MaintenanceDialogKind.ClearChoice ? "清除前，是否先保存一份备份？":"确定不备份，直接清除所有内容？",Foreground=P.Text,FontSize=15,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap,LineHeight=24,Margin=new Thickness(0,0,0,12)});
            content.Children.Add(new TextBlock {Text=reset ? "透明度、主题、背景、快捷键、窗口位置和大小等恢复默认，关闭开机启动。\n事项、步骤、分类、草稿和分类密码会保留。":"将清除全部事项、步骤、自定义分类、回收站、草稿及分类密码。\n外观、快捷键等设置会保留。",Foreground=P.Secondary,FontSize=12,TextWrapping=TextWrapping.Wrap,LineHeight=22});
            if(!reset)content.Children.Add(new TextBlock {Text=kind==MaintenanceDialogKind.ClearChoice ? "备份成功后才会清除，已保存的备份文件会保留。":"此操作不能通过回收站撤销。",Foreground=kind==MaintenanceDialogKind.ClearDirect ? P.AccentText:P.Secondary,FontSize=12,TextWrapping=TextWrapping.Wrap,LineHeight=22,Margin=new Thickness(0,10,0,0)});
            var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};Grid.SetRow(buttons,1);Body.Children.Add(buttons);
            var cancel=ActionButton("取消",false);cancel.IsDefault=true;cancel.IsCancel=true;cancel.Click+=delegate{DialogResult=false;};buttons.Children.Add(cancel);
            if(kind==MaintenanceDialogKind.ClearChoice){var direct=ActionButton("直接清除",false);direct.Click+=delegate{Choice=ClearDataChoice.Direct;DialogResult=true;};buttons.Children.Add(direct);var backup=ActionButton("备份并清除",true);backup.Click+=delegate{Choice=ClearDataChoice.Backup;DialogResult=true;};buttons.Children.Add(backup);}
            else {var confirm=ActionButton(reset ? "恢复默认":"确认清除",true);confirm.Click+=delegate{DialogResult=true;};buttons.Children.Add(confirm);}
            Loaded+=delegate{cancel.Focus();};
        }
        internal DataMaintenanceWindow(MainWindow main,string error):base(main,"操作未完成",460,290) {
            Body.RowDefinitions.Add(new RowDefinition());Body.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});Body.Children.Add(new TextBlock {Text=error,TextWrapping=TextWrapping.Wrap,LineHeight=23,Foreground=P.Text,Margin=new Thickness(0,8,0,15)});
            var ok=ActionButton("知道了",true);ok.IsDefault=true;ok.IsCancel=true;ok.HorizontalAlignment=HorizontalAlignment.Right;ok.Click+=delegate{DialogResult=false;};Grid.SetRow(ok,1);Body.Children.Add(ok);
        }
    }
}
