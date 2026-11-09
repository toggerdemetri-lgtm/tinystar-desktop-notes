using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopNotes;
using Forms=System.Windows.Forms;
class WindowResourceProbe {
    [DllImport("kernel32.dll",EntryPoint="SetLastError",SetLastError=true)] static extern void ClearLastError(uint error);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern int GetClipboardFormatName(uint atom,StringBuilder name,int length);
    [DllImport("user32.dll")] static extern uint GetGuiResources(IntPtr process,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateWindowEx(int ex,string cls,string name,int style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr param);
    [DllImport("user32.dll",EntryPoint="SendMessageW")] static extern IntPtr InsertItem(IntPtr h,uint message,IntPtr w,ref ListItem item);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h,int command);
    [DllImport("user32.dll")] static extern bool DestroyWindow(IntPtr h);
    [DllImport("comctl32.dll")] static extern bool InitCommonControlsEx(ref CommonControls controls);
    [StructLayout(LayoutKind.Sequential)] struct CommonControls {public int Size,Classes;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct ListItem {public uint Mask;public int Item,SubItem;public uint State,StateMask;[MarshalAs(UnmanagedType.LPWStr)]public string Text;public int TextMax,Image;public IntPtr Param;public int Indent,Group,Columns;public IntPtr ColumnIndices,ColumnFormats;public int GroupIndex;}
    static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static int TotalAtoms(out int own) {
        int total=0;own=0;var text=new StringBuilder(1024);
        string prefix="HwndWrapper["+AppDomain.CurrentDomain.FriendlyName+";";
        for(uint atom=0xC000;atom<=0xFFFF;atom++) {text.Clear();if(GetClipboardFormatName(atom,text,text.Capacity)>0) {total++;if(text.ToString().StartsWith(prefix,StringComparison.Ordinal))own++;}}
        ClearLastError(0);return total;
    }
    static void Pump() {
        var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate {frame.Continue=false;}));Dispatcher.PushFrame(frame);
    }
    static void Check(bool ok,string message) {if(!ok)throw new Exception(message);}
    static void RefreshAndWait(DesktopIconOverlay overlay,bool redraw) {
        var type=typeof(DesktopIconOverlay);
        if(redraw) {
            type.GetField("imageEpoch",Private).SetValue(overlay,DateTime.UtcNow.Ticks/TimeSpan.FromSeconds(5).Ticks);
            type.GetField("previousSignature",Private).SetValue(overlay,null);
            ((System.Collections.IDictionary)type.GetField("images",Private).GetValue(overlay)).Clear();
            type.GetField("paths",Private).SetValue(overlay,new Dictionary<string,string>());
        }
        type.GetMethod("Refresh",Private).Invoke(overlay,null);Wait(overlay);
    }
    static void Wait(DesktopIconOverlay overlay) {
        var busy=typeof(DesktopIconOverlay).GetField("busy",Private);var deadline=Stopwatch.StartNew();
        while((int)busy.GetValue(overlay)!=0) {Check(deadline.ElapsedMilliseconds<15000,"Refresh timed out");Pump();Thread.Sleep(10);}Pump();
    }
    static void PrepareWallpaper(DesktopIconOverlay overlay) {
        var wallpaper=typeof(DesktopIconOverlay).GetField("wallpaper",Private).GetValue(overlay);var type=wallpaper.GetType();
        var monitorType=type.GetNestedType("Monitor",BindingFlags.NonPublic);var monitor=Activator.CreateInstance(monitorType,true);
        var pixels=new byte[16*16*4];for(int p=0;p<pixels.Length;p+=4){pixels[p]=90;pixels[p+1]=50;pixels[p+2]=20;pixels[p+3]=255;}
        var image=BitmapSource.Create(16,16,96,96,System.Windows.Media.PixelFormats.Bgra32,null,pixels,64);image.Freeze();
        var bounds=new Rect(-5000,-5000,2000,2000);monitorType.GetField("Bounds").SetValue(monitor,bounds);monitorType.GetField("Image").SetValue(monitor,image);
        ((System.Collections.IList)type.GetField("monitors",Private).GetValue(wallpaper)).Add(monitor);
        type.GetField("position",Private).SetValue(wallpaper,2);type.GetField("span",Private).SetValue(wallpaper,bounds);type.GetProperty("Ready",Private).SetValue(wallpaper,true,null);
    }
    static void VerifyPixels(BitmapSource image) {
        Check(image!=null && image.IsFrozen,"Worker bitmap must be present and frozen");
        var visual=new System.Windows.Media.DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawImage(image,new Rect(0,0,image.PixelWidth,image.PixelHeight));
        var rendered=new RenderTargetBitmap(image.PixelWidth,image.PixelHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);rendered.Render(visual);
        byte[] pixels=new byte[rendered.PixelWidth*rendered.PixelHeight*4];rendered.CopyPixels(pixels,rendered.PixelWidth*4,0);Check(pixels.Any(b=>b!=0),"Composed pixels must render on main thread after worker shutdown");
    }
    [STAThread] static int Main(string[] args) {
        if(DesktopLaunchContext.EnsureDesktopContext(ref args))return 0;
        int result=2;
        using(var log=new System.IO.StreamWriter(args[1],false,Encoding.UTF8) {AutoFlush=true}) {
            Console.SetOut(log);
            try {result=Run(args);}
            catch(Exception e) {Console.WriteLine(e.GetType().FullName+": "+e.Message);Console.WriteLine(e.StackTrace);var native=e as System.ComponentModel.Win32Exception;if(native!=null)Console.WriteLine("NativeError="+native.NativeErrorCode);}
        }
        System.IO.File.WriteAllText(args[1]+".exit",result.ToString());return result;
    }
    static int Run(string[] args) {
        int rounds=args.Length==0 ? 30:Int32.Parse(args[0]);if(rounds<1 || rounds>150)throw new ArgumentOutOfRangeException("rounds");
        var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var grid=new Grid();var note=new Window {Content=grid,ShowInTaskbar=false};new WindowInteropHelper(note).EnsureHandle();
        var fixture=new Forms.Form {ShowInTaskbar=false,FormBorderStyle=Forms.FormBorderStyle.None,StartPosition=Forms.FormStartPosition.Manual,Left=-4000,Top=-4000,Width=200,Height=200};
        var controls=new CommonControls {Size=8,Classes=1};InitCommonControlsEx(ref controls);fixture.Show();
        IntPtr list=CreateWindowEx(0,"SysListView32","",0x50000000,0,0,180,180,fixture.Handle,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero);Check(list!=IntPtr.Zero,"Synthetic native list creation failed");
        var item=new ListItem {Mask=1,Text="TinyStar synthetic resource probe"};Check(InsertItem(list,0x104D,IntPtr.Zero,ref item).ToInt64()>=0,"Synthetic list insertion failed");
        DesktopIconOverlay overlay=null;
        try {
            overlay=new DesktopIconOverlay(note,grid,fixture.Handle,false);PrepareWallpaper(overlay);Pump();
            var type=typeof(DesktopIconOverlay);
            int warmOwn=0,warmTotal=0;
            var samples=new List<int>();
            for(int i=0;i<rounds+2;i++) {
                int own,total=TotalAtoms(out own);Check(total<4000,"Safety stop: shared USER atom count too high");
                RefreshAndWait(overlay,true);
                Check(overlay.LastError==null,"Refresh failed: "+overlay.LastError);Check(overlay.ItemCount==1,"Synthetic list item was not composed");
                if(i==1) {warmTotal=TotalAtoms(out warmOwn);Console.WriteLine("BASELINE totalAtoms={0} ownWpfClasses={1}",warmTotal,warmOwn);}
                else if(i>1) {total=TotalAtoms(out own);if(i==rounds+1 || (i-1)%10==0)Console.WriteLine("SAMPLE rounds={0} totalAtoms={1} ownWpfClasses={2} USER={3} GDI={4}",i-1,total,own,GetGuiResources(Process.GetCurrentProcess().Handle,1),GetGuiResources(Process.GetCurrentProcess().Handle,0));}
            }
            foreach(DesktopIconVisual visual in overlay.IconCanvas.Children.OfType<DesktopIconVisual>()) {
                VerifyPixels(visual.Item.Image);VerifyPixels(visual.Item.Backdrop);
            }
            int finalOwn,finalTotal=TotalAtoms(out finalOwn);Console.WriteLine("RESULT rounds={0} ownClassGrowth={1} sharedAtomGrowth={2}",rounds,finalOwn-warmOwn,finalTotal-warmTotal);
            Check(finalOwn-warmOwn<=4,"WPF window class registrations keep growing after completed icon refreshes");
            for(int i=0;i<10;i++)RefreshAndWait(overlay,false);
            Check(overlay.ItemCount==1 && overlay.LastError==null,"Unchanged refresh must preserve icons");
            ShowWindow(list,0);RefreshAndWait(overlay,false);Check(overlay.ItemCount==0,"Hidden native list must clear the icon layer");
            ShowWindow(list,5);RefreshAndWait(overlay,true);Check(overlay.ItemCount==1 && overlay.LastError==null,"Visible native list must recover");
            var listField=type.GetField("list",Private);listField.SetValue(overlay,IntPtr.Zero);RefreshAndWait(overlay,false);
            Check(overlay.LastError=="Desktop icon list unavailable","Invalid list must reach the expected exception path");
            listField.SetValue(overlay,list);RefreshAndWait(overlay,true);Check(overlay.LastError==null && overlay.ItemCount==1,"Refresh must recover after failure");
            type.GetField("previousSignature",Private).SetValue(overlay,null);type.GetMethod("Refresh",Private).Invoke(overlay,null);overlay.Stop();Wait(overlay);
            Check(overlay.IconCanvas.Children.Count==0 && overlay.BackdropCanvas.Children.Count==0,"Stopping during a refresh must not repopulate closed surfaces");
            TotalAtoms(out finalOwn);Check(finalOwn-warmOwn<=4,"Early-return, error or stop path leaked WPF classes");
            Console.WriteLine("PASS: cached, hidden, failed, recovered and in-flight stop paths release resources");
            Console.WriteLine("PASS: real icon and synthetic wallpaper pixels render after worker shutdown");return 0;
        } catch(Exception error) {Console.WriteLine("FAIL: "+error.Message);return 1;}
        finally {if(overlay!=null)overlay.Stop();DestroyWindow(list);fixture.Dispose();note.Close();app.Shutdown();}
    }
}
