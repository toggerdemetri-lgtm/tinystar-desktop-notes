using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace DesktopNotes {
    public partial class MainWindow {
        public CategoryPrivacy Privacy {get;private set;}
        bool syncingPrivateDraft;
        internal bool TrySetCategoryUnlockScope(bool all) {
            bool previous=Service.State.Settings.UnlockAllCategories;Service.State.Settings.UnlockAllCategories=all;
            if(Save())return true;Service.State.Settings.UnlockAllCategories=previous;return false;
        }
        internal bool TrySetCategoryPassword(string current,string next,out string error) {
            error=null;if(Privacy.HasPassword && !Privacy.VerifyPassword(current,out error))return false;
            PasswordCredential credential;try {credential=PasswordSecrets.Create(next);}catch(ArgumentException ex) {error=ex.Message;return false;}
            return PersistCategoryPassword(credential,null,out error);
        }
        internal bool TrySetPasswordAndProtectCategory(string id,string password,out string error) {
            if(Privacy.HasPassword) {error="分类密码已设置，请重新点击锁头上锁。";return false;}
            var category=Service.State.Categories.FirstOrDefault(c=>c.Id==id);if(category==null) {error="只能给自定义分类上锁。";return false;}
            PasswordCredential credential;try {credential=PasswordSecrets.Create(password);}catch(ArgumentException ex) {error=ex.Message;return false;}
            return PersistCategoryPassword(credential,category,out error);
        }
        bool PersistCategoryPassword(PasswordCredential credential,Category target,out string error) {
            if(!CommitCategoryRename(false)) {error="请先完成分类名称修改。";return false;}
            CommitEdit(false);var previous=Service.State.CategoryPassword;bool previousProtection=target!=null && target.IsPrivate;
            Service.State.CategoryPassword=credential;if(target!=null)target.IsPrivate=true;
            if(!Save()) {Service.State.CategoryPassword=previous;if(target!=null)target.IsPrivate=previousProtection;error="密码未能保存，请重试。原有密码和分类状态保持不变。";return false;}
            CancelCategoryDrag();CancelStepDrag();CancelNoteDrag();Privacy.LockAll();Refresh();error=null;return true;
        }
        internal bool TryRemoveCategoryPassword(string current,out string error) {
            if(!Privacy.VerifyPassword(current,out error))return false;
            if(!CommitCategoryRename(false)) {error="请先完成分类名称修改。";return false;}
            CommitEdit(false);var previous=Service.State.CategoryPassword;var flags=Service.State.Categories.Select(c=>c.IsPrivate).ToArray();
            Service.State.CategoryPassword=null;foreach(var category in Service.State.Categories)category.IsPrivate=false;
            if(!Save()) {Service.State.CategoryPassword=previous;for(int i=0;i<flags.Length;i++)Service.State.Categories[i].IsPrivate=flags[i];error="密码未能移除，原有保护保持不变。";return false;}
            Privacy.LockAll();Refresh();error=null;return true;
        }
        internal bool TryProtectCategory(string id,out string error) {
            error=null;var category=Service.State.Categories.FirstOrDefault(c=>c.Id==id);
            if(category==null) {error="只能给自定义分类上锁。";return false;}
            if(!Privacy.HasPassword) {error="请先在设置中设置分类密码。";return false;}
            if(!CommitCategoryRename(false)) {error="请先完成分类名称修改。";return false;}
            CommitEdit(false);bool previous=category.IsPrivate;category.IsPrivate=true;
            if(!Save()) {category.IsPrivate=previous;error="上锁状态未能保存，请重试。";return false;}
            CancelCategoryDrag();CancelStepDrag();CancelNoteDrag();Privacy.Lock(id);Refresh();return true;
        }
        internal bool TryUnlockCategory(string id,string password,out string error) {
            if(!Privacy.TryUnlock(id,password,Service.State.Settings.UnlockAllCategories,out error))return false;Refresh();
            var p=Service.State.Settings;var pending=Service.State.Notes.FirstOrDefault(n=>n.Id==p.EditId && (n.CategoryId==id || p.UnlockAllCategories) && Privacy.CanSee(n) && !n.Deleted);
            if(pending!=null) {active=pending.Done ? "done":pending.CategoryId;Refresh();if(p.EditNewStep || !String.IsNullOrEmpty(p.EditStepId))BeginStepEdit(pending,p.EditStepId,p.EditDraft,p.EditNewStep);else BeginEdit(pending,p.EditDraft);}
            return true;
        }
        void ToggleCategoryPrivacy(string id) {
            if(!Service.State.Categories.Any(c=>c.Id==id))return;
            if(!Privacy.HasPassword) {new CategoryPasswordWindow(this,CategoryPasswordMode.Set,id).ShowDialog();return;}
            if(Privacy.IsLocked(id)) {new CategoryPasswordWindow(this,CategoryPasswordMode.Unlock,id).ShowDialog();return;}
            string error;if(!TryProtectCategory(id,out error))Tell(error);
        }
        internal bool AuthorizePrivateOperation(string title) {return !Privacy.HasLockedCategories || new CategoryPasswordWindow(this,CategoryPasswordMode.Verify,title).ShowDialog()==true;}
        bool RequestClearTrash() {
            int count=Service.State.Notes.Count(n=>n.Deleted && Privacy.CanSee(n));if(count==0)return false;
            bool retainsLockedTrash=Service.State.Notes.Any(n=>n.Deleted && !Privacy.CanSee(n));
            return new TrashConfirmWindow(this,count,retainsLockedTrash).ShowDialog()==true && ClearVisibleTrash();
        }
        bool ClearVisibleTrash() {
            var previous=Service.State.Notes;Service.State.Notes=previous.Where(n=>!n.Deleted || !Privacy.CanSee(n)).ToList();
            if(!Save()) {Service.State.Notes=previous;return false;}Refresh();return true;
        }
        void SyncPrivateDraftView() {
            string text=Privacy.IsLocked(Service.State.Settings.DraftCategory) ? "":Service.State.Settings.Draft ?? "";
            if(input.Text==text)return;syncingPrivateDraft=true;try {input.Text=text;}finally {syncingPrivateDraft=false;}
        }
        void ShowCategoryLockedCard() {
            var stack=new StackPanel {Margin=new Thickness(10,38,10,20),HorizontalAlignment=HorizontalAlignment.Center};
            var icon=UiDesign.Icon("lock",Palette.Secondary,30);icon.HorizontalAlignment=HorizontalAlignment.Center;icon.Opacity=.6;stack.Children.Add(icon);
            stack.Children.Add(new TextBlock {Text="这个分类已上锁",Foreground=Palette.Text,FontSize=15,TextAlignment=TextAlignment.Center,Margin=new Thickness(0,16,0,7)});
            stack.Children.Add(new TextBlock {Text="解锁后查看事项和步骤",Foreground=Palette.Secondary,FontSize=12,TextAlignment=TextAlignment.Center,Margin=new Thickness(0,0,0,16)});
            string id=active;var unlock=Button("解锁此分类","解锁此分类");unlock.Background=Palette.Selected;unlock.Foreground=Palette.AccentText;unlock.HorizontalAlignment=HorizontalAlignment.Center;unlock.Click+=delegate {ToggleCategoryPrivacy(id);};stack.Children.Add(unlock);rows.Children.Add(stack);
        }
        Button AddCategoryLock(Button categoryButton,string id) {
            var grid=(Grid)categoryButton.Content;var badge=grid.Children.OfType<TextBlock>().FirstOrDefault(t=>Grid.GetColumn(t)==2);
            bool locked=Privacy.IsLocked(id);var button=Button("",(locked ? "解锁分类":"上锁分类")+Service.CategoryName(id));button.Tag="category-lock:"+id;
            button.Width=22;button.Height=24;button.Padding=new Thickness(4);button.Margin=new Thickness(1,-3,-3,-3);button.Visibility=Visibility.Hidden;button.VerticalAlignment=VerticalAlignment.Center;
            var icon=UiDesign.Icon(locked ? "lock":"unlock",Palette.Secondary,13);icon.Opacity=.5;button.Content=icon;Grid.SetColumn(button,2);grid.Children.Add(button);
            Action<bool> show=delegate(bool visible) {button.Visibility=visible ? Visibility.Visible:Visibility.Hidden;if(badge!=null)badge.Visibility=visible ? Visibility.Hidden:Visibility.Visible;};
            categoryButton.MouseEnter+=delegate {show(true);};categoryButton.MouseLeave+=delegate {if(!categoryButton.IsKeyboardFocusWithin)show(false);};
            categoryButton.GotKeyboardFocus+=delegate {show(true);};categoryButton.LostKeyboardFocus+=delegate {Dispatcher.BeginInvoke(new Action(delegate {show(categoryButton.IsMouseOver || categoryButton.IsKeyboardFocusWithin);}));};
            button.Click+=delegate(object sender,RoutedEventArgs e) {e.Handled=true;ToggleCategoryPrivacy(id);};return button;
        }
    }
}
