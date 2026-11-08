using System.Windows;
using System.Windows.Controls;
namespace DesktopNotes {
    public partial class SettingsWindow {
        void AddMaintenanceControls(Panel panel) {
            panel.Children.Add(new TextBlock {Text="数据与设置",FontSize=17,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,20,0,12)});
            var card=Card(panel);var clear=MainWindow.Button("清除数据","清除事项分类草稿和分类密码");clear.HorizontalContentAlignment=HorizontalAlignment.Left;clear.Foreground=palette.AccentText;clear.Padding=new Thickness(10,9,10,9);clear.Click+=delegate{main.RequestClearAllData();};card.Children.Add(clear);
            card.Children.Add(new TextBlock {Text="清除事项、分类、回收站、草稿及分类密码，保留外观和快捷键。清除前可备份。",Foreground=palette.Secondary,FontSize=11,TextWrapping=TextWrapping.Wrap,LineHeight=18,Margin=new Thickness(10,2,10,12)});
            var reset=MainWindow.Button("恢复默认设置","恢复软件自带设置并保留事项");reset.HorizontalContentAlignment=HorizontalAlignment.Left;reset.Padding=new Thickness(10,9,10,9);reset.Click+=delegate{main.RequestRestoreDefaults();};card.Children.Add(reset);
            card.Children.Add(new TextBlock {Text="恢复主题、透明度、背景、快捷键、窗口和开机启动等默认设置，保留事项、分类、草稿及分类密码。",Foreground=palette.Secondary,FontSize=11,TextWrapping=TextWrapping.Wrap,LineHeight=18,Margin=new Thickness(10,2,10,4)});
            string notice=main.TakeMaintenanceNotice();if(!string.IsNullOrEmpty(notice))card.Children.Add(new TextBlock {Text=notice,Foreground=palette.AccentText,FontSize=12,TextWrapping=TextWrapping.Wrap,LineHeight=20,Margin=new Thickness(10,12,10,2)});
        }
    }
}
