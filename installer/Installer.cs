using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
[assembly: AssemblyTitle("TinyStar桌面便签安装")]
[assembly: AssemblyProduct("TinyStar桌面便签")]
[assembly: AssemblyVersion("1.1.8.0")]
[assembly: AssemblyFileVersion("1.1.8.0")]
class Installer {
    const string AppName="TinyStar桌面便签";
    static string ShellIconName {get {
        using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("Shell.ico"))
        using(var hash=System.Security.Cryptography.SHA256.Create())
            return "TinyStar-1.1.8-"+BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").Substring(0,8).ToLowerInvariant()+".ico";
    }}
    const string UninstallKey=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\ZhuomianNotes";
    static string Destination {get {return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","ZhuomianNotes");}}
    static string ShortcutFolder {get {return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),AppName);}}
    static void Extract(string path) {using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("App.exe"))using(var output=new FileStream(path,FileMode.Create,FileAccess.Write))input.CopyTo(output);}
    static void ExtractIcon(string path) {using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("Shell.ico"))using(var output=new FileStream(path,FileMode.Create,FileAccess.Write))input.CopyTo(output);}
    static string FileHash(string path) {
        using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))
        using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").ToLowerInvariant();
    }
    internal static string PrepareUninstaller(string folder,string source) {
        string hash=FileHash(source),version=Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
        string target=Path.Combine(folder,"TinyStar-Uninstall-"+version+"-"+hash.Substring(0,16)+".exe");
        if(File.Exists(target)) {if(FileHash(target)!=hash)throw new IOException("卸载程序校验失败，安装未继续。请关闭旧安装或卸载窗口后重试。");return target;}
        string temporary=target+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {File.Copy(source,temporary,false);if(FileHash(temporary)!=hash)throw new IOException("卸载程序写入不完整，安装未继续。");File.Move(temporary,target);return target;}
        finally {if(File.Exists(temporary))File.Delete(temporary);}
    }
    [System.Runtime.InteropServices.DllImport("shell32.dll")] static extern void SHChangeNotify(uint eventId,uint flags,IntPtr item1,IntPtr item2);
    static void Shortcut(string path,string target,string args) {
        Type type=Type.GetTypeFromProgID("WScript.Shell");dynamic shell=Activator.CreateInstance(type);dynamic link=shell.CreateShortcut(path);link.TargetPath=target;link.Arguments=args;link.WorkingDirectory=Destination;link.Description=AppName;link.IconLocation=Path.Combine(Destination,ShellIconName)+",0";link.Save();
    }
    // Only replace legacy shortcuts that actually belong to this installation.
    static void RemoveOwnedShortcut(string path,string expectedTarget) {
        if(!File.Exists(path))return;
        Type type=Type.GetTypeFromProgID("WScript.Shell");dynamic shell=Activator.CreateInstance(type);dynamic link=shell.CreateShortcut(path);
        string target=(string)link.TargetPath;
        if(!String.IsNullOrEmpty(target) && String.Equals(Path.GetFullPath(target),Path.GetFullPath(expectedTarget),StringComparison.OrdinalIgnoreCase))File.Delete(path);
    }
    static void RemoveLegacyShortcuts() {
        string oldFolder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"桌面便签");
        RemoveOwnedShortcut(Path.Combine(oldFolder,"桌面便签.lnk"),Path.Combine(Destination,"ZhuomianNotes.exe"));
        RemoveOwnedShortcut(Path.Combine(oldFolder,"卸载桌面便签.lnk"),Path.Combine(Destination,"Uninstall.exe"));
        RemoveOwnedShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"桌面便签.lnk"),Path.Combine(Destination,"ZhuomianNotes.exe"));
        if(Directory.Exists(oldFolder) && Directory.GetFileSystemEntries(oldFolder).Length==0)Directory.Delete(oldFolder);
    }
    static bool AppRunning() {
        string identity="Local\\ZhuomianNotes-"+System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
        try {using(var mutex=Mutex.OpenExisting(identity))return true;}catch(WaitHandleCannotBeOpenedException){}
        foreach(var process in Process.GetProcessesByName("ZhuomianNotes")) {
            try {if(String.Equals(process.MainModule.FileName,Path.Combine(Destination,"ZhuomianNotes.exe"),StringComparison.OrdinalIgnoreCase))return true;}catch {return true;}finally {process.Dispose();}
        }return false;
    }
    [STAThread] static int Main(string[] args) {
        Application.EnableVisualStyles();
        try {
            if(args.Length==2 && args[0]=="--verify-payload") {Extract(Path.GetFullPath(args[1]));return 0;}
            if(DesktopNotes.DesktopLaunchContext.EnsureDesktopContext(ref args))return 0;
            if(args.Length==2 && args[0]=="--remove") {int pid;if(!Int32.TryParse(args[1],out pid))return 1;try {using(var p=Process.GetProcessById(pid))p.WaitForExit(10000);}catch(ArgumentException){}Remove();return 0;}
            if(args.Length==1 && args[0]=="/uninstall") {
                if(MessageBox.Show("卸载TinyStar桌面便签？\n你的事项、分类和备份会保留。请先从托盘退出便签。",AppName,MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return 0;
                if(AppRunning())throw new IOException("便签还在运行。请从右下角托盘选择“退出”，再卸载。");
                string helper=Path.Combine(Path.GetTempPath(),"ZhuomianNotes-Uninstall-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(Assembly.GetExecutingAssembly().Location,helper);
                Process.Start(new ProcessStartInfo(helper,"--remove "+Process.GetCurrentProcess().Id) {UseShellExecute=true,WindowStyle=ProcessWindowStyle.Hidden});return 0;
            }
            if(MessageBox.Show("安装TinyStar桌面便签 1.1.8\n\n安装后可以在桌面上管理事项。\n安装位置："+Destination+"\n仅为当前用户安装，不需要管理员权限。",AppName+"安装",MessageBoxButtons.OKCancel,MessageBoxIcon.Information)!=DialogResult.OK)return 0;
            if(AppRunning())throw new IOException("便签还在运行。请先从托盘退出，再重新安装。原有内容会保留。");
            Directory.CreateDirectory(Destination);
            // Never overwrite a running or read-only legacy Uninstall.exe. Prepare
            // the new, verified uninstall entry before modifying the application.
            string uninstall=PrepareUninstaller(Destination,Assembly.GetExecutingAssembly().Location);
            string exe=Path.Combine(Destination,"ZhuomianNotes.exe");Extract(exe);
            ExtractIcon(Path.Combine(Destination,ShellIconName));
            Directory.CreateDirectory(ShortcutFolder);Shortcut(Path.Combine(ShortcutFolder,AppName+".lnk"),exe,"");Shortcut(Path.Combine(ShortcutFolder,"卸载TinyStar桌面便签.lnk"),uninstall,"/uninstall");
            Shortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),AppName+".lnk"),exe,"");
            RemoveLegacyShortcuts();
            using(var key=Registry.CurrentUser.CreateSubKey(UninstallKey)) {key.SetValue("DisplayName",AppName);key.SetValue("DisplayVersion","1.1.8");key.SetValue("Publisher","TinyStar桌面便签");key.SetValue("InstallLocation",Destination);key.SetValue("DisplayIcon",exe);key.SetValue("UninstallString","\""+uninstall+"\" /uninstall");key.SetValue("NoModify",1,RegistryValueKind.DWord);key.SetValue("NoRepair",1,RegistryValueKind.DWord);}
            using(var run=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true)) {if(run!=null && run.GetValue("ZhuomianNotes")!=null)run.SetValue("ZhuomianNotes","\""+exe+"\"");}
            SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);
            if(MessageBox.Show("安装完成。\n现在打开TinyStar桌面便签吗？",AppName,MessageBoxButtons.YesNo,MessageBoxIcon.Information)==DialogResult.Yes)DesktopNotes.DesktopLaunchContext.StartFromDesktop(exe,new string[0],1);return 0;
        }catch(Exception ex) {MessageBox.Show("未能完成：\n"+ex.Message,AppName,MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
    }
    static void Remove() {
        string expected=Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","ZhuomianNotes"));
        if(!String.Equals(Path.GetFullPath(Destination),expected,StringComparison.OrdinalIgnoreCase))throw new IOException("卸载位置校验失败。");
        if(AppRunning())throw new IOException("便签还在运行，请先退出。");
        foreach(string name in new[]{"ZhuomianNotes.exe","Uninstall.exe"}) {string file=Path.Combine(expected,name);if(File.Exists(file))File.Delete(file);}
        if(Directory.Exists(expected))foreach(string file in Directory.GetFiles(expected,"TinyStar-Uninstall-*.exe"))if(System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(file),@"^TinyStar-Uninstall-\d+\.\d+\.\d+-[a-f0-9]{16}\.exe$"))File.Delete(file);
        if(Directory.Exists(expected))foreach(string icon in Directory.GetFiles(expected,"TinyStar-*.ico"))if(System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(icon),@"^TinyStar-\d+\.\d+\.\d+(?:-[a-f0-9]{8})?\.ico$"))File.Delete(icon);
        foreach(string name in new[]{AppName+".lnk","卸载TinyStar桌面便签.lnk"}) {string file=Path.Combine(ShortcutFolder,name);if(File.Exists(file))File.Delete(file);}
        string desktop=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),AppName+".lnk");if(File.Exists(desktop))File.Delete(desktop);
        RemoveLegacyShortcuts();
        if(Directory.Exists(expected) && Directory.GetFileSystemEntries(expected).Length==0)Directory.Delete(expected);
        if(Directory.Exists(ShortcutFolder) && Directory.GetFileSystemEntries(ShortcutFolder).Length==0)Directory.Delete(ShortcutFolder);
        using(var run=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true))if(run!=null)run.DeleteValue("ZhuomianNotes",false);
        Registry.CurrentUser.DeleteSubKeyTree(UninstallKey,false);
        MessageBox.Show("TinyStar桌面便签已卸载。\n你的内容仍保存在：\n"+Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ZhuomianNotes"),AppName);
    }
}







