using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Microsoft.Win32;
namespace DesktopNotes {
    public partial class SettingsWindow {
        Action AddBackgroundControls(Panel parent) {
            var section=new StackPanel {Margin=new Thickness(0,0,0,18)};parent.Children.Add(section);
            section.Children.Add(Label("自定义背景"));
            var choices=new UniformGrid {Columns=3,Margin=new Thickness(0,9,0,10)};section.Children.Add(choices);
            var colorPanel=new StackPanel();var imagePanel=new StackPanel();
            var hint=new TextBlock {Foreground=palette.Secondary,FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0)};
            var colors=new WrapPanel {Margin=new Thickness(0,0,0,8)};colorPanel.Children.Add(colors);
            var colorRow=new Grid();colorRow.ColumnDefinitions.Add(new ColumnDefinition());colorRow.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});colorPanel.Children.Add(colorRow);
            var colorInput=new TextBox {Text=main.Service.State.Settings.BackgroundColor,MaxLength=7,FontSize=13,Padding=new Thickness(10,8,10,8),Foreground=palette.Text,CaretBrush=palette.Accent,Background=Brushes.Transparent,BorderThickness=new Thickness(0)};
            System.Windows.Automation.AutomationProperties.SetName(colorInput,"背景颜色值");
            colorRow.Children.Add(new Border {Background=palette.Input,CornerRadius=new CornerRadius(8),BorderBrush=palette.Line,BorderThickness=new Thickness(1),Child=colorInput});
            var apply=MainWindow.Button("应用","应用背景颜色");apply.Margin=new Thickness(8,0,0,0);apply.Padding=new Thickness(12,7,12,7);Grid.SetColumn(apply,1);colorRow.Children.Add(apply);
            Action<string> setColor=delegate(string color) {string error;if(!main.TrySetCustomBackground("color",color,out error)) {hint.Text=error;return;}colorInput.Text=main.Service.State.Settings.BackgroundColor;};
            foreach(string color in new[]{"#26364F","#20242D","#31423C","#3B3048","#E6EAF2","#E8E2D8"}) {string selected=color;var swatch=MainWindow.Button("",color);swatch.Width=30;swatch.Height=30;swatch.Padding=new Thickness(3);swatch.Margin=new Thickness(0,0,9,0);swatch.Content=new Border {Width=24,Height=24,Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),CornerRadius=new CornerRadius(9),BorderBrush=palette.Line,BorderThickness=new Thickness(1)};swatch.Click+=delegate{setColor(selected);};colors.Children.Add(swatch);}
            apply.Click+=delegate{setColor(colorInput.Text.Trim());};
            var preview=new Image {Height=90,Stretch=Stretch.UniformToFill};imagePanel.Children.Add(new Border {CornerRadius=new CornerRadius(9),Background=palette.Input,Child=preview,Margin=new Thickness(0,0,0,8)});
            var select=MainWindow.Button("选择图片","选择电脑中的背景图片");select.Padding=new Thickness(10,8,10,8);imagePanel.Children.Add(select);
            Action chooseImage=delegate {var dialog=new OpenFileDialog {Title="选择便签背景图片",Filter="背景图片 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",Multiselect=false};if(dialog.ShowDialog(this)==true){string error;if(!main.TryImportBackground(dialog.FileName,out error))hint.Text=error;}};
            select.Click+=delegate{chooseImage();};
            var buttons=new List<Button>();string[] modes={"default","color","image"},names={"默认背景","纯色","图片"};
            for(int i=0;i<modes.Length;i++) {string mode=modes[i];var button=MainWindow.Button(names[i],names[i]);button.Tag=mode;button.Padding=new Thickness(4,8,4,8);button.Click+=delegate{string error;var p=main.Service.State.Settings;if(mode=="image" && main.BackgroundPreview==null)chooseImage();else if(!main.TrySetCustomBackground(mode,mode=="color" ? p.BackgroundColor:p.BackgroundImage,out error))hint.Text=error;};buttons.Add(button);choices.Children.Add(button);}
            section.Children.Add(colorPanel);section.Children.Add(imagePanel);section.Children.Add(hint);
            Action sync=delegate {var p=main.Service.State.Settings;foreach(var button in buttons){bool selected=(string)button.Tag==p.BackgroundMode;button.Background=selected ? palette.Selected:Brushes.Transparent;button.Foreground=selected ? palette.AccentText:palette.Text;}colorPanel.Visibility=p.BackgroundMode=="color" ? Visibility.Visible:Visibility.Collapsed;imagePanel.Visibility=p.BackgroundMode=="image" ? Visibility.Visible:Visibility.Collapsed;preview.Source=p.BackgroundMode=="image" ? main.BackgroundPreview:null;hint.Text=p.BackgroundMode=="image" ? (preview.Source==null ? "图片已丢失，当前使用主题背景。请选择新图片。":"图片居中铺满 · PNG / JPG / BMP · 最大20MB"):p.BackgroundMode=="color" ? "选择预设颜色，或输入 # 开头的六位颜色值。":"使用当前主题的默认背景。";};sync();return sync;
        }
    }
}
