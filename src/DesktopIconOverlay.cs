using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
namespace DesktopNotes {
    internal sealed class DesktopIconItem {
        public string Name;public Rect Bounds,BackdropBounds,NativeBounds;public Point NativeAnchor;public BitmapSource Image,Backdrop;public bool Selected,NativeSelected;
        public Rect CollapsedBounds,ExpandedBounds,CollapsedBackdropBounds,ExpandedBackdropBounds;
        public BitmapSource CollapsedImage,ExpandedImage,CollapsedBackdrop,ExpandedBackdrop;
        internal string Identity {get {return Name+"|"+NativeAnchor.X.ToString(CultureInfo.InvariantCulture)+"|"+NativeAnchor.Y.ToString(CultureInfo.InvariantCulture);}}
        internal void Select(bool selected) {
            Selected=selected;var image=selected ? ExpandedImage:CollapsedImage;if(image==null)return;
            Image=image;Bounds=selected ? ExpandedBounds:CollapsedBounds;Backdrop=selected ? ExpandedBackdrop:CollapsedBackdrop;BackdropBounds=selected ? ExpandedBackdropBounds:CollapsedBackdropBounds;
        }
    }
    // Composite only transparent icon artwork and glyphs. Never reveal the wallpaper
    // through window-region holes or change Explorer's windows, icons or wallpaper.
    public sealed class DesktopIconOverlay {
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
        [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr hwnd,out NativeRect rect);
        [DllImport("user32.dll")]static extern uint GetDpiForWindow(IntPtr hwnd);
        [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr hwnd);
        [DllImport("user32.dll")]static extern bool ScreenToClient(IntPtr hwnd,ref NativePoint point);
        [DllImport("user32.dll")]static extern bool PostMessage(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam);
        [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr hwnd,uint flags);
        [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr hwnd);
        [DllImport("user32.dll")]static extern bool DestroyIcon(IntPtr icon);
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SHParseDisplayName(string name,IntPtr bind,out IntPtr pidl,uint attributes,out uint result);
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SHGetFileInfo(IntPtr pidl,uint attributes,ref FileInfo info,uint size,uint flags);
        [DllImport("shell32.dll",EntryPoint="#727")]static extern int SHGetImageList(int size,ref Guid id,[MarshalAs(UnmanagedType.Interface)]out IImageList list);
        [ComImport,Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IImageList {
            void Add();void ReplaceIcon();void SetOverlayImage();void Replace();void AddMasked();void Draw();void Remove();
            [PreserveSig]int GetIcon(int index,int flags,out IntPtr icon);
        }
        [StructLayout(LayoutKind.Sequential)]struct NativeRect {public int Left,Top,Right,Bottom;}
        [StructLayout(LayoutKind.Sequential)]struct NativePoint {public int X,Y;}
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]struct FileInfo {public IntPtr Icon;public int Index;public uint Attributes;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string Name;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)]public string Type;}
        readonly Window window;readonly Canvas layer,backdrops;readonly DesktopIconSurface iconSurface,backdropSurface;readonly DispatcherTimer timer;
        readonly Border captionCard;readonly TextBlock captionText;bool captionRequested;string captionIdentity;Rect captionScreenBounds=Rect.Empty;
        readonly DesktopWallpaperSource wallpaper=new DesktopWallpaperSource();
        readonly Dictionary<string,BitmapSource> images=new Dictionary<string,BitmapSource>();
        Dictionary<string,string> paths;string previousSignature;long imageEpoch;
        IntPtr view,list;int busy,generation,reset;volatile bool stopped;
        Color labelColor=Colors.White;
        double layoutScale=Double.NaN,layoutOriginX,layoutOriginY;
        string clickedIdentity,nativeSelectionSignature;int clickRevision;bool clickedAdditive;
        internal int ClickRevision {get {return clickRevision;}}
        public int ItemCount {get {return layer.Children.OfType<DesktopIconVisual>().Count();}}
        internal Rect CaptionScreenBounds {get {return captionScreenBounds;}}
        internal bool CleanBackdropReady {get {return wallpaper.Ready;}}
        internal Canvas IconCanvas {get {return layer;}}
        internal Canvas BackdropCanvas {get {return backdrops;}}
        public string LastError {get;private set;}
        public DesktopIconOverlay(Window window,Grid root,IntPtr view) : this(window,root,view,true) { }
        internal DesktopIconOverlay(Window window,Grid root,IntPtr view,bool poll) {
            this.window=window;this.view=view;list=FindWindowEx(view,IntPtr.Zero,"SysListView32",null);
            backdropSurface=new DesktopIconSurface(window,true);iconSurface=new DesktopIconSurface(window,false);backdrops=backdropSurface.Canvas;layer=iconSurface.Canvas;
            captionText=new TextBlock {TextWrapping=TextWrapping.Wrap,FontFamily=SystemFonts.IconFontFamily,FontSize=SystemFonts.IconFontSize,FontWeight=SystemFonts.IconFontWeight};
            captionCard=new Border {Child=captionText,CornerRadius=new CornerRadius(9),Padding=new Thickness(9,7,9,7),BorderThickness=new Thickness(1),Visibility=Visibility.Collapsed,IsHitTestVisible=false,ClipToBounds=true};Panel.SetZIndex(captionCard,100);layer.Children.Add(captionCard);
            window.IsVisibleChanged+=VisibilityChanged;
            backdropSurface.SetVisible(window.IsVisible);iconSurface.SetVisible(window.IsVisible);
            window.PreviewMouseDown+=NotePressed;
            timer=new DispatcherTimer(TimeSpan.FromMilliseconds(1000),DispatcherPriority.Background,delegate {Refresh();},window.Dispatcher);
            if(poll) {timer.Start();Refresh();}else timer.Stop();
        }
        public void SetView(IntPtr value) {iconSurface.ResetPlacement();backdropSurface.ResetPlacement();view=value;list=FindWindowEx(view,IntPtr.Zero,"SysListView32",null);Interlocked.Increment(ref generation);Interlocked.Exchange(ref reset,1);Refresh();}
        void VisibilityChanged(object sender,DependencyPropertyChangedEventArgs e) {if(!stopped) {backdropSurface.SetVisible(window.IsVisible);iconSurface.SetVisible(window.IsVisible);}}
        void NotePressed(object sender,MouseButtonEventArgs e) {captionRequested=false;captionIdentity=null;if(clickedIdentity!=null) {clickRevision++;clickedIdentity=null;foreach(var visual in layer.Children.OfType<DesktopIconVisual>()) {visual.Item.Select(visual.Item.NativeSelected);visual.UpdateAppearance();Panel.SetZIndex(visual,visual.Item.Selected ? 1:0);}UpdateBackdropImages();}Apply();}
        public void SetLabelTheme(bool dark) {
            Color next=dark ? Colors.White:Color.FromRgb(35,38,43);if(next==labelColor)return;labelColor=next;Interlocked.Increment(ref generation);Interlocked.Exchange(ref reset,1);Refresh();
        }
        void Refresh() {
            if(Mouse.LeftButton==MouseButtonState.Pressed || Mouse.RightButton==MouseButtonState.Pressed)return;
            if(stopped || Interlocked.CompareExchange(ref busy,1,0)!=0)return;
            IntPtr target=list;int version=generation,inputRevision=clickRevision;Color ink=labelColor;
            var worker=new Thread(delegate() {
                try {
                    if(target==IntPtr.Zero)throw new InvalidOperationException("Desktop icon list unavailable");
                    if(!IsWindowVisible(target)) {previousSignature=null;window.Dispatcher.BeginInvoke(new Action(delegate {if(!stopped && version==generation)ReplaceItems(new List<DesktopIconItem>());}));return;}
                    if(Interlocked.Exchange(ref reset,0)!=0) {images.Clear();paths=null;previousSignature=null;}
                    var items=new List<KeyValuePair<string,Rect>>();var signature=new StringBuilder();var selectedNames=new HashSet<string>();
                    var cache=new CacheRequest();cache.Add(AutomationElement.BoundingRectangleProperty);cache.Add(AutomationElement.NameProperty);cache.Add(SelectionItemPattern.IsSelectedProperty);cache.TreeScope=TreeScope.Element;
                    using(cache.Activate()) {
                        var children=AutomationElement.FromHandle(target).FindAll(TreeScope.Children,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ListItem));
                        foreach(AutomationElement item in children) {
                            var rect=item.Cached.BoundingRectangle;var name=item.Cached.Name;
                            if(rect.IsEmpty || rect.Width<=0 || rect.Height<=0 || rect.Width>1000 || rect.Height>1000)continue;
                            object selection=item.GetCachedPropertyValue(SelectionItemPattern.IsSelectedProperty,true);bool selected=selection is bool && (bool)selection;if(selected)selectedNames.Add(name);
                            items.Add(new KeyValuePair<string,Rect>(name,rect));signature.Append(name).Append('|').Append(rect.ToString(CultureInfo.InvariantCulture)).Append('|').Append(selected).Append(';');
                        }
                    }
                    long epoch=DateTime.UtcNow.Ticks/TimeSpan.FromSeconds(5).Ticks;
                    if(epoch!=imageEpoch) {imageEpoch=epoch;images.Clear();paths=null;previousSignature=null;wallpaper.Refresh();}
                    string current=signature.ToString();if(current==previousSignature)return;
                    var result=new List<DesktopIconItem>();double scale=1;
                    try {uint dpi=GetDpiForWindow(target);if(dpi>0)scale=dpi/96.0;}catch(EntryPointNotFoundException){scale=DesktopHost.SystemScale;}
                    int size=48;
                    using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\Shell\Bags\1\Desktop"))if(key!=null)size=Convert.ToInt32(key.GetValue("IconSize",48));
                    size=(int)Math.Max(16,Math.Min(256,size)*scale);
                    foreach(var item in items) {
                        int width=(int)Math.Ceiling(item.Value.Width),height=(int)Math.Ceiling(item.Value.Height);
                        bool selected=selectedNames.Contains(item.Key);string key=item.Key+"|"+width+"|"+height+"|"+size;BitmapSource collapsed,expanded,art=null;
                        if(!images.TryGetValue(key+"|closed",out collapsed)) {art=Icon(item.Key,size);collapsed=Compose(item.Key,width,height,size,scale,art,ink,false);images[key+"|closed"]=collapsed;}
                        if(!images.TryGetValue(key+"|open",out expanded)) {if(art==null)art=Icon(item.Key,size);expanded=Compose(item.Key,width,height,size,scale,art,ink,true);images[key+"|open"]=expanded;}
                        var closedBounds=new Rect(item.Value.Left,item.Value.Top,width,Math.Max(item.Value.Height,collapsed.PixelHeight));var openBounds=new Rect(item.Value.Left,item.Value.Top,width,Math.Max(item.Value.Height,expanded.PixelHeight));
                        var closedPatch=closedBounds;closedPatch.Inflate(4,4);var openPatch=openBounds;openPatch.Inflate(4,4);var closedBackground=wallpaper.Crop(closedPatch);
                        var record=new DesktopIconItem {Name=item.Key,NativeBounds=item.Value,NativeAnchor=new Point(item.Value.Left+item.Value.Width/2,item.Value.Top+Math.Min(size/2.0,item.Value.Height/2)),NativeSelected=selected,CollapsedImage=collapsed,ExpandedImage=expanded,CollapsedBounds=closedBounds,ExpandedBounds=openBounds,CollapsedBackdropBounds=closedPatch,ExpandedBackdropBounds=openPatch,CollapsedBackdrop=closedBackground,ExpandedBackdrop=openPatch==closedPatch ? closedBackground:wallpaper.Crop(openPatch)};
                        record.Select(selected);result.Add(record);
                    }
                    if(!stopped && !window.Dispatcher.HasShutdownStarted)window.Dispatcher.BeginInvoke(new Action(delegate {
                        if(stopped || version!=generation)return;
                        // Do not remove the element holding a physical mouse capture.
                        if(Mouse.LeftButton==MouseButtonState.Pressed || Mouse.RightButton==MouseButtonState.Pressed)return;
                        if(inputRevision!=clickRevision) {previousSignature=null;return;}
                        ReplaceItems(result,inputRevision);previousSignature=current;LastError=null;
                    }));
                }catch(Exception ex) {LastError=ex.Message;}finally {Interlocked.Exchange(ref busy,0);}
            });worker.IsBackground=true;worker.SetApartmentState(ApartmentState.STA);worker.Start();
        }
        void LoadPaths() {
            paths=new Dictionary<string,string>(StringComparer.CurrentCultureIgnoreCase);object shell=null,folder=null,items=null;
            try {
                shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));folder=((dynamic)shell).NameSpace(0);items=((dynamic)folder).Items();int count=((dynamic)items).Count;
                for(int i=0;i<count;i++) {object item=null;try {item=((dynamic)items).Item(i);string name=((dynamic)item).Name;string path=((dynamic)item).Path;if(!paths.ContainsKey(name))paths[name]=path;}finally {Release(item);}}
            }finally {Release(items);Release(folder);Release(shell);}
        }
        static void Release(object value) {if(value!=null && Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}
        BitmapSource Icon(string name,int size) {
            try {
                if(paths==null)LoadPaths();string path;if(!paths.TryGetValue(name,out path))return null;
                IntPtr pidl;uint attrs;if(SHParseDisplayName(path,IntPtr.Zero,out pidl,0,out attrs)!=0)return null;
                var info=new FileInfo();IntPtr icon=IntPtr.Zero;IImageList imageList=null;
                try {
                    // Ask the shell image list for a native-size transparent icon.
                    if(SHGetFileInfo(pidl,0,ref info,(uint)Marshal.SizeOf(typeof(FileInfo)),0x4000|0x8)!=IntPtr.Zero) {
                        var guid=typeof(IImageList).GUID;
                        if(SHGetImageList(size>48 ? 4:size>32 ? 2:0,ref guid,out imageList)==0)imageList.GetIcon(info.Index,1,out icon);
                    }
                    if(icon==IntPtr.Zero && SHGetFileInfo(pidl,0,ref info,(uint)Marshal.SizeOf(typeof(FileInfo)),0x100|0x8|0x20)!=IntPtr.Zero)icon=info.Icon;
                    if(icon==IntPtr.Zero)return null;
                    var image=Imaging.CreateBitmapSourceFromHIcon(icon,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());image.Freeze();return image;
                }finally {if(icon!=IntPtr.Zero)DestroyIcon(icon);Release(imageList);Marshal.FreeCoTaskMem(pidl);}
            }catch {return null;}
        }
        internal static BitmapSource Compose(string name,int width,int height,int iconSize,double scale,BitmapSource icon,Color? labelInk=null,bool selected=false) {
            var visual=new DrawingVisual();RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.HighQuality);TextOptions.SetTextRenderingMode(visual,TextRenderingMode.Grayscale);TextOptions.SetTextFormattingMode(visual,TextFormattingMode.Display);double side=Math.Min(iconSize,Math.Min(width,height));
            double textTop=Math.Round(side+2*scale);FormattedText text=null;
            if(!String.IsNullOrEmpty(name)) {
                var face=new Typeface(SystemFonts.IconFontFamily,SystemFonts.IconFontStyle,SystemFonts.IconFontWeight,FontStretches.Normal);
                text=new FormattedText(name,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,face,SystemFonts.IconFontSize*scale,new SolidColorBrush(labelInk ?? Colors.White),1.0);
                text.MaxTextWidth=Math.Max(1,width);text.MaxLineCount=selected ? 32:2;text.TextAlignment=TextAlignment.Center;text.Trimming=TextTrimming.CharacterEllipsis;
                height=Math.Max(height,(int)Math.Ceiling(textTop+text.Height+2*scale));
            }
            using(var dc=visual.RenderOpen()) {
                var rect=new Rect(Math.Floor((width-side)/2),0,side,side);
                if(icon!=null)dc.DrawImage(icon,rect);
                else {var file=new Rect(rect.Left+side*.2,rect.Top+side*.08,side*.6,side*.8);dc.DrawRoundedRectangle(Brushes.White,new Pen(Brushes.Gray,1),file,2,2);}
                if(text!=null)dc.DrawText(text,new Point(0,textTop));
            }
            var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);bitmap.Freeze();return bitmap;
        }
        internal void ReplaceItems(List<DesktopIconItem> items,int snapshotClickRevision=-1) {
            if(snapshotClickRevision>=0 && snapshotClickRevision!=clickRevision) {previousSignature=null;return;}
            var signature=new StringBuilder();bool clickedNativeSelected=false,clickedExists=false,captionNativeSelected=false,captionExists=false;
            foreach(var item in items) {if(item.Identity==clickedIdentity)clickedExists=true;if(item.Identity==captionIdentity)captionExists=true;if(item.NativeSelected) {signature.Append(item.Identity).Append(';');if(item.Identity==clickedIdentity)clickedNativeSelected=true;if(item.Identity==captionIdentity)captionNativeSelected=true;}}
            if(clickedIdentity!=null && !clickedExists)clickedIdentity=null;
            string nextNative=signature.ToString();
            if(clickedNativeSelected)clickedIdentity=null;
            if(captionRequested && (!captionExists || (nativeSelectionSignature!=null && nextNative!=nativeSelectionSignature && !captionNativeSelected && (nextNative.Length>0 || clickedIdentity!=captionIdentity)))) {captionRequested=false;captionIdentity=null;}
            if(clickedIdentity!=null && nativeSelectionSignature!=null && nextNative!=nativeSelectionSignature && ((nextNative.Length>0 && !clickedNativeSelected) || (nextNative.Length==0 && nativeSelectionSignature.Contains(clickedIdentity+";"))))clickedIdentity=null;
            nativeSelectionSignature=nextNative;
            foreach(var item in items)item.Select(clickedIdentity==null ? item.NativeSelected:item.Identity==clickedIdentity || (clickedAdditive && item.NativeSelected));
            layer.Children.Clear();backdrops.Children.Clear();foreach(var item in items) {
                var visual=new DesktopIconVisual(this,item);Panel.SetZIndex(visual,item.Selected ? 1:0);layer.Children.Add(visual);
                if(item.Backdrop!=null) {var patch=new Image {Source=item.Backdrop,Tag=item,IsHitTestVisible=false,Stretch=Stretch.Fill,SnapsToDevicePixels=true};RenderOptions.SetBitmapScalingMode(patch,BitmapScalingMode.NearestNeighbor);backdrops.Children.Add(patch);}
            }layer.Children.Add(captionCard);layoutScale=Double.NaN;Apply();
        }
        internal void RevealClickedItem(DesktopIconItem item,bool additive=false) {
            clickRevision++;clickedIdentity=item.Identity;clickedAdditive=additive;captionIdentity=item.Identity;captionRequested=true;
            foreach(var visual in layer.Children.OfType<DesktopIconVisual>()) {visual.Item.Select(visual.Item.Identity==clickedIdentity || (additive && visual.Item.Selected));visual.UpdateAppearance();Panel.SetZIndex(visual,visual.Item.Selected ? 1:0);}
            UpdateBackdropImages();Apply();
        }
        void UpdateBackdropImages() {layoutScale=Double.NaN;foreach(Image patch in backdrops.Children)patch.Source=((DesktopIconItem)patch.Tag).Backdrop;}
        public void Apply() {
            NativeRect bounds;if(stopped || !GetWindowRect(new WindowInteropHelper(window).Handle,out bounds))return;
            backdropSurface.Update();iconSurface.Update();double scale=iconSurface.Scale,originX=iconSurface.OriginX,originY=iconSurface.OriginY;
            bool layoutChanged=layoutScale!=scale || layoutOriginX!=originX || layoutOriginY!=originY;
            foreach(Image patch in backdrops.Children) {
                if(!layoutChanged)break;
                var item=(DesktopIconItem)patch.Tag;var rect=item.BackdropBounds;
                patch.Width=rect.Width/scale;patch.Height=rect.Height/scale;Canvas.SetLeft(patch,(rect.Left-originX)/scale);Canvas.SetTop(patch,(rect.Top-originY)/scale);
            }
            foreach(var visual in layer.Children.OfType<DesktopIconVisual>()) {
                var item=visual.Item;
                if(layoutChanged) {visual.Width=item.Bounds.Width/scale;visual.Height=item.Bounds.Height/scale;Canvas.SetLeft(visual,(item.Bounds.Left-originX)/scale);Canvas.SetTop(visual,(item.Bounds.Top-originY)/scale);}
                visual.Visibility=item.Bounds.IntersectsWith(new Rect(bounds.Left,bounds.Top,bounds.Right-bounds.Left,bounds.Bottom-bounds.Top)) ? Visibility.Visible:Visibility.Collapsed;
            }
            layoutScale=scale;layoutOriginX=originX;layoutOriginY=originY;
            UpdateCaption(new Rect(bounds.Left,bounds.Top,bounds.Right-bounds.Left,bounds.Bottom-bounds.Top),originX,originY,scale);
        }
        void UpdateCaption(Rect note,double originX,double originY,double scale) {
            var target=captionRequested ? layer.Children.OfType<DesktopIconVisual>().FirstOrDefault(v=>v.Item.Identity==captionIdentity && v.Visibility==Visibility.Visible):null;
            if(target==null) {captionCard.Visibility=Visibility.Collapsed;captionScreenBounds=Rect.Empty;return;}
            var safe=new Rect((note.Left-originX)/scale+12,(note.Top-originY)/scale+12,Math.Max(1,note.Width/scale-24),Math.Max(1,note.Height/scale-24));
            captionText.Text=target.Item.Name;captionText.Foreground=new SolidColorBrush(labelColor);
            bool dark=labelColor==Colors.White;captionCard.Background=new SolidColorBrush(dark ? Color.FromRgb(35,38,45):Color.FromRgb(250,251,253));captionCard.BorderBrush=new SolidColorBrush(dark ? Color.FromRgb(96,124,162):Color.FromRgb(164,184,211));
            captionCard.Width=Double.NaN;captionCard.Height=Double.NaN;captionCard.MaxWidth=Math.Min(320,safe.Width);captionCard.MaxHeight=safe.Height;captionCard.Visibility=Visibility.Visible;captionCard.Measure(new Size(captionCard.MaxWidth,safe.Height));
            var icon=new Rect((target.Item.Bounds.Left-originX)/scale,(target.Item.Bounds.Top-originY)/scale,target.Item.Bounds.Width/scale,target.DisplayHeightPixels/scale);
            var size=captionCard.DesiredSize;
            if(size.Height>Math.Max(icon.Top-safe.Top,safe.Bottom-icon.Bottom)-6) {captionCard.MaxWidth=safe.Width;captionCard.Measure(new Size(safe.Width,safe.Height));size=captionCard.DesiredSize;}
            var placed=PlaceCaption(icon,size,safe,6);captionCard.Width=placed.Width;captionCard.Height=placed.Height;Canvas.SetLeft(captionCard,placed.Left);Canvas.SetTop(captionCard,placed.Top);
            captionScreenBounds=new Rect(placed.Left*scale+originX,placed.Top*scale+originY,placed.Width*scale,placed.Height*scale);
        }
        internal static Rect PlaceCaption(Rect icon,Size desired,Rect safe,double gap) {
            double width=Math.Min(desired.Width,safe.Width),height=Math.Min(desired.Height,safe.Height),x=icon.Left+(icon.Width-width)/2,y=icon.Bottom+gap;
            if(y+height>safe.Bottom) {
                y=icon.Top-height-gap;
                if(y<safe.Top) {y=icon.Top;if(icon.Right+gap+width<=safe.Right)x=icon.Right+gap;else if(icon.Left-gap-width>=safe.Left)x=icon.Left-gap-width;}
            }
            x=Math.Max(safe.Left,Math.Min(safe.Right-width,x));y=Math.Max(safe.Top,Math.Min(safe.Bottom-height,y));return new Rect(x,y,width,height);
        }
        internal bool Forward(uint message,Point screen,int buttons,bool activate) {
            if(stopped || list==IntPtr.Zero)return false;
            if(activate)SetForegroundWindow(GetAncestor(list,2));
            var point=new NativePoint {X=(int)Math.Round(screen.X),Y=(int)Math.Round(screen.Y)};
            if(message!=0x20a)ScreenToClient(list,ref point);
            return PostMessage(list,message,new IntPtr(buttons),new IntPtr((point.X&0xffff)|((point.Y&0xffff)<<16)));
        }
        internal static Point NativeTarget(DesktopIconItem item,Point screen) {
            if(item.NativeBounds.Width<=0 || item.NativeBounds.Height<=0 || item.NativeBounds.Contains(screen))return screen;
            return item.NativeAnchor;
        }
        public void Stop() {stopped=true;timer.Stop();window.IsVisibleChanged-=VisibilityChanged;window.PreviewMouseDown-=NotePressed;layer.Children.Clear();backdrops.Children.Clear();iconSurface.Close();backdropSurface.Close();}
    }
    internal sealed class DesktopIconVisual : FrameworkElement {
        readonly DesktopIconOverlay owner;byte[] pixels;BitmapSource displayImage;bool starting;int pressed;Point? mappedPress;
        internal readonly DesktopIconItem Item;
        internal int DisplayHeightPixels {get;private set;}
        internal DesktopIconVisual(DesktopIconOverlay owner,DesktopIconItem item) {
            this.owner=owner;Item=item;UpdateAppearance();Cursor=Cursors.Arrow;SnapsToDevicePixels=true;RenderOptions.SetBitmapScalingMode(this,BitmapScalingMode.NearestNeighbor);
            AutomationProperties.SetName(this,item.Name);
        }
        internal void UpdateAppearance() {
            displayImage=Item.Selected && Item.CollapsedImage!=null ? Item.CollapsedImage:Item.Image;pixels=new byte[displayImage.PixelWidth*displayImage.PixelHeight*4];displayImage.CopyPixels(pixels,displayImage.PixelWidth*4,0);
            DisplayHeightPixels=1;bool found=false;for(int y=displayImage.PixelHeight-1;y>=0 && !found;y--)for(int x=0;x<displayImage.PixelWidth;x++)if(pixels[(y*displayImage.PixelWidth+x)*4+3]>0) {DisplayHeightPixels=y+1;found=true;break;}
            InvalidateVisual();
        }
        protected override void OnRender(DrawingContext dc) {double height=displayImage.PixelHeight*ActualHeight/Item.Image.PixelHeight,inkHeight=DisplayHeightPixels*ActualHeight/Item.Image.PixelHeight;if(Item.Selected)dc.DrawRoundedRectangle(null,new Pen(new SolidColorBrush(Color.FromArgb(180,116,171,255)),1),new Rect(.5,.5,Math.Max(0,ActualWidth-1),Math.Max(0,inkHeight-1)),3,3);dc.DrawImage(displayImage,new Rect(0,0,ActualWidth,height));}
        internal bool ContainsInk(Point point) {
            if(ActualWidth<=0 || ActualHeight<=0)return false;
            int x=(int)(point.X*Item.Image.PixelWidth/ActualWidth),y=(int)(point.Y*Item.Image.PixelHeight/ActualHeight);
            for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++) {int xx=x+dx,yy=y+dy;if(xx>=0 && xx<displayImage.PixelWidth && yy>=0 && yy<displayImage.PixelHeight && pixels[(yy*displayImage.PixelWidth+xx)*4+3]>32)return true;}return false;
        }
        protected override HitTestResult HitTestCore(PointHitTestParameters args) {return ContainsInk(args.HitPoint) ? new PointHitTestResult(this,args.HitPoint):null;}
        static int Buttons() {return (Mouse.LeftButton==MouseButtonState.Pressed ? 1:0)|(Mouse.RightButton==MouseButtonState.Pressed ? 2:0)|((Keyboard.Modifiers&ModifierKeys.Shift)!=0 ? 4:0)|((Keyboard.Modifiers&ModifierKeys.Control)!=0 ? 8:0)|(Mouse.MiddleButton==MouseButtonState.Pressed ? 16:0);}
        protected override void OnMouseDown(MouseButtonEventArgs e) {
            base.OnMouseDown(e);uint message=e.ChangedButton==MouseButton.Left ? (e.ClickCount%2==0 ? 0x203u:0x201u):e.ChangedButton==MouseButton.Right ? 0x204u:0x207u;
            var physical=PointToScreen(e.GetPosition(this));var target=DesktopIconOverlay.NativeTarget(Item,physical);mappedPress=target==physical ? (Point?)null:target;
            starting=true;pressed=Buttons();try {CaptureMouse();owner.RevealClickedItem(Item,(Keyboard.Modifiers&(ModifierKeys.Control|ModifierKeys.Shift))!=0);owner.Forward(message,target,pressed,true);}finally {starting=false;}e.Handled=true;
        }
        protected override void OnMouseMove(MouseEventArgs e) {base.OnMouseMove(e);if(IsMouseCaptured && !starting && pressed!=0) {owner.Forward(0x200,PointToScreen(e.GetPosition(this)),Buttons(),false);e.Handled=true;}}
        protected override void OnMouseUp(MouseButtonEventArgs e) {
            base.OnMouseUp(e);owner.Forward(e.ChangedButton==MouseButton.Left ? 0x202u:e.ChangedButton==MouseButton.Right ? 0x205u:0x208u,mappedPress ?? PointToScreen(e.GetPosition(this)),Buttons(),false);mappedPress=null;pressed=0;if(IsMouseCaptured)ReleaseMouseCapture();e.Handled=true;
        }
        protected override void OnMouseWheel(MouseWheelEventArgs e) {base.OnMouseWheel(e);owner.Forward(0x20a,PointToScreen(e.GetPosition(this)),Buttons()|((e.Delta&0xffff)<<16),false);e.Handled=true;}
    }
}

