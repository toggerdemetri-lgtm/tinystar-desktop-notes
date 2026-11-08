using System;
using System.Collections.Generic;
using System.Linq;
namespace DesktopNotes {
    public class Note {
        public string Id { get; set; }
        public string Text { get; set; }
        public string Created { get; set; }
        public string Completed { get; set; }
        public string CategoryId { get; set; }
        public bool Done { get; set; }
        public bool Deleted { get; set; }
        public int SortOrder {get;set;}
        public List<NoteStep> Steps {get;set;}
        public Note() {Steps=new List<NoteStep>();}
    }
    public class NoteStep {public string Id {get;set;}public string Text {get;set;}public bool Done {get;set;}}
    public class Category { public string Id { get; set; } public string Name { get; set; } public bool IsPrivate {get;set;} }
    public class PasswordCredential {
        public string Algorithm {get;set;}public string Salt {get;set;}public string Hash {get;set;}public int Iterations {get;set;}
        public bool IsWellFormed() {
            if(Algorithm!="PBKDF2-SHA256" || Iterations<100000 || Iterations>600000 || Salt==null || Salt.Length!=24 || Hash==null || Hash.Length!=44)return false;
            try {return Convert.FromBase64String(Salt).Length==16 && Convert.FromBase64String(Hash).Length==32;}catch(FormatException) {return false;}
        }
    }
    public class Preferences {
        public double Opacity { get; set; }
        public double BackgroundOpacity { get; set; }
        public double TextOpacity { get; set; }
        public double UiSize {get;set;}
        public string ThemeMode { get; set; }
        public string BackgroundMode {get;set;}
        public string BackgroundColor {get;set;}
        public string BackgroundImage {get;set;}
        public bool AnimationsEnabled {get;set;}
        public bool Locked { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double SidebarWidth {get;set;}
        public double Height { get; set; }
        public string Draft { get; set; }
        public string DraftCategory { get; set; }
        public string EditId { get; set; }
        public string EditDraft { get; set; }
        public string EditStepId {get;set;}
        public bool EditNewStep {get;set;}
        public string QuickAddShortcut {get;set;}
        public string QuickAddDestination {get;set;}
        public string QuickAddDraft {get;set;}
        public double QuickAddPositionX {get;set;}
        public double QuickAddPositionY {get;set;}
        public double QuickAddOpacity {get;set;}
        public bool UnlockAllCategories {get;set;}
        public Preferences() { UiSize=50;Opacity = .9; BackgroundOpacity=.7;TextOpacity=1;ThemeMode="dark";BackgroundMode="default";BackgroundColor="#26364F";BackgroundImage="";AnimationsEnabled=true;QuickAddShortcut="Ctrl+I";QuickAddDestination="current";QuickAddDraft="";QuickAddPositionX=-1;QuickAddPositionY=-1;QuickAddOpacity=.85;SidebarWidth=108; Left = -99999; Top = 60; Width = 460; Height = 560; }
    }
    public class NoteState {
        public int Version { get; set; }
        public List<Note> Notes { get; set; }
        public List<Category> Categories { get; set; }
        public Preferences Settings { get; set; }
        public PasswordCredential CategoryPassword {get;set;}
        public bool IncludesDrafts {get;set;}
        public NoteState() { Version = 1; Notes = new List<Note>(); Categories = new List<Category>(); Settings = new Preferences(); }
    }
    public class NoteService {
        public NoteState State { get; private set; }
        public NoteService(NoteState state) { State = state; }
        static string Text(string value) { if (String.IsNullOrWhiteSpace(value)) throw new ArgumentException("内容不能为空。"); if (value.Length > 20000) throw new ArgumentException("内容太长，请控制在两万字以内。"); return value.Trim(); }
        public bool HasCategory(string id) { return id == "inbox" || State.Categories.Any(c => c.Id == id); }
        public string CategoryName(string id) { var c = State.Categories.FirstOrDefault(x => x.Id == id); return c == null ? "未完成" : c.Name; }
        public Note Find(string id) { var n = State.Notes.FirstOrDefault(x => x.Id == id); if (n == null) throw new ArgumentException("事项不存在。"); return n; }
        public Note Add(string text, string category) {
            var n = new Note { Id = Guid.NewGuid().ToString("N"), Text = Text(text), Created = DateTimeOffset.Now.ToString("o"), CategoryId = HasCategory(category) ? category : "inbox",SortOrder=NextOrder() };
            var existing=State.Notes.Where(item=>!item.Done && !item.Deleted && item.CategoryId==n.CategoryId).OrderByDescending(item=>item.SortOrder).ThenByDescending(item=>item.Created).ToList();
            if(existing.Count>0) {int order=existing.Count+1;foreach(var item in existing)item.SortOrder=order--;n.SortOrder=1;}
            State.Notes.Add(n); return n;
        }
        public void Edit(string id, string text) { Find(id).Text = Text(text); }
        public void Toggle(string id) { var n = Find(id); n.Done = !n.Done;if(n.Done)foreach(var step in n.Steps)step.Done=true;n.SortOrder=NextOrder();n.Completed = n.Done ? DateTimeOffset.Now.ToString("o") : null; if (!HasCategory(n.CategoryId)) n.CategoryId = "inbox"; }
        public void SetSteps(string id,IEnumerable<NoteStep> steps) {
            var n=Find(id);if(n.Deleted)throw new ArgumentException("请先还原事项再管理步骤。");
            var copy=new List<NoteStep>();foreach(var step in steps ?? Enumerable.Empty<NoteStep>()) {
                if(step==null || String.IsNullOrWhiteSpace(step.Id))throw new ArgumentException("步骤信息不完整。");
                copy.Add(new NoteStep {Id=step.Id,Text=Text(step.Text),Done=step.Done});
                if(copy.Count>200)throw new ArgumentException("每个事项最多支持200个步骤。");
            }
            if(copy.Select(s=>s.Id).Distinct().Count()!=copy.Count)throw new ArgumentException("步骤编号不能重复。");n.Steps=copy;
        }
        public void ToggleStep(string id,string stepId) {
            var n=Find(id);if(n.Deleted)throw new ArgumentException("请先还原事项再修改步骤。");
            var step=n.Steps.FirstOrDefault(s=>s.Id==stepId);if(step==null)throw new ArgumentException("步骤不存在。");
            step.Done=!step.Done;
            if(n.Done && !step.Done)Toggle(id);
            else if(!n.Done && step.Done && n.Steps.All(s=>s.Done))Toggle(id);
        }
        public void ReorderStep(string id,string stepId,string targetId,bool before) {
            var note=Find(id);if(note.Deleted)throw new ArgumentException("请先还原事项再调整步骤。");
            var source=note.Steps.FirstOrDefault(s=>s.Id==stepId);var target=note.Steps.FirstOrDefault(s=>s.Id==targetId);
            if(source==null || target==null)throw new ArgumentException("只能调整同一事项中的步骤。");if(source==target)return;
            var list=note.Steps.ToList();list.Remove(source);list.Insert(list.IndexOf(target)+(before ? 0:1),source);note.Steps=list;
        }
        int NextOrder() {int max=State.Notes.Count==0 ? 0:State.Notes.Max(n=>n.SortOrder);if(max>=1000000000) {int i=1;foreach(var note in State.Notes.OrderBy(n=>n.SortOrder))note.SortOrder=i++;max=i;}return max+1;}
        public void Reorder(string id,string targetId,bool before) {
            var note=Find(id);var target=Find(targetId);if(id==targetId)return;
            if(note.Deleted || target.Deleted || note.Done!=target.Done || (!note.Done && note.CategoryId!=target.CategoryId))throw new ArgumentException("只能在当前分类内调整顺序。");
            var list=State.Notes.Where(n=>!n.Deleted && n.Done==note.Done && (note.Done || n.CategoryId==note.CategoryId)).OrderByDescending(n=>n.SortOrder).ThenByDescending(n=>n.Created).ToList();
            list.Remove(note);int position=list.IndexOf(target)+(before ? 0:1);list.Insert(position,note);int order=list.Count;foreach(var item in list)item.SortOrder=order--;
        }
        public void Move(string id, string category) { if (!HasCategory(category)) throw new ArgumentException("分类不存在。"); Find(id).CategoryId = category; }
        void CheckCategoryName(string name, string except) {
            if (name.Length > 30) throw new ArgumentException("分类名称最多 30 个字。");
            if (name == "未完成" || name == "已完成" || name == "回收站" || State.Categories.Any(c => c.Name == name && c.Id != except)) throw new ArgumentException("这个分类名称已被使用。");
        }
        public Category AddCategory(string name) { name = Text(name); CheckCategoryName(name, null); var c = new Category { Id = Guid.NewGuid().ToString("N"), Name = name }; State.Categories.Add(c); return c; }
        public void RenameCategory(string id, string name) { name = Text(name); CheckCategoryName(name, id); var c = State.Categories.FirstOrDefault(x => x.Id == id); if (c == null) throw new ArgumentException("不能修改系统分类。"); c.Name = name; }
        public void ReorderCategory(string id,string targetId,bool before) {
            var source=State.Categories.FirstOrDefault(c=>c.Id==id);var target=State.Categories.FirstOrDefault(c=>c.Id==targetId);
            if(source==null || target==null)throw new ArgumentException("只能调整自定义分类的顺序。");if(source==target)return;
            var list=State.Categories.ToList();list.Remove(source);list.Insert(list.IndexOf(target)+(before ? 0:1),source);State.Categories=list;
        }
        public void DeleteCategory(string id) { if (id == "inbox") throw new ArgumentException("不能删除系统分类。"); State.Categories.RemoveAll(c => c.Id == id); foreach(var n in State.Notes.Where(x => x.CategoryId == id)) n.CategoryId = "inbox"; }
        public void Trash(string id) { Find(id).Deleted = true; }
        public void Restore(string id) { var n = Find(id); n.Deleted = false; if (!HasCategory(n.CategoryId)) n.CategoryId = "inbox"; }
        public void Remove(string id) { State.Notes.RemoveAll(n => n.Id == id && n.Deleted); }
    }
}

