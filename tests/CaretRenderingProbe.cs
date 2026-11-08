using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DesktopNotes;
class CaretRenderingProbe {
    static IEnumerable<DependencyObject> All(DependencyObject d) {
        yield return d;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(d);i++)foreach(var c in All(VisualTreeHelper.GetChild(d,i)))yield return c;
    }
    static void Check(bool ok,string name) {if(!ok)throw new Exception(name);Console.WriteLine("PASS: "+name);}
    static int CountCaret(Window w,TextBox input,string path) {
        w.UpdateLayout();var bmp=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(w);
        Rect r=input.GetRectFromCharacterIndex(input.CaretIndex);if(r.IsEmpty)return 0;
        r=input.TransformToAncestor(w).TransformBounds(new Rect(r.X-2,r.Y,5,r.Height));
        var pixels=new byte[bmp.PixelWidth*bmp.PixelHeight*4];bmp.CopyPixels(pixels,bmp.PixelWidth*4,0);int count=0;
        for(int y=Math.Max(0,(int)r.Top);y<Math.Min(bmp.PixelHeight,(int)Math.Ceiling(r.Bottom));y++)
            for(int x=Math.Max(0,(int)r.Left);x<Math.Min(bmp.PixelWidth,(int)Math.Ceiling(r.Right));x++) {
                int i=(y*bmp.PixelWidth+x)*4;
                if(pixels[i]>150 && pixels[i]>pixels[i+2]+30 && pixels[i+1]>pixels[i+2]+15)count++;
            }
        if(path!=null) {var e=new PngBitmapEncoder();e.Frames.Add(BitmapFrame.Create(bmp));using(var f=File.Create(path))e.Save(f);}
        return count;
    }
    static void DetachNativeCaret(Window w) {
        foreach(var caret in All(w).OfType<Adorner>().Where(c=>c.GetType().Name=="CaretElement").ToList()) {
            var layer=AdornerLayer.GetAdornerLayer(caret.AdornedElement);if(layer!=null)layer.Remove(caret);
        }
    }
    [STAThread]static int Main(string[] args) {
        bool desktop=args.Contains("--desktop"),detach=args.Contains("--detach-caret");
        var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};var state=new NoteState();
        state.Settings.ThemeMode="dark";state.Settings.Left=600;state.Settings.Top=150;state.Settings.Width=650;state.Settings.Height=480;
        state.Settings.UiSize=args.Contains("--fractional") ? 47:50;state.Settings.QuickAddShortcut="Ctrl+Alt+Shift+F7";
        var w=new MainWindow(new Storage(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"caret-render-data")),state,!desktop);
        TextBox input=null;Button settings=null;int result=1,stage=0,sample=0,max=0,min=Int32.MaxValue,retries=0;
        bool resetVerified=false,pendingReset=false;
        var cases=new List<Action>();var names=new List<string>();var visible=new List<bool>();
        Action<string,Action,bool> add=delegate(string name,Action setup,bool show) {names.Add(name);cases.Add(setup);visible.Add(show);};
        Action focus=delegate {w.Activate();input.Focus();Keyboard.Focus(input);};
        w.Loaded+=delegate {
            input=All(w).OfType<TextBox>().Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="新事项");
            settings=All(w).OfType<Button>().Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="设置");
            add("focused empty input",delegate {input.Clear();focus();},true);
            add("Chinese text insertion point",delegate {input.Text="测试中文光标";input.CaretIndex=2;focus();},true);
            add("selected text hides insertion cue",delegate {focus();input.SelectAll();},false);
            add("clearing horizontally scrolled text",delegate {input.Text=new string('长',2000);input.CaretIndex=input.Text.Length;input.ScrollToEnd();w.UpdateLayout();input.Clear();focus();},true);
            add("unfocused input hides insertion cue",delegate {settings.Focus();},false);
            add("refocusing restores insertion cue",delegate {focus();},true);
            foreach(double size in new[]{0.0,47.0,100.0}) {
                double value=size;add("UI size "+size,delegate {state.Settings.UiSize=value;w.ApplyAppearance();input.Clear();focus();},true);
            }
            add("light theme",delegate {state.Settings.ThemeMode="light";w.ApplyAppearance();focus();},true);
            add("transparent backgrounds and text",delegate {state.Settings.ThemeMode="dark";state.Settings.BackgroundOpacity=0;state.Settings.TextOpacity=0;w.ApplyAppearance();focus();},true);
            add("Enter saves note and retains insertion cue",delegate {
                state.Settings.BackgroundOpacity=.7;state.Settings.TextOpacity=1;state.Settings.UiSize=47;w.ApplyAppearance();input.Text="光标回归测试";input.CaretIndex=input.Text.Length;focus();
                input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(input),0,Key.Enter){RoutedEvent=Keyboard.PreviewKeyDownEvent});
                Check(state.Notes.Any(n=>n.Text=="光标回归测试") && input.Text=="","Enter adds Chinese note without changing input behavior");
            },true);
            var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(100)};
            timer.Tick+=delegate {
                try {
                    if(sample==0) {cases[stage]();w.UpdateLayout();}
                    if((visible[stage] || stage==2) && !input.IsKeyboardFocused){if(++retries>3)throw new Exception("Test input repeatedly lost foreground focus");Console.WriteLine("ENV: reacquiring test input focus");focus();if(stage==2)input.SelectAll();if(detach)DetachNativeCaret(w);sample=1;max=0;min=Int32.MaxValue;}if(sample==1 && detach)DetachNativeCaret(w);
                    if(sample>=1) {int count=CountCaret(w,input,null);min=Math.Min(min,count);max=Math.Max(max,count);if(pendingReset){Check(count>=5,"keyboard activity restarts with a visible caret");pendingReset=false;resetVerified=true;}if(args.Contains("--blink") && stage==0 && sample>3 && count==0 && !resetVerified && !pendingReset){input.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(input),0,Key.Left){RoutedEvent=Keyboard.PreviewKeyDownEvent});pendingReset=true;}}
                    sample++;
                    if(sample<(args.Contains("--idle") && stage==0 ? 150:16))return;
                    Console.WriteLine("STATE: "+names[stage]+" focused="+input.IsKeyboardFocused+" selection="+input.SelectionLength);Check(input.IsKeyboardFocused==(visible[stage] || input.SelectionLength>0),names[stage]+": expected keyboard focus/selection state");
                    bool selection=input.SelectionLength>0;
                    if(visible[stage]) {Check(max>=5,names[stage]+": visible blue insertion pixels");if(args.Contains("--blink"))Check(min==0,names[stage]+": caret alternates visible and hidden");else if(detach)Check(min>=5,names[stage]+": cue stays visible with native caret detached");}
                    else Check(max==0,names[stage]+": no extra insertion pixels");
                    Console.WriteLine("PIXELS: "+names[stage]+" min="+min+" max="+max+" selection="+selection);
                    if(stage==0)CountCaret(w,input,Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"caret-"+(desktop?"desktop":"window")+".png"));
                    stage++;sample=0;max=0;min=Int32.MaxValue;retries=0;
                    if(stage!=cases.Count)return;timer.Stop();result=0;w.Exit();app.Shutdown();
                }catch(Exception ex) {timer.Stop();Console.Error.WriteLine(ex);w.Exit();app.Shutdown();}
            };timer.Start();
        };w.Show();app.Run();return result;
    }
}





