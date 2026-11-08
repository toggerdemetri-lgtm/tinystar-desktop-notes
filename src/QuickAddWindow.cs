using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Forms=System.Windows.Forms;
namespace DesktopNotes {
    public sealed class QuickAddWindow : Window,IDisposable {
        [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")]static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr hwnd,StringBuilder name,int size);
        [DllImport("user32.dll")]static extern int GetWindowLong(IntPtr hwnd,int index);
        [DllImport("user32.dll")]static extern int SetWindowLong(IntPtr hwnd,int index,int value);
        readonly MainWindow main;readonly ThemePalette palette=new ThemePalette();readonly TextBox input;readonly TextBlock hint,error;readonly Border shell;
        readonly Canvas particles=new Canvas {IsHitTestVisible=false,ClipToBounds=true};
        bool syncing,composing,submitting,disposed,dismissing,opening,dragging;IntPtr previousForeground;
        DispatcherTimer noticeTimer;
        internal TextBox InputBox {get {return input;}}
        internal int ParticleCount {get {return particles.Children.Count;}}
        public QuickAddWindow(MainWindow main) {
            this.main=main;Title="快捷添加事项";Width=712;Height=172;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;Topmost=true;ShowActivated=true;FontFamily=new FontFamily("Microsoft YaHei UI");
            ApplyPalette();var outer=new Grid {Background=Brushes.Transparent};Content=outer;var root=new Grid {Margin=new Thickness(44)};root.RowDefinitions.Add(new RowDefinition {Height=new GridLength(60)});root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});outer.Children.Add(root);
            var edge=new LinearGradientBrush(Color.FromArgb(170,119,188,255),Color.FromArgb(65,57,119,214),new Point(0,0),new Point(1,1));
            shell=new Border {CornerRadius=new CornerRadius(30),Background=palette.Surface,BorderBrush=edge,BorderThickness=new Thickness(1),Padding=new Thickness(20,2,20,2),Effect=new DropShadowEffect {Color=Color.FromRgb(71,147,255),BlurRadius=64,ShadowDepth=0,Opacity=.64}};root.Children.Add(shell);
            var body=new Grid();shell.Child=body;
            body.Children.Add(new Border {Width=24,Height=2,CornerRadius=new CornerRadius(1),Background=palette.Accent,Opacity=.3,VerticalAlignment=VerticalAlignment.Top,HorizontalAlignment=HorizontalAlignment.Center});
            var row=new Grid {Height=32,VerticalAlignment=VerticalAlignment.Center};row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(30)});row.ColumnDefinitions.Add(new ColumnDefinition());body.Children.Add(row);
            var symbol=new Image {Source=main.Icon,Width=26,Height=26,VerticalAlignment=VerticalAlignment.Center,Effect=new DropShadowEffect {Color=Color.FromRgb(146,207,255),BlurRadius=7,ShadowDepth=0,Opacity=.7}};row.Children.Add(symbol);
            var field=new Grid {Margin=new Thickness(12,0,0,0)};Grid.SetColumn(field,1);row.Children.Add(field);
            input=new TextBox {Foreground=palette.Text,CaretBrush=palette.Accent,Background=Brushes.Transparent,BorderThickness=new Thickness(0),FontSize=15,Padding=new Thickness(0,3,0,3),VerticalContentAlignment=VerticalAlignment.Center,MaxLength=20000,AcceptsReturn=false};System.Windows.Automation.AutomationProperties.SetName(input,"快捷添加内容");field.Children.Add(input);
            hint=new TextBlock {Text="添加事项，按回车保存，按Esc保存草稿并取消",Foreground=palette.Secondary,FontSize=14,TextTrimming=TextTrimming.CharacterEllipsis,IsHitTestVisible=false,VerticalAlignment=VerticalAlignment.Center};field.Children.Add(hint);
            error=new TextBlock {Foreground=new SolidColorBrush(Color.FromRgb(239,135,125)),FontSize=11,Margin=new Thickness(20,5,20,0),TextWrapping=TextWrapping.Wrap,Visibility=Visibility.Collapsed};Grid.SetRow(error,1);root.Children.Add(error);
            outer.Children.Add(particles);
            PreviewMouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) {
                if(e.ClickCount!=1 || !CanDrag(e.OriginalSource as DependencyObject))return;
                dragging=true;try {DragMove();RememberPosition();}finally {dragging=false;}e.Handled=true;
            };
            input.TextChanged+=delegate {hint.Visibility=String.IsNullOrEmpty(input.Text) ? Visibility.Visible:Visibility.Collapsed;if(!syncing) {if(noticeTimer!=null && noticeTimer.IsEnabled){StopNotice();error.Visibility=Visibility.Collapsed;Height=172;}main.SetQuickDraft(input.Text);}};
            TextCompositionManager.AddPreviewTextInputStartHandler(input,delegate {composing=true;});TextCompositionManager.AddPreviewTextInputHandler(input,delegate {composing=false;});input.LostKeyboardFocus+=delegate {composing=false;};
            input.PreviewKeyDown+=delegate(object sender,KeyEventArgs e) {
                if(e.Key==Key.Escape && !composing && e.ImeProcessedKey!=Key.Escape) {Dismiss(true);e.Handled=true;}
                else if(e.Key==Key.Enter && !composing && e.ImeProcessedKey!=Key.Enter) {if(!e.IsRepeat)Submit();e.Handled=true;}
            };
            Deactivated+=delegate {if(!opening && !disposed && !dragging)Dismiss(false);};Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e) {if(!disposed) {e.Cancel=true;Dismiss(true);}};
            SourceInitialized+=delegate {var handle=new WindowInteropHelper(this).Handle;SetWindowLong(handle,-20,GetWindowLong(handle,-20)|0x80);};
            main.AppearanceChanged+=AppearanceChanged;
        }
        void ApplyPalette() {palette.Apply(main.Service.State.Settings,true);palette.Resources(this);Opacity=main.Service.State.Settings.QuickAddOpacity;}
        void AppearanceChanged(object sender,EventArgs e) {ApplyPalette();}
        internal static WindowBounds Placement(System.Drawing.Rectangle area,double scale) {return Placement(area,scale,-1,-1);}
        internal static WindowBounds Placement(System.Drawing.Rectangle area,double scale,double x,double y) {
            double availableWidth=area.Width/scale,availableHeight=area.Height/scale,height=Math.Min(172,availableHeight),width=Math.Min(712,Math.Max(1,availableWidth-16));
            double horizontal=Math.Max(0,availableWidth-width),vertical=Math.Max(0,availableHeight-height);
            bool saved=x>=0 && x<=1 && y>=0 && y<=1;
            return new WindowBounds(area.Left/scale+(saved ? horizontal*x:horizontal/2),area.Top/scale+(saved ? vertical*y:vertical),width,height);
        }
        internal bool CanDrag(DependencyObject source) {return MainWindow.IsDragSurface(source,Content as DependencyObject);}
        internal void RememberPosition() {
            var area=Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;double scale=DesktopHost.SystemScale;
            double horizontal=Math.Max(0,area.Width/scale-Width),vertical=Math.Max(0,area.Height/scale-Height);
            Left=Math.Max(area.Left/scale,Math.Min(Left,area.Left/scale+horizontal));Top=Math.Max(area.Top/scale,Math.Min(Top,area.Top/scale+vertical));
            var preferences=main.Service.State.Settings;preferences.QuickAddPositionX=horizontal>0 ? (Left-area.Left/scale)/horizontal:.5;preferences.QuickAddPositionY=vertical>0 ? (Top-area.Top/scale)/vertical:.5;main.Save();
        }
        void ClearStars() {foreach(UIElement particle in particles.Children)particle.BeginAnimation(UIElement.OpacityProperty,null);particles.Children.Clear();}
        internal void PlayStars() {
            ClearStars();if(!main.Service.State.Settings.AnimationsEnabled)return;
            double width=Width;for(int i=0;i<26;i++) {
                double angle=i*2*Math.PI/26;double x=width/2+Math.Cos(angle)*(width/2-54),y=74+Math.Sin(angle)*39;
                double size=i%6==0 ? 7:2+(i%3);Shape star;
                if(i%6==0)star=new Path {Data=Geometry.Parse("M3.5,0 Q4,3 7,3.5 Q4,4 3.5,7 Q3,4 0,3.5 Q3,3 3.5,0 Z")};else star=new Ellipse();
                star.Width=size;star.Height=size;star.Fill=new SolidColorBrush(i%3==0 ? Color.FromRgb(210,235,255):Color.FromRgb(112,182,255));star.Opacity=0;
                Canvas.SetLeft(star,x);Canvas.SetTop(star,y);particles.Children.Add(star);
                var shift=new TranslateTransform();star.RenderTransform=shift;var ease=new CubicEase {EasingMode=EasingMode.EaseOut};var duration=TimeSpan.FromMilliseconds(850);
                shift.BeginAnimation(TranslateTransform.XProperty,new DoubleAnimation(0,Math.Cos(angle)*(12+i%11),duration) {EasingFunction=ease});
                shift.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(0,Math.Sin(angle)*13-9,duration) {EasingFunction=ease});
                var fade=new DoubleAnimationUsingKeyFrames();fade.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(TimeSpan.Zero)));fade.KeyFrames.Add(new LinearDoubleKeyFrame(.9,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100+i%4*25))));fade.KeyFrames.Add(new LinearDoubleKeyFrame(0,KeyTime.FromTimeSpan(duration)));
                var current=star;fade.Completed+=delegate {particles.Children.Remove(current);};star.BeginAnimation(UIElement.OpacityProperty,fade);
            }
        }
        public void ShowForActiveScreen() {
            if(disposed)return;StopNotice();opening=true;
            try {
                previousForeground=GetForegroundWindow();var className=new StringBuilder(128);GetClassName(previousForeground,className,className.Capacity);
                var name=className.ToString();var screen=previousForeground==IntPtr.Zero || name=="Progman" || name=="WorkerW" || name=="Shell_TrayWnd" ? Forms.Screen.FromPoint(Forms.Cursor.Position):Forms.Screen.FromHandle(previousForeground);
                var preferences=main.Service.State.Settings;var placement=Placement(screen.WorkingArea,DesktopHost.SystemScale,preferences.QuickAddPositionX,preferences.QuickAddPositionY);Width=placement.Width;Height=placement.Height;Left=placement.X;Top=placement.Y;
                error.Visibility=Visibility.Collapsed;syncing=true;input.Text=main.Service.State.Settings.QuickAddDraft ?? "";syncing=false;ApplyPalette();
                Show();SetForegroundWindow(new WindowInteropHelper(this).Handle);Activate();input.Focus();input.CaretIndex=input.Text.Length;UiMotion.Appear(shell);PlayStars();
                Dispatcher.BeginInvoke(new Action(delegate {if(IsVisible) {input.Focus();input.CaretIndex=input.Text.Length;}}),DispatcherPriority.Input);
            }finally {opening=false;}
        }
        public void Dismiss(bool restoreFocus) {
            StopNotice();if(disposed || dismissing || !IsVisible)return;dismissing=true;
            try {bool ownedFocus=GetForegroundWindow()==new WindowInteropHelper(this).Handle;main.SetQuickDraft(input.Text);main.Save();ClearStars();Hide();if(restoreFocus && ownedFocus && IsWindow(previousForeground))SetForegroundWindow(previousForeground);}finally {dismissing=false;}
        }
        internal bool Submit() {
            if(submitting || String.IsNullOrWhiteSpace(input.Text))return false;submitting=true;
            try {
                StopNotice();string message;if(!main.TryAddQuickNote(input.Text,out message)) {ShowMessage(message,true);input.Focus();return false;}
                syncing=true;input.Clear();syncing=false;
                if(!String.IsNullOrEmpty(main.QuickAddNotice)) {ShowMessage(main.QuickAddNotice,false);noticeTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(1100)};noticeTimer.Tick+=delegate {Dismiss(true);};noticeTimer.Start();}
                else Dismiss(true);return true;
            }finally {submitting=false;}
        }
        void ShowMessage(string message,bool failed) {
            error.Text=message;error.Foreground=failed ? new SolidColorBrush(Color.FromRgb(239,135,125)):palette.AccentText;error.Visibility=Visibility.Visible;Height=196;
            var area=Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle).WorkingArea;Top=Math.Max(area.Top/DesktopHost.SystemScale,Math.Min(Top,area.Bottom/DesktopHost.SystemScale-Height));
        }
        void StopNotice() {if(noticeTimer!=null){noticeTimer.Stop();noticeTimer=null;}}
        public void Dispose() {if(disposed)return;StopNotice();disposed=true;ClearStars();main.AppearanceChanged-=AppearanceChanged;Close();}
    }
}
