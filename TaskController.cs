using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
namespace DesktopGrowth {
public enum DeleteScope {One,Series}
public class TaskController {
    readonly TaskStore store;
    readonly string folder;
    readonly MonthlyArchive archive;
    readonly DailyJournal journal;
    public event Action Changed;
    public DateTime Day {get;private set;}
    public string ArchiveFolder {get {return archive.Folder;}}
    public string DailyRecordFolder {get {return journal.Folder;}}
    public TaskController(string folder,DateTime day) {
        this.folder=folder;Day=day.Date;store=new TaskStore(folder);
        if(!store.Exists) Migrate();
        archive=new MonthlyArchive(store,Path.Combine(folder,"月度记录"));archive.Recover();
        journal=new DailyJournal(Path.Combine(folder,"每日记录"));EnsureDay();journal.RepairKnownDays(store.State,Day);
    }
    static string Key(DateTime day) {return day.ToString("yyyy-MM-dd");}
    static string LegacyId(string rule,string day) {
        using(var md5=System.Security.Cryptography.MD5.Create()) return new Guid(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rule+day))).ToString("N");
    }
    void Migrate() {
        var state=new AppState();string tasks=Path.Combine(folder,"tasks.xml"),rules=Path.Combine(folder,"controller-rules.xml");
        if(File.Exists(tasks)) foreach(var x in XElement.Load(tasks).Elements("task")) state.Tasks.Add(new TaskRecord {Id=(string)x.Attribute("id"),Day=(string)x.Attribute("day"),Time=(string)x.Attribute("time"),Title=(string)x.Attribute("title"),Done=(bool)x.Attribute("done")});
        if(File.Exists(rules)) foreach(var x in XElement.Load(rules).Elements("daily")) state.DailyRules.Add(new DailyRule {Id=(string)x.Attribute("id"),Time=(string)x.Attribute("time"),Title=(string)x.Attribute("title"),StartDate=Key(Day)});
        else {
            string[] times={"07:00","07:30","12:30","18:30","23:30"},titles={"起床","早餐","散步 20分钟","运动 40分钟","睡觉"};
            for(int i=0;i<5;i++)state.DailyRules.Add(new DailyRule{Id="sample-"+i,Time=times[i],Title=titles[i],StartDate=Key(Day)});
        }
        foreach(var rule in state.DailyRules) foreach(var task in state.Tasks.Where(t=>t.Id==LegacyId(rule.Id,t.Day))) state.Occurrences.Add(new Occurrence{RuleId=rule.Id,Day=task.Day,TaskId=task.Id});
        Generate(state,Day);
        string old=Path.Combine(folder,"preview-state.txt");
        if(!File.Exists(tasks)&&File.Exists(old)) {
            var parts=File.ReadAllText(old).Split('|');
            if(parts.Length==2&&parts[0]==Key(Day))for(int i=0;i<5&&i<parts[1].Length;i++) {
                var occurrence=state.Occurrences.FirstOrDefault(o=>o.RuleId=="sample-"+i&&o.Day==Key(Day));
                if(occurrence!=null)state.Tasks.Single(t=>t.Id==occurrence.TaskId).Done=parts[1][i]=='1';
            }
        }
        store.Commit(state);
    }
    static void Generate(AppState state,DateTime day) {
        string date=Key(day);if(state.ArchivedDays.Contains(date))return;
        foreach(var rule in state.DailyRules.Where(r=>String.CompareOrdinal(r.StartDate,date)<=0)) {
            if(state.Occurrences.Any(o=>o.RuleId==rule.Id&&o.Day==date))continue;
            var task=new TaskRecord{Id=Guid.NewGuid().ToString("N"),Day=date,Time=rule.Time,Title=rule.Title};
            state.Tasks.Add(task);state.Occurrences.Add(new Occurrence{RuleId=rule.Id,Day=date,TaskId=task.Id});
        }
    }
    void Mutate(Action<AppState> action) {archive.Recover();var next=store.Copy();action(next);store.Commit(next);journal.RepairKnownDays(store.State,Day);if(Changed!=null)Changed();}
    void EnsureDay() {var next=store.Copy();int count=next.Tasks.Count;Generate(next,Day);if(next.Tasks.Count!=count)store.Commit(next);journal.Sync(store.State,Key(Day));}
    public List<TaskRecord> Today() {return store.State.Tasks.Where(t=>t.Day==Key(Day)).OrderBy(t=>t.Time,StringComparer.Ordinal).ToList();}
    public void ChangeDay(DateTime day) {if(Day==day.Date){journal.Sync(store.State,Key(Day));return;}var old=Day;Day=day.Date;try{EnsureDay();}catch{Day=old;throw;}if(Changed!=null)Changed();}
    public void Add(string time,string title,bool daily) {
        if(String.IsNullOrWhiteSpace(time)||String.IsNullOrWhiteSpace(title))throw new ArgumentException("请填写时间和任务内容。");
        Mutate(s=>{if(daily){s.DailyRules.Add(new DailyRule{Id=Guid.NewGuid().ToString("N"),Time=time.Trim(),Title=title.Trim(),StartDate=Key(Day)});Generate(s,Day);}else s.Tasks.Add(new TaskRecord{Id=Guid.NewGuid().ToString("N"),Day=Key(Day),Time=time.Trim(),Title=title.Trim()});});
    }
    public void SetDone(TaskRecord task,bool value) {Mutate(s=>{var current=s.Tasks.Single(t=>t.Id==task.Id);current.Done=value;current.CompletedAt=value?(current.CompletedAt??DateTimeOffset.Now.ToString("o")):null;});}
    public void SetDetails(string id,int? minutes,string note) {
        if(minutes<0)throw new ArgumentException("耗时不能为负数。");
        Mutate(s=>{var task=s.Tasks.Single(t=>t.Id==id);task.ActualMinutes=minutes;task.Note=note??"";});
    }
    public bool IsRecurring(string id) {return store.State.Occurrences.Any(o=>o.TaskId==id)&&store.State.DailyRules.Any(r=>store.State.Occurrences.Any(o=>o.TaskId==id&&o.RuleId==r.Id));}
    public void Delete(string id,DeleteScope scope) {
        if(scope!=DeleteScope.One&&scope!=DeleteScope.Series)throw new ArgumentException("不支持的删除范围。");
        Mutate(s=>{
            var task=s.Tasks.Single(t=>t.Id==id);var occurrence=s.Occurrences.FirstOrDefault(o=>o.TaskId==id);
            if(String.CompareOrdinal(task.Day,Key(Day))<0)throw new InvalidOperationException("过去的任务记录保留，不支持删除。");
            if(scope==DeleteScope.One) {s.Tasks.Remove(task);if(occurrence!=null)occurrence.TaskId=null;return;}
            if(occurrence==null||!s.DailyRules.Any(r=>r.Id==occurrence.RuleId))throw new ArgumentException("这条任务没有可删除的循环规则，请选择删除这一条。");
            string rule=occurrence.RuleId;
            var ids=new HashSet<string>(s.Occurrences.Where(o=>o.RuleId==rule&&String.CompareOrdinal(o.Day,Key(Day))>=0).Select(o=>o.TaskId));
            s.Tasks.RemoveAll(t=>ids.Contains(t.Id));s.DailyRules.RemoveAll(r=>r.Id==rule);s.Occurrences.RemoveAll(o=>o.RuleId==rule);
        });
        // Obsolete migration inputs are cleared on deletion; archived MD remains immutable.
        foreach(string name in new[]{"tasks.xml","tasks.xml.bak","tasks.xml.tmp","controller-rules.xml","controller-rules.xml.bak","controller-rules.xml.tmp","preview-state.txt"}) {
            string path=Path.Combine(folder,name);if(File.Exists(path))File.Delete(path);
        }
    }
    public List<string> UnarchivedDays() {return store.State.Tasks.Select(t=>t.Day).Concat(new[]{Key(Day)}).Distinct().Where(d=>!store.State.ArchivedDays.Contains(d)).OrderBy(d=>d).ToList();}
    public string Review(string date) {string value;return store.State.Reviews.TryGetValue(date,out value)?value:"";}
    public void SaveReview(string date,string text) {DateTime.ParseExact(date,"yyyy-MM-dd",System.Globalization.CultureInfo.InvariantCulture);Mutate(s=>s.Reviews[date]=text??"");journal.Sync(store.State,date,true);}
    public string FinishDay(string date,string review) {archive.Recover();if(store.State.ArchivedDays.Contains(date))throw new InvalidOperationException("这一天已经归档，不会重复写入。");SaveReview(date,review);var result=archive.Finish(date);if(Changed!=null)Changed();return result;}
}
}
