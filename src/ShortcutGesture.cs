using System;
using System.Collections.Generic;
using System.Windows.Input;
namespace DesktopNotes {
    public sealed class ShortcutGesture {
        public uint Modifiers {get;private set;}public uint VirtualKey {get;private set;}public string Canonical {get;private set;}
        public string Display {get {return Canonical.Replace("+"," + ");}}
        public static bool TryCreate(Key key,ModifierKeys modifiers,out ShortcutGesture gesture,out string error) {
            gesture=null;error=null;
            if((modifiers&ModifierKeys.Windows)!=0 || (modifiers&(ModifierKeys.Control|ModifierKeys.Alt))==0) {error="请使用 Ctrl 或 Alt 搭配字母、数字或功能键。";return false;}
            string name;
            if(key>=Key.A && key<=Key.Z)name=key.ToString();
            else if(key>=Key.D0 && key<=Key.D9)name=((int)key-(int)Key.D0).ToString();
            else if(key>=Key.NumPad0 && key<=Key.NumPad9)name="Num"+((int)key-(int)Key.NumPad0);
            else if(key>=Key.F1 && key<=Key.F24 && key!=Key.F12)name=key.ToString();
            else {error=key==Key.F12 ? "F12 是系统保留键，请换一个组合。":"请搭配字母、数字或功能键。";return false;}
            var parts=new List<string>();if((modifiers&ModifierKeys.Control)!=0)parts.Add("Ctrl");if((modifiers&ModifierKeys.Alt)!=0)parts.Add("Alt");if((modifiers&ModifierKeys.Shift)!=0)parts.Add("Shift");parts.Add(name);
            gesture=new ShortcutGesture {Modifiers=(uint)modifiers,VirtualKey=(uint)KeyInterop.VirtualKeyFromKey(key),Canonical=String.Join("+",parts)};return true;
        }
        public static bool TryParse(string text,out ShortcutGesture gesture,out string error) {
            gesture=null;error="快捷键格式不正确。";if(String.IsNullOrWhiteSpace(text))return false;
            var parts=text.Split('+');if(parts.Length<2)return false;ModifierKeys modifiers=ModifierKeys.None;
            for(int i=0;i<parts.Length-1;i++) {string token=parts[i].Trim().ToUpperInvariant();ModifierKeys bit;if(token=="CTRL" || token=="CONTROL")bit=ModifierKeys.Control;else if(token=="ALT")bit=ModifierKeys.Alt;else if(token=="SHIFT")bit=ModifierKeys.Shift;else return false;if((modifiers&bit)!=0)return false;modifiers|=bit;}
            string name=parts[parts.Length-1].Trim().ToUpperInvariant();Key key;int number;
            if(name.Length==1 && name[0]>='A' && name[0]<='Z')key=(Key)((int)Key.A+name[0]-'A');
            else if(name.Length==1 && name[0]>='0' && name[0]<='9')key=(Key)((int)Key.D0+name[0]-'0');
            else if(name.StartsWith("NUM") && Int32.TryParse(name.Substring(3),out number) && number>=0 && number<=9)key=(Key)((int)Key.NumPad0+number);
            else if(name.StartsWith("F") && Int32.TryParse(name.Substring(1),out number) && number>=1 && number<=24)key=(Key)((int)Key.F1+number-1);
            else return false;
            return TryCreate(key,modifiers,out gesture,out error);
        }
    }
}
