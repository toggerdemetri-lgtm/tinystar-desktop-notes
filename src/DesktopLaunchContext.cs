using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
namespace DesktopNotes {
    // Ask the existing desktop shell to launch desktop programs in its context.
    // A plain new Shell.Application may itself inherit the caller's MSIX identity.
    internal static class DesktopLaunchContext {
        const string Attempt="--tinystar-desktop-start";
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern uint GetFinalPathNameByHandle(IntPtr file,StringBuilder path,uint count,uint flags);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern int GetCurrentPackageFamilyName(ref uint length,StringBuilder name);
        internal static string PackageFamily {
            get {uint length=0;int result=GetCurrentPackageFamilyName(ref length,null);if(result==15700)return null;if(result!=122)throw new InvalidOperationException("无法检查程序启动环境。");var value=new StringBuilder((int)length);if(GetCurrentPackageFamilyName(ref length,value)!=0)throw new InvalidOperationException("无法检查程序启动环境。");return value.ToString();}
        }
        internal static bool EnsureDesktopContext(ref string[] args) {
            bool attempted=args.Contains(Attempt);args=args.Where(a=>a!=Attempt).ToArray();
            // Filesystem virtualization can be inherited even when package identity
            // APIs report no package. Always use the real desktop broker once.
            if(attempted) {
                if(HasForeignDataContext)throw new InvalidOperationException("请从桌面或开始菜单打开程序，避免使用启动工具的旧数据副本。");
                return false;
            }
            string target=Assembly.GetEntryAssembly().Location;
            StartFromDesktop(target,args.Concat(new[]{Attempt}).ToArray(),1);return true;
        }
        internal static bool HasForeignDataContext {
            get {
                string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if(PackageFamily!=null || local.IndexOf("\\Packages\\",StringComparison.OrdinalIgnoreCase)>=0)return true;
                string file=Path.Combine(local,"ZhuomianNotes","notes.json");
                if(!File.Exists(file))return false;
                var physical=new StringBuilder(4096);
                using(var input=File.Open(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)) {
                    if(GetFinalPathNameByHandle(input.SafeFileHandle.DangerousGetHandle(),physical,4096,0)==0)throw new IOException("无法验证数据文件位置。");
                }
                string final=physical.ToString();
                return final.IndexOf("\\Packages\\",StringComparison.OrdinalIgnoreCase)>=0 && final.IndexOf("\\LocalCache\\Local\\",StringComparison.OrdinalIgnoreCase)>=0;
            }
        }
        internal static string QuoteArgument(string argument) {
            var text=new StringBuilder("\"");int slashes=0;
            foreach(char c in argument) {if(c=='\\'){slashes++;continue;}if(c=='\"'){text.Append('\\',slashes*2+1).Append(c);slashes=0;continue;}text.Append('\\',slashes).Append(c);slashes=0;}
            return text.Append('\\',slashes*2).Append('"').ToString();
        }
        internal static void StartFromDesktop(string executable,string[] args,int show) {
            executable=Path.GetFullPath(executable);if(!File.Exists(executable))throw new FileNotFoundException("程序文件不存在。",executable);
            object windows=null,desktop=null,browser=null,view=null,folder=null,shell=null;
            try {
                windows=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39")));
                object location=0,unused=null;int hwnd;
                desktop=((dynamic)windows).FindWindowSW(ref location,ref unused,8,out hwnd,1);
                if(desktop==null)throw new InvalidOperationException("桌面暂时不可用，请从桌面快捷方式打开便签。");
                Guid service=new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837"),iid=typeof(ShellBrowser).GUID;
                browser=((ServiceProvider)desktop).QueryService(ref service,ref iid);
                view=((ShellBrowser)browser).QueryActiveShellView();Guid dispatch=new Guid("00020400-0000-0000-C000-000000000046");
                folder=((ShellView)view).GetItemObject(0,ref dispatch);shell=((dynamic)folder).Application;
                ((dynamic)shell).ShellExecute(executable,String.Join(" ",args.Select(QuoteArgument)),Path.GetDirectoryName(executable),"open",show);
            }finally {foreach(var value in new[]{shell,folder,view,browser,desktop,windows})if(value!=null && Marshal.IsComObject(value))try{Marshal.ReleaseComObject(value);}catch {}}
        }
        [ComImport,Guid("6D5140C1-7436-11CE-8034-00AA006009FA"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface ServiceProvider {
            [return:MarshalAs(UnmanagedType.Interface)]object QueryService(ref Guid service,ref Guid iid);
        }
        [ComImport,Guid("000214E2-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface ShellBrowser {
            void GetWindow();void ContextSensitiveHelp();void InsertMenus();void SetMenu();void RemoveMenus();void SetStatusText();void EnableModeless();void TranslateAccelerator();void BrowseObject();void GetViewStateStream();void GetControlWindow();void SendControlMessage();
            ShellView QueryActiveShellView();
        }
        [ComImport,Guid("000214E3-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]interface ShellView {
            void GetWindow();void ContextSensitiveHelp();void TranslateAccelerator();void EnableModeless();void Activate();void Refresh();void CreateViewWindow();void DestroyViewWindow();void GetCurrentInfo();void AddPropertyPages();void SaveViewState();void SelectItem();
            [return:MarshalAs(UnmanagedType.IDispatch)]object GetItemObject(uint aspect,ref Guid iid);
        }
    }
}

