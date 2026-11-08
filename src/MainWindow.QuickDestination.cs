namespace DesktopNotes {
    public partial class MainWindow {
        internal string QuickAddNotice {get;private set;}
        internal bool TrySetQuickDestination(string mode,out string error) {
            error=null;if(mode!="current" && mode!="inbox") {error="请选择快捷添加的去向。";return false;}
            var p=Service.State.Settings;string previous=p.QuickAddDestination;p.QuickAddDestination=mode;
            if(Save())return true;p.QuickAddDestination=previous;error="设置未能保存，原选项已保留。";return false;
        }
    }
}
