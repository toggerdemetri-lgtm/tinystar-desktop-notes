using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
namespace DesktopNotes {
    public static class PasswordSecrets {
        public static PasswordCredential Create(string password) {
            if(String.IsNullOrWhiteSpace(password) || password.Length<4 || password.Length>128)throw new ArgumentException("密码需为4—128个字符，不能全为空格。");
            var salt=new byte[16];using(var random=RandomNumberGenerator.Create())random.GetBytes(salt);
            using(var derive=new Rfc2898DeriveBytes(password,salt,120000,HashAlgorithmName.SHA256))return new PasswordCredential {Algorithm="PBKDF2-SHA256",Iterations=120000,Salt=Convert.ToBase64String(salt),Hash=Convert.ToBase64String(derive.GetBytes(32))};
        }
        public static bool Verify(PasswordCredential credential,string password) {
            if(credential==null || !credential.IsWellFormed() || password==null || password.Length>128)return false;
            var expected=Convert.FromBase64String(credential.Hash);byte[] actual;
            using(var derive=new Rfc2898DeriveBytes(password,Convert.FromBase64String(credential.Salt),credential.Iterations,HashAlgorithmName.SHA256))actual=derive.GetBytes(32);
            int difference=0;for(int i=0;i<expected.Length;i++)difference|=expected[i]^actual[i];Array.Clear(actual,0,actual.Length);return difference==0;
        }
    }
    public sealed class CategoryPrivacy {
        readonly NoteState state;readonly HashSet<string> unlocked=new HashSet<string>();
        List<Category> indexedCategories;int indexedCount=-1;readonly Dictionary<string,Category> categories=new Dictionary<string,Category>();
        int failures;DateTime blockedUntil=DateTime.MinValue;
        public CategoryPrivacy(NoteState state) {this.state=state;}
        public bool HasPassword {get {return state.CategoryPassword!=null && state.CategoryPassword.IsWellFormed();}}
        Category FindCategory(string id) {
            if(indexedCategories!=state.Categories || indexedCount!=state.Categories.Count) {categories.Clear();foreach(var item in state.Categories)categories[item.Id]=item;indexedCategories=state.Categories;indexedCount=state.Categories.Count;}
            Category category;return id!=null && categories.TryGetValue(id,out category) ? category:null;
        }
        public bool IsLocked(string id) {var category=FindCategory(id);return category!=null && category.IsPrivate && !unlocked.Contains(id);}
        public bool HasLockedCategories {get {return state.Categories.Any(c=>IsLocked(c.Id));}}
        public bool CanSee(Note note) {return note!=null && !IsLocked(note.CategoryId);}
        public bool VerifyPassword(string password,out string error) {
            if(DateTime.UtcNow<blockedUntil) {error="尝试次数过多，请"+Math.Max(1,(int)Math.Ceiling((blockedUntil-DateTime.UtcNow).TotalSeconds))+"秒后再试。";return false;}
            if(!HasPassword) {error="请先在设置中设置分类密码。";return false;}
            if(!PasswordSecrets.Verify(state.CategoryPassword,password)) {failures++;if(failures>=5) {failures=0;blockedUntil=DateTime.UtcNow.AddSeconds(30);error="尝试次数过多，请30秒后再试。";}else error="密码不正确，请重新输入。";return false;}
            failures=0;blockedUntil=DateTime.MinValue;error=null;return true;
        }
        public bool TryUnlock(string id,string password,out string error) {
            return TryUnlock(id,password,false,out error);
        }
        public bool TryUnlock(string id,string password,bool all,out string error) {
            var category=FindCategory(id);if(category==null) {error="只能解锁自定义分类。";return false;}
            if(!category.IsPrivate) {error=all ? "当前分类没有上锁，请从加锁分类输入密码解锁。":null;return !all;}
            if(!VerifyPassword(password,out error))return false;
            if(all)foreach(var item in state.Categories.Where(c=>c.IsPrivate))unlocked.Add(item.Id);else unlocked.Add(id);return true;
        }
        public void Lock(string id) {unlocked.Remove(id);}
        public void LockAll() {unlocked.Clear();}
    }
}
