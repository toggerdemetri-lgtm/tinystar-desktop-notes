using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace DesktopNotes {
    public partial class MainWindow {
        string backgroundCachePath;BitmapSource backgroundCache;
        internal BitmapSource BackgroundPreview {get {return ManagedBackground();}}
        BitmapSource ManagedBackground() {
            var p=Service.State.Settings;string path=BackgroundImages.ValidName(p.BackgroundImage) ? Path.Combine(BackgroundImages.Folder(Store),p.BackgroundImage):null;
            if(path==null || !File.Exists(path)) {backgroundCachePath=null;backgroundCache=null;return null;}
            if(backgroundCachePath!=path || backgroundCache==null) {backgroundCache=BackgroundImages.Load(Store,p.BackgroundImage);backgroundCachePath=path;}
            return backgroundCache;
        }
        void ApplyCustomBackground() {
            if(noteShell==null)return;var p=Service.State.Settings;Brush background=null;
            if(p.BackgroundMode=="color" && p.BackgroundColor!=null && Regex.IsMatch(p.BackgroundColor,@"\A#[0-9a-fA-F]{6}\z"))background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(p.BackgroundColor));
            if(p.BackgroundMode=="image") {var image=ManagedBackground();if(image!=null)background=new ImageBrush(image) {Stretch=Stretch.UniformToFill,AlignmentX=AlignmentX.Center,AlignmentY=AlignmentY.Center};}
            if(background!=null)background.Opacity=Math.Max(1.0/255,p.BackgroundOpacity);
            noteShell.Background=background ?? Palette.Surface;noteSidebar.Background=background==null ? Palette.Sidebar:Brushes.Transparent;
        }
        internal bool TrySetCustomBackground(string mode,string value,out string error) {
            error=null;
            if(mode!="default" && mode!="color" && mode!="image") {error="请选择背景类型。";return false;}
            if(mode=="color" && (value==null || !Regex.IsMatch(value,@"\A#[0-9a-fA-F]{6}\z"))) {error="请输入六位颜色值，例如 #26364F。";return false;}
            if(mode=="image" && BackgroundImages.Load(Store,value)==null) {error="图片无法读取，请重新选择。";return false;}
            var p=Service.State.Settings;string oldMode=p.BackgroundMode,oldColor=p.BackgroundColor,oldImage=p.BackgroundImage;
            p.BackgroundMode=mode;if(mode=="color")p.BackgroundColor=value.ToUpperInvariant();if(mode=="image")p.BackgroundImage=value;
            if(!Save()) {p.BackgroundMode=oldMode;p.BackgroundColor=oldColor;p.BackgroundImage=oldImage;error="背景设置未能保存，原背景已保留。";return false;}
            ApplyAppearance();return true;
        }
        internal bool TryImportBackground(string path,out string error) {
            error=null;bool created=false;string name=null;
            try {name=BackgroundImages.Import(Store,path,out created);backgroundCachePath=null;backgroundCache=null;if(TrySetCustomBackground("image",name,out error))return true;}
            catch(Exception ex) {error="无法设置背景图片："+ex.Message;}
            if(created && BackgroundImages.ValidName(name)) {try{File.Delete(Path.Combine(BackgroundImages.Folder(Store),name));}catch{}}
            return false;
        }
    }
}
