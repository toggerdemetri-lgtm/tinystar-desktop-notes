using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Forms=System.Windows.Forms;
namespace DesktopNotes {
    // A stationary desktop surface. Only its native clip changes as the note moves;
    // its icon pixels never need a delayed WPF counter-translation.
    internal sealed class DesktopIconSurface {
        [DllImport("user32.dll")]static extern IntPtr GetParent(IntPtr h);
        [DllImport("user32.dll")]static extern IntPtr SetParent(IntPtr h,IntPtr parent);
        [DllImport("user32.dll")]static extern int GetWindowLong(IntPtr h,int index);
        [DllImport("user32.dll")]static extern int SetWindowLong(IntPtr h,int index,int value);
        [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int width,int height,uint flags);
        [DllImport("user32.dll")]static extern bool ScreenToClient(IntPtr h,ref NativePoint point);
        [DllImport("user32.dll")]static extern bool GetWindowRect(IntPtr h,out NativeRect rect);
        [DllImport("user32.dll")]static extern int SetWindowRgn(IntPtr h,IntPtr region,bool redraw);
        [DllImport("gdi32.dll")]static extern IntPtr CreateRoundRectRgn(int left,int top,int right,int bottom,int ellipseWidth,int ellipseHeight);
        [DllImport("gdi32.dll")]static extern bool DeleteObject(IntPtr region);
        [StructLayout(LayoutKind.Sequential)]struct NativePoint {public int X,Y;}
        [StructLayout(LayoutKind.Sequential)]struct NativeRect {public int Left,Top,Right,Bottom;}
        readonly Window note,surface;readonly bool background;IntPtr handle;System.Drawing.Rectangle desktop;
        HwndSource nativeSource;
        bool IsClosed {get {return closed || nativeSource!=null && nativeSource.IsDisposed;}}
        IntPtr currentParent;NativeRect previousBounds;double previousScale;bool positioned,hasClip,closed;
        internal int ClipUpdates {get;private set;}
        internal int PlacementUpdates {get;private set;}
        internal readonly Canvas Canvas=new Canvas {ClipToBounds=true};
        internal double Scale {get;private set;}
        internal double OriginX {get {return desktop.Left;}}
        internal double OriginY {get {return desktop.Top;}}
        internal DesktopIconSurface(Window note,bool background) {
            this.note=note;this.background=background;Scale=DesktopHost.WindowScale(note);
            surface=new Window {Title=background ? "便签背景合成层":"便签图标合成层",WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,ShowInTaskbar=false,ShowActivated=false,Focusable=false,Content=Canvas};
            surface.Closed+=delegate {closed=true;};
            Canvas.IsHitTestVisible=!background;surface.Show();handle=new WindowInteropHelper(surface).Handle;
            nativeSource=HwndSource.FromHwnd(handle);if(nativeSource!=null)nativeSource.Disposed+=delegate {closed=true;};
            int style=GetWindowLong(handle,-16);SetWindowLong(handle,-16,(style&~unchecked((int)0x80000000))|0x40000000);
            SetWindowLong(handle,-20,GetWindowLong(handle,-20)|0x80|0x08000000|(background ? 0x20:0));
            Update();
        }
        internal void Update() {
            if(IsClosed)return;
            var noteHandle=new WindowInteropHelper(note).Handle;var parent=GetParent(noteHandle);bool parentChanged=!positioned || currentParent!=parent;
            if(GetParent(handle)!=parent) {SetParent(handle,parent);parentChanged=true;}
            double scale=DesktopHost.WindowScale(surface);bool scaleChanged=Math.Abs(Scale-scale)>.001;Scale=scale;
            var screen=Forms.SystemInformation.VirtualScreen;
            if(!positioned || screen!=desktop || parentChanged || scaleChanged) {
                desktop=screen;surface.Width=desktop.Width/Scale;surface.Height=desktop.Height/Scale;
                var point=new NativePoint {X=desktop.Left,Y=desktop.Top};if(parent!=IntPtr.Zero)ScreenToClient(parent,ref point);
                SetWindowPos(handle,background ? noteHandle:IntPtr.Zero,point.X,point.Y,desktop.Width,desktop.Height,0x10|0x20);PlacementUpdates++;currentParent=parent;positioned=true;hasClip=false;
            }
            NativeRect bounds;if(!GetWindowRect(noteHandle,out bounds))return;
            if(hasClip && previousBounds.Left==bounds.Left && previousBounds.Top==bounds.Top && previousBounds.Right==bounds.Right && previousBounds.Bottom==bounds.Bottom && previousScale==Scale)return;
            int margin=(int)Math.Round(4*Scale),diameter=(int)Math.Round(2*UiDesign.NoteCornerRadius*Scale);
            var region=CreateRoundRectRgn(bounds.Left-desktop.Left+margin,bounds.Top-desktop.Top+margin,bounds.Right-desktop.Left-margin,bounds.Bottom-desktop.Top-margin,diameter,diameter);
            if(region!=IntPtr.Zero) {
                if(SetWindowRgn(handle,region,true)==0)DeleteObject(region);
                else {previousBounds=bounds;previousScale=Scale;hasClip=true;ClipUpdates++;}
            }
        }
        internal void ResetPlacement() {positioned=false;hasClip=false;}
        internal void SetVisible(bool visible) {if(IsClosed)return;if(visible) {surface.Show();ResetPlacement();Update();}else surface.Hide();}
        internal void Close() {if(!IsClosed)surface.Close();}
    }
}
