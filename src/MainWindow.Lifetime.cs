using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
namespace DesktopNotes {
    public partial class MainWindow {
        bool nativeDestroying,retired,resourcesReleased,revealRequested,restoreVisible;
        string recoveryCategoryId,recoveryCategoryText;
        HwndSource lifetimeSource;
        MainWindow CurrentWindow {get {var current=Application.Current.MainWindow as MainWindow;return current!=this && current!=null && !current.retired ? current:null;}}

        void InitializeLifetime() {
            lifetimeSource=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            if(lifetimeSource!=null) {lifetimeSource.AddHook(LifetimeMessage);lifetimeSource.Disposed+=LifetimeSourceDisposed;}
        }
        void CaptureLostWindow() {
            if(nativeDestroying)return;
            nativeDestroying=true;restoreVisible=Visibility==Visibility.Visible;RecordEditDraft();
            if(categoryNameEditor!=null) {recoveryCategoryId=editingCategoryId;recoveryCategoryText=categoryNameEditor.Text;}
        }
        void LifetimeSourceDisposed(object sender,EventArgs e) {
            // A foreign parent can disappear without WM_DESTROY/Window.Closed.
            // WPF still permanently disposes Window from HwndSource.Disposed.
            if(exiting || retired || Dispatcher.HasShutdownStarted)return;
            CaptureLostWindow();WindowClosed(this,EventArgs.Empty);
        }
        void CheckWindowLifetime() {if(!retired && !exiting && lifetimeSource!=null && lifetimeSource.IsDisposed)LifetimeSourceDisposed(lifetimeSource,EventArgs.Empty);}
        IntPtr LifetimeMessage(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled) {
            // Explorer can destroy a native child without sending WM_CLOSE. Capture
            // state before WPF disposes its HwndSource; Closing cannot cancel this.
            if(message==2 && !exiting)CaptureLostWindow();
            return IntPtr.Zero;
        }
        void WindowClosed(object sender,EventArgs e) {
            if(retired)return;
            if(exiting || Dispatcher.HasShutdownStarted) {ReleaseWindowResources();return;}
            retired=true;exiting=true;RecordEditDraft();
            // Queued focus-loss callbacks must not commit the retired controls
            // into the state now owned by the replacement window.
            editor=null;editing=null;categoryNameEditor=null;
            // Keep the in-memory state even if its last disk save failed.
            try {Store.Save(Service.State);}catch(Exception ex) {recoverySaveError=ex.Message;}
            ReleaseWindowResources();
            Dispatcher.BeginInvoke(new Action(RecoverWindow),DispatcherPriority.Normal);
        }
        string recoverySaveError;
        void RecoverWindow() {
            if(Dispatcher.HasShutdownStarted)return;
            var next=new MainWindow(Store,Service.State,testMode);
            next.Privacy=Privacy;next.active=active;next.completedSearch=completedSearch;
            next.completedFrom=completedFrom;next.completedTo=completedTo;
            foreach(var id in expandedSteps)next.expandedSteps.Add(id);
            next.completedSearchInput.Text=completedSearchInput.Text;
            string categoryId=recoveryCategoryId,categoryText=recoveryCategoryText;
            if(categoryId!=null)next.Loaded+=delegate {
                if(categoryId==null)return;string id=categoryId;categoryId=null;
                next.BeginCategoryRename(id);if(next.categoryNameEditor!=null)next.categoryNameEditor.Text=categoryText;
            };
            Application.Current.MainWindow=next;
            // Creating the HWND first lets a hidden note remain hidden while still
            // rebuilding its desktop host, tray and hotkey registration.
            new WindowInteropHelper(next).EnsureHandle();
            if(restoreVisible || revealRequested)next.Show();
            else {next.ready=true;next.KeepOnScreen();next.StartQuickHotkey();next.Refresh();}
            if(recoverySaveError!=null)next.Tell("窗口已恢复，保存失败，请重试："+recoverySaveError);
        }
        void ReleaseWindowResources() {
            if(resourcesReleased)return;resourcesReleased=true;
            StopQueuedMove(false);if(categoryAddPopup!=null)categoryAddPopup.IsOpen=false;
            CancelCategoryDrag();CancelStepDrag();CancelNoteDrag();
            if(placementRetryTimer!=null)placementRetryTimer.Stop();searchTimer.Stop();saveTimer.Stop();if(stepRedrawTimer!=null)stepRedrawTimer.Stop();if(snapTimer!=null)snapTimer.Stop();
            if(lifetimeSource!=null)lifetimeSource.Disposed-=LifetimeSourceDisposed;
            if(settingsWindow!=null)settingsWindow.Close();if(quickWindow!=null)quickWindow.Dispose();if(quickHotkey!=null)quickHotkey.Dispose();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplayChanged;Microsoft.Win32.SystemEvents.UserPreferenceChanged-=SystemThemeChanged;
            Host.Stop();tray.Dispose();if(trayIcon!=null)trayIcon.Dispose();
        }
    }
}




