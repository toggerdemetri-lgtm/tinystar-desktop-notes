using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace DesktopNotes {
    // Read-only wallpaper reconstruction, used behind the note to suppress the
    // original icon shining through its translucent background. No screen capture.
    internal sealed class DesktopWallpaperSource {
        [StructLayout(LayoutKind.Sequential)]struct NativeRect {public int Left,Top,Right,Bottom;}
        [ComImport,Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface IDesktopWallpaper {
            void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)]string monitor,[MarshalAs(UnmanagedType.LPWStr)]string path);
            void GetWallpaper([MarshalAs(UnmanagedType.LPWStr)]string monitor,[MarshalAs(UnmanagedType.LPWStr)]out string path);
            void GetMonitorDevicePathAt(uint index,[MarshalAs(UnmanagedType.LPWStr)]out string monitor);
            void GetMonitorDevicePathCount(out uint count);
            void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)]string monitor,out NativeRect rect);
            void SetBackgroundColor(uint color);void GetBackgroundColor(out uint color);void SetPosition(int position);void GetPosition(out int position);
        }
        sealed class Monitor {public Rect Bounds;public BitmapSource Image;}
        sealed class CachedImage {public long Stamp;public BitmapSource Image;}
        readonly Dictionary<string,CachedImage> cache=new Dictionary<string,CachedImage>(StringComparer.OrdinalIgnoreCase);
        readonly List<Monitor> monitors=new List<Monitor>();Rect span;int position;Color color;
        internal bool Ready {get;private set;}
        internal void Refresh() {
            IDesktopWallpaper desktop=null;Ready=false;monitors.Clear();span=Rect.Empty;
            try {
                desktop=(IDesktopWallpaper)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD")));
                uint count,rgb;desktop.GetMonitorDevicePathCount(out count);desktop.GetPosition(out position);desktop.GetBackgroundColor(out rgb);color=Color.FromRgb((byte)rgb,(byte)(rgb>>8),(byte)(rgb>>16));
                for(uint i=0;i<count;i++) {
                    string id,path;NativeRect rect;desktop.GetMonitorDevicePathAt(i,out id);desktop.GetMonitorRECT(id,out rect);desktop.GetWallpaper(id,out path);
                    BitmapSource image=null;
                    if(!String.IsNullOrWhiteSpace(path)) {
                        long stamp=File.GetLastWriteTimeUtc(path).Ticks;CachedImage entry;
                        if(!cache.TryGetValue(path,out entry) || entry.Stamp!=stamp) {
                            var bitmap=new BitmapImage();using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();}
                            entry=new CachedImage {Stamp=stamp,Image=bitmap};cache[path]=entry;
                        }
                        image=entry.Image;
                    }
                    var bounds=new Rect(rect.Left,rect.Top,rect.Right-rect.Left,rect.Bottom-rect.Top);span.Union(bounds);monitors.Add(new Monitor {Bounds=bounds,Image=image});
                }
                Ready=monitors.Count>0;
                var used=new HashSet<BitmapSource>();foreach(var monitor in monitors)if(monitor.Image!=null)used.Add(monitor.Image);
                var expired=new List<string>();foreach(var pair in cache)if(!used.Contains(pair.Value.Image))expired.Add(pair.Key);foreach(var path in expired)cache.Remove(path);
            }catch {Ready=false;monitors.Clear();}finally {if(desktop!=null)Marshal.ReleaseComObject(desktop);}
        }
        internal BitmapSource Crop(Rect area) {
            if(!Ready)return null;
            var visual=new DrawingVisual();RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.HighQuality);
            using(var dc=visual.RenderOpen()) {
                dc.PushTransform(new TranslateTransform(-area.Left,-area.Top));dc.DrawRectangle(new SolidColorBrush(color),null,area);
                foreach(var monitor in monitors) {
                    if(!area.IntersectsWith(monitor.Bounds) || monitor.Image==null)continue;
                    dc.PushClip(new RectangleGeometry(monitor.Bounds));var image=monitor.Image;var screen=position==5 ? span:monitor.Bounds;
                    if(position==1) {
                        var brush=new ImageBrush(image) {TileMode=TileMode.Tile,ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,image.PixelWidth,image.PixelHeight),Stretch=Stretch.Fill};dc.DrawRectangle(brush,null,monitor.Bounds);
                    }else dc.DrawImage(image,ImageBounds(screen,image.PixelWidth,image.PixelHeight,position));
                    dc.Pop();
                }
                dc.Pop();
            }
            var result=new RenderTargetBitmap(Math.Max(1,(int)Math.Ceiling(area.Width)),Math.Max(1,(int)Math.Ceiling(area.Height)),96,96,PixelFormats.Pbgra32);result.Render(visual);result.Freeze();return result;
        }
        internal static Rect ImageBounds(Rect screen,double width,double height,int mode) {
            if(mode==2)return screen;
            double factor=mode==0 ? 1:mode==3 ? Math.Min(screen.Width/width,screen.Height/height):Math.Max(screen.Width/width,screen.Height/height);
            double w=width*factor,h=height*factor;return new Rect(Math.Round(screen.Left+(screen.Width-w)/2),Math.Round(screen.Top+(screen.Height-h)/2),Math.Round(w),Math.Round(h));
        }
    }
}
