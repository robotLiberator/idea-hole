using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
namespace DesktopGrowth {
// Regenerate only the marked checklist; preserve handwritten reflection.
public class DailyJournal {
    const string Begin="<!-- daily-checklist:start -->",End="<!-- daily-checklist:end -->";
    const string ReviewBegin="<!-- daily-review:start -->",ReviewEnd="<!-- daily-review:end -->";
    public string Folder {get;private set;}
    public DailyJournal(string folder) {Folder=folder;}
    public string PathFor(string day) {
        var date=DateTime.ParseExact(day,"yyyy-MM-dd",CultureInfo.InvariantCulture);
        return Path.Combine(Folder,date.ToString("yyyy"),day+".md");
    }
    static string Line(string value) {return (value??"").Replace("\r"," ").Replace("\n"," ").Replace("<","&lt;").Replace(">","&gt;");}
    static string ReplaceBlock(string document,string begin,string end,string body) {
        int first=document.IndexOf(begin,StringComparison.Ordinal),last=document.IndexOf(end,StringComparison.Ordinal);
        if(first<0||last<first||document.IndexOf(begin,first+begin.Length,StringComparison.Ordinal)>=0||document.IndexOf(end,last+end.Length,StringComparison.Ordinal)>=0)
            throw new InvalidDataException("每日记录的同步标记不完整，已保留原文，请修复标记后重试。");
        return document.Substring(0,first+begin.Length)+"\n"+body.TrimEnd()+"\n"+document.Substring(last);
    }
    static string Checklist(AppState state,string day) {
        var tasks=state.Tasks.Where(t=>t.Day==day).OrderBy(t=>t.Time,StringComparer.Ordinal).ToList();
        var b=new StringBuilder();
        foreach(bool afternoon in new[]{false,true}) {
            b.Append(afternoon?"\n## 下午与晚上\n\n":"\n## 早晨\n\n");
            foreach(var t in tasks.Where(t=>(String.CompareOrdinal(t.Time,"14:00")>=0)==afternoon)) {
                b.Append(t.Done?"- [x] ":"- [ ] ").Append(Line(t.Time)).Append(" · ").Append(Line(t.Title));
                if(t.Done&&!String.IsNullOrEmpty(t.CompletedAt)) {
                    DateTimeOffset completed;
                    if(DateTimeOffset.TryParse(t.CompletedAt,out completed))b.Append("（勾选于 ").Append(completed.ToString("HH:mm:ss zzz")).Append("）");
                }
                b.Append("\n");
                if(!String.IsNullOrWhiteSpace(t.Note))b.Append("  记录：").Append(Line(t.Note)).Append("\n");
            }
        }
        return b.ToString();
    }
    public void Sync(AppState state,string day,bool updateReview=false) {
        if(String.CompareOrdinal(day,state.DailyLogStartDate)<0)return;
        string path=PathFor(day),original=File.Exists(path)?File.ReadAllText(path,Encoding.UTF8):null;
        string review;state.Reviews.TryGetValue(day,out review);
        string document=original??("# "+day+" · 每日记录\n\n时间表示开始的起点；未勾选表示尚未记录完成。\n\n"+Begin+"\n"+End+"\n\n## 每日复盘\n\n"+ReviewBegin+"\n"+(String.IsNullOrWhiteSpace(review)?"待晚上复盘：今天做了什么、身体和情绪如何、有什么感受。":review)+"\n"+ReviewEnd+"\n\n## 后续微调\n\n待复盘后填写；调整从之后的日程生效。\n");
        document=ReplaceBlock(document,Begin,End,Checklist(state,day));
        if(updateReview)document=ReplaceBlock(document,ReviewBegin,ReviewEnd,review??"");
        if(document==original)return;
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try {
            using(var file=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
                byte[] bytes=new UTF8Encoding(false).GetBytes(document);file.Write(bytes,0,bytes.Length);file.Flush(true);
            }
            if(original==null)File.Move(temp,path);
            else {
                if(File.ReadAllText(path,Encoding.UTF8)!=original)throw new IOException("每日记录正在被其他编辑器修改，请稍后重试同步。");
                File.Replace(temp,path,null);
            }
        }finally {if(File.Exists(temp))File.Delete(temp);}
    }
    public void RepairKnownDays(AppState state,DateTime through) {
        foreach(string day in state.Tasks.Select(t=>t.Day).Concat(state.Reviews.Keys).Distinct().Where(d=>String.CompareOrdinal(d,through.ToString("yyyy-MM-dd"))<=0).OrderBy(d=>d))Sync(state,day);
        Sync(state,through.ToString("yyyy-MM-dd"));
    }
}
}
