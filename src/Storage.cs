using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace DesktopNotes {
    public partial class Storage {
        readonly string folder;
        public string Notice { get; private set; }
        public string FilePath { get { return Path.Combine(folder, "notes.json"); } }
        public Storage(string folder) { this.folder = folder; }
        static JavaScriptSerializer Serializer() { return new JavaScriptSerializer { MaxJsonLength = 16 * 1024 * 1024, RecursionLimit = 50 }; }
        static byte[] DataBytes(NoteState state) {
            byte[] bytes=new UTF8Encoding(false).GetBytes(Serializer().Serialize(state));
            if(bytes.Length>16*1024*1024)throw new ArgumentException("便签数据超过16MB，请减少过长的事项或步骤后重试。原有文件已保留。");return bytes;
        }
        static void Validate(NoteState s) {
            if (s == null || s.Version != 1 || s.Notes == null || s.Categories == null || s.Settings == null) throw new ArgumentException("备份格式或版本不正确。");
            if (s.Notes.Count > 100000 || s.Categories.Count > 1000) throw new ArgumentException("备份内容超出支持范围。");
            if((s.CategoryPassword!=null && !s.CategoryPassword.IsWellFormed()) || (s.CategoryPassword==null && s.Categories.Any(c=>c!=null && c.IsPrivate)))throw new ArgumentException("分类密码信息不完整或格式不正确，原有数据不会被替换。");
            if (s.Categories.Any(c => c == null || String.IsNullOrWhiteSpace(c.Id) || c.Id == "inbox" || c.Id == "done" || c.Id == "trash" || String.IsNullOrWhiteSpace(c.Name) || c.Name.Length > 30 || c.Name == "未完成" || c.Name == "已完成" || c.Name == "回收站") || s.Categories.Select(c => c.Id).Distinct().Count() != s.Categories.Count || s.Categories.Select(c => c.Name).Distinct().Count() != s.Categories.Count) throw new ArgumentException("备份的分类不正确或重复。");
            foreach (var n in s.Notes) {
                DateTimeOffset time;
                if (n == null || String.IsNullOrWhiteSpace(n.Id) || String.IsNullOrWhiteSpace(n.Text) || n.Text.Length > 20000 || !DateTimeOffset.TryParse(n.Created, out time) || (n.Done && !DateTimeOffset.TryParse(n.Completed, out time))) throw new ArgumentException("备份包含无效事项或时间。");
                if (n.CategoryId != "inbox" && !s.Categories.Any(c => c.Id == n.CategoryId)) n.CategoryId = "inbox";
                if(n.Steps==null)n.Steps=new List<NoteStep>();
                if(n.Steps.Count>200 || n.Steps.Any(step=>step==null || String.IsNullOrWhiteSpace(step.Id) || String.IsNullOrWhiteSpace(step.Text) || step.Text.Length>20000) || n.Steps.Select(step=>step.Id).Distinct().Count()!=n.Steps.Count)throw new ArgumentException("备份包含无效或重复的步骤。");
            }
            if (s.Notes.Select(n => n.Id).Distinct().Count() != s.Notes.Count) throw new ArgumentException("备份包含重复事项。");
            if(s.Notes.Any(n=>n.SortOrder<=0)) {
                int next=s.Notes.Count==0 ? 0:s.Notes.Max(n=>Math.Min(1000000000,n.SortOrder));
                foreach(var note in s.Notes.Where(n=>n.SortOrder<=0).OrderBy(n=>DateTimeOffset.Parse(n.Created)))note.SortOrder=++next;
            }
            var p = s.Settings;
            if (Double.IsNaN(p.Opacity) || Double.IsInfinity(p.Opacity)) p.Opacity = .9;
            p.Opacity = Math.Max(.3, Math.Min(1, p.Opacity));
            p.BackgroundOpacity=NormalizeOpacity(p.BackgroundOpacity,.7);
            p.TextOpacity=NormalizeOpacity(p.TextOpacity,1);
            p.UiSize=Double.IsNaN(p.UiSize) || Double.IsInfinity(p.UiSize) ? 50:Math.Max(0,Math.Min(100,p.UiSize));
            if(p.ThemeMode!="light" && p.ThemeMode!="dark" && p.ThemeMode!="system")p.ThemeMode="dark";
            if(p.BackgroundMode!="color" && p.BackgroundMode!="image")p.BackgroundMode="default";
            if(p.BackgroundColor==null || !System.Text.RegularExpressions.Regex.IsMatch(p.BackgroundColor,@"\A#[0-9a-fA-F]{6}\z"))p.BackgroundColor="#26364F";
            if(p.BackgroundImage==null || !System.Text.RegularExpressions.Regex.IsMatch(p.BackgroundImage,@"\A[0-9a-f]{64}\.(png|jpg|jpeg|bmp)\z"))p.BackgroundImage="";
            ShortcutGesture shortcut;string shortcutError;if(!ShortcutGesture.TryParse(p.QuickAddShortcut,out shortcut,out shortcutError))p.QuickAddShortcut="Ctrl+I";else p.QuickAddShortcut=shortcut.Canonical;
            if(p.QuickAddDraft==null)p.QuickAddDraft="";if(p.QuickAddDraft.Length>20000)throw new ArgumentException("快捷添加草稿过长。");
            if(p.QuickAddDestination!="inbox")p.QuickAddDestination="current";
            p.QuickAddOpacity=NormalizeOpacity(p.QuickAddOpacity,.85);
            if(Double.IsNaN(p.QuickAddPositionX) || Double.IsInfinity(p.QuickAddPositionX) || p.QuickAddPositionX<0 || p.QuickAddPositionX>1 || Double.IsNaN(p.QuickAddPositionY) || Double.IsInfinity(p.QuickAddPositionY) || p.QuickAddPositionY<0 || p.QuickAddPositionY>1) {p.QuickAddPositionX=-1;p.QuickAddPositionY=-1;}
            if (Double.IsNaN(p.Left) || Double.IsInfinity(p.Left)) p.Left = -99999;
            if (Double.IsNaN(p.Top) || Double.IsInfinity(p.Top)) p.Top = 60;
            if (Double.IsNaN(p.Width) || Double.IsInfinity(p.Width)) p.Width = 460;
            if (Double.IsNaN(p.Height) || Double.IsInfinity(p.Height)) p.Height = 560;
            p.Width = Math.Max(380, Math.Min(1600, p.Width)); p.Height = Math.Max(360, Math.Min(1600, p.Height));
            if(Double.IsNaN(p.SidebarWidth) || Double.IsInfinity(p.SidebarWidth))p.SidebarWidth=108;
            p.SidebarWidth=Math.Max(100,Math.Min(1300,p.SidebarWidth));
        }
        static double NormalizeOpacity(double value,double fallback) {return Double.IsNaN(value)||Double.IsInfinity(value) ? fallback : Math.Max(0,Math.Min(1,value));}
        public NoteState Import(string path) {
            try {
                if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new ArgumentException("备份文件过大。");
                string json=File.ReadAllText(path,Encoding.UTF8);
                var root=Serializer().DeserializeObject(json) as Dictionary<string,object>;
                if(root==null || new []{"Version","Notes","Categories","Settings"}.Any(key=>!root.ContainsKey(key)))throw new ArgumentException("这不是完整的便签备份文件。");
                var s = Serializer().Deserialize<NoteState>(json);
                var settings=root["Settings"] as Dictionary<string,object>;
                if(settings!=null && s!=null && s.Settings!=null) {
                    double legacy=NormalizeOpacity(s.Settings.Opacity,.9);
                    if(!settings.ContainsKey("BackgroundOpacity"))s.Settings.BackgroundOpacity=legacy;
                    if(!settings.ContainsKey("TextOpacity"))s.Settings.TextOpacity=legacy;
                }
                Validate(s); return s;
            }
            catch (ArgumentException) { throw; } catch(Exception ex) { throw new ArgumentException("无法读取备份：" + ex.Message); }
        }
        public NoteState Load() {
            Directory.CreateDirectory(folder); Notice = null;
            if (!File.Exists(FilePath) && !File.Exists(FilePath + ".bak")) return new NoteState();
            try { return Import(FilePath); }
            catch (Exception ex) {
                Notice = "主数据无法读取：" + ex.Message;
                if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".damaged-" + DateTime.Now.ToString("yyyyMMddHHmmssfff"), true);
                if (File.Exists(FilePath + ".bak")) {
                    try { var backup = Import(FilePath + ".bak"); Notice += "\n已恢复上一次有效备份。损坏文件已保留。"; File.Copy(FilePath + ".bak", FilePath, true); return backup; } catch { }
                }
                // Do not let an empty state overwrite unreadable originals.
                throw new IOException(Notice + "\n自动备份也无法恢复，原文件已保留。请从导出的备份恢复，或联系制作方。", ex);
            }
        }
        public void Save(NoteState state) {
            Validate(state); Directory.CreateDirectory(folder);
            byte[] bytes=DataBytes(state);
            string original=Path.Combine(folder,"notes.before-steps.json");
            if(File.Exists(FilePath) && !File.Exists(original)) {
                var root=Serializer().DeserializeObject(File.ReadAllText(FilePath,Encoding.UTF8)) as Dictionary<string,object>;
                object raw;var notes=root!=null && root.TryGetValue("Notes",out raw) ? raw as object[]:null;
                if(notes!=null && (notes.Length==0 || notes.Any(item=> {var note=item as Dictionary<string,object>;return note!=null && !note.ContainsKey("Steps");})))File.Copy(FilePath,original,false);
            }
            string temp = FilePath + ".tmp";
            using(var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak"); else File.Move(temp, FilePath);
        }
        public void Export(string path, NoteState state) {
            var copy = Serializer().Deserialize<NoteState>(Serializer().Serialize(state)); copy.Settings = new Preferences();copy.IncludesDrafts=false; Validate(copy);
            byte[] bytes=DataBytes(copy);string temp = path + ".tmp"; File.WriteAllBytes(temp,bytes);
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
        public string Snapshot(NoteState state) { string path = Path.Combine(folder, "before-import-" + DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".json"); Export(path, state); return path; }
    }
}

