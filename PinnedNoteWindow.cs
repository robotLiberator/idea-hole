using System;
using System.IO;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
namespace DesktopGrowth {
public class PinnedNoteWindow:Window {
    readonly WebView2 web=new WebView2();
    readonly NoteRecord note;
    readonly Action<NoteRecord> save;
    readonly JavaScriptSerializer json=new JavaScriptSerializer();
    readonly double targetLeft,targetTop;
    bool unpinPending;
    public event EventHandler Ready;
    public event EventHandler UnpinRequested;
    public class Message {public string type="",title="",body="";}
    public PinnedNoteWindow(NoteRecord n,string folder,Action<NoteRecord> saveNote,double left,double top,double width,double height) {
        note=n;save=saveNote;Title="便签 · "+n.Title;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;
        targetLeft=left;targetTop=top;Width=Math.Max(1,width);Height=Math.Max(1,height);
        Left=SystemParameters.VirtualScreenLeft-Width-200;Top=SystemParameters.VirtualScreenTop-Height-200;
        var color=NoteColor(n.Color);Background=new SolidColorBrush(color);Content=web;
        SourceInitialized+=delegate{ApplyRoundedRegion();};
        Closed+=delegate{web.Dispose();};
        Loaded+=async delegate {
            try {
                var env=await CoreWebView2Environment.CreateAsync(null,Path.Combine(folder,"WebView2"));await web.EnsureCoreWebView2Async(env);
                web.DefaultBackgroundColor=System.Drawing.Color.FromArgb(255,color.R,color.G,color.B);
                web.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;web.CoreWebView2.Settings.IsStatusBarEnabled=false;web.CoreWebView2.Settings.IsZoomControlEnabled=false;
                web.CoreWebView2.SetVirtualHostNameToFolderMapping("desktopgrowth.local",Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"web"),CoreWebView2HostResourceAccessKind.DenyCors);
                web.CoreWebView2.WebMessageReceived+=(s,e)=>{try{var m=json.Deserialize<Message>(e.WebMessageAsJson);if(m.type=="rendered"&&Left!=targetLeft){Left=targetLeft;Top=targetTop;Activate();var handler=Ready;if(handler!=null)handler(this,EventArgs.Empty);}else if(m.type=="unpin"&&!unpinPending){unpinPending=true;var handler=UnpinRequested;if(handler!=null)handler(this,EventArgs.Empty);}else if(m.type=="edit"){note.Title=m.title;note.Body=m.body;save(note);}else if(m.type=="drag"){Native.ReleaseCapture();Native.SendMessage(new WindowInteropHelper(this).Handle,0x0112,new IntPtr(0xF012),IntPtr.Zero);}}catch(Exception ex){MessageBox.Show(ex.Message,"便签保存失败");}};
                web.NavigationCompleted+=async delegate{await web.CoreWebView2.ExecuteScriptAsync("window.hostReceive("+json.Serialize(note)+")");};
                web.Source=new Uri("https://desktopgrowth.local/pinned.html");
            }catch(Exception ex){MessageBox.Show("便签未能置顶："+ex.Message);Close();}
        };
    }
    void ApplyRoundedRegion() {
        IntPtr handle=new WindowInteropHelper(this).Handle;Native.Rect rect;
        if(handle==IntPtr.Zero||!Native.GetClientRect(handle,out rect))return;
        int radius=Math.Max(12,(int)Math.Round(rect.Right*.045));
        IntPtr region=Native.CreateRoundRectRgn(0,0,rect.Right+1,rect.Bottom+1,radius,radius);
        if(Native.SetWindowRgn(handle,region,true)==0)Native.DeleteObject(region);
    }
    static Color NoteColor(string value) {
        switch(value){case "yellow":return Color.FromRgb(254,243,201);case "blue":return Color.FromRgb(220,232,250);case "peach":return Color.FromRgb(254,229,209);case "coral":return Color.FromRgb(252,228,228);case "lavender":return Color.FromRgb(232,224,248);default:return Color.FromRgb(249,240,229);}
    }
}
}
