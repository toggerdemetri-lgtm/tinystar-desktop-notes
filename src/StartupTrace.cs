using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace DesktopNotes {
    public partial class MainWindow {
        internal object StartupView {get {return new {Category=active,RenderedNoteIds=rows.Children.OfType<FrameworkElement>().Select(r=>r.Tag as string).Where(id=>id!=null).ToArray(),Editing=editor!=null};}}
    }
    // Local, bounded diagnostics. Never record note text, passwords or drafts.
    internal static class StartupTrace {
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern uint GetFinalPathNameByHandle(IntPtr file,StringBuilder path,uint count,uint flags);
        static string PhysicalPath(string file){var value=new StringBuilder(4096);using(var input=File.Open(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){if(GetFinalPathNameByHandle(input.SafeFileHandle.DangerousGetHandle(),value,4096,0)==0)return null;}return value.ToString();}
        static string path,folder;static int records;static bool firstSave;
        internal static string TracePath {get {return path;}}
        internal static void Begin(string dataFolder) {
            try {
                folder=Path.GetFullPath(dataFolder);records=0;firstSave=false;
                string directory=Path.Combine(Path.GetTempPath(),"TinyStar-Startup");Directory.CreateDirectory(directory);
                path=Path.Combine(directory,"startup-"+DateTime.Now.ToString("yyyyMMdd-HHmmssfff")+"-"+Process.GetCurrentProcess().Id+".jsonl");
                Record("start",null,null,null);
            }catch {path=null;}
        }
        static string Hash(byte[] bytes) {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
        static string ContentHash(NoteState state) {
            if(state==null)return null;
            var content=new {Notes=state.Notes.OrderBy(n=>n.Id).ToArray(),Categories=state.Categories.OrderBy(c=>c.Id).ToArray()};
            return Hash(Encoding.UTF8.GetBytes(new JavaScriptSerializer {MaxJsonLength=16*1024*1024}.Serialize(content)));
        }
        static IEnumerable<DependencyObject> All(DependencyObject d) {yield return d;for(int i=0;i<VisualTreeHelper.GetChildrenCount(d);i++)foreach(var c in All(VisualTreeHelper.GetChild(d,i)))yield return c;}
        internal static void BeforeFirstSave(Storage store,NoteState state,MainWindow window) {
            if(path==null || firstSave)return;firstSave=true;Record("before-first-save",store,state,window);
        }
        internal static void Record(string phase,Storage store,NoteState state,MainWindow window) {
            if(path==null || records++>=24)return;
            try {
                string file=store==null ? Path.Combine(folder,"notes.json"):store.FilePath;
                var item=new Dictionary<string,object> {
                    {"time",DateTimeOffset.Now.ToString("o")},{"phase",phase},{"pid",Process.GetCurrentProcess().Id},
                    {"version",typeof(StartupTrace).Assembly.GetName().Version.ToString()},
                    {"executable",Process.GetCurrentProcess().MainModule.FileName},{"assembly",typeof(StartupTrace).Assembly.Location},
                    {"knownLocalAppData",Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)},
                    {"workingDirectory",Environment.CurrentDirectory},{"dataFile",file},
                    {"packageFamily",DesktopLaunchContext.PackageFamily},{"primaryExists",File.Exists(file)},{"backupExists",File.Exists(file+".bak")},
                    {"modelContentHash",ContentHash(state)},{"modelNoteCount",state==null ? -1:state.Notes.Count}
                };
                if(state!=null && File.Exists(file)) {
                    try {
                        string before=Hash(File.ReadAllBytes(file));var disk=new Storage(Path.GetDirectoryName(file)).Import(file);string after=Hash(File.ReadAllBytes(file));
                        item["physicalDataFile"]=PhysicalPath(file);item["primaryHash"]=after;item["primaryWriteUtc"]=File.GetLastWriteTimeUtc(file).ToString("o");item["stableRead"]=before==after;
                        item["diskContentHash"]=ContentHash(disk);item["diskNoteCount"]=disk.Notes.Count;
                        item["contentMatchesDisk"]=before==after && ContentHash(state)==ContentHash(disk);
                    }catch(Exception ex) {item["diagnosticReadErrorType"]=ex.GetType().Name;}
                }
                if(window!=null && state!=null) {
                    var labels=All(window).OfType<TextBlock>().Where(t=>t.IsVisible).Select(t=>t.Text).ToList();
                    item["placement"]=window.PlacementDiagnostic;item["visibleNoteCount"]=state.Notes.Count(n=>labels.Contains(n.Text));
                    item["pendingEdit"]=!String.IsNullOrEmpty(state.Settings.EditId);
                    item["visible"]=window.IsVisible;item["view"]=window.StartupView;item["draftCategory"]=state.Settings.DraftCategory;
                }
                File.AppendAllText(path,new JavaScriptSerializer().Serialize(item)+Environment.NewLine,new UTF8Encoding(false));
            }catch { /* Diagnostics must never interrupt reading, rendering or saving. */ }
        }
    }
}



