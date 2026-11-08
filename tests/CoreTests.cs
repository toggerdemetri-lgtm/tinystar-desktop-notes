using System;
using System.IO;
using System.Linq;
using DesktopNotes;
class CoreTests
{
    static int count;
    static void Check(bool ok, string name) { if (!ok) throw new Exception("FAIL: " + name); count++; Console.WriteLine("PASS: " + name); }
    static void Reject(Action a, string name) { bool rejected = false; try { a(); } catch (ArgumentException) { rejected = true; } Check(rejected, name); }
    static int Main() {
        string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DesktopNotesTests-" + Guid.NewGuid().ToString("N"));
        try {
            var s = new NoteService(new NoteState());
            var cat = s.AddCategory("工作");
            var n = s.Add("完成报告", cat.Id); string created = n.Created;
            s.Toggle(n.Id); Check(n.Done && n.CategoryId == cat.Id, "custom completion remembers category");
            s.Edit(n.Id, "新的报告"); Check(n.Created == created && n.Text == "新的报告", "edit preserves creation time");
            s.Toggle(n.Id); Check(!n.Done && n.CategoryId == cat.Id, "uncomplete returns to original category");
            s.Toggle(n.Id); s.DeleteCategory(cat.Id); s.Toggle(n.Id); Check(!n.Done && n.CategoryId == "inbox", "deleted category falls back");
            s.Trash(n.Id); s.Restore(n.Id); Check(!n.Deleted && n.Created == created, "trash restore preserves creation");
            Reject(delegate { s.Add("  ", "inbox"); }, "blank note rejected");
            Reject(delegate { s.Edit(n.Id, " "); }, "blank edit rejected");
            Reject(delegate { s.AddCategory("未完成"); }, "system category name reserved");
            var store = new Storage(dir); store.Save(s.State); s.Add("第二项", "inbox"); store.Save(s.State);
            var loaded = store.Load(); Check(loaded.Notes.Count == 2 && loaded.Notes[0].Created == created, "save reload preserves all data");
            string exported = Path.Combine(dir, "export.json"); store.Export(exported, s.State);
            var imported = store.Import(exported); Check(imported.Notes.Count == 2 && imported.Notes[0].Created == created, "backup round trip preserves timestamps");
            File.WriteAllText(exported, "{\"Version\":999,\"Notes\":[]}"); Reject(delegate { store.Import(exported); }, "unknown backup version rejected");
            File.WriteAllText(exported, "{}"); Reject(delegate { store.Import(exported); }, "empty object cannot replace user data");
            File.WriteAllText(exported, "{\"Version\":1,\"Notes\":[{\"Id\":\"x\",\"Text\":\"hello\",\"Created\":\"invalid\",\"CategoryId\":\"inbox\"}],\"Categories\":[]}");
            Reject(delegate { store.Import(exported); }, "invalid timestamp rejected");
            File.WriteAllText(Path.Combine(dir, "notes.json"), "broken json"); var recovered = store.Load();
            Check(recovered.Notes.Count == 1 && !String.IsNullOrEmpty(store.Notice), "corrupt primary recovers previous backup visibly");
            Check(Directory.GetFiles(dir, "*.damaged-*").Length == 1, "corrupt original preserved");
            var c2 = s.AddCategory("家里"); s.Move(n.Id, c2.Id); s.Trash(n.Id); s.DeleteCategory(c2.Id); s.Restore(n.Id);
            Check(n.CategoryId == "inbox" && !n.Deleted, "deleted category trash restoration fallback");
            Check(s.State.Settings.Opacity == .9, "default opacity 90 percent");
            var screens = new [] { new System.Drawing.Rectangle(0,0,1920,1080) };
            Check(!ScreenPlacement.TitleVisible(1500,60,380,1.5,screens), "scaled offscreen title detected");
            Check(ScreenPlacement.TitleVisible(800,60,380,1.5,screens), "scaled visible title retained");
            var prefs = new Preferences();
            Check(prefs.Width == 460 && prefs.BackgroundOpacity == .7 && prefs.TextOpacity == 1 && prefs.ThemeMode == "dark", "new visual defaults dark 70 percent");
            string legacy = Path.Combine(dir,"legacy.json");
            File.WriteAllText(legacy,"{\"Version\":1,\"Notes\":[],\"Categories\":[],\"Settings\":{\"Opacity\":0.42,\"Width\":320,\"Height\":500}}");
            var migrated=store.Import(legacy);
            Check(migrated.Settings.UiSize==50,"legacy settings retain current UI at slider 50 percent");
            Check(migrated.Settings.BackgroundOpacity==.42 && migrated.Settings.TextOpacity==.42 && migrated.Settings.Width==380,"legacy opacity migrates independently and minimum width grows");
            migrated.Settings.BackgroundOpacity=0;migrated.Settings.TextOpacity=.8;store.Save(migrated);var split=store.Load();
            Check(split.Settings.BackgroundOpacity==0 && split.Settings.TextOpacity==.8,"independent opacity zero survives restart");
            var start=new WindowBounds(100,200,460,560);
            var bounds=WindowGeometry.Resize(start,500,500,ResizeEdges.Left|ResizeEdges.Top,380,360);
            Check(bounds.Width==380 && bounds.Height==360 && bounds.X==180 && bounds.Y==400,"top left resize preserves opposite edges at minimum");
            bounds=WindowGeometry.Resize(start,-60,-80,ResizeEdges.Left|ResizeEdges.Top,380,360);
            Check(bounds.X==40 && bounds.Y==120 && bounds.Width==520 && bounds.Height==640,"top left expansion follows cursor");
            bounds=WindowGeometry.Resize(start,20,30,ResizeEdges.Right|ResizeEdges.Bottom,380,360);
            Check(bounds.X==100 && bounds.Y==200 && bounds.Width==480 && bounds.Height==590,"bottom right resize retains origin");
            foreach(ResizeEdges edge in new []{ResizeEdges.Left,ResizeEdges.Top,ResizeEdges.Right,ResizeEdges.Bottom}) {
                bounds=WindowGeometry.Resize(start,20,30,edge,380,360);
                bool horizontal=edge==ResizeEdges.Left || edge==ResizeEdges.Right;
                Check(horizontal ? bounds.Height==start.Height && bounds.Y==start.Y:bounds.Width==start.Width && bounds.X==start.X,"single edge resize preserves perpendicular dimension "+edge);
            }
            bounds=WindowGeometry.Snap(new WindowBounds(9,13,460,560),new WindowBounds(0,0,1920,1040),16);
            Check(bounds.X==0 && bounds.Y==0,"window snaps to work area left and top");
            bounds=WindowGeometry.Snap(new WindowBounds(1451,470,460,560),new WindowBounds(0,0,1920,1040),16);
            Check(bounds.X==1460 && bounds.Y==480,"window snaps above taskbar at right and bottom");
            bounds=WindowGeometry.Snap(new WindowBounds(-1905,100,460,560),new WindowBounds(-1920,0,1920,1040),16);
            Check(bounds.X==-1920,"negative monitor coordinates supported");
            var drag=new NoteDragSession("test-note",0,0);Check(!drag.Move(1,1,4) && drag.Move(10,0,4),"note drag starts only after threshold");
            Check(drag.Drop(false)==null,"drop outside trash cancels deletion");
            drag=new NoteDragSession("test-note",0,0);drag.Move(10,0,4);Check(drag.Drop(true)=="test-note" && drag.Drop(true)==null,"trash accepts dragged note only once");
            var ordered=new NoteService(new NoteState());var first=ordered.Add("第一项","inbox");var second=ordered.Add("第二项","inbox");string firstTime=first.Created;
            ordered.Reorder(first.Id,second.Id,true);
            Check(first.SortOrder>second.SortOrder && first.Created==firstTime,"reordering preserves creation time");
            double smooth=36,velocity=0;bool monotonic=true;
            for(int i=0;i<90;i++) {double previous=smooth;smooth=WindowGeometry.SmoothPosition(smooth,0,ref velocity,.14,1.0/60);monotonic&=smooth>=0 && smooth<=previous;}
            Check(monotonic && smooth<.001,"magnetic motion settles smoothly without overshoot");
            smooth=20;velocity=-150;double continued=WindowGeometry.SmoothPosition(smooth,2,ref velocity,.14,1.0/60);
            Check(continued<20 && velocity<0,"updating magnetic target preserves continuous velocity");
            var shortcutProperty=typeof(Preferences).GetProperty("QuickAddShortcut");
            Check(shortcutProperty!=null && (string)shortcutProperty.GetValue(new Preferences(),null)=="Ctrl+I","new users have a persisted Ctrl+I quick-add shortcut");
            Console.WriteLine("ALL " + count + " CHECKS PASSED"); return 0;
        } catch(Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}

