using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
namespace DesktopGrowth {
public class TaskRecord {
    public string Id, Day, Time, Title;
    public bool Done;
    public string CompletedAt;
    public int? ActualMinutes;
    public string Note="";
}
public class DailyRule {public string Id,Time,Title,StartDate;}
public class Occurrence {public string RuleId,Day,TaskId;}
public class PendingArchive {public string Day,Content;public long Offset;}
public class AppState {
    public int Version=1;
    public string DailyLogStartDate="2026-10-04";
    public List<TaskRecord> Tasks=new List<TaskRecord>();
    public List<DailyRule> DailyRules=new List<DailyRule>();
    public List<Occurrence> Occurrences=new List<Occurrence>();
    public Dictionary<string,string> Reviews=new Dictionary<string,string>();
    public List<string> ArchivedDays=new List<string>();
    public PendingArchive Pending;
}
public class TaskStore {
    readonly string path;
    readonly JavaScriptSerializer json=new JavaScriptSerializer {MaxJsonLength=Int32.MaxValue};
    public AppState State;
    public bool Exists {get {return File.Exists(path);}}
    public TaskStore(string folder) {
        Directory.CreateDirectory(folder);path=Path.Combine(folder,"state.json");
        State=Exists?json.Deserialize<AppState>(File.ReadAllText(path,Encoding.UTF8)):new AppState();
        if(State==null || State.Version!=1) throw new InvalidDataException("无法识别 state.json，原文件未修改。");
    }
    public AppState Copy() {return json.Deserialize<AppState>(json.Serialize(State));}
    public void Commit(AppState next) {
        string temp=path+".tmp";
        using(var file=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) {
            byte[] bytes=new UTF8Encoding(false).GetBytes(json.Serialize(next));file.Write(bytes,0,bytes.Length);file.Flush(true);
        }
        if(File.Exists(path)) File.Replace(temp,path,null);else File.Move(temp,path);
        State=next;
    }
}
}
