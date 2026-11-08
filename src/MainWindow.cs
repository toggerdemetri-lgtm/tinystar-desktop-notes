using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Automation;
using Forms=System.Windows.Forms;
namespace DesktopNotes {
    public partial class MainWindow : Window {
        public NoteService Service {get;private set;}
        public Storage Store {get;private set;}
        public DesktopHost Host {get;private set;}
        public ThemePalette Palette {get;private set;}
        readonly ThemePalette popupPalette=new ThemePalette();
        public event EventHandler AppearanceChanged;
        readonly bool testMode;
        readonly bool previewProcess;
        internal bool IsPreviewProcess {get {return previewProcess;}}
        StackPanel tabs,rows;TextBox input;TextBlock heading,subtitle,status,completedDateHint;Button lockButton,clearTrash,completedDateButton;
        DateTime? completedFrom,completedTo;
        TextBox completedSearchInput;Border addFrame,searchFrame;
        string completedSearch="";DispatcherTimer searchTimer;
        readonly HashSet<string> expandedSteps=new HashSet<string>();
        readonly HashSet<string> pendingStepRedraw=new HashSet<string>();DispatcherTimer stepRedrawTimer;
        ScrollViewer noteScroll,categoryScroll;
        Grid root;Border noteShell,noteSidebar;DropShadowEffect shadow;TrayIcon tray;System.Drawing.Icon trayIcon;
        Grid columns;ColumnDefinition sidebarColumn;bool resizingSidebar,adjustingSidebar;
        SettingsWindow settingsWindow;
        GlobalHotkey quickHotkey;QuickAddWindow quickWindow;
        Canvas effectsLayer;Border trashDrop,dragGhost,dragRow,insertionMark;NoteDragSession noteDrag;string dragNoteId,reorderTarget;bool overTrash,reorderBefore,startingNoteDrag;
        DispatcherTimer snapTimer;
        DispatcherTimer saveTimer;
        readonly List<Thumb> grips=new List<Thumb>();
        string active="inbox";TextBox editor;Note editing;string editingStepId;bool addingStep;
        bool committing,rendering,exiting,ready,composing;
        bool moving,moveStarted;double[] moveCursor,moveBounds;

        public MainWindow(Storage store,NoteState state,bool testMode) {
            this.testMode=testMode;var entry=System.Reflection.Assembly.GetEntryAssembly();previewProcess=testMode || entry==null || !String.Equals(entry.GetName().Name,"ZhuomianNotes",StringComparison.OrdinalIgnoreCase);Store=store;Service=new NoteService(state);Privacy=new CategoryPrivacy(state);Host=new DesktopHost();Palette=new ThemePalette();
            Title="TinyStar桌面便签";FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=14;
            WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=testMode;
            MinWidth=380;MinHeight=360;Width=Math.Max(MinWidth,state.Settings.Width);Height=Math.Max(MinHeight,state.Settings.Height);Opacity=1;
            if(Service.HasCategory(state.Settings.DraftCategory))active=state.Settings.DraftCategory;
            var area=SystemParameters.WorkArea;Left=state.Settings.Left < -90000 ? area.Right-Width-32 : state.Settings.Left;Top=state.Settings.Top;if(state.Settings.Left < -90000) {state.Settings.Left=Left;state.Settings.Top=Top;state.Settings.Width=Width;state.Settings.Height=Height;}
            Palette.Apply(state.Settings,false);popupPalette.Apply(state.Settings,true);Palette.Resources(this);
            KeepOnScreen();Build();SetupTray();ApplyAppearance();
            SourceInitialized+=delegate {InitializeLifetime();Host.Attach(this,testMode);};
            Closed+=WindowClosed;
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplayChanged;
            Microsoft.Win32.SystemEvents.UserPreferenceChanged+=SystemThemeChanged;
            Loaded+=delegate {
                if(retired || exiting)return;
                ready=true;KeepOnScreen();StartQuickHotkey();Refresh();
                if(!String.IsNullOrEmpty(Service.State.Settings.EditId)) {
                    var note=Service.State.Notes.FirstOrDefault(n=>n.Id==Service.State.Settings.EditId && !n.Deleted);
                    if(note!=null && Privacy.CanSee(note)) {active=note.Done ? "done":note.CategoryId;Refresh();var p=Service.State.Settings;if(p.EditNewStep || !String.IsNullOrEmpty(p.EditStepId))BeginStepEdit(note,p.EditStepId,p.EditDraft,p.EditNewStep);else BeginEdit(note,p.EditDraft);}
                }
                if(!Host.Attached && !testMode)Tell("未能连接桌面，请从托盘重试显示。");
            };
            PreviewMouseDown+=delegate(object sender,MouseButtonEventArgs e) {if(categoryNameEditor!=null && !IsInside(e.OriginalSource as DependencyObject,categoryNameEditor) && !CommitCategoryRename(false)) {e.Handled=true;return;}if(editor!=null && !IsInside(e.OriginalSource as DependencyObject,editor))CommitEdit(false);};
            Deactivated+=delegate {if(nativeDestroying || retired)return;CancelCategoryDrag();CancelStepDrag();CancelNoteDrag();CommitCategoryRename(false);CommitEdit(false);Save();};
            Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e) {if(!exiting) {e.Cancel=true;if(previewProcess)Dispatcher.BeginInvoke(new Action(Exit));else {CommitEdit(false);Save();Hide();}}};
            SizeChanged+=delegate {ApplySidebarWidth();if(ready) {Host.UpdateIconLayer();ScheduleSave();}};
            saveTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(600)};
            saveTimer.Tick+=delegate {saveTimer.Stop();Save();};
        }
        public static Button Button(string text,string tip) {return UiDesign.Button(text,tip);}
        Button Tool(string icon,string tip) {var b=Button("",tip);b.Width=32;b.Height=32;b.Padding=new Thickness(8);b.Content=UiDesign.Icon(icon,Palette.Icon,16);return b;}
        static DependencyObject Ancestor(DependencyObject d) {return d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d):LogicalTreeHelper.GetParent(d);}
        static bool IsInside(DependencyObject node,DependencyObject target) {while(node!=null) {if(node==target)return true;node=Ancestor(node);}return false;}
        public static bool IsDragSurface(DependencyObject node,DependencyObject boundary) {
            while(node!=null && node!=boundary) {
                if(node is ButtonBase || node is TextBoxBase || node is ScrollBar || node is Thumb || node is MenuItem || node is TextBlock || node is DesktopIconVisual || (node is Border && (((Border)node).Tag as string)=="category-rename"))return false;
                node=Ancestor(node);
            }
            return node==boundary;
        }
        void Build() {
            NameScope.SetNameScope(this,new NameScope());
            root=new Grid {Background=new SolidColorBrush(Color.FromArgb(1,0,0,0))};Content=root;
            shadow=new DropShadowEffect {Color=Colors.Black,BlurRadius=16,ShadowDepth=2,Opacity=.12};
            var shell=new Border {CornerRadius=new CornerRadius(UiDesign.NoteCornerRadius),Background=Palette.Surface,BorderBrush=Palette.Line,BorderThickness=new Thickness(1),Margin=new Thickness(4),Effect=shadow};noteShell=shell;root.Children.Add(shell);
            var layout=new Grid();columns=layout;sidebarColumn=new ColumnDefinition {Width=new GridLength(Service.State.Settings.SidebarWidth),MinWidth=100};layout.ColumnDefinitions.Add(sidebarColumn);layout.ColumnDefinitions.Add(new ColumnDefinition {MinWidth=220});shell.Child=layout;
            layout.SizeChanged+=delegate {ApplySidebarWidth();};
            var side=new Border {Background=Palette.Sidebar,CornerRadius=new CornerRadius(UiDesign.NoteCornerRadius-1,0,0,UiDesign.NoteCornerRadius-1),Padding=new Thickness(8,16,8,12)};noteSidebar=side;layout.Children.Add(side);
            var sideGrid=new Grid();sideGrid.RowDefinitions.Add(new RowDefinition());sideGrid.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});side.Child=sideGrid;
            tabs=new StackPanel {Orientation=Orientation.Vertical,Name="CategoryList"};RegisterName(tabs.Name,tabs);
            categoryScroll=new ScrollViewer {Content=tabs,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};sideGrid.Children.Add(categoryScroll);
            var sideBottom=new StackPanel {Margin=new Thickness(0,10,0,0)};
            var addCat=SidebarButton("新增分类","plus",null,0);addCategoryButton=addCat;addCat.Click+=delegate {ToggleCategoryAddPopup();};sideBottom.Children.Add(addCat);
            var trash=SidebarButton("回收站","trash","trash",0);trash.Click+=delegate {CommitEdit(false);active="trash";Refresh();};sideBottom.Children.Add(trash);
            Grid.SetRow(sideBottom,1);sideGrid.Children.Add(sideBottom);sideBottom.Tag="sidebar-bottom";
            var main=new Grid {Margin=new Thickness(18,14,16,16)};Grid.SetColumn(main,1);layout.Children.Add(main);
            main.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});main.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            main.RowDefinitions.Add(new RowDefinition());main.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});main.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            var toolbar=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,0,-4,6)};
            lockButton=Tool("unlock","锁定或解锁位置");lockButton.Click+=delegate {Service.State.Settings.Locked=!Service.State.Settings.Locked;UpdateLock();Save();};toolbar.Children.Add(lockButton);
            var settings=Tool("settings","设置");settings.Click+=delegate {ShowSettings();};toolbar.Children.Add(settings);
            var close=Tool("close",previewProcess ? "关闭预览":"隐藏到托盘");close.Click+=delegate {Close();};toolbar.Children.Add(close);main.Children.Add(toolbar);
            var section=new StackPanel {Margin=new Thickness(2,0,0,16)};
            heading=new TextBlock {FontSize=22,FontWeight=FontWeights.SemiBold,Foreground=Palette.Text,TextTrimming=TextTrimming.CharacterEllipsis};
            subtitle=new TextBlock {FontSize=11,Foreground=Palette.Secondary,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis};
            var summary=new Grid {Margin=new Thickness(0,5,0,0)};summary.ColumnDefinitions.Add(new ColumnDefinition());summary.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});summary.Children.Add(subtitle);
            clearTrash=Button("清空回收站","永久删除回收站全部事项");clearTrash.FontSize=11;clearTrash.Padding=new Thickness(9,5,9,5);clearTrash.Margin=new Thickness(8,0,0,0);clearTrash.Visibility=Visibility.Collapsed;
            clearTrash.Click+=delegate {RequestClearTrash();};Grid.SetColumn(clearTrash,1);summary.Children.Add(clearTrash);
            completedDateButton=Button("完成日期","筛选完成日期");completedDateButton.FontSize=11;completedDateButton.Padding=new Thickness(9,5,9,5);completedDateButton.Margin=new Thickness(8,0,0,0);completedDateButton.Visibility=Visibility.Collapsed;
            completedDateButton.Click+=delegate {CommitEdit(false);var dialog=new CompletedDateWindow(this,completedFrom,completedTo);if(dialog.ShowDialog()==true)SetCompletedDateRange(dialog.From,dialog.To);};Grid.SetColumn(completedDateButton,1);summary.Children.Add(completedDateButton);
            completedDateHint=new TextBlock {FontSize=11,Foreground=Palette.AccentText,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,7,0,0),Visibility=Visibility.Collapsed};
            section.Children.Add(heading);section.Children.Add(summary);section.Children.Add(completedDateHint);Grid.SetRow(section,1);main.Children.Add(section);
            rows=new StackPanel();noteScroll=new ScrollViewer {Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(noteScroll,2);main.Children.Add(noteScroll);
            addFrame=new Border {CornerRadius=new CornerRadius(28),Background=Palette.Input,BorderBrush=Palette.Line,BorderThickness=new Thickness(1),Padding=new Thickness(9,5,9,5),Margin=new Thickness(0,14,0,0),Cursor=Cursors.IBeam};
            addFrame.SizeChanged+=delegate {addFrame.CornerRadius=new CornerRadius(addFrame.ActualHeight/2);};
            var addGrid=new Grid();addGrid.ColumnDefinitions.Add(new ColumnDefinition());addGrid.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});addFrame.Child=addGrid;
            input=new TextBox {Text=Service.State.Settings.Draft ?? "",Foreground=Palette.Text,CaretBrush=Palette.Accent,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(8,9,3,9),FontSize=14,MaxLength=20000,VerticalContentAlignment=VerticalAlignment.Center,ToolTip="写下新事项，按回车添加",MinWidth=20,SnapsToDevicePixels=true};
            AutomationProperties.SetName(input,"新事项");ConfigureIme(input);
            input.PreviewKeyDown+=delegate(object sender,KeyEventArgs e) {if(e.Key==Key.Enter && !composing && e.ImeProcessedKey!=Key.Enter) {AddNote();e.Handled=true;}};
            input.TextChanged+=delegate {if(syncingPrivateDraft)return;Service.State.Settings.Draft=input.Text;Service.State.Settings.DraftCategory=NewCategory();ScheduleSave();};addGrid.Children.Add(input);addGrid.Children.Add(new InputCaret(input));
            var hint=new TextBlock {Text="添加事项",Foreground=Palette.Secondary,Margin=new Thickness(8,9,0,9),IsHitTestVisible=false,FontSize=14,VerticalAlignment=VerticalAlignment.Center};
            Action hintUpdate=delegate {hint.Visibility=String.IsNullOrEmpty(input.Text) && !input.IsKeyboardFocusWithin ? Visibility.Visible:Visibility.Collapsed;addFrame.BorderBrush=input.IsKeyboardFocusWithin ? Palette.Accent:Palette.Line;};input.TextChanged+=delegate {hintUpdate();};input.IsKeyboardFocusWithinChanged+=delegate {hintUpdate();};hintUpdate();addGrid.Children.Add(hint);
            addFrame.PreviewMouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) {var node=e.OriginalSource as DependencyObject;while(node!=null && node!=addFrame) {if(node is ButtonBase || node is TextBoxBase)return;node=Ancestor(node);}input.Focus();Keyboard.Focus(input);e.Handled=true;};
            var add=Tool("plus","添加事项");add.Content=UiDesign.Icon("plus",Palette.Accent,17);add.Click+=delegate {AddNote();};Grid.SetColumn(add,1);addGrid.Children.Add(add);Grid.SetRow(addFrame,3);main.Children.Add(addFrame);
            searchFrame=new Border {CornerRadius=new CornerRadius(14),Background=Palette.Input,BorderBrush=Palette.Line,BorderThickness=new Thickness(1),Padding=new Thickness(5),Margin=new Thickness(0,14,0,0),Visibility=Visibility.Collapsed};
            var searchGrid=new Grid();searchGrid.ColumnDefinitions.Add(new ColumnDefinition());searchGrid.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});searchFrame.Child=searchGrid;
            completedSearchInput=new TextBox {Foreground=Palette.Text,CaretBrush=Palette.Icon,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(8,9,3,9),FontSize=14,MaxLength=20000,VerticalContentAlignment=VerticalAlignment.Center,ToolTip="输入关键词，查找已完成事项",MinWidth=20};
            AutomationProperties.SetName(completedSearchInput,"搜索已完成事项");ConfigureIme(completedSearchInput);searchGrid.Children.Add(completedSearchInput);
            var searchHint=new TextBlock {Text="搜索已完成事项",Foreground=Palette.Secondary,Margin=new Thickness(8,9,0,9),IsHitTestVisible=false,FontSize=14,VerticalAlignment=VerticalAlignment.Center};searchGrid.Children.Add(searchHint);
            searchTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(180)};searchTimer.Tick+=delegate {if(!composing)ApplyCompletedSearch();};
            completedSearchInput.TextChanged+=delegate {searchHint.Visibility=String.IsNullOrEmpty(completedSearchInput.Text) ? Visibility.Visible:Visibility.Collapsed;searchTimer.Stop();searchTimer.Start();};
            completedSearchInput.PreviewKeyDown+=delegate(object sender,KeyEventArgs e) {if(e.Key==Key.Enter && !composing && e.ImeProcessedKey!=Key.Enter) {ApplyCompletedSearch();e.Handled=true;}};
            var searchButton=Tool("search","搜索已完成事项");searchButton.Content=UiDesign.Icon("search",Palette.Accent,17);searchButton.Click+=delegate {if(!composing)ApplyCompletedSearch();};Grid.SetColumn(searchButton,1);searchGrid.Children.Add(searchButton);Grid.SetRow(searchFrame,3);main.Children.Add(searchFrame);
            status=new TextBlock {FontSize=11,Foreground=Palette.Icon,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(2,8,0,0),Visibility=Visibility.Collapsed};AutomationProperties.SetLiveSetting(status,AutomationLiveSetting.Polite);Grid.SetRow(status,4);main.Children.Add(status);
            var divider=new GridSplitter {Name="SidebarSplitter",Width=8,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Stretch,ResizeDirection=GridResizeDirection.Columns,ResizeBehavior=GridResizeBehavior.CurrentAndNext,ShowsPreview=false,DragIncrement=1,KeyboardIncrement=8,Cursor=Cursors.SizeWE,Background=Brushes.Transparent,ToolTip="左右拖动，调整分类栏宽度",Focusable=true};
            AutomationProperties.SetName(divider,"调整分类栏宽度");
            var grip=new FrameworkElementFactory(typeof(Border));grip.SetValue(Border.BackgroundProperty,Brushes.Transparent);
            var line=new FrameworkElementFactory(typeof(Border));line.Name="DividerLine";line.SetValue(Border.WidthProperty,1.0);line.SetValue(Border.CornerRadiusProperty,new CornerRadius(1));line.SetValue(Border.MarginProperty,new Thickness(0,18,0,18));line.SetValue(Border.BackgroundProperty,Palette.Line);grip.AppendChild(line);
            var template=new ControlTemplate(typeof(GridSplitter)) {VisualTree=grip};
            foreach(var property in new[]{UIElement.IsMouseOverProperty,UIElement.IsKeyboardFocusWithinProperty}) {var trigger=new Trigger {Property=property,Value=true};trigger.Setters.Add(new Setter(Border.BackgroundProperty,Palette.Accent,"DividerLine"));trigger.Setters.Add(new Setter(Border.WidthProperty,2.0,"DividerLine"));template.Triggers.Add(trigger);}divider.Template=template;
            divider.DragStarted+=delegate {resizingSidebar=true;CommitEdit(false);};
            divider.DragCompleted+=delegate(object sender,DragCompletedEventArgs e) {resizingSidebar=false;if(e.Canceled)ApplySidebarWidth();else SaveSidebarWidth();};
            divider.KeyUp+=delegate(object sender,KeyEventArgs e) {if(e.Key==Key.Left || e.Key==Key.Right)SaveSidebarWidth();};
            Panel.SetZIndex(divider,2);layout.Children.Add(divider);
            AddGrip(ResizeEdges.Left,HorizontalAlignment.Left,VerticalAlignment.Stretch,6,Double.NaN,Cursors.SizeWE);
            AddGrip(ResizeEdges.Right,HorizontalAlignment.Right,VerticalAlignment.Stretch,6,Double.NaN,Cursors.SizeWE);
            AddGrip(ResizeEdges.Top,HorizontalAlignment.Stretch,VerticalAlignment.Top,Double.NaN,6,Cursors.SizeNS);
            AddGrip(ResizeEdges.Bottom,HorizontalAlignment.Stretch,VerticalAlignment.Bottom,Double.NaN,6,Cursors.SizeNS);
            AddGrip(ResizeEdges.Left|ResizeEdges.Top,HorizontalAlignment.Left,VerticalAlignment.Top,13,13,Cursors.SizeNWSE);
            AddGrip(ResizeEdges.Right|ResizeEdges.Top,HorizontalAlignment.Right,VerticalAlignment.Top,13,13,Cursors.SizeNESW);
            AddGrip(ResizeEdges.Left|ResizeEdges.Bottom,HorizontalAlignment.Left,VerticalAlignment.Bottom,13,13,Cursors.SizeNESW);
            AddGrip(ResizeEdges.Right|ResizeEdges.Bottom,HorizontalAlignment.Right,VerticalAlignment.Bottom,13,13,Cursors.SizeNWSE);
            effectsLayer=new Canvas {IsHitTestVisible=false,ClipToBounds=true};Panel.SetZIndex(effectsLayer,1000);root.Children.Add(effectsLayer);
            var trashContents=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
            trashContents.Children.Add(UiDesign.Icon("trash",Brushes.White,18));trashContents.Children.Add(new TextBlock {Text="拖到这里删除",Foreground=Brushes.White,FontSize=12,Margin=new Thickness(8,0,0,0),VerticalAlignment=VerticalAlignment.Center});
            trashDrop=new Border {Width=150,Height=54,CornerRadius=new CornerRadius(20),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,18),Background=new SolidColorBrush(Color.FromArgb(245,67,69,78)),BorderBrush=new SolidColorBrush(Color.FromRgb(125,129,143)),BorderThickness=new Thickness(1),Child=trashContents,Visibility=Visibility.Collapsed,IsHitTestVisible=false};
            Panel.SetZIndex(trashDrop,999);root.Children.Add(trashDrop);
            root.PreviewMouseDown+=BeginMove;root.MouseMove+=ContinueMove;
            root.AddHandler(Mouse.PreviewMouseMoveEvent,new MouseEventHandler(delegate(object sender,MouseEventArgs e) {if(categoryDrag!=null) {MoveCategoryDrag(e.GetPosition(root),Mouse.LeftButton==MouseButtonState.Pressed);if(categoryGhost!=null)e.Handled=true;}}),true);
            root.PreviewMouseUp+=delegate(object sender,MouseButtonEventArgs e) {if(e.ChangedButton!=MouseButton.Left)return;if(categoryDrag!=null) {if(FinishCategoryDrag())e.Handled=true;return;}if(stepDrag!=null)FinishStepDrag();else if(noteDrag!=null)FinishNoteDrag();else EndMove();};
            root.LostMouseCapture+=delegate {if(categoryDrag!=null && Mouse.Captured!=root)CancelCategoryDrag();if(stepDrag!=null && Mouse.Captured!=root)CancelStepDrag();if(noteDrag!=null)CancelNoteDrag();if(moving) {StopQueuedMove(true);moving=false;shadow.Opacity=.12*Service.State.Settings.BackgroundOpacity;Save();}};
            PreviewKeyDown+=delegate(object sender,KeyEventArgs e) {if(e.Key==Key.Escape && categoryDrag!=null) {CancelCategoryDrag();e.Handled=true;}else if(e.Key==Key.Escape && stepDrag!=null) {CancelStepDrag();e.Handled=true;}else if(e.Key==Key.Escape && noteDrag!=null) {CancelNoteDrag();e.Handled=true;}};
            UpdateLock();
        }
        void ApplySidebarWidth() {
            if(adjustingSidebar || resizingSidebar || columns==null)return;adjustingSidebar=true;
            try {double factor=UiScaleFactor;double available=((ActualWidth>0 ? ActualWidth:Width)-10)/factor;sidebarColumn.MaxWidth=Math.Max(100,available-220/factor);double width=Math.Max(100,Math.Min(sidebarColumn.MaxWidth,Service.State.Settings.SidebarWidth));if(!sidebarColumn.Width.IsAbsolute || Math.Abs(sidebarColumn.Width.Value-width)>.1)sidebarColumn.Width=new GridLength(width);}finally {adjustingSidebar=false;}
        }
        void SaveSidebarWidth() {columns.UpdateLayout();Service.State.Settings.SidebarWidth=Math.Max(100,Math.Min(sidebarColumn.MaxWidth,sidebarColumn.ActualWidth));Save();}
        void BeginMove(object sender,MouseButtonEventArgs e) {
            if(e.ChangedButton!=MouseButton.Left)return;
            if(snapTimer!=null)snapTimer.Stop();
            var source=e.OriginalSource as DependencyObject;var row=FindNoteRow(source);
            if(IsInside(source,addFrame))return;
            var stepRow=FindStepRow(source);
            if(stepRow!=null && !IsStepControl(source,stepRow)) {if(e.ClickCount==1)BeginStepDrag(stepRow,e.GetPosition(root));else CancelStepDrag();return;}
            if(row!=null && !IsNoteControl(source,row)) {
                CancelNoteDrag();var id=row.Tag as string;var note=Service.Find(id);
                if(e.ClickCount==1 && !note.Deleted) {var point=e.GetPosition(root);noteDrag=new NoteDragSession(id,point.X,point.Y);dragNoteId=id;dragRow=row;}
                return;
            }
            if(Service.State.Settings.Locked || !IsDragSurface(source,root))return;
            moveCursor=Host.Cursor();moveBounds=Host.Bounds();moving=true;moveStarted=false;root.CaptureMouse();e.Handled=true;
            shadow.BeginAnimation(DropShadowEffect.OpacityProperty,null);shadow.Opacity=.24*Service.State.Settings.BackgroundOpacity;
        }
        void ContinueMove(object sender,MouseEventArgs e) {
            if(stepDrag!=null) {MoveStepDrag(e.GetPosition(root),Mouse.LeftButton==MouseButtonState.Pressed);e.Handled=true;return;}
            if(noteDrag!=null) {ContinueNoteDrag(e);return;}
            if(!moving)return;if(Mouse.LeftButton!=MouseButtonState.Pressed || Service.State.Settings.Locked) {EndMove();return;}
            var cursor=Host.Cursor();double dx=cursor[0]-moveCursor[0],dy=cursor[1]-moveCursor[1];
            if(!moveStarted && Math.Abs(dx)<SystemParameters.MinimumHorizontalDragDistance && Math.Abs(dy)<SystemParameters.MinimumVerticalDragDistance)return;
            moveStarted=true;
            double x=moveBounds[0]+dx,y=moveBounds[1]+dy,scale=Host.Scale;
            var area=Forms.Screen.FromPoint(new System.Drawing.Point((int)((x+ActualWidth/2)*scale),(int)((y+ActualHeight/2)*scale))).WorkingArea;
            var target=WindowGeometry.SnapToScreen(new WindowBounds(x,y,moveBounds[2],moveBounds[3]),new WindowBounds(area.Left,area.Top,area.Width,area.Height),scale,36);
            if(target.X!=x || target.Y!=y) {StopQueuedMove(false);AnimateWindowMove(target.X,target.Y,60,false);}
            else if(snapTimer!=null && snapTimer.IsEnabled) {StopQueuedMove(false);AnimateWindowMove(x,y,50,false);}
            else QueueWindowMove(x,y);
            e.Handled=true;
        }
        void EndMove() {
            if(!moving)return;StopQueuedMove(true);moving=false;root.ReleaseMouseCapture();
            shadow.Opacity=.12*Service.State.Settings.BackgroundOpacity;
            if(moveStarted) {AcceptCurrentPlacement();SnapToEdge();}else Save();
        }
        DispatcherTimer dragFrameTimer;double queuedMoveX,queuedMoveY;bool queuedMove;
        void QueueWindowMove(double x,double y) {
            queuedMoveX=x;queuedMoveY=y;queuedMove=true;
            if(dragFrameTimer==null) {dragFrameTimer=new DispatcherTimer(DispatcherPriority.Render) {Interval=TimeSpan.FromMilliseconds(16)};dragFrameTimer.Tick+=delegate {if(!moving || exiting) {StopQueuedMove(false);return;}if(queuedMove) {queuedMove=false;Host.Move(queuedMoveX,queuedMoveY);}};}
            if(!dragFrameTimer.IsEnabled)dragFrameTimer.Start();
        }
        void StopQueuedMove(bool flush) {
            if(dragFrameTimer!=null)dragFrameTimer.Stop();bool pending=queuedMove;queuedMove=false;if(flush && pending)Host.Move(queuedMoveX,queuedMoveY);
        }
        void SnapToEdge() {
            var b=Host.Bounds();double scale=Host.Scale;
            // A quick release can happen before the visual window catches its target.
            if(snapTimer!=null && snapTimer.IsEnabled) {b[0]=snapX;b[1]=snapY;}
            var area=Forms.Screen.FromPoint(new System.Drawing.Point((int)((b[0]+b[2]/2)*scale),(int)((b[1]+b[3]/2)*scale))).WorkingArea;
            var target=WindowGeometry.SnapToScreen(new WindowBounds(b[0],b[1],b[2],b[3]),new WindowBounds(area.Left,area.Top,area.Width,area.Height),scale,36);
            AnimateWindowMove(target.X,target.Y,70,true);
        }
        double snapX,snapY,snapCurrentX,snapCurrentY,snapVelocityX,snapVelocityY,snapSmoothTime;bool snapSave;DateTime snapTick;
        void AnimateWindowMove(double x,double y,double duration,bool save) {
            snapX=x;snapY=y;snapSmoothTime=Math.Max(.02,duration/1000.0);
            if(!UiMotion.Enabled) {if(snapTimer!=null)snapTimer.Stop();Host.Move(x,y);if(save) {AcceptCurrentPlacement();Save();}return;}
            if(snapTimer!=null && snapTimer.IsEnabled) {snapSave|=save;return;}
            var b=Host.Bounds();snapCurrentX=b[0];snapCurrentY=b[1];snapVelocityX=snapVelocityY=0;snapSave=save;snapTick=DateTime.UtcNow;
            if(snapTimer==null) {
                snapTimer=new DispatcherTimer(DispatcherPriority.Render) {Interval=TimeSpan.FromMilliseconds(16)};
                snapTimer.Tick+=delegate {
                    if(!snapTimer.IsEnabled)return;var now=DateTime.UtcNow;double dt=Math.Min(.05,Math.Max(.001,(now-snapTick).TotalSeconds));snapTick=now;
                    snapCurrentX=WindowGeometry.SmoothPosition(snapCurrentX,snapX,ref snapVelocityX,snapSmoothTime,dt);
                    snapCurrentY=WindowGeometry.SmoothPosition(snapCurrentY,snapY,ref snapVelocityY,snapSmoothTime,dt);
                    Host.Move(snapCurrentX,snapCurrentY);
                    if(Math.Abs(snapX-snapCurrentX)<.2 && Math.Abs(snapY-snapCurrentY)<.2 && Math.Abs(snapVelocityX)<2 && Math.Abs(snapVelocityY)<2) {
                        snapTimer.Stop();Host.Move(snapX,snapY);if(snapSave) {AcceptCurrentPlacement();Save();}snapSave=false;
                    }
                };
            }
            snapTimer.Start();
        }
        Border FindNoteRow(DependencyObject source) {
            while(source!=null && source!=root) {var border=source as Border;if(border!=null && border.Tag is string && Service.State.Notes.Any(n=>n.Id==(string)border.Tag))return border;source=Ancestor(source);}return null;
        }
        static bool IsNoteControl(DependencyObject source,DependencyObject row) {
            while(source!=null && source!=row) {if(source is ButtonBase || source is TextBoxBase || source is ScrollBar || source is Thumb || source is MenuItem || (source is Grid && (((Grid)source).Tag as string)=="step-row"))return true;source=Ancestor(source);}return false;
        }
        void ContinueNoteDrag(MouseEventArgs e) {
            MoveNoteDrag(e.GetPosition(root),Mouse.LeftButton==MouseButtonState.Pressed);if(noteDrag!=null)e.Handled=true;
        }
        void MoveNoteDrag(Point point,bool pressed) {
            // Capturing the mouse can synchronously dispatch another MouseMove.
            // Do not create a second preview while the first capture is in progress.
            if(startingNoteDrag)return;
            if(!pressed) {CancelNoteDrag();return;}
            if(noteDrag==null || !noteDrag.Move(point.X,point.Y,Math.Max(SystemParameters.MinimumHorizontalDragDistance,SystemParameters.MinimumVerticalDragDistance)))return;
            if(dragGhost==null) {
                startingNoteDrag=true;
                try {
                    CommitEdit(false);
                    if(!root.CaptureMouse() || noteDrag==null) {CancelNoteDrag();return;}
                    var image=UiMotion.Snapshot(dragRow,UiScaleFactor);
                    dragGhost=new Border {Width=image.PixelWidth,Height=image.PixelHeight,Background=new ImageBrush(image),Opacity=.9,IsHitTestVisible=false};
                    effectsLayer.Children.Add(dragGhost);dragRow.Opacity=.3;PositionNoteTrash();
                } finally {startingNoteDrag=false;}
            }
            PositionNoteTrash();Canvas.SetLeft(dragGhost,point.X-dragGhost.Width/2);Canvas.SetTop(dragGhost,point.Y-18);
            var rectangle=BoundsInRoot(trashDrop);overTrash=rectangle.Contains(point);
            trashDrop.Background=new SolidColorBrush(overTrash ? Color.FromRgb(202,57,80):Color.FromRgb(67,69,78));
            UpdateReorderTarget(point);
        }
        void PositionNoteTrash() {
            var anchor=BoundsInRoot(addFrame);var origin=anchor.TopLeft;
            trashDrop.HorizontalAlignment=HorizontalAlignment.Left;trashDrop.VerticalAlignment=VerticalAlignment.Top;
            trashDrop.Margin=new Thickness(origin.X+(anchor.Width-trashDrop.Width)/2,Math.Max(4,origin.Y-trashDrop.Height-10),0,0);
            trashDrop.Visibility=Visibility.Visible;trashDrop.UpdateLayout();
        }
        void UpdateReorderTarget(Point point) {
            reorderTarget=null;if(insertionMark!=null)effectsLayer.Children.Remove(insertionMark);insertionMark=null;
            UpdateNoteCategoryTarget(point);if(overTrash || noteDropCategory!=null)return;
            var viewport=BoundsInRoot(noteScroll);if(!viewport.Contains(point))return;
            foreach(var row in rows.Children.OfType<Border>()) {
                if((string)row.Tag==dragNoteId)continue;var rect=BoundsInRoot(row);
                if(point.X<rect.Left || point.X>rect.Right || point.Y<rect.Top || point.Y>rect.Bottom)continue;
                reorderTarget=(string)row.Tag;reorderBefore=point.Y<rect.Top+rect.Height/2;
                insertionMark=new Border {Width=rect.Width,Height=3,Background=Palette.Accent,CornerRadius=new CornerRadius(2),IsHitTestVisible=false};
                Canvas.SetLeft(insertionMark,rect.Left);Canvas.SetTop(insertionMark,Math.Max(viewport.Top,Math.Min(viewport.Bottom-3,reorderBefore ? rect.Top-3:rect.Bottom+2)));effectsLayer.Children.Add(insertionMark);break;
            }
        }
        void FinishNoteDrag() {
            if(noteDrag==null)return;
            try {
                string id=noteDrag.Drop(overTrash);
                if(id!=null && dragGhost!=null) {
                    var note=Service.Find(id);note.Deleted=true;
                    if(Save()) {UiMotion.Shatter(dragGhost,effectsLayer,new Point(Canvas.GetLeft(dragGhost),Canvas.GetTop(dragGhost)));Refresh();}else note.Deleted=false;
                } else if(dragGhost!=null && noteDropCategory!=null) {FinishNoteCategoryDrop();}
                else if(dragGhost!=null && reorderTarget!=null) {Service.Reorder(dragNoteId,reorderTarget,reorderBefore);Save();Refresh();}
            } finally {CancelNoteDrag();}
        }
        void CancelNoteDrag() {
            ClearNoteCategoryTarget();
            var session=noteDrag;noteDrag=null;if(session!=null)session.Cancel();
            if(dragRow!=null)dragRow.Opacity=1;if(dragGhost!=null)effectsLayer.Children.Remove(dragGhost);
            if(insertionMark!=null)effectsLayer.Children.Remove(insertionMark);insertionMark=null;reorderTarget=null;
            dragRow=null;dragGhost=null;dragNoteId=null;overTrash=false;
            if(trashDrop!=null)trashDrop.Visibility=Visibility.Collapsed;
            if(root!=null && Mouse.Captured==root && !moving)root.ReleaseMouseCapture();
        }
        void AddGrip(ResizeEdges edges,HorizontalAlignment h,VerticalAlignment v,double width,double height,Cursor cursor) {
            var factory=new FrameworkElementFactory(typeof(Border));factory.SetValue(Border.BackgroundProperty,new SolidColorBrush(Color.FromArgb(1,0,0,0)));
            var grip=new Thumb {Tag=edges,HorizontalAlignment=h,VerticalAlignment=v,Width=width,Height=height,Cursor=cursor,Focusable=false,Template=new ControlTemplate(typeof(Thumb)) {VisualTree=factory}};
            AutomationProperties.SetName(grip,"调整大小 "+edges);
            WindowBounds start=new WindowBounds();double[] initial=null;
            grip.DragStarted+=delegate {var b=Host.Bounds();start=new WindowBounds(b[0],b[1],b[2],b[3]);initial=Host.Cursor();};
            grip.DragDelta+=delegate {
                if(Service.State.Settings.Locked || initial==null)return;var p=Host.Cursor();
                var b=WindowGeometry.Resize(start,p[0]-initial[0],p[1]-initial[1],edges,MinWidth,MinHeight);
                Width=b.Width;Height=b.Height;Host.Move(b.X,b.Y);
            };
            grip.DragCompleted+=delegate {initial=null;AcceptCurrentPlacement();Save();};grips.Add(grip);root.Children.Add(grip);
        }
        void ConfigureIme(TextBox box) {
            TextCompositionManager.AddPreviewTextInputStartHandler(box,delegate {composing=true;});
            TextCompositionManager.AddPreviewTextInputHandler(box,delegate {composing=false;});
            box.LostKeyboardFocus+=delegate {composing=false;};
        }
        string NewCategory() {return Service.HasCategory(active) ? active:"inbox";}
        void AddNote() {
            if(Privacy.IsLocked(NewCategory())) {Tell("请先解锁此分类。");return;}
            if(active=="done" || String.IsNullOrWhiteSpace(input.Text))return;
            Try(delegate {CommitEdit(false);var note=Service.Add(input.Text,NewCategory());input.Clear();if(active=="done" || active=="trash")active="inbox";Save();Refresh();var row=rows.Children.OfType<Border>().FirstOrDefault(b=>(string)b.Tag==note.Id);if(row!=null) {rows.UpdateLayout();row.BringIntoView();UiMotion.Appear(row);}input.Focus();});
        }
        void ApplyCompletedSearch() {
            searchTimer.Stop();completedSearch=completedSearchInput.Text.Trim();if(active=="done")Refresh();
        }
        internal bool SetCompletedDateRange(DateTime? from,DateTime? to) {
            if(from.HasValue && to.HasValue && from.Value.Date>to.Value.Date)return false;
            completedFrom=from.HasValue ? from.Value.Date:(DateTime?)null;completedTo=to.HasValue ? to.Value.Date:(DateTime?)null;Refresh();return true;
        }
        bool InCompletedRange(Note note) {
            if(!completedFrom.HasValue && !completedTo.HasValue)return true;
            DateTimeOffset stamp;if(!DateTimeOffset.TryParse(note.Completed,out stamp))return false;
            var date=stamp.ToLocalTime().Date;return (!completedFrom.HasValue || date>=completedFrom.Value) && (!completedTo.HasValue || date<=completedTo.Value);
        }
        bool MatchesCompletedSearch(Note note) {return String.IsNullOrEmpty(completedSearch) || note.Text.IndexOf(completedSearch,StringComparison.OrdinalIgnoreCase)>=0 || note.Steps.Any(s=>s.Text.IndexOf(completedSearch,StringComparison.OrdinalIgnoreCase)>=0);}
        public void Refresh() {
            if(!CommitCategoryRename(false))return;
            rendering=true;
            try {
                tabs.Children.Clear();rows.Children.Clear();AddTab("inbox","未完成");AddTab("done","已完成");foreach(var c in Service.State.Categories)AddTab(c.Id,c.Name);
                // The two persistent bottom buttons live outside the independently scrolling list.
                var bottom=FindDescendants(root).OfType<StackPanel>().First(p=>(p.Tag as string)=="sidebar-bottom");
                var trash=(Button)bottom.Children[1];trash.Background=active=="trash" ? Palette.Selected:Brushes.Transparent;
                SyncPrivateDraftView();bool categoryLocked=Privacy.IsLocked(active);
                addFrame.Visibility=active=="done" || categoryLocked ? Visibility.Collapsed:Visibility.Visible;searchFrame.Visibility=active=="done" ? Visibility.Visible:Visibility.Collapsed;
                var list=Service.State.Notes.Where(n=>Privacy.CanSee(n) && (active=="trash" ? n.Deleted:!n.Deleted && (active=="done" ? n.Done && InCompletedRange(n) && MatchesCompletedSearch(n):!n.Done && n.CategoryId==active))).OrderByDescending(n=>n.SortOrder).ThenByDescending(n=>n.Created).ToList();
                heading.Text=active=="done" ? "已完成":active=="trash" ? "回收站":Service.CategoryName(active);heading.ToolTip=heading.Text;
                subtitle.Text=categoryLocked ? "分类已上锁":list.Count+(active=="done" ? (String.IsNullOrEmpty(completedSearch) ? " 项已完成":" 项匹配"):active=="trash" ? " 项已删除":" 项待办");
                clearTrash.Visibility=active=="trash" ? Visibility.Visible:Visibility.Collapsed;clearTrash.IsEnabled=active=="trash" && list.Count>0;
                bool dateFiltered=completedFrom.HasValue || completedTo.HasValue;
                completedDateButton.Visibility=active=="done" ? Visibility.Visible:Visibility.Collapsed;completedDateButton.Content=dateFiltered ? "日期已筛选":"完成日期";completedDateButton.Background=dateFiltered ? Palette.Selected:Brushes.Transparent;
                completedDateHint.Visibility=active=="done" && dateFiltered ? Visibility.Visible:Visibility.Collapsed;
                completedDateHint.Text=(completedFrom.HasValue ? completedFrom.Value.ToString("yyyy-MM-dd"):"不限开始")+" 至 "+(completedTo.HasValue ? completedTo.Value.ToString("yyyy-MM-dd"):"不限结束");
                if(categoryLocked)ShowCategoryLockedCard();
                else if(list.Count==0)rows.Children.Add(new TextBlock {Text=active=="done" ? (!String.IsNullOrEmpty(completedSearch) ? "没有找到相关的已完成事项":dateFiltered ? "这段时间没有已完成事项":"完成的事项会收在这里"):active=="trash" ? "回收站是空的":"暂时没有事项",Foreground=Palette.Secondary,FontSize=12,TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center,Margin=new Thickness(6,55,6,20)});
                foreach(var note in list)rows.Children.Add(NoteRow(note));
            } finally {rendering=false;}
        }
        static IEnumerable<DependencyObject> FindDescendants(DependencyObject d) {yield return d;for(int i=0;i<VisualTreeHelper.GetChildrenCount(d);i++)foreach(var c in FindDescendants(VisualTreeHelper.GetChild(d,i)))yield return c;}
        Button SidebarButton(string name,string icon,string id,int count) {
            var b=Button("",name);b.ToolTip=name;b.Padding=new Thickness(9,11,7,11);b.Margin=new Thickness(0,2,0,2);b.HorizontalContentAlignment=HorizontalAlignment.Stretch;
            if(active==id)b.Background=Palette.Selected;
            var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(21)});grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
            var image=UiDesign.Icon(icon,active==id ? Palette.Accent:Palette.Icon,14);image.VerticalAlignment=VerticalAlignment.Center;grid.Children.Add(image);
            var label=new TextBlock {Text=name,TextTrimming=TextTrimming.CharacterEllipsis,FontSize=12,Foreground=active==id ? Palette.AccentText:Palette.Text,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(label,1);grid.Children.Add(label);
            if(count>0) {var badge=new TextBlock {Text=count.ToString(),FontSize=10,Foreground=Palette.Secondary,Margin=new Thickness(3,0,0,0),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(badge,2);grid.Children.Add(badge);}
            b.Content=grid;return b;
        }
        void AddTab(string id,string name) {
            int count=Service.State.Notes.Count(n=>Privacy.CanSee(n) && !n.Deleted && (id=="done" ? n.Done:!n.Done && n.CategoryId==id));
            var b=SidebarButton(name,id=="done" ? "done":"inbox",id,count);b.Click+=delegate {CommitEdit(false);active=id;Refresh();};
            if(Service.State.Categories.Any(c=>c.Id==id)) {
                var categoryLock=AddCategoryLock(b,id);b.Tag="category:"+id;b.PreviewMouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) {if(IsInside(e.OriginalSource as DependencyObject,categoryLock))return;if(e.ClickCount==2) {BeginCategoryRename(id);e.Handled=true;}else if(e.ClickCount==1)BeginCategoryDrag(b,id,e.GetPosition(root));};
                var menu=NewMenu();Menu(menu,"重命名",delegate {BeginCategoryRename(id);});
                Menu(menu,"删除分类",delegate {if(Privacy.IsLocked(id))return;if(Confirm("删除“"+Service.CategoryName(id)+"”分类？事项会保留并移入未完成。")) {CommitEdit(false);Service.DeleteCategory(id);if(active==id)active="inbox";Save();Refresh();}});foreach(MenuItem item in menu.Items)item.IsEnabled=!Privacy.IsLocked(id);b.ContextMenu=menu;
            }
            tabs.Children.Add(b);
        }
        TextBlock NoteLabel(Note n) {
            var text=new TextBlock {Text=n.Text,TextWrapping=TextWrapping.Wrap,FontSize=15,Foreground=n.Done ? Palette.Secondary:Palette.Text,Tag="note-text",LineHeight=23};
            if(n.Done)text.TextDecorations=TextDecorations.Strikethrough;
            text.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) {if(e.ClickCount==2 && !n.Deleted) {BeginEdit(n,null);e.Handled=true;}};return text;
        }
        FrameworkElement NoteRow(Note n) {
            bool compact=n.Steps.Count==0 && !n.Done && !n.Deleted;
            var border=new Border {CornerRadius=new CornerRadius(14),Background=Palette.Card,Padding=compact ? new Thickness(11,6,10,6):new Thickness(11,12,10,12),MinHeight=60,Margin=new Thickness(0,0,0,8),Tag=n.Id};
            var grid=new Grid {VerticalAlignment=compact ? VerticalAlignment.Center:VerticalAlignment.Stretch};grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(27)});grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});border.Child=grid;
            var toggle=Button("",n.Deleted ? "还原事项":n.Done ? "恢复未完成":"标记完成");toggle.Padding=new Thickness(1);toggle.Margin=new Thickness(0,3,7,0);toggle.VerticalAlignment=VerticalAlignment.Top;
            var circle=new Grid {Width=18,Height=18};circle.Children.Add(new Ellipse {Stroke=Palette.Accent,StrokeThickness=1.3,Fill=n.Done ? Palette.Accent:Brushes.Transparent});
            if(n.Done)circle.Children.Add(new System.Windows.Shapes.Path {Data=Geometry.Parse("M4,9 L7,12 L14,5"),Stroke=Palette.CheckInk,StrokeThickness=1.7,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round});
            toggle.Content=circle;toggle.Click+=delegate {CommitEdit(false);if(n.Deleted)Service.Restore(n.Id);else {int remaining=n.Steps.Count(s=>!s.Done);if(!n.Done && remaining>0 && new CompleteStepsWindow(this,remaining).ShowDialog()!=true)return;Service.Toggle(n.Id);}Save();Refresh();};grid.Children.Add(toggle);
            var content=new StackPanel();Grid.SetColumn(content,1);grid.Children.Add(content);content.Children.Add(NoteLabel(n));
            if(n.Steps.Count>0) {
                bool expanded=expandedSteps.Contains(n.Id);var expand=Button("",expanded ? "收起步骤":"展开步骤");expand.HorizontalContentAlignment=HorizontalAlignment.Left;expand.Padding=new Thickness(0,6,3,4);expand.Margin=new Thickness(0,2,0,0);
                var progress=new StackPanel {Orientation=Orientation.Horizontal};progress.Children.Add(new TextBlock {Text=expanded ? "⌄":"›",Foreground=Palette.AccentText,FontSize=15,Width=17,VerticalAlignment=VerticalAlignment.Center});progress.Children.Add(new TextBlock {Text="已完成 "+n.Steps.Count(s=>s.Done)+"/"+n.Steps.Count+" 步",FontSize=11,Foreground=Palette.Secondary,VerticalAlignment=VerticalAlignment.Center});expand.Content=progress;content.Children.Add(expand);
                expand.Click+=delegate {CommitEdit(false);if(!expandedSteps.Add(n.Id))expandedSteps.Remove(n.Id);Refresh();};
                if(expanded)foreach(var step in n.Steps)content.Children.Add(StepRow(n,step));
            }
            content.Children.Add(NoteMetadata(n));
            if(!n.Deleted && !n.Done) {var add=AddStepAction(n);Grid.SetColumn(add,2);grid.Children.Add(add);}
            var menu=NewMenu();
            if(n.Deleted) {
                Menu(menu,"还原",delegate {Service.Restore(n.Id);Save();Refresh();});
                Menu(menu,"永久删除",delegate {if(Confirm("永久删除这条事项？此操作不能撤销。")) {Service.Remove(n.Id);Save();Refresh();}});
            } else {
                Menu(menu,"编辑",delegate {BeginEdit(n,null);});
                var move=new MenuItem {Header="移到分类"};menu.Items.Add(move);AddMove(move,n,"inbox","未完成");foreach(var c in Service.State.Categories)AddMove(move,n,c.Id,c.Name);
                Menu(menu,"删除到回收站",delegate {CommitEdit(false);Service.Trash(n.Id);Save();Refresh();});
            }
            border.ContextMenu=menu;return border;
        }
        ContextMenu NewMenu() {var menu=new ContextMenu();popupPalette.StyleMenu(menu);return menu;}
        TextBlock StepLabel(Note note,NoteStep step) {
            var label=new TextBlock {Text=step.Text,Tag="step:"+step.Id,TextWrapping=TextWrapping.Wrap,FontSize=12,LineHeight=20,Margin=new Thickness(0,4,2,4),ToolTip=note.Deleted ? null:"拖动调整顺序，双击修改步骤"};if(step.Done)label.TextDecorations=TextDecorations.Strikethrough;
            var style=new Style(typeof(TextBlock));style.Setters.Add(new Setter(TextBlock.ForegroundProperty,step.Done ? Palette.Secondary:Palette.Text));var hover=new DataTrigger {Binding=new Binding("IsMouseOver") {RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(Grid),1)},Value=true};hover.Setters.Add(new Setter(TextBlock.ForegroundProperty,Palette.AccentText));style.Triggers.Add(hover);label.Style=style;
            label.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) {if(e.ClickCount==2 && !note.Deleted) {BeginStepEdit(note,step.Id,null,false);e.Handled=true;}};return label;
        }
        FrameworkElement StepRow(Note note,NoteStep step) {
            var grid=new Grid {Tag="step-row",Background=Brushes.Transparent,Margin=new Thickness(0,0,0,1)};grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(23)});grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(23)});
            AutomationProperties.SetAutomationId(grid,step.Id);
            var toggle=Button("","切换步骤："+step.Text);toggle.IsEnabled=!note.Deleted;toggle.Padding=new Thickness(1,4,5,4);toggle.Margin=new Thickness(0,1,0,0);toggle.VerticalAlignment=VerticalAlignment.Top;
            var mark=new Grid {Width=14,Height=14};mark.Children.Add(new Ellipse {Stroke=step.Done ? Palette.Accent:Palette.Secondary,StrokeThickness=1.1,Fill=step.Done ? Palette.Accent:Brushes.Transparent});if(step.Done)mark.Children.Add(UiDesign.Icon("done",Palette.CheckInk,9));toggle.Content=mark;grid.Children.Add(toggle);
            toggle.Click+=delegate {CommitEdit(false);Service.ToggleStep(note.Id,step.Id);Save();Refresh();};
            var label=StepLabel(note,step);Grid.SetColumn(label,1);grid.Children.Add(label);
            if(!note.Deleted) {
                var remove=Button("","删除步骤："+step.Text);remove.Content=UiDesign.Icon("trash",Palette.Icon,12);remove.Padding=new Thickness(4,6,4,6);remove.Margin=new Thickness(1,0,0,0);remove.VerticalAlignment=VerticalAlignment.Top;Grid.SetColumn(remove,2);grid.Children.Add(remove);
                var style=new Style(typeof(Button));style.Setters.Add(new Setter(UIElement.OpacityProperty,0.0));foreach(var property in new[]{"IsMouseOver","IsKeyboardFocusWithin"}) {var trigger=new DataTrigger {Binding=new Binding(property) {Source=grid},Value=true};trigger.Setters.Add(new Setter(UIElement.OpacityProperty,1.0));style.Triggers.Add(trigger);}remove.Style=style;
                remove.Click+=delegate {DeleteStep(note,step.Id,grid);};
            }
            return grid;
        }
        void AddMove(MenuItem parent,Note n,string id,string name) {var item=new MenuItem {Header=name,IsEnabled=!Privacy.IsLocked(id)};if(!item.IsEnabled)item.ToolTip="请先解锁此分类";item.Click+=delegate {if(Privacy.IsLocked(id) || !Privacy.CanSee(n))return;CommitEdit(false);Service.Move(n.Id,id);Save();Refresh();};parent.Items.Add(item);}
        public static void Menu(ContextMenu menu,string text,Action action) {var item=new MenuItem {Header=text};item.Click+=delegate {action();};menu.Items.Add(item);}
        void BeginEdit(Note n,string draft) {
            if(!Privacy.CanSee(n))return;
            CommitEdit(false);var row=rows.Children.OfType<Border>().FirstOrDefault(b=>(string)b.Tag==n.Id);if(row==null)return;
            var stack=(StackPanel)((Grid)row.Child).Children[1];
            editor=new TextBox {Text=draft ?? n.Text,TextWrapping=TextWrapping.Wrap,FontSize=15,Padding=new Thickness(4),MaxLength=20000,MinHeight=28,Foreground=Palette.Text,CaretBrush=Palette.Icon,Background=Palette.Input,BorderBrush=Palette.Accent};
            AutomationProperties.SetName(editor,"编辑事项");editing=n;var current=editor;stack.Children.RemoveAt(0);stack.Children.Insert(0,editor);ConfigureIme(editor);
            editor.TextChanged+=delegate {if(editor==current) {RecordEditDraft();ScheduleSave();}};
            editor.PreviewKeyDown+=delegate(object sender,KeyEventArgs e) {if(e.Key==Key.Escape) {CommitEdit(true);e.Handled=true;}else if(e.Key==Key.Enter && !composing && e.ImeProcessedKey!=Key.Enter) {CommitEdit(false);e.Handled=true;}};
            editor.LostKeyboardFocus+=delegate {if(!rendering && !committing && editor==current)Dispatcher.BeginInvoke(new Action(delegate {if(editor==current && !current.IsKeyboardFocusWithin)CommitEdit(false);}),DispatcherPriority.Background);};
            editor.Focus();editor.SelectAll();
        }
        void BeginStepEdit(Note note,string stepId,string draft,bool create) {
            if(!Privacy.CanSee(note))return;
            CommitEdit(false);CancelStepDrag();CancelNoteDrag();if(note.Deleted)return;
            if(create && note.Steps.Count>=200) {Tell("每个事项最多支持200个步骤。");return;}
            var step=create ? null:note.Steps.FirstOrDefault(s=>s.Id==stepId);if(!create && step==null)return;
            expandedSteps.Add(note.Id);Refresh();UpdateLayout();var row=rows.Children.OfType<Border>().FirstOrDefault(b=>(string)b.Tag==note.Id);if(row==null)return;
            Grid slot;
            if(create) {
                var stack=(StackPanel)((Grid)row.Child).Children[1];slot=new Grid {Tag="step-row",Margin=new Thickness(0,4,0,3)};slot.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(23)});slot.ColumnDefinitions.Add(new ColumnDefinition());
                slot.Children.Add(new TextBlock {Text="•",FontSize=15,Foreground=Palette.AccentText,Margin=new Thickness(5,5,0,0)});stack.Children.Insert(stack.Children.Count-1,slot);
            } else {var label=FindDescendants(row).OfType<TextBlock>().Single(t=>(t.Tag as string)=="step:"+stepId);slot=(Grid)label.Parent;slot.Children.Remove(label);}
            editor=new TextBox {Text=draft ?? (create ? "":step.Text),TextWrapping=TextWrapping.Wrap,FontSize=12,Padding=new Thickness(5),MaxLength=20000,MinHeight=30,Foreground=Palette.Text,CaretBrush=Palette.Icon,Background=Palette.Input,BorderBrush=Palette.Accent,BorderThickness=new Thickness(1),ToolTip=create ? "输入步骤，回车保存并继续添加，Esc取消":"回车或点击其他位置保存，Esc取消"};
            AutomationProperties.SetName(editor,create ? "新增步骤内容":"编辑步骤");Grid.SetColumn(editor,1);slot.Children.Add(editor);editing=note;editingStepId=stepId;addingStep=create;var current=editor;ConfigureIme(current);
            current.TextChanged+=delegate {if(editor==current) {RecordEditDraft();ScheduleSave();}};
            current.PreviewKeyDown+=delegate(object sender,KeyEventArgs e) {
                if(e.Key==Key.Escape) {CommitEdit(true);e.Handled=true;}
                else if(e.Key==Key.Enter && !composing && e.ImeProcessedKey!=Key.Enter) {bool next=addingStep && !String.IsNullOrWhiteSpace(current.Text);CommitEdit(false);if(next)BeginStepEdit(note,null,null,true);e.Handled=true;}
            };
            current.LostKeyboardFocus+=delegate {if(!rendering && !committing && editor==current)Dispatcher.BeginInvoke(new Action(delegate {if(editor==current && !current.IsKeyboardFocusWithin)CommitEdit(false);}),DispatcherPriority.Background);};
            RecordEditDraft();ScheduleSave();current.Focus();current.SelectAll();current.BringIntoView();
        }
        void RecordEditDraft() {if(editor==null)return;var p=Service.State.Settings;p.EditId=editing.Id;p.EditDraft=editor.Text;p.EditStepId=editingStepId;p.EditNewStep=addingStep;}
        void RedrawStepBody(Note note) {
            if(editor!=null || exiting)return;
            if(active=="done" && !String.IsNullOrEmpty(completedSearch)) {Refresh();return;}
            var row=rows.Children.OfType<Border>().FirstOrDefault(b=>(string)b.Tag==note.Id);if(row==null)return;
            var fresh=(Grid)((Border)NoteRow(note)).Child;var body=fresh.Children[1];fresh.Children.Remove(body);var target=(Grid)row.Child;target.Children.RemoveAt(1);target.Children.Insert(1,body);
        }
        void QueueStepRedraw(Note note) {
            pendingStepRedraw.Add(note.Id);
            if(stepRedrawTimer==null) {
                stepRedrawTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(40)};
                stepRedrawTimer.Tick+=delegate {
                    // MouseDown and Click can be separate dispatcher turns. Keep pressed
                    // controls alive until release, including keyboard Space presses.
                    if(stepDrag!=null || Mouse.LeftButton==MouseButtonState.Pressed || FindDescendants(root).OfType<ButtonBase>().Any(b=>b.IsPressed))return;
                    stepRedrawTimer.Stop();if(editor!=null || exiting)return;
                    foreach(var id in pendingStepRedraw.ToList()) {var item=Service.State.Notes.FirstOrDefault(n=>n.Id==id);if(item!=null)RedrawStepBody(item);}pendingStepRedraw.Clear();
                };
            }
            stepRedrawTimer.Stop();stepRedrawTimer.Start();
        }
        void CommitEdit(bool cancel) {
            if(retired || nativeDestroying)return;
            if(editor==null || committing)return;committing=true;var current=editor;var note=editing;string stepId=editingStepId;bool create=addingStep;editor=null;editing=null;editingStepId=null;addingStep=false;
            bool isStep=create || stepId!=null;
            if(!cancel && !String.IsNullOrWhiteSpace(current.Text)) {
                if(isStep) {var steps=note.Steps.Select(s=>new NoteStep {Id=s.Id,Text=s.Text,Done=s.Done}).ToList();if(create) {stepId=Guid.NewGuid().ToString("N");steps.Add(new NoteStep {Id=stepId,Text=current.Text});}else steps.Single(s=>s.Id==stepId).Text=current.Text;Service.SetSteps(note.Id,steps);}
                else Service.Edit(note.Id,current.Text);
            }
            Service.State.Settings.EditId=null;Service.State.Settings.EditDraft=null;Service.State.Settings.EditStepId=null;Service.State.Settings.EditNewStep=false;
            if(isStep) {
                var slot=current.Parent as Grid;if(slot!=null) {slot.Children.Remove(current);var step=note.Steps.FirstOrDefault(s=>s.Id==stepId);if(step!=null) {var label=StepLabel(note,step);Grid.SetColumn(label,1);slot.Children.Add(label);}else {var panel=slot.Parent as Panel;if(panel!=null)panel.Children.Remove(slot);}}
                QueueStepRedraw(note);
            } else {var stack=current.Parent as StackPanel;if(stack!=null) {stack.Children.Remove(current);stack.Children.Insert(0,NoteLabel(note));}}
            if(!isStep && pendingStepRedraw.Count>0)QueueStepRedraw(note);
            committing=false;Save();
        }
        void UpdateLock() {
            bool locked=Service.State.Settings.Locked;lockButton.Content=UiDesign.Icon(locked ? "lock":"unlock",Palette.Icon,16);lockButton.ToolTip=locked ? "已锁定，点击解锁":"已解锁，点击固定";
            foreach(var grip in grips)grip.IsHitTestVisible=!locked;if(locked)EndMove();
        }
        public void ApplyAppearance() {
            Opacity=1;UiMotion.Enabled=Service.State.Settings.AnimationsEnabled;Palette.Apply(Service.State.Settings,false);popupPalette.Apply(Service.State.Settings,true);Palette.Resources(this);
            Foreground=Palette.Text;shadow.Opacity=.12*Service.State.Settings.BackgroundOpacity;Host.SetIconLabelTheme(Palette.IsDark);
            ApplyCustomBackground();
            ApplyUiSize();
            if(lockButton!=null)UpdateLock();
            var handler=AppearanceChanged;if(handler!=null)handler(this,EventArgs.Empty);
        }
        void SystemThemeChanged(object sender,Microsoft.Win32.UserPreferenceChangedEventArgs e) {Dispatcher.BeginInvoke(new Action(delegate {if(!exiting && Service.State.Settings.ThemeMode=="system")ApplyAppearance();}));}
        public void Tell(string message) {status.Text=message;status.Visibility=String.IsNullOrEmpty(message) ? Visibility.Collapsed:Visibility.Visible;}
        void ScheduleSave() {if(saveTimer==null || retired)return;saveTimer.Stop();saveTimer.Start();}
        public bool Save() {
            if(!ready || retired)return true;
            try {
                if(editor!=null)RecordEditDraft();
                StartupTrace.BeforeFirstSave(Store,Service.State,this);Store.Save(Service.State);Tell(null);return true;
            }catch(Exception ex) {Tell("保存失败，请重试："+ex.Message);return false;}
        }
        void SetupTray() {
            tray=new TrayIcon {Text="TinyStar桌面便签",Visible=false};
            var assembly=typeof(MainWindow).Assembly;var stream=assembly.GetManifestResourceStream("Shell.ico") ?? assembly.GetManifestResourceStream("App.ico");
            if(stream!=null) {using(stream)using(var icon=new System.Drawing.Icon(stream))trayIcon=(System.Drawing.Icon)icon.Clone();tray.Icon=trayIcon;Icon=System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(trayIcon.Handle,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());}
            else tray.Icon=System.Drawing.SystemIcons.Application;
            var menu=new Forms.ContextMenuStrip();menu.Items.Add("显示便签",null,delegate {Dispatcher.Invoke(new Action(Reveal));});
            menu.Items.Add("快捷添加",null,delegate {Dispatcher.BeginInvoke(new Action(ToggleQuickAdd));});
            menu.Items.Add("恢复位置和两项不透明度",null,delegate {Dispatcher.Invoke(new Action(RestorePlacement));});
            menu.Items.Add("设置",null,delegate {Dispatcher.Invoke(new Action(delegate {ShowSettings();}));});
            menu.Items.Add("退出",null,delegate {Dispatcher.Invoke(new Action(Exit));});tray.ContextMenuStrip=menu;tray.DoubleClick+=delegate {Dispatcher.Invoke(new Action(Reveal));};tray.Visible=!previewProcess;
        }
        public void Reveal() {CheckWindowLifetime();if(retired) {revealRequested=true;var current=CurrentWindow;if(current!=null)current.Reveal();return;}if(exiting || nativeDestroying) {revealRequested=true;return;}Show();if(!testMode)Host.Reattach();else Activate();}
        public void RestorePlacement() {
            CheckWindowLifetime();if(retired) {var current=CurrentWindow;if(current!=null)current.RestorePlacement();return;}if(exiting || nativeDestroying)return;
            Service.State.Settings.BackgroundOpacity=1;Service.State.Settings.TextOpacity=1;ApplyAppearance();var area=SystemParameters.WorkArea;Host.Move(Math.Max(area.Left,area.Right-Width-32),area.Top+40);AcceptCurrentPlacement();Reveal();Save();
        }
        void StartQuickHotkey() {
            if(quickHotkey!=null)return;
            quickHotkey=new GlobalHotkey(delegate {if(settingsWindow!=null && settingsWindow.IsRecordingShortcut)settingsWindow.AcceptRecordedShortcut(quickHotkey.Gesture);else ToggleQuickAdd();});
            string error;if(!quickHotkey.TrySet(Service.State.Settings.QuickAddShortcut,out error))Tell("快捷添加快捷键暂不可用，请在设置中修改。");
        }
        public string CurrentShortcutDisplay {get {ShortcutGesture gesture;string error;if(quickHotkey!=null && quickHotkey.IsRegistered)return quickHotkey.Gesture.Display;return ShortcutGesture.TryParse(Service.State.Settings.QuickAddShortcut,out gesture,out error) ? gesture.Display:"Ctrl + I";}}
        public bool ShortcutRegistered {get {return quickHotkey!=null && quickHotkey.IsRegistered;}}
        public void ToggleQuickAdd() {CheckWindowLifetime();if(retired) {var current=CurrentWindow;if(current!=null)current.ToggleQuickAdd();return;}if(exiting || nativeDestroying)return;if(quickWindow==null)quickWindow=new QuickAddWindow(this);if(quickWindow.IsVisible)quickWindow.Dismiss(true);else quickWindow.ShowForActiveScreen();}
        internal QuickAddWindow QuickWindow {get {return quickWindow;}}
        internal void SetQuickDraft(string value) {Service.State.Settings.QuickAddDraft=value ?? "";ScheduleSave();}
        public bool TryChangeQuickShortcut(string value,out string error) {
            StartQuickHotkey();string previous=Service.State.Settings.QuickAddShortcut;
            if(!quickHotkey.TrySet(value,out error))return false;Service.State.Settings.QuickAddShortcut=quickHotkey.Gesture.Canonical;
            if(!Save()) {Service.State.Settings.QuickAddShortcut=previous;string rollback;quickHotkey.TrySet(previous,out rollback);error="快捷键设置未能保存。当前快捷键为 "+CurrentShortcutDisplay+"。";return false;}
            error=null;return true;
        }
        internal bool TryAddQuickNote(string text,out string error) {
            error=null;QuickAddNotice=null;bool follow=Service.State.Settings.QuickAddDestination!="inbox";
            bool fallback=follow && !Service.HasCategory(active);string target=follow && !fallback ? active:"inbox";
            if(Privacy.IsLocked(target)) {error="当前分类已上锁，请先解锁后再保存。";return false;}
            Note note;try {note=Service.Add(text,target);}catch(ArgumentException ex) {error=ex.Message;return false;}
            string draft=Service.State.Settings.QuickAddDraft;Service.State.Settings.QuickAddDraft="";
            if(!Save()) {Service.State.Notes.Remove(note);Service.State.Settings.QuickAddDraft=draft;error="保存失败，内容已保留，请稍后重试。";return false;}
            if(fallback)QuickAddNotice="已添加到“未完成”";
            Refresh();if(IsVisible) {var row=rows.Children.OfType<Border>().FirstOrDefault(b=>(string)b.Tag==note.Id);if(row!=null)UiMotion.Appear(row);}return true;
        }
        public SettingsWindow ShowSettings() {
            CheckWindowLifetime();
            if(retired) {var current=CurrentWindow;return current==null ? null:current.ShowSettings();}
            CommitEdit(false);
            if(settingsWindow!=null) {if(settingsWindow.WindowState==WindowState.Minimized)settingsWindow.WindowState=WindowState.Normal;settingsWindow.Activate();settingsWindow.Focus();return settingsWindow;}
            settingsWindow=new SettingsWindow(this);settingsWindow.Closed+=delegate {settingsWindow=null;};settingsWindow.Show();return settingsWindow;
        }
        public void Exit() {
            CheckWindowLifetime();
            if(retired) {var current=CurrentWindow;if(current!=null)current.Exit();else Application.Current.Shutdown();return;}
            if(exiting)return;
            if(!CommitCategoryRename(false))return;
            CommitEdit(false);if(!Save() && !Confirm("保存失败。仍然退出吗？尚未保存的内容可能丢失。"))return;
            StopQueuedMove(true);exiting=true;ReleaseWindowResources();Close();Application.Current.Shutdown();
        }
        public void ReplaceState(NoteState state) {ReplaceState(state,false);}
        internal void ReplaceState(NoteState state,bool privateAccessVerified) {
            if(Privacy.HasLockedCategories && !privateAccessVerified)throw new InvalidOperationException("请先验证分类密码，再恢复备份。");
            CommitEdit(false);CommitCategoryRename(true);CancelCategoryDrag();CancelStepDrag();CancelNoteDrag();
            bool drafts=state.IncludesDrafts;var settings=Storage.CopyPreferences(Service.State.Settings);if(drafts)Storage.CopyContentDrafts(state.Settings,settings);state.Settings=settings;state.IncludesDrafts=false;
            Store.Save(state);if(quickWindow!=null){quickWindow.Dispose();quickWindow=null;}Service=new NoteService(state);Privacy=new CategoryPrivacy(state);active=drafts && Service.HasCategory(settings.DraftCategory) ? settings.DraftCategory:"inbox";
            syncingPrivateDraft=true;try{input.Text=drafts && !Privacy.IsLocked(settings.DraftCategory) ? settings.Draft ?? "":"";}finally{syncingPrivateDraft=false;}
            if(!drafts){settings.Draft="";settings.DraftCategory="inbox";}Save();Refresh();if(drafts)RestoreImportedEdit();
        }
        void DisplayChanged(object sender,EventArgs e) {Dispatcher.BeginInvoke(new Action(delegate {if(exiting)return;KeepOnScreen();Save();}));}
        public void Try(Action action) {try {action();}catch(Exception ex) {MessageBox.Show(ex.Message,"TinyStar桌面便签");}}
        public static bool Confirm(string text) {return MessageBox.Show(text,"TinyStar桌面便签",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes;}
        public string Prompt(string title,string hint,string initial) {
            var window=new CategoryPromptWindow(this,title,hint,initial);return window.ShowDialog()==true ? window.Value:null;
        }
    }
}




















