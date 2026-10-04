using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Threading;

namespace DesktopGrowth {
public class BridgeMessage {
    public string type="", id="", requestId="", data="";
    public bool done;
    public double x,y,width,height,viewportWidth,viewportHeight;
    public List<NoteRecord> notes;
}

public class WebWorkspace : DesktopWebHostWindow {
    static readonly DesktopHostOptions HostOptions=new DesktopHostOptions {
        AppId="DesktopGrowth",WindowTitle="灵感池 · Idea Hole",DataFolderName="DesktopGrowth",PortableDataFolder="数据",
        WebFolderName="web",VirtualHostName="desktopgrowth.local",
        BackgroundR=248,BackgroundG=245,BackgroundB=241,PreviewWidth=1500,PreviewHeight=930
    };
    readonly JavaScriptSerializer json=new JavaScriptSerializer {MaxJsonLength=Int32.MaxValue};
    readonly TaskController controller;
    readonly NotesStore notes;
    string IdeasFolder {get{return Path.Combine(DataFolder,"灵感池");}}
    bool closeFlushed,closePending;
    readonly DispatcherTimer dayTimer;
    readonly Dictionary<string,PinnedNoteWindow> pinned=new Dictionary<string,PinnedNoteWindow>();
    readonly HashSet<string> returning=new HashSet<string>();
    public WebWorkspace() : base(HostOptions) {
        controller=new TaskController(Path.Combine(DataFolder,"日程"),DateTime.Today);notes=new NotesStore(IdeasFolder);
        controller.Changed+=SendState;
        dayTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(15)};
        dayTimer.Tick+=delegate {try{controller.ChangeDay(DateTime.Today);}catch(Exception ex){Send(new {type="error",message="日程记录同步失败："+ex.Message});}};dayTimer.Start();
        Closed+=delegate {dayTimer.Stop();foreach(var window in pinned.Values.ToArray())window.Close();};
        Closing+=async delegate(object sender,System.ComponentModel.CancelEventArgs e) {
            if(closeFlushed||!IsWebReady)return;e.Cancel=true;if(closePending)return;closePending=true;
            try {string value=await Browser.CoreWebView2.ExecuteScriptAsync("window.flushForClose()");SaveNotes(json.Deserialize<List<NoteRecord>>(value));closeFlushed=true;Close();}
            catch(Exception ex){closePending=false;MessageBox.Show("便签未能保存，窗口暂未关闭：\n"+ex.Message,"成长桌面");}
        };
    }
    protected override void OnWebViewCreated() {Browser.CoreWebView2.SetVirtualHostNameToFolderMapping("noteimages.desktopgrowth.local",NoteImages.Folder(IdeasFolder),Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);}
    protected override void OnWebReady() {SendState();}
    protected override void OnWebMessage(string rawJson) {
        try {
            var message=json.Deserialize<BridgeMessage>(rawJson);
            if(message==null)return;
            if(message.type=="ready")SendState();
            else if(message.type=="pickNoteImage"||message.type=="importNoteImage") {
                try {
                    byte[] bytes;
                    if(message.type=="pickNoteImage") {
                        var picker=new Microsoft.Win32.OpenFileDialog{Filter="图片|*.png;*.jpg;*.jpeg;*.webp;*.gif;*.bmp",Multiselect=false};
                        if(picker.ShowDialog(this)!=true){Send(new {type="noteImage",requestId=message.requestId,path=""});return;}
                        if(new FileInfo(picker.FileName).Length>20*1024*1024)throw new InvalidOperationException("请选择20 MB以内的图片。");bytes=File.ReadAllBytes(picker.FileName);
                    }else{int comma=message.data.IndexOf(',');if(comma<0||message.data.Length>29000000)throw new InvalidOperationException("图片数据无效或过大。");bytes=Convert.FromBase64String(message.data.Substring(comma+1));}
                    string imagePath=NoteImages.Import(IdeasFolder,bytes);Send(new {type="noteImage",requestId=message.requestId,path=imagePath});
                }catch(Exception ex){Send(new {type="noteImage",requestId=message.requestId,error="图片未插入："+ex.Message});}
            }
            else if(message.type=="toggleTask") {
                controller.ChangeDay(DateTime.Today);
                var task=controller.Today().FirstOrDefault(t=>t.Id==message.id);if(task!=null)controller.SetDone(task,message.done);
            } else if(message.type=="saveNotes") SaveNotes(message.notes);
            else if(message.type=="archiveNote") {
                if(pinned.ContainsKey(message.id))throw new InvalidOperationException("请先把便签收回桌面，再归档。");
                SaveNotes(message.notes);var archived=notes.Archive(message.id,DateTimeOffset.Now);
                Send(new {type="archivedNote",id=message.id,createdAt=archived.CreatedAt,archivedAt=archived.ArchivedAt});
            }
            else if(message.type=="pinNote") {
                SaveNotes(message.notes);if(pinned.ContainsKey(message.id)){pinned[message.id].Activate();return;}
                var note=notes.Load().FirstOrDefault(n=>n.Id==message.id);if(note==null)return;
                double scaleX=message.viewportWidth>0?Browser.ActualWidth/message.viewportWidth:1;
                double scaleY=message.viewportHeight>0?Browser.ActualHeight/message.viewportHeight:scaleX;
                var local=new Point(message.x*scaleX,message.y*scaleY);
                var device=Browser.PointToScreen(local);var source=PresentationSource.FromVisual(Browser);
                var screen=source!=null&&source.CompositionTarget!=null?source.CompositionTarget.TransformFromDevice.Transform(device):device;
                var originDevice=Browser.PointToScreen(new Point(0,0));
                var origin=source!=null&&source.CompositionTarget!=null?source.CompositionTarget.TransformFromDevice.Transform(originDevice):originDevice;
                var window=new PinnedNoteWindow(note,DataFolder,SavePinnedNote,screen.X,screen.Y,message.width*scaleX,message.height*scaleY);pinned.Add(note.Id,window);
                window.Ready+=delegate{Send(new {type="pinState",id=note.Id,pinned=true});};
                window.UnpinRequested+=delegate{Send(new {type="prepareUnpin",id=note.Id,note=notes.Load().FirstOrDefault(n=>n.Id==note.Id),x=(window.Left-origin.X)/scaleX,y=(window.Top-origin.Y)/scaleY});};
                window.Closed+=delegate{pinned.Remove(note.Id);if(!returning.Remove(note.Id))Send(new {type="pinState",id=note.Id,pinned=false,note=notes.Load().FirstOrDefault(n=>n.Id==note.Id)});};
                window.Show();
            } else if(message.type=="unpinReady") {
                PinnedNoteWindow window;if(!pinned.TryGetValue(message.id,out window))return;
                returning.Add(message.id);SaveReturningNotes(message.notes,message.id);window.Close();
            }
        } catch(Exception ex) {Send(new {type="error",message=ex.Message});}
    }
    void SaveNotes(List<NoteRecord> incoming) {
        if(incoming==null)return;
        // Main-page snapshots may predate edits in a floating note.
        var saved=notes.Load();var merged=incoming.Where(n=>!pinned.ContainsKey(n.Id)).ToList();
        merged.AddRange(saved.Where(n=>pinned.ContainsKey(n.Id)));notes.Save(merged);
    }
    void SavePinnedNote(NoteRecord note) {
        var saved=notes.Load();int index=saved.FindIndex(n=>n.Id==note.Id);if(index<0)return;saved[index]=note;notes.Save(saved);
    }
    void SaveReturningNotes(List<NoteRecord> incoming,string returningId) {
        if(incoming==null)return;
        var saved=notes.Load();var merged=incoming.Where(n=>!pinned.ContainsKey(n.Id)||n.Id==returningId).ToList();
        merged.AddRange(saved.Where(n=>pinned.ContainsKey(n.Id)&&n.Id!=returningId));notes.Save(merged);
    }
    void SendState() {Send(new {type="state",tasks=controller.Today(),notes=notes.Load(),notesPath=notes.Path});}
    void Send(object value) {
        SendToWeb(json.Serialize(value));
    }
    [STAThread] public static void Main() {
        DesktopHostRunner.Run(HostOptions,delegate{return new WebWorkspace();});
    }
}
}
