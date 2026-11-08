using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace DesktopNotes {
    internal interface IHotkeyBackend {bool Register(int id,ShortcutGesture gesture);void Unregister(int id);}
    public sealed class GlobalHotkey : IDisposable {
        sealed class NativeBackend : IHotkeyBackend {
            [DllImport("user32.dll",SetLastError=true)]static extern bool RegisterHotKey(IntPtr hwnd,int id,uint modifiers,uint key);
            [DllImport("user32.dll")]static extern bool UnregisterHotKey(IntPtr hwnd,int id);
            readonly IntPtr handle;public NativeBackend(IntPtr handle) {this.handle=handle;}
            public bool Register(int id,ShortcutGesture gesture) {return RegisterHotKey(handle,id,gesture.Modifiers|0x4000,gesture.VirtualKey);}
            public void Unregister(int id) {UnregisterHotKey(handle,id);}
        }
        readonly IHotkeyBackend backend;readonly Action triggered;readonly HwndSource source;bool disposed;int activeId;
        public ShortcutGesture Gesture {get;private set;}public bool IsRegistered {get {return activeId!=0 && !disposed;}}
        internal int ActiveId {get {return activeId;}}
        public GlobalHotkey(Action triggered) : this(triggered,null) { }
        internal GlobalHotkey(Action triggered,IHotkeyBackend backend) {
            this.triggered=triggered;
            if(backend==null) {var parameters=new HwndSourceParameters("ZhuomianNotes.QuickAdd.Hotkey") {ParentWindow=new IntPtr(-3),WindowStyle=0,Width=0,Height=0};source=new HwndSource(parameters);source.AddHook(Hook);this.backend=new NativeBackend(source.Handle);}else this.backend=backend;
        }
        public bool TrySet(string value,out string error) {
            ShortcutGesture gesture;if(!ShortcutGesture.TryParse(value,out gesture,out error))return false;
            if(disposed) {error="快捷键服务已关闭。";return false;}
            if(IsRegistered && Gesture.Canonical==gesture.Canonical) {error=null;return true;}
            int next=activeId==0x5710 ? 0x5711:0x5710;
            if(!backend.Register(next,gesture)) {error="这个快捷键已被占用或暂不可用，请换一个组合。";return false;}
            int previous=activeId;activeId=next;Gesture=gesture;if(previous!=0)backend.Unregister(previous);error=null;return true;
        }
        IntPtr Hook(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled) {
            if(message==0x312) {long value=lParam.ToInt64();handled=Process((int)wParam.ToInt64(),(uint)(value&0xf),(uint)((value>>16)&0xffff));}return IntPtr.Zero;
        }
        internal bool Process(int id,uint modifiers,uint key) {if(!IsRegistered || id!=activeId || modifiers!=Gesture.Modifiers || key!=Gesture.VirtualKey)return false;triggered();return true;}
        public void Dispose() {if(disposed)return;disposed=true;if(activeId!=0)backend.Unregister(activeId);activeId=0;if(source!=null) {source.RemoveHook(Hook);source.Dispose();}}
    }
}
