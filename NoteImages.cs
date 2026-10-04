using System;
using System.IO;
using System.Windows.Media.Imaging;
namespace DesktopGrowth {
public static class NoteImages {
    public static string Folder(string dataFolder){string dir=Path.Combine(dataFolder,"便签图片");Directory.CreateDirectory(dir);return dir;}
    public static string Import(string dataFolder,byte[] bytes) {
        if(bytes==null||bytes.Length==0||bytes.Length>20*1024*1024)throw new InvalidOperationException("请选择20 MB以内的图片。");
        using(var stream=new MemoryStream(bytes)) {
            var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
            var frame=decoder.Frames[0];if((long)frame.PixelWidth*frame.PixelHeight>40000000)throw new InvalidOperationException("图片分辨率过大，请缩小后再插入。");
            string name=Guid.NewGuid().ToString("N")+".png",path=Path.Combine(Folder(dataFolder),name);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(frame));
            using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write)){encoder.Save(output);output.Flush(true);}
            return "便签图片/"+name;
        }
    }
}
}
