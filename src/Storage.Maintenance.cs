using System;
using System.IO;
namespace DesktopNotes {
    public partial class Storage {
        internal static Preferences CopyPreferences(Preferences source) {return Serializer().Deserialize<Preferences>(Serializer().Serialize(source));}
        internal static void CopyContentDrafts(Preferences source,Preferences target) {
            target.Draft=source.Draft;target.DraftCategory=source.DraftCategory;target.EditId=source.EditId;target.EditDraft=source.EditDraft;target.EditStepId=source.EditStepId;target.EditNewStep=source.EditNewStep;target.QuickAddDraft=source.QuickAddDraft;
        }
        internal void ExportBeforeClear(string path,NoteState state) {
            string target=Path.GetFullPath(path),directory=Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(target.StartsWith(directory,StringComparison.OrdinalIgnoreCase))throw new ArgumentException("请把清除前备份保存到便签数据目录以外，例如文档或桌面。");
            var copy=Serializer().Deserialize<NoteState>(Serializer().Serialize(state));copy.Settings=new Preferences();CopyContentDrafts(state.Settings,copy.Settings);copy.IncludesDrafts=true;Validate(copy);
            byte[] bytes=DataBytes(copy);string temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}var verified=Import(temp);if(verified.Notes.Count!=state.Notes.Count || !verified.IncludesDrafts)throw new IOException("备份校验未通过，数据未清除。");if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);}finally{SafeDelete(temp);}
        }
        static void SafeDelete(string path) {try{if(File.Exists(path))File.Delete(path);}catch{}}
        internal void SaveClearedState(NoteState state) {
            Validate(state);if(state.Notes.Count!=0 || state.Categories.Count!=0 || state.CategoryPassword!=null)throw new ArgumentException("清除后的数据必须为空。");
            Directory.CreateDirectory(folder);byte[] bytes=DataBytes(state);string token=Guid.NewGuid().ToString("N");
            string temp=FilePath+".clear-"+token+".tmp",backupTemp=FilePath+".clear-"+token+".bak.tmp",rollback=FilePath+".clear-"+token+".rollback";
            bool existed=File.Exists(FilePath),changed=false;
            try {
                foreach(string path in new[]{temp,backupTemp})using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                if(existed)File.Replace(temp,FilePath,rollback);else File.Move(temp,FilePath);changed=true;
                if(File.Exists(FilePath+".bak"))File.Replace(backupTemp,FilePath+".bak",null);else File.Move(backupTemp,FilePath+".bak");
                if(File.Exists(FilePath+".tmp"))File.Delete(FilePath+".tmp");
                if(File.Exists(rollback))File.Delete(rollback);
            }catch(Exception ex) {
                if(changed)try{if(existed && File.Exists(rollback))File.Replace(rollback,FilePath,null);else if(!existed && File.Exists(FilePath))File.Delete(FilePath);}catch(Exception restore){throw new IOException("清除未完成，原始副本保留在："+rollback,new AggregateException(ex,restore));}
                throw;
            }finally{SafeDelete(temp);SafeDelete(backupTemp);}
        }
    }
}
