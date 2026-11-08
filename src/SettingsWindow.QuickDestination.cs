using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace DesktopNotes {
    public partial class SettingsWindow {
        Action AddQuickDestinationControls(Panel parent) {
            parent.Children.Add(new TextBlock {Text="添加到",Foreground=palette.Text,FontSize=13,Margin=new Thickness(0,18,0,8)});
            var choices=new UniformGrid {Columns=2};parent.Children.Add(new Border {CornerRadius=new CornerRadius(10),Background=palette.Input,Padding=new Thickness(3),Child=choices});
            var follow=MainWindow.Button("跟随当前分类","快捷添加跟随当前分类");var inbox=MainWindow.Button("未完成","快捷添加到未完成");
            follow.Margin=inbox.Margin=new Thickness(0);follow.Padding=inbox.Padding=new Thickness(5,9,5,9);choices.Children.Add(follow);choices.Children.Add(inbox);
            var hint=new TextBlock {FontSize=11,Foreground=palette.Secondary,TextWrapping=TextWrapping.Wrap,LineHeight=18,Margin=new Thickness(1,9,1,0)};parent.Children.Add(hint);
            Action sync=delegate {bool current=main.Service.State.Settings.QuickAddDestination!="inbox";follow.Background=current ? palette.Selected:Brushes.Transparent;follow.Foreground=current ? palette.AccentText:palette.Text;inbox.Background=current ? Brushes.Transparent:palette.Selected;inbox.Foreground=current ? palette.Text:palette.AccentText;hint.Text=current ? "跟随便签当前分类；已完成和回收站会存入未完成，加锁分类需先解锁。":"快捷添加的事项始终进入“未完成”，不切换便签正在查看的分类。";};
            Action<string> select=delegate(string mode) {string error;bool saved=main.TrySetQuickDestination(mode,out error);sync();if(!saved)hint.Text=error;};
            follow.Click+=delegate{select("current");};inbox.Click+=delegate{select("inbox");};Activated+=delegate{sync();};sync();return sync;
        }
    }
}
