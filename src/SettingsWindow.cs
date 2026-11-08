using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Markup;
using Microsoft.Win32;
namespace DesktopNotes {
    public partial class SettingsWindow : Window {
        readonly ThemePalette palette=new ThemePalette();
        readonly MainWindow main;TextBox shortcutField;TextBlock shortcutHint;Button shortcutButton;Border shortcutFrame;bool recordingShortcut;
        internal bool IsRecordingShortcut {get {return recordingShortcut && IsActive && shortcutField.IsKeyboardFocusWithin;}}
        static readonly ControlTemplate OpacityTemplate=(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Slider'>
          <Grid Height='24'><Track x:Name='PART_Track' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{TemplateBinding Value}' Orientation='Horizontal'>
            <Track.DecreaseRepeatButton><RepeatButton Command='{x:Static Slider.DecreaseLarge}' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Height='4' CornerRadius='2' Background='{DynamicResource SettingsAccentBrush}'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
            <Track.Thumb><Thumb Width='14' Height='24'><Thumb.Template><ControlTemplate TargetType='Thumb'><Ellipse Width='14' Height='14' Fill='{DynamicResource SettingsAccentBrush}' VerticalAlignment='Center'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
            <Track.IncreaseRepeatButton><RepeatButton Command='{x:Static Slider.IncreaseLarge}' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Height='4' CornerRadius='2' Background='{DynamicResource SettingsRailBrush}'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
          </Track></Grid></ControlTemplate>");
        static readonly ControlTemplate SwitchTemplate=(ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='CheckBox'>
          <Grid Background='Transparent' MinHeight='26'><Grid.ColumnDefinitions><ColumnDefinition/><ColumnDefinition Width='36'/></Grid.ColumnDefinitions>
            <ContentPresenter VerticalAlignment='Center' RecognizesAccessKey='True'/>
            <Border x:Name='rail' Grid.Column='1' Width='34' Height='20' CornerRadius='10' Background='{DynamicResource SettingsRailBrush}' VerticalAlignment='Center'>
              <Ellipse x:Name='knob' Width='14' Height='14' Fill='{DynamicResource SettingsOffThumbBrush}' HorizontalAlignment='Left' Margin='3,0'/>
            </Border>
          </Grid><ControlTemplate.Triggers>
            <Trigger Property='IsChecked' Value='True'><Setter TargetName='rail' Property='Background' Value='{DynamicResource SettingsAccentBrush}'/><Setter TargetName='knob' Property='HorizontalAlignment' Value='Right'/><Setter TargetName='knob' Property='Fill' Value='{DynamicResource SettingsOnThumbBrush}'/></Trigger>
            <Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.45'/></Trigger>
          </ControlTemplate.Triggers></ControlTemplate>");
        public SettingsWindow(MainWindow main) {
            this.main=main;
            Title="TinyStar桌面便签 · 设置";Width=440;Height=650;MinHeight=450;WindowStartupLocation=WindowStartupLocation.CenterScreen;ResizeMode=ResizeMode.NoResize;FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=13;
            WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;
            palette.Apply(main.Service.State.Settings,true);palette.Resources(this);Foreground=palette.Text;
            Resources["SettingsAccentBrush"]=palette.Accent;Resources["SettingsRailBrush"]=palette.Line;Resources["SettingsOffThumbBrush"]=palette.Secondary;Resources["SettingsOnThumbBrush"]=palette.CheckInk;
            Action sync=null;EventHandler changed=delegate {palette.Apply(main.Service.State.Settings,true);if(sync!=null)sync();};main.AppearanceChanged+=changed;
            var shell=new Border {Background=palette.Surface,CornerRadius=new CornerRadius(16),Margin=new Thickness(8),Effect=new DropShadowEffect {Color=Colors.Black,BlurRadius=16,ShadowDepth=2,Opacity=.22}};Content=shell;
            var layout=new Grid();layout.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});layout.RowDefinitions.Add(new RowDefinition());shell.Child=layout;
            var header=new Grid {Height=44,Margin=new Thickness(22,6,12,0),Background=Brushes.Transparent};header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});layout.Children.Add(header);
            header.Children.Add(new TextBlock {Text="TinyStar桌面便签 · 设置",FontSize=15,FontWeight=FontWeights.SemiBold,Foreground=palette.Text,VerticalAlignment=VerticalAlignment.Center});
            var close=MainWindow.Button("","关闭设置窗口");close.Width=30;close.Height=30;close.Padding=new Thickness(8);close.Margin=new Thickness(0);close.Content=UiDesign.Icon("close",palette.Icon,14);close.Click+=delegate {Close();};Grid.SetColumn(close,1);header.Children.Add(close);
            header.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) {
                DependencyObject node=e.OriginalSource as DependencyObject;while(node!=null && node!=header) {if(node is ButtonBase)return;node=node is Visual ? VisualTreeHelper.GetParent(node):LogicalTreeHelper.GetParent(node);}
                if(e.LeftButton==MouseButtonState.Pressed) {DragMove();e.Handled=true;}
            };
            var panel=new StackPanel {Margin=new Thickness(24,12,24,24)};var scroll=new ScrollViewer {Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Background=Brushes.Transparent};Grid.SetRow(scroll,1);layout.Children.Add(scroll);
            panel.Children.Add(new TextBlock {Text="外观",FontSize=23,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,18)});
            var look=Card(panel);look.Children.Add(Label("主题"));
            var theme=new UniformGrid {Columns=3,Margin=new Thickness(0,8,0,18)};
            var themeButtons=new System.Collections.Generic.List<Button>();
            string[] modes={"system","light","dark"},names={"跟随系统","浅色","深色"};
            for(int i=0;i<modes.Length;i++) {
                string mode=modes[i];var button=MainWindow.Button(names[i],names[i]+"主题");button.Tag=mode;button.Padding=new Thickness(5,10,5,10);
                button.Click+=delegate {main.Service.State.Settings.ThemeMode=mode;main.ApplyAppearance();main.Save();};
                themeButtons.Add(button);theme.Children.Add(button);
            }
            sync=delegate {foreach(var button in themeButtons) {bool selected=(string)button.Tag==main.Service.State.Settings.ThemeMode;button.Background=selected ? palette.Selected:Brushes.Transparent;button.Foreground=selected ? palette.AccentText:palette.Text;}};sync();look.Children.Add(theme);
            var sizeLabel=Label("界面大小  "+Math.Round(main.Service.State.Settings.UiSize)+"%");look.Children.Add(sizeLabel);
            var sizeSlider=new Slider {Minimum=0,Maximum=100,Value=main.Service.State.Settings.UiSize,TickFrequency=1,IsSnapToTickEnabled=true,Margin=new Thickness(0,9,0,6),Template=OpacityTemplate};System.Windows.Automation.AutomationProperties.SetName(sizeSlider,"主便签界面大小");look.Children.Add(sizeSlider);
            look.Children.Add(new TextBlock {Text="50%为默认大小，只缩放主便签内容，窗口外框不变。",Foreground=palette.Secondary,FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,18)});
            bool syncingSize=false;sizeSlider.ValueChanged+=delegate {if(syncingSize)return;if(!main.TrySetUiSize(sizeSlider.Value)) {syncingSize=true;sizeSlider.Value=main.Service.State.Settings.UiSize;syncingSize=false;}sizeLabel.Text="界面大小  "+Math.Round(sizeSlider.Value)+"%";};
            var syncBackground=AddBackgroundControls(look);var syncTheme=sync;sync=delegate {syncTheme();syncBackground();};
            AddOpacity(look,"背景不透明度",main.Service.State.Settings.BackgroundOpacity,delegate(double value) {main.Service.State.Settings.BackgroundOpacity=value;main.ApplyAppearance();main.Save();});
            AddOpacity(look,"文字不透明度",main.Service.State.Settings.TextOpacity,delegate(double value) {main.Service.State.Settings.TextOpacity=value;main.ApplyAppearance();main.Save();});
            look.Children.Add(new TextBlock {Text="背景和文字分别调整，工具图标保持清楚。",Foreground=palette.Secondary,FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,0)});
            var motion=new CheckBox {Content="界面动效",IsChecked=main.Service.State.Settings.AnimationsEnabled,Foreground=palette.Text,Template=SwitchTemplate,Margin=new Thickness(0,15,0,0)};
            motion.Click+=delegate {main.Service.State.Settings.AnimationsEnabled=motion.IsChecked==true;main.ApplyAppearance();main.Save();};look.Children.Add(motion);
            panel.Children.Add(new TextBlock {Text="快捷添加",FontSize=17,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,20,0,12)});
            var quick=Card(panel);quick.Children.Add(Label("全局快捷键"));
            var shortcutRow=new Grid {Margin=new Thickness(0,10,0,0)};shortcutRow.ColumnDefinitions.Add(new ColumnDefinition());shortcutRow.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});quick.Children.Add(shortcutRow);
            shortcutField=new TextBox {Text=main.CurrentShortcutDisplay,IsReadOnly=true,Focusable=false,BorderThickness=new Thickness(0),Background=Brushes.Transparent,Foreground=palette.Text,CaretBrush=palette.Accent,FontSize=14,Padding=new Thickness(10,9,10,9)};
            System.Windows.Automation.AutomationProperties.SetName(shortcutField,"快捷添加组合键");InputMethod.SetIsInputMethodEnabled(shortcutField,false);
            shortcutFrame=new Border {Background=palette.Input,BorderBrush=palette.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Child=shortcutField};shortcutRow.Children.Add(shortcutFrame);
            shortcutButton=MainWindow.Button("修改","修改快捷添加快捷键");shortcutButton.Margin=new Thickness(8,0,0,0);shortcutButton.Background=palette.Selected;shortcutButton.Click+=delegate {if(recordingShortcut)CancelShortcutRecording();else BeginShortcutRecording();};Grid.SetColumn(shortcutButton,1);shortcutRow.Children.Add(shortcutButton);
            shortcutHint=new TextBlock {Text=main.ShortcutRegistered ? "按此快捷键呼出添加栏，默认在任务栏上方，可拖动空白处。":"快捷键暂不可用，请点“修改”换一个组合。",Foreground=palette.Secondary,FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(1,9,1,0)};quick.Children.Add(shortcutHint);
            var syncDestination=AddQuickDestinationControls(quick);var syncAppearance=sync;sync=delegate {syncAppearance();syncDestination();};
            quick.Children.Add(new Border {Height=16});
            AddOpacity(quick,"快捷栏不透明度",main.Service.State.Settings.QuickAddOpacity,delegate(double value) {main.Service.State.Settings.QuickAddOpacity=value;main.ApplyAppearance();main.Save();});
            quick.Children.Add(new TextBlock {Text="单独调整快捷栏整体，不影响桌面便签和设置页。",Foreground=palette.Secondary,FontSize=11,TextWrapping=TextWrapping.Wrap});
            shortcutField.PreviewKeyDown+=delegate(object sender,KeyEventArgs e) {
                if(!recordingShortcut)return;Key key=e.Key==Key.System ? e.SystemKey:e.Key;e.Handled=true;
                if(key==Key.Escape) {CancelShortcutRecording();return;}
                if(key==Key.LeftCtrl || key==Key.RightCtrl || key==Key.LeftAlt || key==Key.RightAlt || key==Key.LeftShift || key==Key.RightShift)return;
                ShortcutGesture gesture;string error;if(!ShortcutGesture.TryCreate(key,Keyboard.Modifiers,out gesture,out error)) {shortcutHint.Text=error;return;}AcceptRecordedShortcut(gesture);
            };
            shortcutField.LostKeyboardFocus+=delegate {if(recordingShortcut)Dispatcher.BeginInvoke(new Action(delegate {if(recordingShortcut && !shortcutField.IsKeyboardFocusWithin)CancelShortcutRecording();}));};
            panel.Children.Add(new TextBlock {Text="分类密码",FontSize=17,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,20,0,12)});
            var privacyCard=Card(panel);var privacyStatus=new TextBlock {Foreground=palette.Text,FontSize=13};privacyCard.Children.Add(privacyStatus);
            var privacyButtons=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,10,0,8)};privacyCard.Children.Add(privacyButtons);
            var passwordEdit=MainWindow.Button("设置密码","设置分类密码");passwordEdit.Background=palette.Selected;passwordEdit.Foreground=palette.AccentText;privacyButtons.Children.Add(passwordEdit);
            var passwordRemove=MainWindow.Button("移除密码","移除分类密码");privacyButtons.Children.Add(passwordRemove);
            Action syncPrivacy=delegate {bool has=main.Privacy.HasPassword;privacyStatus.Text=has ? "已设置 · 自定义分类共用一个密码":"尚未设置分类密码";passwordEdit.Content=has ? "修改密码":"设置密码";System.Windows.Automation.AutomationProperties.SetName(passwordEdit,has ? "修改分类密码":"设置分类密码");passwordRemove.Visibility=has ? Visibility.Visible:Visibility.Collapsed;};syncPrivacy();
            passwordEdit.Click+=delegate {new CategoryPasswordWindow(main,CategoryPasswordMode.Set,null).ShowDialog();syncPrivacy();};passwordRemove.Click+=delegate {new CategoryPasswordWindow(main,CategoryPasswordMode.Remove,null).ShowDialog();syncPrivacy();};
            privacyCard.Children.Add(new TextBlock {Text="解锁范围",Foreground=palette.Text,FontSize=12,Margin=new Thickness(0,6,0,8)});
            var unlockScope=new UniformGrid {Columns=2};privacyCard.Children.Add(new Border {CornerRadius=new CornerRadius(10),Background=palette.Input,Padding=new Thickness(3),Child=unlockScope,Margin=new Thickness(0,0,0,9)});
            var currentScope=MainWindow.Button("仅当前分类","仅解锁当前分类");var allScope=MainWindow.Button("全部加锁分类","解锁所有加锁分类");currentScope.Padding=allScope.Padding=new Thickness(5,9,5,9);currentScope.Margin=allScope.Margin=new Thickness(0);unlockScope.Children.Add(currentScope);unlockScope.Children.Add(allScope);
            Action syncUnlockScope=delegate {bool all=main.Service.State.Settings.UnlockAllCategories;currentScope.Background=all ? Brushes.Transparent:palette.Selected;currentScope.Foreground=all ? palette.Text:palette.AccentText;allScope.Background=all ? palette.Selected:Brushes.Transparent;allScope.Foreground=all ? palette.AccentText:palette.Text;};
            currentScope.Click+=delegate {main.TrySetCategoryUnlockScope(false);syncUnlockScope();};allScope.Click+=delegate {main.TrySetCategoryUnlockScope(true);syncUnlockScope();};syncUnlockScope();
            Activated+=delegate {syncPrivacy();syncUnlockScope();};
            privacyCard.Children.Add(new TextBlock {Text="输入正确密码后，按所选范围解锁；重启后重新上锁。",Foreground=palette.Secondary,FontSize=11,TextWrapping=TextWrapping.Wrap,LineHeight=18,Margin=new Thickness(0,0,0,8)});
            privacyCard.Children.Add(new TextBlock {Text="鼠标移到自定义分类右侧，点击锁头上锁或解锁。重启后重新上锁；未完成和已完成不加锁。",Foreground=palette.Secondary,FontSize=11,TextWrapping=TextWrapping.Wrap,LineHeight=18});
            privacyCard.Children.Add(new TextBlock {Text="仅控制软件内查看，本地文件和备份未加密。",Foreground=palette.Secondary,FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0)});
            panel.Children.Add(new TextBlock {Text="启动与备份",FontSize=17,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,20,0,12)});
            var data=Card(panel);bool enabled=false;string startupError=null;try{enabled=main.ReadStartupValue()!=null;}catch(Exception ex){startupError=ex.Message;}
            var startup=new CheckBox {Content="开机启动",IsChecked=enabled,IsEnabled=!main.IsPreviewProcess,Foreground=palette.Text,Template=SwitchTemplate,Margin=new Thickness(0,2,0,14)};if(main.IsPreviewProcess)startup.ToolTip="独立预览不加入开机启动";data.Children.Add(startup);
            var startupStatus=new TextBlock {FontSize=11,Foreground=palette.AccentText,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10),Visibility=Visibility.Collapsed};data.Children.Add(startupStatus);
            startup.Click+=delegate {
                try {bool desired=startup.IsChecked==true;main.WriteStartupValue(desired ? "\""+System.Reflection.Assembly.GetExecutingAssembly().Location+"\"":null);enabled=desired;startupStatus.Visibility=Visibility.Collapsed;}catch(Exception ex){startupStatus.Text="开机启动未能更改："+ex.Message;startupStatus.Visibility=Visibility.Visible;}
                try{enabled=main.ReadStartupValue()!=null;}catch(Exception ex){startupStatus.Text="暂时无法核对开机启动状态："+ex.Message;startupStatus.Visibility=Visibility.Visible;}startup.IsChecked=enabled;
            };
            if(startupError!=null){startup.IsEnabled=false;startup.ToolTip="暂时无法读取开机启动状态："+startupError;}
            var export=MainWindow.Button("导出备份…","导出备份");export.HorizontalContentAlignment=HorizontalAlignment.Left;data.Children.Add(export);
            export.Click+=delegate {
                if(!main.AuthorizePrivateOperation("导出备份"))return;
                var dialog=new SaveFileDialog {Title="导出便签备份",Filter="便签备份 (*.json)|*.json",FileName="TinyStar桌面便签备份-"+DateTime.Now.ToString("yyyyMMdd")+".json"};
                if(dialog.ShowDialog()==true)main.Try(delegate {
                    if(String.Equals(Path.GetFullPath(dialog.FileName),Path.GetFullPath(main.Store.FilePath),StringComparison.OrdinalIgnoreCase))throw new ArgumentException("请选择其他位置，不能覆盖正在使用的数据文件。");
                    main.Store.Export(dialog.FileName,main.Service.State);MessageBox.Show("备份已导出，包含全部分类、事项和回收站。","TinyStar桌面便签");
                });
            };
            var import=MainWindow.Button("从备份恢复…","从备份恢复");import.HorizontalContentAlignment=HorizontalAlignment.Left;data.Children.Add(import);
            import.Click+=delegate {
                if(!main.AuthorizePrivateOperation("恢复备份"))return;
                var dialog=new OpenFileDialog {Title="选择便签备份",Filter="便签备份 (*.json)|*.json"};
                if(dialog.ShowDialog()==true)main.Try(delegate {
                    var state=main.Store.Import(dialog.FileName);if(!MainWindow.Confirm("用这份备份替换当前的事项、分类和回收站？\n替换前会自动备份当前内容。"))return;
                    string previous=main.Store.Snapshot(main.Service.State);main.ReplaceState(state,true);syncPrivacy();syncUnlockScope();MessageBox.Show("已恢复。替换前的内容已备份到：\n"+previous,"TinyStar桌面便签");
                });
            };
            AddMaintenanceControls(panel);
            panel.Children.Add(new TextBlock {Text="内容保存在本机。点窗口关闭按钮后，可以从右下角托盘重新打开。",Foreground=palette.Secondary,FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(2,16,2,14)});
            var done=MainWindow.Button("完成","关闭设置");done.Background=palette.Selected;done.Click+=delegate {Close();};panel.Children.Add(done);
            Closed+=delegate {recordingShortcut=false;main.AppearanceChanged-=changed;main.Save();};
        }
        void BeginShortcutRecording() {recordingShortcut=true;shortcutField.Focusable=true;shortcutField.Text="按下新的组合键…";shortcutFrame.BorderBrush=palette.Accent;shortcutButton.Content="取消";shortcutHint.Text="请按 Ctrl 或 Alt 加字母、数字或功能键；Esc 取消。";shortcutHint.Foreground=palette.AccentText;shortcutField.Focus();shortcutField.SelectAll();}
        void CancelShortcutRecording() {recordingShortcut=false;shortcutField.Focusable=false;shortcutField.Text=main.CurrentShortcutDisplay;shortcutFrame.BorderBrush=palette.Line;shortcutButton.Content="修改";shortcutHint.Text="按此快捷键呼出添加栏，默认在任务栏上方，可拖动空白处。";shortcutHint.Foreground=palette.Secondary;}
        internal void AcceptRecordedShortcut(ShortcutGesture gesture) {
            if(!recordingShortcut || gesture==null)return;recordingShortcut=false;string error;bool success=main.TryChangeQuickShortcut(gesture.Canonical,out error);
            shortcutField.Focusable=false;shortcutField.Text=main.CurrentShortcutDisplay;shortcutFrame.BorderBrush=palette.Line;shortcutButton.Content="修改";shortcutHint.Text=success ? "已设置为 "+main.CurrentShortcutDisplay:error;shortcutHint.Foreground=success ? palette.AccentText:new SolidColorBrush(Color.FromRgb(222,125,113));shortcutButton.Focus();
        }
        StackPanel Card(Panel parent) {var content=new StackPanel();parent.Children.Add(new Border {CornerRadius=new CornerRadius(16),Background=palette.Card,Padding=new Thickness(17),Child=content});return content;}
        TextBlock Label(string text) {return new TextBlock {Text=text,Foreground=palette.Text,FontSize=13};}
        void AddOpacity(Panel parent,string name,double value,Action<double> change) {
            var label=Label(name+"  "+Math.Round(value*100)+"%");parent.Children.Add(label);
            var slider=new Slider {Minimum=0,Maximum=100,Value=value*100,TickFrequency=5,Margin=new Thickness(0,9,0,16),Foreground=palette.Accent,Template=OpacityTemplate};
            System.Windows.Automation.AutomationProperties.SetName(slider,name);
            slider.ValueChanged+=delegate {label.Text=name+"  "+Math.Round(slider.Value)+"%";change(slider.Value/100);};parent.Children.Add(slider);
        }
    }
}
