using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace DesktopGrowth {
public class NoteRecord {
    public string Id="", Title="", Body="", Color="yellow";
    // Body is the card summary; Markdown holds the expanded document.
    public string Markdown="";
    // Height=0 preserves the exact square layout of existing v1 notes.
    public double X, Y, Size=245, Height;
    public int Z;
}

public class NotesStore {
    public class Entry {
        public string Id,File,Color;
        public double X,Y,Size,Height;
        public int Z;
        public string Status="active",CreatedAt="",ArchivedAt="";
    }
    readonly string path,folder,legacy;
    readonly JavaScriptSerializer json=new JavaScriptSerializer {MaxJsonLength=Int32.MaxValue};
    public string Path {get {return path;}}
    public NotesStore(string folder) {Directory.CreateDirectory(folder);this.folder=folder;path=System.IO.Path.Combine(folder,"index.json");legacy=System.IO.Path.Combine(folder,"灵感便签.md");}
    public List<NoteRecord> Load() {
        if(File.Exists(path)) {
            var entries=json.Deserialize<List<Entry>>(File.ReadAllText(path,Encoding.UTF8));
            if(entries==null)throw new InvalidDataException("便签索引损坏，未覆盖原文件。");
            var result=new List<NoteRecord>();
            bool needsMigration=false;
            foreach(var entry in entries){
                ValidateId(entry.Id);
                string documentPath=EntryPath(entry.File);
                if(IsArchived(entry))continue;
                string document=File.ReadAllText(documentPath,Encoding.UTF8);
                var note=Parse(document);note.Id=entry.Id;note.Color=entry.Color;note.X=entry.X;note.Y=entry.Y;note.Size=entry.Size;note.Height=entry.Height;note.Z=entry.Z;result.Add(note);
                if(String.IsNullOrWhiteSpace(entry.CreatedAt)||entry.File==entry.Id+".md")needsMigration=true;
            }
            if(needsMigration)Save(result);
            return result;
        }
        if(!File.Exists(legacy)){var initial=Defaults();Save(initial);return initial;}
        string text=File.ReadAllText(legacy,Encoding.UTF8);
        const string marker="<!-- desktop-growth-notes:v1\n";
        int start=text.IndexOf(marker,StringComparison.Ordinal);
        if(start<0)throw new InvalidDataException("旧便签格式无法识别，未覆盖原文件。");
        start+=marker.Length;int end=text.IndexOf("\n-->",start,StringComparison.Ordinal);
        if(end<0)throw new InvalidDataException("旧便签数据不完整。");
        {
            string encoded=text.Substring(start,end-start).Trim();
            var notes=json.Deserialize<List<NoteRecord>>(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
            if(notes==null)throw new InvalidDataException("旧便签数据无效。");
            Save(notes);
            string backup=System.IO.Path.Combine(folder,"迁移备份");Directory.CreateDirectory(backup);
            File.Move(legacy,System.IO.Path.Combine(backup,"灵感便签-"+DateTime.Now.ToString("yyyyMMddHHmmssfff")+".md"));
            return notes;
        }
    }
    const string BodyMarker="\n<!-- 正文 -->\n";
    static void ValidateId(string id){if(String.IsNullOrEmpty(id)||!System.Text.RegularExpressions.Regex.IsMatch(id,"^[A-Za-z0-9_-]+$"))throw new InvalidDataException("便签编号无效。");}
    static bool IsArchived(Entry entry){return String.Equals(entry.Status,"archived",StringComparison.OrdinalIgnoreCase);}
    string EntryPath(string file){
        if(String.IsNullOrWhiteSpace(file)||System.IO.Path.IsPathRooted(file)||!file.EndsWith(".md",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("便签路径无效。");
        string root=System.IO.Path.GetFullPath(folder).TrimEnd(System.IO.Path.DirectorySeparatorChar)+System.IO.Path.DirectorySeparatorChar;
        string full=System.IO.Path.GetFullPath(System.IO.Path.Combine(folder,file));
        if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("便签路径超出灵感池。");
        return full;
    }
    static string Timestamp(DateTime value){return new DateTimeOffset(value).ToString("o");}
    static string FileStem(string title){
        var value=new StringBuilder();foreach(char c in (title??"")){value.Append(Array.IndexOf(System.IO.Path.GetInvalidFileNameChars(),c)>=0?'_':c);}
        string stem=value.ToString().Trim().TrimEnd('.');if(stem.Length>60)stem=stem.Substring(0,60).TrimEnd('.');if(stem.Length>0&&Char.IsHighSurrogate(stem[stem.Length-1]))stem=stem.Substring(0,stem.Length-1);
        if(String.IsNullOrWhiteSpace(stem))stem="未命名便签";
        if(System.Text.RegularExpressions.Regex.IsMatch(stem,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])($|\.)",System.Text.RegularExpressions.RegexOptions.IgnoreCase))stem="便签-"+stem;
        return stem;
    }
    static string Document(NoteRecord note){return "# "+(note.Title??"").Replace("\r"," ").Replace("\n"," ")+"\n\n"+(note.Body??"")+BodyMarker+(note.Markdown??"");}
    static NoteRecord Parse(string text){
        int titleEnd=text.IndexOf("\n\n",StringComparison.Ordinal),bodyStart=text.IndexOf(BodyMarker,StringComparison.Ordinal);
        if(!text.StartsWith("# ")||titleEnd<2||bodyStart<titleEnd+2)throw new InvalidDataException("便签文档格式损坏，未覆盖原文件。");
        return new NoteRecord{Title=text.Substring(2,titleEnd-2),Body=text.Substring(titleEnd+2,bodyStart-titleEnd-2),Markdown=text.Substring(bodyStart+BodyMarker.Length)};
    }
    public void Save(List<NoteRecord> notes) {
        if(notes==null)throw new ArgumentNullException("notes");
        var ids=new HashSet<string>();
        foreach(var note in notes){ValidateId(note.Id);if(!ids.Add(note.Id))throw new InvalidDataException("便签编号重复。");if((note.Body??"").Contains(BodyMarker))throw new InvalidDataException("摘要包含保留的正文分隔标记。");}
        var old=File.Exists(path)?json.Deserialize<List<Entry>>(File.ReadAllText(path,Encoding.UTF8)):new List<Entry>();
        if(old==null)throw new InvalidDataException("便签索引损坏，未覆盖原文件。");
        var previous=new Dictionary<string,Entry>();var entries=new List<Entry>();
        var owners=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in old){ValidateId(entry.Id);EntryPath(entry.File);previous[entry.Id]=entry;owners[entry.File]=entry.Id;if(IsArchived(entry))entries.Add(entry);}
        var used=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var note in notes) {
            string stem=FileStem(note.Title),file=stem+".md";int number=2;string owner;
            while(used.Contains(file)||(owners.TryGetValue(file,out owner)?owner!=note.Id:File.Exists(System.IO.Path.Combine(folder,file))))file=stem+"（"+(number++)+"）.md";
            used.Add(file);WriteAtomic(System.IO.Path.Combine(folder,file),Document(note));
            Entry before;previous.TryGetValue(note.Id,out before);
            string created=before!=null?before.CreatedAt:"";
            if(String.IsNullOrWhiteSpace(created)){
                string source=before!=null&&File.Exists(EntryPath(before.File))?EntryPath(before.File):System.IO.Path.Combine(folder,file);
                created=Timestamp(File.GetCreationTime(source));
            }
            entries.Add(new Entry{Id=note.Id,File=file,Color=note.Color,X=note.X,Y=note.Y,Size=note.Size,Height=note.Height,Z=note.Z,Status="active",CreatedAt=created,ArchivedAt=""});
        }
        WriteAtomic(path,json.Serialize(entries));
        foreach(var entry in old){if(IsArchived(entry)||used.Contains(entry.File))continue;string removed=EntryPath(entry.File);if(File.Exists(removed)){string trash=System.IO.Path.Combine(folder,ids.Contains(entry.Id)?"命名备份":"回收站");Directory.CreateDirectory(trash);File.Move(removed,System.IO.Path.Combine(trash,System.IO.Path.GetFileNameWithoutExtension(entry.File)+"-"+Guid.NewGuid().ToString("N")+".md"));}}
    }
    public Entry Archive(string id,DateTimeOffset when) {
        ValidateId(id);
        var entries=File.Exists(path)?json.Deserialize<List<Entry>>(File.ReadAllText(path,Encoding.UTF8)):null;
        if(entries==null)throw new InvalidDataException("便签索引损坏，未执行归档。");
        Entry entry=entries.Find(e=>e.Id==id&&!IsArchived(e));
        if(entry==null)throw new InvalidOperationException("没有找到要归档的便签。");
        string source=EntryPath(entry.File);if(!File.Exists(source))throw new FileNotFoundException("便签文档不存在，未执行归档。",source);
        string year=when.ToString("yyyy"),archiveFolder=System.IO.Path.Combine(folder,"已完成",year);Directory.CreateDirectory(archiveFolder);
        string stem=System.IO.Path.GetFileNameWithoutExtension(entry.File),target=System.IO.Path.Combine(archiveFolder,stem+".md");int number=2;
        while(File.Exists(target))target=System.IO.Path.Combine(archiveFolder,stem+"（"+(number++)+"）.md");
        File.Copy(source,target,false);
        string previousFile=entry.File,previousStatus=entry.Status,previousArchived=entry.ArchivedAt;
        entry.File=System.IO.Path.Combine("已完成",year,System.IO.Path.GetFileName(target));entry.Status="archived";entry.ArchivedAt=when.ToString("o");
        try {WriteAtomic(path,json.Serialize(entries));File.Delete(source);}
        catch {entry.File=previousFile;entry.Status=previousStatus;entry.ArchivedAt=previousArchived;if(File.Exists(target))File.Delete(target);throw;}
        return entry;
    }
    static void WriteAtomic(string path,string text){
        if(File.Exists(path)&&File.ReadAllText(path,Encoding.UTF8)==text)return;
        string temp=path+".tmp";byte[] bytes=new UTF8Encoding(false).GetBytes(text);
        using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){file.Write(bytes,0,bytes.Length);file.Flush(true);}
        if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
    }
    static List<NoteRecord> Defaults() {
        return new List<NoteRecord> {
            N("整理本周想法","把零散的想法写下来，\n留一点空间慢慢想。","yellow",0,0,1),
            N("做一个小作品","先做喜欢的部分，\n再把它打磨好。","blue",334,0,2),
            N("读书","读几页，\n记下一句喜欢的话。","coral",668,0,3),
            N("出去走走","晒晒太阳，\n让脑袋休息一下。","cream",0,360,4),
            N("录一段视频","把今天的新发现，\n讲得简单一点。","peach",334,360,5),
            N("以后想做的事","不急着开始，\n先好好收在这里。","lavender",668,360,6)
        };
    }
    static NoteRecord N(string title,string body,string color,double x,double y,int z) {return new NoteRecord{Id=Guid.NewGuid().ToString("N"),Title=title,Body=body,Color=color,X=x,Y=y,Size=245,Height=0,Z=z};}
}
}
