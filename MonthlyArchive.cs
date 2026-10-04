using System;
using System.IO;
using System.Linq;
using System.Text;
namespace DesktopGrowth {
// Output only: never reads Markdown content or feeds it back to the application.
public class MonthlyArchive {
    readonly TaskStore store;
    public string Folder {get;private set;}
    public MonthlyArchive(TaskStore store,string folder) {this.store=store;Folder=folder;}
    string PathFor(string day) {var parsed=DateTime.ParseExact(day,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);return Path.Combine(Folder,parsed.ToString("yyyy-MM")+".md");}
    static string Cell(string text) {return String.IsNullOrWhiteSpace(text)?"—":text.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;").Replace("\\","&#92;").Replace("|","&#124;").Replace("*","&#42;").Replace("_","&#95;").Replace("`","&#96;").Replace("\r\n","<br>").Replace("\n","<br>").Replace("\r","<br>");}
    string Render(string day) {
        var b=new StringBuilder();b.Append("## ").Append(day).Append("\n\n| 时间 | 事项 | 完成 | 耗时 | 小总结 |\n|---|---|---|---|---|\n");
        foreach(var t in store.State.Tasks.Where(t=>t.Day==day).OrderBy(t=>t.Time,StringComparer.Ordinal)) b.Append("| ").Append(Cell(t.Time)).Append(" | ").Append(Cell(t.Title)).Append(" | ").Append(t.Done?"✓":"未完成").Append(" | ").Append(t.ActualMinutes.HasValue?t.ActualMinutes+" 分钟":"—").Append(" | ").Append(Cell(t.Note)).Append(" |\n");
        string review;store.State.Reviews.TryGetValue(day,out review);b.Append("\n今日复盘：").Append(Cell(review)).Append("\n\n");return b.ToString();
    }
    public string Finish(string day) {
        Recover();string path=PathFor(day);
        if(store.State.ArchivedDays.Contains(day))throw new InvalidOperationException("这一天已经归档，不会重复写入。");
        Directory.CreateDirectory(Folder);
        using(var file=new FileStream(path,FileMode.OpenOrCreate,FileAccess.Write,FileShare.None)) {
            var next=store.Copy();next.Pending=new PendingArchive{Day=day,Offset=file.Length,Content=(file.Length==0?"# "+day.Substring(0,7)+" 成长记录\n\n":"")+Render(day)};
            store.Commit(next);WritePending(file);
        }
        Complete();return path;
    }
    void WritePending(FileStream file) {
        var p=store.State.Pending;if(file.Length<p.Offset)throw new IOException("归档文件长度异常，已停止写入。");
        byte[] bytes=new UTF8Encoding(false).GetBytes(p.Content);file.Position=p.Offset;file.Write(bytes,0,bytes.Length);file.SetLength(p.Offset+bytes.Length);file.Flush(true);
    }
    void Complete() {var next=store.Copy();if(!next.ArchivedDays.Contains(next.Pending.Day))next.ArchivedDays.Add(next.Pending.Day);next.Pending=null;store.Commit(next);}
    public void Recover() {
        if(store.State.Pending==null)return;
        Directory.CreateDirectory(Folder);
        using(var file=new FileStream(PathFor(store.State.Pending.Day),FileMode.OpenOrCreate,FileAccess.Write,FileShare.None))WritePending(file);
        Complete();
    }
}
}
