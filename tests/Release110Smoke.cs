using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopNotes;
class ReleaseSmoke {
    static IEnumerable<DependencyObject> All(DependencyObject root) {yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var item in All(VisualTreeHelper.GetChild(root,i)))yield return item;}
    static void Check(bool ok,string label) {if(!ok)throw new Exception(label);Console.WriteLine("PASS: "+label);}
    [STAThread]static int Main(string[] args) {
        string expectedVersion=args.Length>0 ? args[0]:"1.1.0.0";
        int result=1;var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};var store=new Storage(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data"));var state=new NoteState();var main=new MainWindow(store,state,false);
        main.Loaded+=delegate {
            var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(180)};timer.Tick+=delegate {timer.Stop();try {
                Check(state.Settings.UiSize==50,"packaged UI size defaults to 50 percent");var scaleMethod=typeof(MainWindow).GetMethod("TrySetUiSize",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);double outerWidth=main.ActualWidth,outerHeight=main.ActualHeight;Check((bool)scaleMethod.Invoke(main,new object[]{75.0}) && main.ActualWidth==outerWidth && main.ActualHeight==outerHeight,"packaged UI scaling preserves outer geometry");Check(store.Load().Settings.UiSize==75,"packaged UI scaling setting persists");scaleMethod.Invoke(main,new object[]{50.0});Check(typeof(MainWindow).Assembly.GetName().Version.ToString()==expectedVersion,"smoke test loads the requested application version "+expectedVersion);
                var bitmap=(System.Windows.Media.Imaging.BitmapSource)main.Icon;var pixels=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(pixels,bitmap.PixelWidth*4,0);int dark=0;for(int i=0;i<pixels.Length;i+=4)if(pixels[i+3]>200 && pixels[i]<50 && pixels[i+1]<50 && pixels[i+2]<50)dark++;Check(dark>bitmap.PixelWidth*bitmap.PixelHeight*.45,"actual window/taskbar icon uses the approved dark background");
                var trayIcon=(System.Drawing.Icon)typeof(MainWindow).GetField("trayIcon",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(main);using(var nativeTrayIcon=System.Drawing.Icon.FromHandle(trayIcon.Handle))using(var trayBitmap=nativeTrayIcon.ToBitmap()){int count=0;for(int y=0;y<trayBitmap.Height;y++)for(int x=0;x<trayBitmap.Width;x++){var p=trayBitmap.GetPixel(x,y);if(p.A>200 && p.R<50 && p.G<50 && p.B<50)count++;}Check(count>trayBitmap.Width*trayBitmap.Height*.45,"actual tray icon uses the approved dark background");}
                Check(main.Host.Attached,"packaged application attaches to the desktop host");Check(main.Title=="TinyStar桌面便签","packaged application exposes the approved name");
                Check(state.Settings.BackgroundMode=="default" && state.Settings.QuickAddDestination=="current","packaged new preferences retain the approved defaults");
                Check(typeof(MainWindow).Assembly.GetType("DesktopNotes.CategoryAddPopup")!=null && typeof(MainWindow).Assembly.GetType("DesktopNotes.BackgroundImages")!=null,"packaged application contains anchored categories and managed backgrounds");
                main.Service.Add("发布程序保存检查","inbox");Check(main.Save() && store.Load().Notes.Count==1,"packaged application saves independent data");
                Check(typeof(MainWindow).Assembly.GetType("DesktopNotes.DataMaintenanceWindow")!=null && typeof(MainWindow).GetMethod("TryRestoreDefaults",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!=null,"packaged application includes both themed maintenance actions");
                var beforeClear=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"clear-backup.json");state.Settings.QuickAddDraft="发布检查快捷草稿";typeof(Storage).GetMethod("ExportBeforeClear",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(store,new object[]{beforeClear,state});var backup=store.Import(beforeClear);Check(backup.IncludesDrafts && backup.Notes.Count==1 && backup.Settings.QuickAddDraft=="发布检查快捷草稿","packaged clear backup retains contents and drafts");state.Settings.QuickAddDraft="";
                main.ToggleQuickAdd();var quick=app.Windows.Cast<Window>().Single(w=>w.Title=="快捷添加事项");
                Check(quick.IsVisible && Math.Abs(quick.Opacity-.85)<.001 && quick.Height==172,"packaged quick bar opens with final opacity and dimensions");
                Check(!All(quick).OfType<Button>().Any() && All(quick).OfType<TextBlock>().Any(t=>t.Text=="添加事项，按回车保存，按Esc保存草稿并取消"),"packaged quick bar includes the approved prompt without buttons");
                var input=All(quick).OfType<TextBox>().Single();input.Text="发布程序快捷添加检查";input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(input),0,Key.Enter) {RoutedEvent=Keyboard.PreviewKeyDownEvent});
                Check(!quick.IsVisible && store.Load().Notes.Count==2,"packaged quick bar submits on Enter");
                var settings=main.ShowSettings();settings.UpdateLayout();var slider=All(settings).OfType<Slider>().Single(s=>System.Windows.Automation.AutomationProperties.GetName(s)=="快捷栏不透明度");Check(slider.Value==85,"packaged settings includes independent quick-bar opacity");var first=main.Service.AddCategory("发布检查工作");var second=main.Service.AddCategory("发布检查生活");main.Service.ReorderCategory(second.Id,first.Id,true);Check(state.Categories[0].Id==second.Id,"packaged custom category ordering is available");Check(typeof(MainWindow).GetMethod("BeginCategoryRename",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!=null,"packaged application contains inline category renaming");state.CategoryPassword=PasswordSecrets.Create("ReleaseOnly-1234");first.IsPrivate=true;var privateNote=main.Service.Add("发布检查受保护事项",first.Id);Check(!main.Privacy.CanSee(privateNote),"packaged privacy filter hides protected notes");string unlockError;Check(main.Privacy.TryUnlock(first.Id,"ReleaseOnly-1234",true,out unlockError) && main.Privacy.CanSee(privateNote),"packaged shared password supports verified category unlock");main.Privacy.LockAll();Check(main.Save() && new CategoryPrivacy(store.Load()).IsLocked(first.Id),"packaged protection remains locked after reload");Check(typeof(MainWindow).Assembly.GetType("DesktopNotes.TrashConfirmWindow")!=null,"packaged application contains themed recycle-bin confirmation");var tray=(TrayIcon)typeof(MainWindow).GetField("tray",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(main);Check(!tray.Visible,"packaged smoke preview does not register a tray history entry");result=0;
            }catch(Exception ex) {Console.Error.WriteLine(ex);}finally {main.Exit();}};timer.Start();
        };main.Show();app.Run();return result;
    }
}


