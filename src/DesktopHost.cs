using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
namespace DesktopNotes {
    // All Explorer-specific integration is isolated here; do not modify Explorer.
    public sealed class DesktopHost {
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls, string title);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
        delegate bool EnumProc(IntPtr hwnd,IntPtr param);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr param);
        [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr hwnd);
        [DllImport("user32.dll", SetLastError=true)] static extern IntPtr SetParent(IntPtr child, IntPtr parent);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hwnd);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int w, int h, uint flags);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
        [DllImport("user32.dll")] static extern bool ScreenToClient(IntPtr hwnd, ref Point pt);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out Point pt);
        [DllImport("user32.dll")] static extern uint GetDpiForSystem();
        [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
        [StructLayout(LayoutKind.Sequential)] struct Point { public int X,Y; }
        [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left,Top,Right,Bottom; }
        Window window; IntPtr handle, parent; DispatcherTimer timer;bool stopped;
        double[] lastBounds;
        DesktopIconOverlay iconOverlay;
        public bool Attached { get; private set; }
        public bool TestMode { get; private set; }
        public static double SystemScale { get { try { return GetDpiForSystem()/96.0; } catch(EntryPointNotFoundException) { return 1; } } }
        public static double WindowScale(Window target) {
            var source=PresentationSource.FromVisual(target);if(source!=null && source.CompositionTarget!=null)return source.CompositionTarget.TransformToDevice.M11;
            try {uint dpi=GetDpiForWindow(new WindowInteropHelper(target).Handle);if(dpi>0)return dpi/96.0;}catch(EntryPointNotFoundException){}return SystemScale;
        }
        public double Scale {get {return WindowScale(window);}}
        public void Attach(Window window, bool testMode) {
            this.window = window; TestMode = testMode; handle = new WindowInteropHelper(window).Handle;
            lastBounds=new[]{window.Left,window.Top,window.Width,window.Height};Bounds();
            if (!testMode) SetWindowLong(handle, -20, GetWindowLong(handle, -20) | 0x80); // tool window, no taskbar entry
            if (!testMode) Reattach();
            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += delegate { if (!TestMode && (!IsWindow(parent) || GetParent(handle) != parent)) Reattach(); };
            timer.Start();
        }
        public void Reattach() {
            if(stopped || !IsWindow(handle)) {Attached=false;return;}
            // Keep the working desktop attachment; transparent icon artwork is
            // composited above the note instead of exposing native wallpaper holes.
            var desktop = FindWindowEx(FindWindow("Progman",null),IntPtr.Zero,"SHELLDLL_DefView",null);
            if(desktop==IntPtr.Zero) EnumWindows(delegate(IntPtr candidate,IntPtr unused) {
                var view=FindWindowEx(candidate,IntPtr.Zero,"SHELLDLL_DefView",null);
                if(view!=IntPtr.Zero) {desktop=view;return false;}return true;
            },IntPtr.Zero);
            if (desktop == IntPtr.Zero) { Attached = false; return; }
            Rect rect; GetWindowRect(handle, out rect);
            // Child of the desktop host survives Show Desktop while other app windows cover it.
            int style = GetWindowLong(handle, -16); SetWindowLong(handle, -16, (style & ~unchecked((int)0x80000000)) | 0x40000000);
            SetParent(handle, desktop); parent = desktop; Attached = GetParent(handle) == desktop;
            var point = new Point { X = rect.Left, Y = rect.Top }; ScreenToClient(desktop, ref point);
            SetWindowPos(handle, IntPtr.Zero, point.X, point.Y, rect.Right-rect.Left, rect.Bottom-rect.Top, 0x10 | 0x20 | 0x40);
            if(iconOverlay==null)iconOverlay=new DesktopIconOverlay(window,(System.Windows.Controls.Grid)window.Content,desktop);else iconOverlay.SetView(desktop);
            SetIconLabelTheme(((MainWindow)window).Palette.IsDark);
        }
        public double[] Bounds() {
            Rect r;if(!stopped && GetWindowRect(handle,out r) && r.Right>r.Left && r.Bottom>r.Top) {
                var source=PresentationSource.FromVisual(window);double scale=source==null ? SystemScale:source.CompositionTarget.TransformToDevice.M11;
                lastBounds=new[]{r.Left/scale,r.Top/scale,(r.Right-r.Left)/scale,(r.Bottom-r.Top)/scale};
            }
            return lastBounds ?? new[]{window.Left,window.Top,window.Width,window.Height};
        }
        public void Move(double x, double y) { var source = PresentationSource.FromVisual(window); double scale = source == null ? 1 : source.CompositionTarget.TransformToDevice.M11; var p = new Point { X=(int)(x*scale),Y=(int)(y*scale) }; if (Attached) ScreenToClient(parent, ref p); SetWindowPos(handle, IntPtr.Zero, p.X,p.Y,0,0,0x1|0x10|0x4);if(iconOverlay!=null)iconOverlay.Apply(); }
        public void UpdateIconLayer() {if(iconOverlay!=null)iconOverlay.Apply();}
        public void SetIconLabelTheme(bool dark) {if(iconOverlay!=null)iconOverlay.SetLabelTheme(dark);}
        public double[] Cursor() { Point p; GetCursorPos(out p); var source = PresentationSource.FromVisual(window); double scale= source == null ? 1 : source.CompositionTarget.TransformToDevice.M11; return new []{p.X/scale,p.Y/scale}; }
        public void Stop() {if(stopped)return;stopped=true;Attached=false;if(timer!=null)timer.Stop();if(iconOverlay!=null)iconOverlay.Stop();}
    }
}
