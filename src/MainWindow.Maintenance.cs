using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
namespace DesktopNotes {
    public partial class MainWindow {
        const string StartupRegistryKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
        internal Func<string> StartupReadOverride=null;internal Action<string> StartupWriteOverride=null;
        string previewStartupValue,maintenanceNotice;
        internal string ReadStartupValue() {
            if(StartupReadOverride!=null)return StartupReadOverride();if(IsPreviewProcess)return previewStartupValue;
#if STORE_PACKAGE
            return StorePlatform.ReadStartupValue();
#else
            using(var key=Registry.CurrentUser.OpenSubKey(StartupRegistryKey))return key==null ? null:key.GetValue("ZhuomianNotes") as string;
#endif
        }
        internal void WriteStartupValue(string value) {
            if(StartupWriteOverride!=null){StartupWriteOverride(value);return;}if(IsPreviewProcess){previewStartupValue=value;return;}
#if STORE_PACKAGE
            StorePlatform.WriteStartupValue(value);
#else
            if(value==null) {using(var key=Registry.CurrentUser.OpenSubKey(StartupRegistryKey,true))if(key!=null)key.DeleteValue("ZhuomianNotes",false);}
            else using(var key=Registry.CurrentUser.CreateSubKey(StartupRegistryKey))key.SetValue("ZhuomianNotes",value);
#endif
        }
        internal string TakeMaintenanceNotice() {string value=maintenanceNotice;maintenanceNotice=null;return value;}
        internal bool TryBackupAndClearData(string path,bool authorized,out string error) {
            error=null;if(Privacy.HasPassword && !authorized){error="请先验证分类密码。";return false;}
            try{Store.ExportBeforeClear(path,Service.State);}catch(Exception ex){error="备份未完成，数据未清除："+ex.Message;return false;}
            return TryClearAllData(authorized,out error);
        }
        internal bool TryClearAllData(bool authorized,out string error) {
            error=null;if(Privacy.HasPassword && !authorized){error="请先验证分类密码。";return false;}
            var preferences=Storage.CopyPreferences(Service.State.Settings);Storage.CopyContentDrafts(new Preferences {Draft="",DraftCategory="inbox",QuickAddDraft=""},preferences);
            var empty=new NoteState {Settings=preferences};
            try{Store.SaveClearedState(empty);}catch(Exception ex){error="清除失败，原有内容已保留："+ex.Message;return false;}
            StopTransientInteractions();CommitCategoryRename(true);editing=null;editor=null;editingStepId=null;addingStep=false;composing=false;
            Service=new NoteService(empty);Privacy=new CategoryPrivacy(empty);active="inbox";expandedSteps.Clear();pendingStepRedraw.Clear();
            completedSearch="";completedFrom=completedTo=null;completedSearchInput.Clear();searchTimer.Stop();
            syncingPrivateDraft=true;try{input.Clear();}finally{syncingPrivateDraft=false;}Refresh();saveTimer.Stop();maintenanceNotice="数据已清除，外观和快捷键已保留。";return true;
        }
        void StopTransientInteractions() {
            saveTimer.Stop();if(snapTimer!=null)snapTimer.Stop();if(stepRedrawTimer!=null)stepRedrawTimer.Stop();moving=false;
            if(categoryAddPopup!=null)categoryAddPopup.IsOpen=false;CancelCategoryDrag();CancelStepDrag();CancelNoteDrag();
            if(quickWindow!=null){quickWindow.Dispose();quickWindow=null;}
        }
        internal bool TryRestoreDefaults(out string error) {
            error=null;if(!CommitCategoryRename(false)){error="请先完成分类名称修改。";return false;}CommitEdit(false);
            var old=Service.State.Settings;var defaults=new Preferences();Storage.CopyContentDrafts(old,defaults);
            var area=SystemParameters.WorkArea;defaults.Left=Math.Max(area.Left,area.Right-defaults.Width-32);defaults.Top=area.Top+Math.Min(60,Math.Max(0,area.Height-defaults.Height));
            string startup;try{startup=ReadStartupValue();}catch(Exception ex){error="无法读取开机启动设置："+ex.Message;return false;}
            StartQuickHotkey();string previousGesture=quickHotkey.Gesture==null ? old.QuickAddShortcut:quickHotkey.Gesture.Canonical;
            string detail;if(!quickHotkey.TrySet(defaults.QuickAddShortcut,out detail)){error="默认快捷键 Ctrl+I 已被占用或不可用，设置未改变。";return false;}
            bool startupChanged=false;
            try {
                WriteStartupValue(null);startupChanged=true;
                Store.Save(new NoteState {Version=Service.State.Version,Notes=Service.State.Notes,Categories=Service.State.Categories,CategoryPassword=Service.State.CategoryPassword,Settings=defaults});
            }catch(Exception ex) {
                string rollback;if(!quickHotkey.TrySet(previousGesture,out rollback))error="原快捷键暂未能恢复，请在设置中重新指定。";
                if(startupChanged)try{WriteStartupValue(startup);}catch(Exception restore){error=(error??"")+" 开机启动未能恢复："+restore.Message;}
                error="恢复默认未完成："+ex.Message+(error==null ? " 原有设置已保留。":" "+error);return false;
            }
            StopTransientInteractions();AcceptCurrentPlacement();Service.State.Settings=defaults;Width=defaults.Width;Height=defaults.Height;ApplySidebarWidth();UpdateLayout();Host.Move(defaults.Left,defaults.Top);ApplyAppearance();Refresh();
            maintenanceNotice="已恢复默认设置，事项、分类、草稿和分类密码已保留。";return true;
        }
        internal void RequestClearAllData() {
            var choice=new DataMaintenanceWindow(this,MaintenanceDialogKind.ClearChoice);if(choice.ShowDialog()!=true)return;
            if(Privacy.HasPassword && new CategoryPasswordWindow(this,CategoryPasswordMode.Verify,"清除所有数据").ShowDialog()!=true)return;
            string error;
            if(choice.Choice==ClearDataChoice.Backup) {
                var picker=new SaveFileDialog {Title="保存清除前备份",Filter="便签备份 (*.json)|*.json",FileName="TinyStar清除前备份-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".json",AddExtension=true,DefaultExt=".json"};
                if(picker.ShowDialog(settingsWindow)!=true)return;if(!TryBackupAndClearData(picker.FileName,true,out error)){ShowMaintenanceError(error);return;}
            }else {
                if(new DataMaintenanceWindow(this,MaintenanceDialogKind.ClearDirect).ShowDialog()!=true)return;
                if(!TryClearAllData(true,out error)){ShowMaintenanceError(error);return;}
            }
            ReopenMaintenanceSettings();
        }
        internal void RequestRestoreDefaults() {
            if(new DataMaintenanceWindow(this,MaintenanceDialogKind.ResetDefaults).ShowDialog()!=true)return;
            string error;if(!TryRestoreDefaults(out error)){ShowMaintenanceError(error);return;}ReopenMaintenanceSettings();
        }
        void ShowMaintenanceError(string error) {new DataMaintenanceWindow(this,error).ShowDialog();}
        void ReopenMaintenanceSettings() {if(settingsWindow!=null)settingsWindow.Close();var current=ShowSettings();current.UpdateLayout();current.Dispatcher.BeginInvoke(new Action(delegate {var scroll=FindDescendants(current).OfType<System.Windows.Controls.ScrollViewer>().FirstOrDefault();if(scroll!=null)scroll.ScrollToEnd();}),DispatcherPriority.Loaded);}
        void RestoreImportedEdit() {
            var p=Service.State.Settings;var note=Service.State.Notes.FirstOrDefault(n=>n.Id==p.EditId && !n.Deleted && Privacy.CanSee(n));if(note==null)return;
            active=note.Done ? "done":note.CategoryId;Refresh();if(p.EditNewStep || !String.IsNullOrEmpty(p.EditStepId))BeginStepEdit(note,p.EditStepId,p.EditDraft,p.EditNewStep);else BeginEdit(note,p.EditDraft);
        }
    }
}

