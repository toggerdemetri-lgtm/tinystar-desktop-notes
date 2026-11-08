using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
namespace DesktopNotes {
    static class Program {
        [STAThread] static void Main(string[] args) {
#if !STORE_PACKAGE
            // A hosted invocation does not own the calling process's launch.
            try {
                for(int i=0;i<args.Length-1;i++)if(args[i]=="--data-dir")args[i+1]=Path.GetFullPath(args[i+1]);
                if(System.Reflection.Assembly.GetEntryAssembly()==typeof(Program).Assembly && DesktopLaunchContext.EnsureDesktopContext(ref args))return;
            }
            catch(Exception ex) {MessageBox.Show("请从桌面或开始菜单重新打开TinyStar桌面便签。\n"+ex.Message,"TinyStar桌面便签");return;}
#endif
            bool test = Array.IndexOf(args, "--test-mode") >= 0;
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZhuomianNotes");
#if STORE_PACKAGE
            folder = StorePlatform.DataDirectory;
#endif
            for (int i=0;i<args.Length-1;i++) if(args[i] == "--data-dir") folder = Path.GetFullPath(args[i+1]);
            string identity = "Local\\ZhuomianNotes-" + System.Security.Principal.WindowsIdentity.GetCurrent().User.Value + (test ? "-test" : "");
            StartupTrace.Begin(folder);
            bool first;
            using(var mutex = new Mutex(true, identity, out first))
            using(var wake = new EventWaitHandle(false, EventResetMode.AutoReset, identity + "-wake")) {
                if (!first) { StartupTrace.Record("secondary-start",null,null,null);wake.Set(); return; }
                try {
                    var storage = new Storage(folder); var state = storage.Load();StartupTrace.Record("loaded",storage,state,null); var app = new Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
                    var window = new MainWindow(storage, state, test);
                    bool firstRender=true;window.ContentRendered+=delegate {if(firstRender){firstRender=false;StartupTrace.Record("first-render",window.Store,window.Service.State,window);}};
                    window.Closing+=delegate {StartupTrace.Record("closing",window.Store,window.Service.State,window);};
                    var wait = ThreadPool.RegisterWaitForSingleObject(wake, delegate { app.Dispatcher.BeginInvoke(new Action(delegate {var current=app.MainWindow as MainWindow;if(current!=null){StartupTrace.Record("wake-existing",current.Store,current.Service.State,current);current.Reveal();}})); }, null, -1, false);
                    app.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e) { e.Handled = true; MessageBox.Show("操作未能完成，当前内容仍保留在窗口中。\n" + e.Exception.Message,"TinyStar桌面便签"); };
                    window.Show();
                    if (!String.IsNullOrEmpty(storage.Notice)) app.Dispatcher.BeginInvoke(new Action(delegate { MessageBox.Show(storage.Notice, "数据恢复提示"); }));
                    app.Run();StartupTrace.Record("shutdown",storage,window.Service.State,null); wait.Unregister(null);
                } catch(Exception ex) { MessageBox.Show("TinyStar桌面便签暂时无法启动。你的原有数据不会被清空。\n" + ex.Message,"TinyStar桌面便签"); }
            }
        }
    }
}



