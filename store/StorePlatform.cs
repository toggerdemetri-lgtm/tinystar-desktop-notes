using System;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
namespace DesktopNotes {
    // Compiled only into the MSIX variant. The EXE release keeps its original behavior.
    internal static class StorePlatform {
        const string StartupId="TinyStarStartup";
        internal static string DataDirectory {get {return Path.Combine(ApplicationData.Current.LocalFolder.Path,"ZhuomianNotes");}}
        internal static bool StartupIsEnabled(int state) {return state==2 || state==4;}
        internal static string StartupEnableError(int state) {
            if(StartupIsEnabled(state))return null;
            if(state==1)return "开机启动已被你在 Windows 中关闭，请到 Windows 设置→应用→启动，允许 TinyStar桌面便签启动。";
            if(state==3)return "开机启动被系统管理策略禁用，请联系设备管理员。";
            return "Windows 未允许开机启动，请在 Windows 设置→应用→启动中检查。";
        }
        static T Complete<T>(Windows.Foundation.IAsyncOperation<T> operation) {
            var completion=new TaskCompletionSource<bool>();
            operation.Completed=delegate {completion.TrySetResult(true);};
            if(!completion.Task.Wait(10000)) {operation.Cancel();throw new TimeoutException("Windows 响应超时，请稍后重试。");}
            try {return operation.GetResults();}finally {operation.Close();}
        }
        static StartupTask GetTask() {return Complete(StartupTask.GetAsync(StartupId));}
        // StartupTask is agile. Create and complete all operations on a pool MTA,
        // so synchronous settings callers never block their own STA COM callback.
        internal static T OnMta<T>(Func<T> action) {return Task.Run(action).GetAwaiter().GetResult();}
        internal static string ReadStartupValue() {return OnMta(delegate {return StartupIsEnabled((int)GetTask().State) ? StartupId:null;});}
        internal static void WriteStartupValue(string value) {
            OnMta(delegate {WriteStartupOnMta(value);return true;});
        }
        static void WriteStartupOnMta(string value) {
            var task=GetTask();
            if(value==null) {
                task.Disable();
                if(StartupIsEnabled((int)GetTask().State))throw new InvalidOperationException("Windows 未允许关闭开机启动，请在系统启动应用列表中关闭。");
                return;
            }
            int state=(int)Complete(task.RequestEnableAsync());
            string error=StartupEnableError(state);if(error!=null)throw new InvalidOperationException(error);
        }
    }
}
