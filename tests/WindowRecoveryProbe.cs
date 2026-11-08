using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using DesktopNotes;
using Forms=System.Windows.Forms;

// Tests only private native parents and an isolated data directory.
class WindowRecoveryProbe {
    [DllImport("user32.dll")]static extern IntPtr SetParent(IntPtr child,IntPtr parent);
    [DllImport("user32.dll")]static extern IntPtr GetParent(IntPtr child);
    [DllImport("user32.dll")]static extern int GetWindowLong(IntPtr window,int index);
    [DllImport("user32.dll")]static extern int SetWindowLong(IntPtr window,int index,int value);
    sealed class PrivateHost:Forms.NativeWindow,IDisposable {
        public PrivateHost(){CreateHandle(new Forms.CreateParams {Caption="Private recovery test parent",Style=unchecked((int)0x80000000)});}
        public void Dispose(){if(Handle!=IntPtr.Zero)DestroyHandle();}
    }
    static readonly BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Field(object target,string name){return target.GetType().GetField(name,Hidden).GetValue(target);}
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS: "+text);}
    static void Pump(){var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(delegate{frame.Continue=false;}));Dispatcher.PushFrame(frame);}
    static void Reparent(MainWindow main,IntPtr parent) {
        var handle=new WindowInteropHelper(main).Handle;
        SetWindowLong(handle,-16,(GetWindowLong(handle,-16)&~unchecked((int)0x80000000))|0x40000000);
        SetParent(handle,parent);Check(GetParent(handle)==parent,"test owns the parent it destroys");
        foreach(var layer in Application.Current.Windows.OfType<Window>().Where(w=>w.Title=="便签背景合成层" || w.Title=="便签图标合成层").ToArray())SetParent(new WindowInteropHelper(layer).Handle,parent);
    }
    static void DestroyParent(MainWindow main,bool disposeSource=false,bool killProcess=false) {
        if(disposeSource) {
            foreach(var layer in Application.Current.Windows.OfType<Window>().Where(w=>w.Title=="便签背景合成层" || w.Title=="便签图标合成层").ToArray())HwndSource.FromHwnd(new WindowInteropHelper(layer).Handle).Dispose();
            HwndSource.FromHwnd(new WindowInteropHelper(main).Handle).Dispose();return;
        }
        if(killProcess) {
            string handshake=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"private-parent-"+Guid.NewGuid().ToString("N")+".txt");
            var start=new ProcessStartInfo(Assembly.GetExecutingAssembly().Location,"--parent-helper \""+handshake+"\"") {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden};
            using(var helper=Process.Start(start)) {
                try {
                    var deadline=DateTime.UtcNow.AddSeconds(5);
                    while(!File.Exists(handshake) && !helper.HasExited && DateTime.UtcNow<deadline)System.Threading.Thread.Sleep(20);
                    if(!File.Exists(handshake))throw new Exception("Private parent helper did not become ready");
                    Reparent(main,new IntPtr(Int64.Parse(File.ReadAllText(handshake))));
                    helper.Kill();Check(helper.WaitForExit(5000),"only the private host helper process is terminated");
                }finally {if(!helper.HasExited){helper.Kill();helper.WaitForExit(5000);}if(File.Exists(handshake))File.Delete(handshake);}
            }
            return;
        }
        using(var parent=new PrivateHost()) {
            Reparent(main,parent.Handle);
        }
    }
    [STAThread]static int Main(string[] args) {
        if(args.Length==2 && args[0]=="--parent-helper") {using(var parent=new PrivateHost()){File.WriteAllText(args[1],parent.Handle.ToInt64().ToString());Forms.Application.Run();}return 0;}
        bool hidden=args.Contains("--hidden"),desktop=args.Contains("--desktop"),editing=args.Contains("--editing"),rename=args.Contains("--rename"),disposeSource=args.Contains("--source-disposed"),killParentProcess=args.Contains("--kill-parent-process");int result=1;var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var storage=new Storage(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,hidden ? "recovery-hidden-data":"recovery-visible-data"));
        var state=new NoteState();state.Settings.Left=120;state.Settings.Top=110;state.Settings.AnimationsEnabled=false;state.Settings.QuickAddShortcut="Ctrl+Alt+Shift+F8";
        var original=new MainWindow(storage,state,!desktop);original.ShowActivated=false;original.ShowInTaskbar=false;int closedEvents=0;original.Closed+=delegate {closedEvents++;};
        original.Show();Pump();
        try {
            var note=original.Service.Add("保留的事项","inbox");
            var unlocked=original.Service.AddCategory("本次已解锁分类");var locked=original.Service.AddCategory("仍然加锁分类");
            state.CategoryPassword=PasswordSecrets.Create("ProbeOnly-1234");unlocked.IsPrivate=true;locked.IsPrivate=true;string privacyError;
            Check(original.Privacy.TryUnlock(unlocked.Id,"ProbeOnly-1234",out privacyError),"private fixture unlocks one category only");
            ((TextBox)Field(original,"input")).Text="尚未添加的草稿";
            original.ToggleQuickAdd();var quick=(Window)Field(original,"quickWindow");
            ((TextBox)quick.GetType().GetField("input",Hidden).GetValue(quick)).Text="快捷栏草稿";
            quick.Hide();Check(original.Save(),"isolated fixture is saved");
            if(editing) {
                original.Refresh();
                typeof(MainWindow).GetMethod("BeginEdit",Hidden).Invoke(original,new object[]{note,null});
                ((TextBox)Field(original,"editor")).Text="正在编辑的内容";
            }
            string renameId=null;
            if(rename) {
                renameId=original.Service.AddCategory("原分类名称").Id;original.Refresh();
                typeof(MainWindow).GetMethod("BeginCategoryRename",Hidden).Invoke(original,new object[]{renameId});
                ((TextBox)Field(original,"categoryNameEditor")).Text="尚未确认的分类名称";
            }
            if(hidden)original.Hide();string expectedNoteText=note.Text;bool editorWasActive=Field(original,"editor") is TextBox;
            var before=original.Host.Bounds();var oldTray=(TrayIcon)Field(original,"tray");var oldHotkey=(GlobalHotkey)Field(original,"quickHotkey");
            DestroyParent(original,disposeSource,killParentProcess);Pump();
            var restored=app.MainWindow as MainWindow;
            if(restored==null || restored==original) {
                Console.WriteLine("LIFETIME: handle="+new WindowInteropHelper(original).Handle+" retired="+Field(original,"retired")+" nativeDestroying="+Field(original,"nativeDestroying"));
                try {original.Reveal();}catch(InvalidOperationException ex) {Console.WriteLine("REPRODUCED: "+ex.Message+"\n"+ex.StackTrace);}
            }
            Check(restored!=null && restored!=original && new WindowInteropHelper(restored).Handle!=IntPtr.Zero,"native parent loss recreates a live main window");
            Check(app.Windows.OfType<MainWindow>().Count()==1,"overlapping loss notifications create exactly one replacement");
            if(disposeSource || killParentProcess)Check(closedEvents==0,"regression exercises source loss without Window.Closed");
            Check(restored.IsVisible==!hidden,"recovery preserves visible or hidden state");
            if(desktop)Check(app.Windows.OfType<Window>().Count(w=>(w.Title=="便签背景合成层" || w.Title=="便签图标合成层") && w.IsVisible)==(hidden ? 0:2),"desktop composition layers follow restored note visibility");
            Check(restored.Service.State.Notes.Single().Text==expectedNoteText && restored.Service.State.Notes.Single().Id==note.Id,"recovery retains the existing note without duplication");
            if(editing && editorWasActive)Check(Field(restored,"editor") is TextBox && ((TextBox)Field(restored,"editor")).Text=="正在编辑的内容","active note editor and its draft survive native destruction");
            if(rename) {
                var renameBox=Field(restored,"categoryNameEditor") as TextBox;
                Check((renameBox!=null && renameBox.Text=="尚未确认的分类名称" || state.Categories.Single(c=>c.Id==renameId).Name=="尚未确认的分类名称") && (string)Field(restored,"active")==renameId,"active category rename text is retained or committed before focus loss");
            }
            Check(((TextBox)Field(restored,"input")).Text=="尚未添加的草稿" && state.Settings.QuickAddDraft=="快捷栏草稿","both input drafts survive recovery");
            Check(Math.Abs(restored.Left-before[0])<2 && Math.Abs(restored.Top-before[1])<2,"parent destruction does not reset saved position");
            Check(oldTray.Handle==IntPtr.Zero,"old tray resources are released");
            Check(!oldHotkey.IsRegistered && restored.ShortcutRegistered,"old hotkey is released and the replacement registers the shortcut");
            Check(!restored.Privacy.IsLocked(unlocked.Id) && restored.Privacy.IsLocked(locked.Id),"recovery preserves both unlocked and locked category state");
            original.Reveal();Check(restored.IsVisible,"queued activation of the old window reaches the replacement");
            var tray=(TrayIcon)Field(restored,"tray");restored.Hide();((Forms.ToolStripMenuItem)tray.ContextMenuStrip.Items[0]).PerformClick();Check(restored.IsVisible,"tray Show note activates the replacement");
            state.Settings.BackgroundOpacity=.25;state.Settings.TextOpacity=.25;restored.Hide();((Forms.ToolStripMenuItem)tray.ContextMenuStrip.Items[2]).PerformClick();Check(restored.IsVisible && state.Settings.BackgroundOpacity==1 && state.Settings.TextOpacity==1,"tray restore placement and opacity uses the live replacement");
            ((Forms.ToolStripMenuItem)tray.ContextMenuStrip.Items[3]).PerformClick();Check(app.Windows.OfType<SettingsWindow>().Count(w=>w.IsVisible)==1,"replacement settings open from tray");
            ((Forms.ToolStripMenuItem)tray.ContextMenuStrip.Items[1]).PerformClick();Pump();
            var restoredQuick=(Window)Field(restored,"quickWindow");Check(restoredQuick!=null && restoredQuick.IsVisible,"quick bar opens after window recovery");
            Check(((TextBox)restoredQuick.GetType().GetField("input",Hidden).GetValue(restoredQuick)).Text=="快捷栏草稿","restored quick bar displays its draft");
            // Repeat before exiting, while settings and the quick bar exist.
            DestroyParent(restored,disposeSource,killParentProcess);Pump();var second=app.MainWindow as MainWindow;
            Check(second!=null && second!=restored && second.IsVisible,"a second parent loss also recovers");
            Check(app.Windows.OfType<MainWindow>().Count()==1,"repeated loss leaves no duplicate live main windows");
            Check(storage.Load().Notes.Single().Text==(editing ? "正在编辑的内容":"保留的事项") && storage.Load().Settings.Draft=="尚未添加的草稿","recovered data remains readable from disk");
            second.Exit();Pump();Check(!app.Windows.OfType<MainWindow>().Any() && ((TrayIcon)Field(second,"tray")).Handle==IntPtr.Zero,"normal Exit closes windows and tray without recreating them");
            result=0;
        }catch(Exception ex){Console.Error.WriteLine(ex);}
        finally {
            if(!app.Dispatcher.HasShutdownStarted){var current=app.MainWindow as MainWindow;try{if(current!=null)current.Exit();}catch{}app.Shutdown();}
        }
        return result;
    }
}

