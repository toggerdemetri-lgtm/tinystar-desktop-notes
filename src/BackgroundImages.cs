using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
namespace DesktopNotes {
    // Only managed, content-addressed filenames are accepted from saved preferences.
    internal static class BackgroundImages {
        internal const long MaxBytes=20L*1024*1024;
        internal static bool ValidName(string name) {return name!=null && Regex.IsMatch(name,@"\A[0-9a-f]{64}\.(png|jpg|jpeg|bmp)\z");}
        internal static string Folder(Storage storage) {return Path.Combine(Path.GetDirectoryName(storage.FilePath),"backgrounds");}
        static BitmapSource Decode(Stream stream) {
            var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.None);
            if(!(decoder is PngBitmapDecoder) && !(decoder is JpegBitmapDecoder) && !(decoder is BmpBitmapDecoder))throw new ArgumentException("请选择 PNG、JPG、JPEG 或 BMP 图片。");
            if(decoder.Frames.Count!=1)throw new ArgumentException("请选择静态图片。");
            var frame=decoder.Frames[0];
            if(frame.PixelWidth<1 || frame.PixelHeight<1 || (long)frame.PixelWidth*frame.PixelHeight>40000000)throw new ArgumentException("图片分辨率过大，请选择4000万像素以内的图片。");
            stream.Position=0;frame=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];frame.Freeze();return frame;
        }
        internal static BitmapSource Load(Storage storage,string name) {
            if(!ValidName(name))return null;
            try {string path=Path.Combine(Folder(storage),name);if(!File.Exists(path) || new FileInfo(path).Length>MaxBytes)return null;using(var stream=File.OpenRead(path))return Decode(stream);}catch{return null;}
        }
        internal static string Import(Storage storage,string path,out bool created) {
            created=false;string extension=Path.GetExtension(path).ToLowerInvariant();
            if(!new[]{".png",".jpg",".jpeg",".bmp"}.Contains(extension))throw new ArgumentException("请选择 PNG、JPG、JPEG 或 BMP 图片。");
            byte[] bytes;
            using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                if(stream.Length<1 || stream.Length>MaxBytes)throw new ArgumentException("图片需要小于或等于20MB。");
                bytes=new byte[(int)stream.Length];int offset=0,read;while(offset<bytes.Length && (read=stream.Read(bytes,offset,bytes.Length-offset))>0)offset+=read;
                if(offset!=bytes.Length)throw new IOException("图片读取不完整，请重试。");
            }
            using(var stream=new MemoryStream(bytes))Decode(stream);
            string name;byte[] expectedHash;using(var hash=SHA256.Create())expectedHash=hash.ComputeHash(bytes);name=BitConverter.ToString(expectedHash).Replace("-","").ToLowerInvariant()+extension;
            string folder=Folder(storage);Directory.CreateDirectory(folder);string destination=Path.Combine(folder,name);
            bool exists=File.Exists(destination),valid=false;
            if(exists)using(var stream=File.OpenRead(destination)) {if(stream.Length==bytes.Length)using(var hash=SHA256.Create())valid=hash.ComputeHash(stream).SequenceEqual(expectedHash);}
            if(!valid) {
                string temp=Path.Combine(folder,Guid.NewGuid().ToString("N")+".tmp");
                try {File.WriteAllBytes(temp,bytes);if(exists)File.Replace(temp,destination,null);else {File.Move(temp,destination);created=true;}}finally{if(File.Exists(temp))File.Delete(temp);}
            }
            return name;
        }
    }
}
