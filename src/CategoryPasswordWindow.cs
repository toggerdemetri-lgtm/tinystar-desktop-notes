using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
namespace DesktopNotes {
    enum CategoryPasswordMode {Set,Unlock,Verify,Remove}
    sealed class CategoryPasswordWindow : StepDialogWindow {
        readonly MainWindow main;readonly CategoryPasswordMode mode;readonly string context;
        readonly List<PasswordBox> fields=new List<PasswordBox>();readonly PasswordBox current,next;readonly TextBlock error;
        static string Caption(MainWindow main,CategoryPasswordMode mode) {return mode==CategoryPasswordMode.Set ? (main.Privacy.HasPassword ? "修改分类密码":"设置分类密码"):mode==CategoryPasswordMode.Remove ? "移除分类密码":mode==CategoryPasswordMode.Unlock ? "解锁分类":"验证分类密码";}
        public CategoryPasswordWindow(MainWindow main,CategoryPasswordMode mode,string context) : base(main,Caption(main,mode),416,mode==CategoryPasswordMode.Set && main.Privacy.HasPassword ? 430:354) {
            this.main=main;this.mode=mode;this.context=context;Body.RowDefinitions.Add(new RowDefinition());Body.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            var panel=new StackPanel();Body.Children.Add(panel);
            bool all=main.Service.State.Settings.UnlockAllCategories;
            string text=mode==CategoryPasswordMode.Set ? (context==null ? "所有自定义分类共用一个密码。\n密码为4—128个字符，请妥善保存。":"先设置共用密码，保存后为“"+main.Service.CategoryName(context)+"”上锁。\n密码为4—128个字符，请妥善保存。"):mode==CategoryPasswordMode.Remove ? "移除后，所有分类都将解除密码保护。\n请输入原密码确认。":mode==CategoryPasswordMode.Unlock ? (all ? "输入共用密码，解锁所有已加锁分类。":"输入共用密码，解锁“"+main.Service.CategoryName(context)+"”。"):"“"+context+"”包含受保护分类，请先验证密码。";
            panel.Children.Add(new TextBlock {Text=text,Foreground=P.Secondary,FontSize=12,TextWrapping=TextWrapping.Wrap,LineHeight=20,Margin=new Thickness(0,0,0,14)});
            if(mode!=CategoryPasswordMode.Set || main.Privacy.HasPassword)current=Field(panel,mode==CategoryPasswordMode.Set || mode==CategoryPasswordMode.Remove ? "原密码":"分类密码");
            if(mode==CategoryPasswordMode.Set)next=Field(panel,main.Privacy.HasPassword ? "新密码":"分类密码");
            error=new TextBlock {Foreground=new SolidColorBrush(Color.FromRgb(222,125,113)),FontSize=11,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,4),Visibility=Visibility.Collapsed};AutomationProperties.SetLiveSetting(error,AutomationLiveSetting.Polite);panel.Children.Add(error);
            var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};Grid.SetRow(buttons,1);Body.Children.Add(buttons);
            var cancel=ActionButton("取消",false);cancel.IsCancel=true;cancel.Click+=delegate {DialogResult=false;};buttons.Children.Add(cancel);
            var ok=ActionButton(mode==CategoryPasswordMode.Set ? (context==null ? "保存密码":"保存并上锁"):mode==CategoryPasswordMode.Remove ? "确认移除":mode==CategoryPasswordMode.Unlock ? (all ? "解锁所有分类":"解锁"):"验证并继续",true);ok.IsDefault=true;ok.Click+=delegate {Submit();};buttons.Children.Add(ok);
            Loaded+=delegate {fields[0].Focus();};Closed+=delegate {foreach(var box in fields)box.Clear();};
        }
        PasswordBox Field(Panel panel,string label) {
            panel.Children.Add(new TextBlock {Text=label,Foreground=P.Text,FontSize=12,Margin=new Thickness(0,0,0,6)});
            var box=new PasswordBox {MaxLength=128,Padding=new Thickness(11,9,11,9),FontSize=14,Foreground=P.Text,CaretBrush=P.Accent,Background=Brushes.Transparent,BorderThickness=new Thickness(0),FocusVisualStyle=null};AutomationProperties.SetName(box,label);
            var frame=new Border {CornerRadius=new CornerRadius(10),Background=P.Input,BorderBrush=P.Line,BorderThickness=new Thickness(1),Child=box,Margin=new Thickness(0,0,0,12)};panel.Children.Add(frame);fields.Add(box);
            box.GotKeyboardFocus+=delegate {frame.BorderBrush=P.Accent;};box.LostKeyboardFocus+=delegate {frame.BorderBrush=P.Line;};return box;
        }
        void Submit() {
            string message=null;bool success=false;
            if(mode==CategoryPasswordMode.Set)success=context==null ? main.TrySetCategoryPassword(current==null ? null:current.Password,next.Password,out message):main.TrySetPasswordAndProtectCategory(context,next.Password,out message);
            else if(mode==CategoryPasswordMode.Remove)success=main.TryRemoveCategoryPassword(current.Password,out message);
            else if(mode==CategoryPasswordMode.Unlock)success=main.TryUnlockCategory(context,current.Password,out message);
            else success=main.Privacy.VerifyPassword(current.Password,out message);
            if(success) {DialogResult=true;return;}error.Text=message;error.Visibility=Visibility.Visible;
        }
    }
}
