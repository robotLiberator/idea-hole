using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml.Linq;
namespace DesktopGrowth {
class ControllerTests {
 static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
 static void Main(){
  string folder=Path.Combine(Path.GetTempPath(),"growth-tests-"+Guid.NewGuid().ToString("N"));var date=new DateTime(2026,9,29);
  var c=new TaskController(folder,date);Check(c.Today().Count==5,"seed tasks");
  c.Add("09:00","只今天",false);c.Add("10:00","每日测试",true);
  var task=c.Today().Single(t=>t.Title=="每日测试");c.SetDone(task,true);c.SetDetails(task.Id,25,"含 | 表格\n第二行");
  c=new TaskController(folder,date);Check(c.Today().Count==7&&c.Today().Single(t=>t.Id==task.Id).Done,"restart and persistent completion");
  c.Delete(task.Id,DeleteScope.One);c=new TaskController(folder,date);Check(!c.Today().Any(t=>t.Title=="每日测试"),"delete one does not regenerate on restart");
  c.ChangeDay(date.AddDays(1));Check(c.Today().Any(t=>t.Title=="每日测试")&&!c.Today().Any(t=>t.Title=="只今天"),"recurrence and one-off across midnight");
  c.Delete(c.Today().Single(t=>t.Title=="每日测试").Id,DeleteScope.Series);c.ChangeDay(date.AddDays(2));Check(!c.Today().Any(t=>t.Title=="每日测试"),"delete series stops generation");
  c.ChangeDay(date);c.Add("11:00","保留历史",true);string historicId=c.Today().Single(t=>t.Title=="保留历史").Id;
  c.ChangeDay(date.AddDays(1));c.Delete(c.Today().Single(t=>t.Title=="保留历史").Id,DeleteScope.Series);
  bool historyBlocked=false;try{c.Delete(historicId,DeleteScope.One);}catch(InvalidOperationException){historyBlocked=true;}
  Check(historyBlocked,"direct historical deletion rejected");
  bool scopeBlocked=false;try{c.Delete(historicId,(DeleteScope)2);}catch(ArgumentException){scopeBlocked=true;}
  Check(scopeBlocked,"removed deletion scope rejected");
  c.ChangeDay(date);Check(c.Today().Any(t=>t.Id==historicId),"series deletion retains historical records");
  var firstTask=c.Today().First();c.SetDetails(firstTask.Id,20,"早餐 | 很好\n继续");c.SetDone(firstTask,true);
  string md=c.FinishDay("2026-09-29","状态不错");string first=File.ReadAllText(md);
  Check(first.Contains("&#124;")&&first.Contains("<br>")&&first.Contains("20 分钟")&&!first.Contains("RuleId"),"Markdown table escaping and no controller fields");
  bool rejected=false;try{c.FinishDay("2026-09-29","再次");}catch(InvalidOperationException){rejected=true;}
  Check(rejected&&File.ReadAllText(md)==first,"duplicate archival blocked");
  c.Delete(c.Today().First().Id,DeleteScope.One);Check(File.ReadAllText(md)==first,"archive remains immutable after live deletion");
  c.ChangeDay(date.AddDays(1));c.FinishDay("2026-09-30","第二天");
  Check(File.ReadAllText(md).StartsWith(first)&&File.ReadAllText(md).Contains("## 2026-09-30"),"same month append");
  c.ChangeDay(date.AddDays(2));c.FinishDay("2026-10-01","新月份");
  Check(Directory.GetFiles(c.ArchiveFolder,"*.md").Length==2,"month rollover");
  var store=new TaskStore(folder);
  Check(typeof(TaskRecord).GetFields().All(f=>f.Name!="RuleId"&&f.Name!="Daily"),"uniform task record");
  // Simulate a crash after the durable pending intent but during the append.
  string october=Path.Combine(c.ArchiveFolder,"2026-10.md");long offset=new FileInfo(october).Length;
  var pending=store.Copy();pending.Pending=new PendingArchive{Day="2026-10-02",Offset=offset,Content="## 2026-10-02\n\n恢复测试\n\n"};store.Commit(pending);
  File.AppendAllText(october,"## 20",new UTF8Encoding(false));
  var arch=new MonthlyArchive(store,c.ArchiveFolder);arch.Recover();arch.Recover();
  Check(File.ReadAllText(october).Split(new[]{"## 2026-10-02"},StringSplitOptions.None).Length==2&&store.State.ArchivedDays.Contains("2026-10-02"),"partial append recovery exactly once");
  string legacy=Path.Combine(folder,"legacy");Directory.CreateDirectory(legacy);
  new XElement("tasks",new XElement("task",new XAttribute("id","old"),new XAttribute("day","2026-09-29"),new XAttribute("time","12:00"),new XAttribute("title","旧任务"),new XAttribute("done",true))).Save(Path.Combine(legacy,"tasks.xml"));
  new XElement("controller").Save(Path.Combine(legacy,"controller-rules.xml"));
  var migrated=new TaskController(legacy,date);Check(migrated.Today().Count==1&&migrated.Today()[0].Done,"XML migration without losing completion");
  Check(File.Exists(Path.Combine(legacy,"state.json")),"single JSON state created");
  var noteStore=new NotesStore(folder);var notes=noteStore.Load();notes[0].Title="带 # 号的标题";notes[0].Body="正文可以自由书写\n第二行";notes[0].X=321;notes[0].Z=42;noteStore.Save(notes);
  var restored=noteStore.Load();var noteEntries=new JavaScriptSerializer().Deserialize<System.Collections.Generic.List<NotesStore.Entry>>(File.ReadAllText(noteStore.Path));
  string noteMarkdown=File.ReadAllText(Path.Combine(folder,noteEntries.Single(e=>e.Id==restored[0].Id).File));
  Check(restored[0].Title=="带 # 号的标题"&&restored[0].Body.Contains("第二行")&&restored[0].X==321&&restored[0].Z==42,"note Markdown roundtrip with layout");
  Check(noteMarkdown.StartsWith("# 带 # 号的标题")&&noteMarkdown.Contains("正文可以自由书写"),"note Markdown remains readable");
  notes[0].Size=310;notes[0].Height=324;noteStore.Save(notes);restored=noteStore.Load();
  Check(restored[0].Size==310&&restored[0].Height==324,"rectangular paper size persists");
  notes[0].Markdown="# 展开的想法\n\n**重点**与==高亮==，*斜体*。\n\n![图片](便签图片/0123456789abcdef0123456789abcdef.png)";noteStore.Save(notes);restored=new NotesStore(folder).Load();
  Check(restored[0].Markdown==notes[0].Markdown&&restored[0].Body==notes[0].Body,"expanded Markdown and summary persist independently");
  noteEntries=new JavaScriptSerializer().Deserialize<System.Collections.Generic.List<NotesStore.Entry>>(File.ReadAllText(noteStore.Path));
  Check(File.ReadAllText(Path.Combine(folder,noteEntries.Single(e=>e.Id==notes[0].Id).File)).Contains(notes[0].Markdown),"expanded document readable in local Markdown");
  string oldJson="[{\"Id\":\"legacy-note\",\"Title\":\"旧便签\",\"Body\":\"不重排\",\"Color\":\"cream\",\"X\":123,\"Y\":234,\"Size\":245,\"Z\":8}]";
  string legacyNotes=Path.Combine(folder,"legacy-notes");Directory.CreateDirectory(legacyNotes);
  File.WriteAllText(Path.Combine(legacyNotes,"灵感便签.md"),"<!-- desktop-growth-notes:v1\n"+Convert.ToBase64String(Encoding.UTF8.GetBytes(oldJson))+"\n-->\n",Encoding.UTF8);
  restored=new NotesStore(legacyNotes).Load();Check(restored.Count==1&&restored[0].Height==0&&restored[0].Size==245&&restored[0].X==123&&restored[0].Y==234&&restored[0].Body=="不重排","legacy square notes load without migration");
  Console.WriteLine("Isolated data: "+folder);
 }
}
}
