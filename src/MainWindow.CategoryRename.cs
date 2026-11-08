using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace DesktopNotes {
    public partial class MainWindow {
        TextBox categoryNameEditor;Border categoryNameRow;Button categoryNameButton;
        string editingCategoryId;bool committingCategoryName;
        void BeginCategoryRename(string id) {
            if(Privacy.IsLocked(id)) {Tell("请先解锁此分类再修改名称。");return;}
            if(!Service.State.Categories.Any(c=>c.Id==id))return;
            if(categoryNameEditor!=null && editingCategoryId==id) {categoryNameEditor.Focus();return;}
            if(!CommitCategoryRename(false))return;
            CancelCategoryDrag();CancelStepDrag();CancelNoteDrag();CommitEdit(false);
            if(active!=id) {active=id;Refresh();}
            var button=tabs.Children.OfType<Button>().FirstOrDefault(b=>(b.Tag as string)=="category:"+id);
            if(button==null)return;
            var category=Service.State.Categories.First(c=>c.Id==id);
            var box=new TextBox {Text=category.Name,FontSize=12,MaxLength=30,MinWidth=20,Padding=new Thickness(2,1,2,1),VerticalContentAlignment=VerticalAlignment.Center,Foreground=Palette.Text,CaretBrush=Palette.Icon,Background=Brushes.Transparent,BorderThickness=new Thickness(0),ToolTip="回车或点击别处保存，Esc取消"};
            AutomationProperties.SetName(box,"分类名称");
            var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(21)});grid.ColumnDefinitions.Add(new ColumnDefinition());
            var icon=UiDesign.Icon("inbox",Palette.Accent,14);icon.VerticalAlignment=VerticalAlignment.Center;grid.Children.Add(icon);Grid.SetColumn(box,1);grid.Children.Add(box);
            var row=new Border {Tag="category-rename",CornerRadius=new CornerRadius(10),Background=Palette.Selected,BorderBrush=Palette.Accent,BorderThickness=new Thickness(1),Padding=new Thickness(8,8,6,8),Margin=new Thickness(0,2,0,2),MinHeight=Math.Max(40,button.ActualHeight),Child=grid};
            int index=tabs.Children.IndexOf(button);categoryNameEditor=box;categoryNameRow=row;categoryNameButton=button;editingCategoryId=id;
            tabs.Children.RemoveAt(index);tabs.Children.Insert(index,row);ConfigureIme(box);
            box.PreviewKeyDown+=delegate(object sender,KeyEventArgs e) {
                if(composing || e.Key==Key.ImeProcessed)return;
                if(e.Key==Key.Escape) {CommitCategoryRename(true);e.Handled=true;}
                else if(e.Key==Key.Enter) {CommitCategoryRename(false);e.Handled=true;}
            };
            box.LostKeyboardFocus+=delegate {
                if(categoryNameEditor==box && !committingCategoryName)Dispatcher.BeginInvoke(new Action(delegate {if(categoryNameEditor==box && !box.IsKeyboardFocusWithin)CommitCategoryRename(false);}),DispatcherPriority.Background);
            };
            box.Focus();box.SelectAll();
        }
        bool CommitCategoryRename(bool cancel) {
            if(retired || nativeDestroying)return true;
            if(categoryNameEditor==null || committingCategoryName)return true;
            committingCategoryName=true;
            try {
                var category=Service.State.Categories.FirstOrDefault(c=>c.Id==editingCategoryId);
                if(!cancel && category!=null) {
                    string previous=category.Name;
                    try {Service.RenameCategory(category.Id,categoryNameEditor.Text);}catch(ArgumentException ex) {Tell(ex.Message);return false;}
                    if(category.Name!=previous && !Save()) {category.Name=previous;return false;}
                }
                var button=categoryNameButton;var row=categoryNameRow;
                categoryNameEditor=null;categoryNameRow=null;categoryNameButton=null;editingCategoryId=null;composing=false;
                if(category!=null) {
                    var label=((Grid)button.Content).Children.OfType<TextBlock>().First(t=>Grid.GetColumn(t)==1);label.Text=category.Name;
                    button.ToolTip=category.Name;AutomationProperties.SetName(button,category.Name);
                    var privacyButton=((Grid)button.Content).Children.OfType<Button>().FirstOrDefault(b=>(b.Tag as string)=="category-lock:"+category.Id);
                    if(privacyButton!=null) {privacyButton.ToolTip="上锁分类"+category.Name;AutomationProperties.SetName(privacyButton,"上锁分类"+category.Name);}
                    if(active==category.Id) {heading.Text=category.Name;heading.ToolTip=category.Name;}
                }
                int index=tabs.Children.IndexOf(row);if(index>=0) {tabs.Children.RemoveAt(index);tabs.Children.Insert(index,button);}
                return true;
            } finally {committingCategoryName=false;}
        }
    }
}
