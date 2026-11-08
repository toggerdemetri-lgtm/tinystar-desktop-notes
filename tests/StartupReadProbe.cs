using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopNotes;
class StartupReadProbe {
    static readonly string Latest="新增事项-仅隔离测试-20261106";
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS: "+message);}
    static IEnumerable<DependencyObject> All(DependencyObject d){yield return d;for(int i=0;i<VisualTreeHelper.GetChildrenCount(d);i++)foreach(var c in All(VisualTreeHelper.GetChild(d,i)))yield return c;}
    [STAThread]static int Main(string[] args){try{if(args.Length>0 && args[0]=="--child")return Child(args[1],args[2]);return Parent();}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
    static int Child(string folder,string receipt){
        int result=1;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(150)};
        timer.Tick+=delegate{var app=Application.Current;var window=app==null ? null:app.MainWindow as MainWindow;if(window==null || !window.IsLoaded)return;timer.Stop();try{
            window.UpdateLayout();Check(window.Store.FilePath==Path.Combine(folder,"notes.json"),"actual Program.Main resolves the intended independent data file");
            Check(window.Service.State.Notes.Any(n=>n.Text==Latest),"first visible state contains the latest saved note");
            Check(All(window).OfType<TextBlock>().Any(t=>t.Text==Latest),"first visible UI renders the latest saved note");
            var traceType=typeof(MainWindow).Assembly.GetType("DesktopNotes.StartupTrace");var tracePath=traceType==null ? "":(string)traceType.GetProperty("TracePath",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null,null);File.WriteAllText(receipt+".trace",tracePath ?? "");if(traceType!=null)traceType.GetMethod("Record",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"test-stale-model",window.Store,new NoteState(),null});File.WriteAllText(receipt,"version="+typeof(MainWindow).Assembly.GetName().Version+"\npath="+window.Store.FilePath+"\nnotes="+window.Service.State.Notes.Count+"\nlatest=True\n");result=0;
        }catch(Exception ex){Console.Error.WriteLine(ex);}finally{window.Exit();}};timer.Start();
        typeof(MainWindow).Assembly.GetType("DesktopNotes.Program").GetMethod("Main",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{new[]{"--test-mode","--data-dir",folder}});return result;
    }
    static void RunChild(string executable,string arguments,string cwd){var info=new ProcessStartInfo(executable,arguments){UseShellExecute=true,WorkingDirectory=cwd};using(var p=Process.Start(info)){if(p==null)throw new Exception("No child process handle");if(!p.WaitForExit(15000)){p.Kill();throw new Exception("Own isolated startup probe timed out");}Check(p.ExitCode==0,"actual entry point completes without startup error");}}
    static int Parent(){bool requireTrace=Environment.GetEnvironmentVariable("TINYSTAR_REQUIRE_STARTUP_TRACE")=="1";
        string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"upgrade-fixture"),data=Path.Combine(root,"data");Directory.CreateDirectory(data);
        var store=new Storage(data);var state=new NoteState();state.Settings.QuickAddShortcut="Ctrl+Alt+Shift+F6";state.Settings.DraftCategory="inbox";
        var service=new NoteService(state);service.Add("旧事项-仅隔离测试","inbox");store.Save(state);service.Add(Latest,"inbox");store.Save(state);
        Check(File.ReadAllText(store.FilePath).Contains(Latest) && !File.ReadAllText(store.FilePath+".bak").Contains(Latest),"fixture has a latest primary and an older backup");
        string exe=Assembly.GetExecutingAssembly().Location,first=Path.Combine(root,"first.txt"),second=Path.Combine(root,"second.txt");
        RunChild(exe,"--child \""+data+"\" \""+first+"\"",Path.GetTempPath());
        Check(File.ReadAllText(first).Contains("latest=True"),"installer-style first launch reads the latest primary");
        string link=Path.Combine(root,"TinyStar-test-shortcut.lnk");Type shellType=Type.GetTypeFromProgID("WScript.Shell");dynamic shell=Activator.CreateInstance(shellType);dynamic shortcut=shell.CreateShortcut(link);shortcut.TargetPath=exe;shortcut.Arguments="--child \""+data+"\" \""+second+"\"";shortcut.WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory;shortcut.Save();
        RunChild(link,"",AppDomain.CurrentDomain.BaseDirectory);
        if(requireTrace){foreach(var receipt in new[]{first,second}){string trace=File.ReadAllText(receipt+".trace");Check(File.Exists(trace),"first launch and relaunch each produce a diagnostic trace");string log=File.ReadAllText(trace);Check(log.Contains("\"phase\":\"loaded\"") && log.Contains("\"phase\":\"first-render\""),"trace distinguishes file load from first rendering");Check(log.Contains("\"contentMatchesDisk\":true"),"trace confirms loaded content matches the primary data file");Check(log.Contains("\"contentMatchesDisk\":false"),"trace detects an injected stale model without changing saved data");Check(!log.Contains(Latest) && !log.Contains("旧事项-仅隔离测试"),"trace omits personal note text");}}Check(File.ReadAllText(first)==File.ReadAllText(second),"installer-style first launch and shortcut relaunch use identical data and notes");
        Console.WriteLine("ALL CLEAN UPGRADE STARTUP CHECKS PASSED");return 0;
    }
}



