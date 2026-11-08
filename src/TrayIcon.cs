using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Forms=System.Windows.Forms;
namespace DesktopNotes {
    public sealed class TrayIcon : Forms.NativeWindow,IDisposable {
        const int Callback=0x8001;
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct Data {
            public uint Size;public IntPtr Window;public uint Id,Flags,Message;public IntPtr Icon;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Tip;
            public uint State,StateMask;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Info;
            public uint Version;
            [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)]public string InfoTitle;
            public uint InfoFlags;public Guid Guid;public IntPtr BalloonIcon;
        }
        [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern bool Shell_NotifyIcon(uint operation,ref Data data);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern uint RegisterWindowMessage(string message);
        [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")]static extern bool PostMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
        readonly uint taskbarCreated;readonly Forms.Timer retry=new Forms.Timer {Interval=2000};
        bool visible,disposed,version4;string text="";Icon icon;
        public readonly Guid Identity;
        public bool Registered {get;private set;}
        public Forms.ContextMenuStrip ContextMenuStrip {get;set;}
        public event EventHandler DoubleClick;
        public static Guid IdentityForPath(string path) {using(var sha=SHA256.Create()) {var hash=sha.ComputeHash(Encoding.UTF8.GetBytes("TinyStar.Tray.v1:"+Path.GetFullPath(path).ToUpperInvariant()));var bytes=new byte[16];Array.Copy(hash,bytes,16);return new Guid(bytes);}}
        public TrayIcon() : this(IdentityForPath(System.Reflection.Assembly.GetEntryAssembly().Location)) {}
        public TrayIcon(Guid identity) {
            Identity=identity;taskbarCreated=RegisterWindowMessage("TaskbarCreated");
            CreateHandle(new Forms.CreateParams {Caption="TinyStar通知图标",Style=unchecked((int)0x80000000),ExStyle=0x80});
            retry.Tick+=delegate {if(disposed || !visible || Registered)retry.Stop();else Update();};
        }
        public string Text {get{return text;}set{text=value ?? "";if(text.Length>127)text=text.Substring(0,127);Update();}}
        public Icon Icon {get{return icon;}set{icon=value;Update();}}
        public bool Visible {get{return visible;}set{if(disposed)return;visible=value;Update();}}
        Data State() {return new Data {Size=(uint)Marshal.SizeOf(typeof(Data)),Window=Handle,Id=1,Flags=1|2|4|0x20|0x80,Message=Callback,Icon=icon==null ? IntPtr.Zero:icon.Handle,Tip=text,Info="",InfoTitle="",Guid=Identity};}
        void Update() {
            if(disposed || Handle==IntPtr.Zero)return;var data=State();
            if(!visible || icon==null) {if(Registered)Shell_NotifyIcon(2,ref data);Registered=false;retry.Stop();return;}
            if(Registered && Shell_NotifyIcon(1,ref data))return;
            Registered=Shell_NotifyIcon(0,ref data);if(Registered) {data.Version=4;version4=Shell_NotifyIcon(4,ref data);retry.Stop();}else retry.Start();
        }
        protected override void WndProc(ref Forms.Message message) {
            if(taskbarCreated!=0 && message.Msg==taskbarCreated) {Registered=false;Update();}
            else if(message.Msg==Callback) {
                int action=version4 ? (int)(message.LParam.ToInt64()&0xffff):message.LParam.ToInt32();
                if(action==0x203 || action==0x401) {var handler=DoubleClick;if(handler!=null)handler(this,EventArgs.Empty);}
                else if(action==0x7b || (!version4 && action==0x205)) {
                    var menu=ContextMenuStrip;if(menu!=null) {SetForegroundWindow(Handle);menu.Show(Forms.Cursor.Position);PostMessage(Handle,0,IntPtr.Zero,IntPtr.Zero);}
                }
            }
            base.WndProc(ref message);
        }
        public void Dispose() {if(disposed)return;visible=false;Update();disposed=true;retry.Dispose();if(ContextMenuStrip!=null)ContextMenuStrip.Dispose();DestroyHandle();}
    }
}
