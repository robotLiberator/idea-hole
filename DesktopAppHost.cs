using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace DesktopGrowth {

// Reusable shell that turns a local HTML page into an independent desktop product.
// It deliberately contains no notes, schedules or other product-specific concepts.
public sealed class DesktopHostOptions {
    public string AppId="DesktopWebApp";
    public string WindowTitle="Desktop Web App";
    public string DataFolderName="DesktopWebApp";
    public string PortableDataFolder="";
    public string WebFolderName="web";
    public string VirtualHostName="desktopapp.local";
    public byte BackgroundR=248,BackgroundG=245,BackgroundB=241;
    public double PreviewWidth=1500,PreviewHeight=930;
    public bool EnableDevTools=true;
}

public abstract class DesktopWebHostWindow : Window {
    readonly DesktopHostOptions options;
    readonly WebView2 webView=new WebView2();
    bool webReady;
    IntPtr windowHandle;
    DispatcherTimer desktopTimer;
    byte[] launchVirtualDesktopId;

    protected DesktopWebHostWindow(DesktopHostOptions options) {
        this.options=options;
        DataFolder=String.IsNullOrEmpty(options.PortableDataFolder)?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),options.DataFolderName):Path.Combine(AppDomain.CurrentDomain.BaseDirectory,options.PortableDataFolder);
        IsPreview=HasArgument("--preview");
        Title=options.WindowTitle;
        WindowStyle=IsPreview?WindowStyle.SingleBorderWindow:WindowStyle.None;
        ResizeMode=IsPreview?ResizeMode.CanResize:ResizeMode.NoResize;
        ShowInTaskbar=IsPreview;
        Background=new SolidColorBrush(Color.FromRgb(options.BackgroundR,options.BackgroundG,options.BackgroundB));
        if(IsPreview) {
            Width=options.PreviewWidth;Height=options.PreviewHeight;
            WindowStartupLocation=WindowStartupLocation.CenterScreen;
        } else {
            Left=SystemParameters.WorkArea.Left;Top=SystemParameters.WorkArea.Top;
            Width=SystemParameters.WorkArea.Width;Height=SystemParameters.WorkArea.Height;
        }
        Content=webView;
        Loaded+=async delegate {await InitializeWebView();};
        SourceInitialized+=delegate {
            if(IsPreview)return;
            windowHandle=new WindowInteropHelper(this).Handle;
            int ex=Native.GetWindowLong(windowHandle,Native.GWL_EXSTYLE);
            Native.SetWindowLong(windowHandle,Native.GWL_EXSTYLE,(ex|Native.WS_EX_TOOLWINDOW)&~Native.WS_EX_APPWINDOW);
            launchVirtualDesktopId=ReadCurrentVirtualDesktopId();
            AttachToDesktopOwner();
            desktopTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(75)};
            desktopTimer.Tick+=delegate {KeepOnDesktopSurface();};
            desktopTimer.Start();
            Closed+=delegate {ReleaseDesktopIntegration();};
            Dispatcher.BeginInvoke(new Action(KeepOnDesktopSurface),DispatcherPriority.ApplicationIdle);
        };
    }

    // Ordinary top-level windows stay on the Windows virtual desktop where launched.
    // The shell intentionally avoids global pinning and Explorer child-window embedding.
    protected string DataFolder {get;private set;}
    protected bool IsPreview {get;private set;}
    protected WebView2 Browser {get{return webView;}}
    protected bool IsWebReady {get{return webReady;}}

    async System.Threading.Tasks.Task InitializeWebView() {
        Directory.CreateDirectory(DataFolder);
        string userData=Path.Combine(DataFolder,"WebView2");
        var environment=await CoreWebView2Environment.CreateAsync(null,userData);
        await webView.EnsureCoreWebView2Async(environment);
        webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;
        webView.CoreWebView2.Settings.AreDevToolsEnabled=options.EnableDevTools;
        webView.CoreWebView2.Settings.IsStatusBarEnabled=false;
        webView.CoreWebView2.Settings.IsZoomControlEnabled=false;
        webView.CoreWebView2.WebMessageReceived+=delegate(object sender,CoreWebView2WebMessageReceivedEventArgs e) {OnWebMessage(e.WebMessageAsJson);};
        string webRoot=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,options.WebFolderName);
        webView.CoreWebView2.SetVirtualHostNameToFolderMapping(options.VirtualHostName,webRoot,CoreWebView2HostResourceAccessKind.DenyCors);
        OnWebViewCreated();
        webView.NavigationCompleted+=async delegate {
            webReady=true;OnWebReady();
            string capture=ArgumentValue("--capture");
            if(!String.IsNullOrWhiteSpace(capture)) {
                try {
                    await System.Threading.Tasks.Task.Delay(1200);
                    using(var file=new FileStream(Path.GetFullPath(capture),FileMode.Create,FileAccess.Write))
                        await webView.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,file);
                } catch(Exception ex) {System.Diagnostics.Debug.WriteLine(ex);}
            }
        };
        webView.Source=new Uri("https://"+options.VirtualHostName+"/index.html");
    }

    protected virtual void OnWebViewCreated() {}
    protected virtual void OnWebReady() {}
    protected abstract void OnWebMessage(string json);
    protected void SendToWeb(string json) {
        if(!webReady||webView.CoreWebView2==null)return;
        Dispatcher.BeginInvoke(new Action(async delegate {try {await webView.CoreWebView2.ExecuteScriptAsync("window.hostReceive("+json+")");}catch{}}));
    }
    static bool HasArgument(string name) {return Array.IndexOf(Environment.GetCommandLineArgs(),name)>=0;}
    static string ArgumentValue(string name) {string[] args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:null;}

    void AttachToDesktopOwner() {
        IntPtr desktop=Native.FindWindow("Progman","Program Manager");
        if(desktop!=IntPtr.Zero && Native.GetWindow(windowHandle,Native.GW_OWNER)!=desktop)
            Native.SetOwner(windowHandle,desktop);
    }

    static byte[] ReadCurrentVirtualDesktopId() {
        try {
            using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops")) {
                var value=key==null?null:key.GetValue("CurrentVirtualDesktop") as byte[];
                return value==null?null:(byte[])value.Clone();
            }
        } catch {return null;}
    }

    bool IsLaunchVirtualDesktopActive() {
        if(launchVirtualDesktopId==null)return true;
        byte[] current=ReadCurrentVirtualDesktopId();
        if(current==null||current.Length!=launchVirtualDesktopId.Length)return true;
        for(int i=0;i<current.Length;i++)if(current[i]!=launchVirtualDesktopId[i])return false;
        return true;
    }

    // An owned top-level window naturally stays above Progman when Show Desktop
    // runs, so the ordinary wallpaper never flashes through. It remains a normal
    // WPF/WebView2 surface rather than an Explorer child window.
    void KeepOnDesktopSurface() {
        if(windowHandle==IntPtr.Zero)return;
        if(!IsLaunchVirtualDesktopActive()) {
            if(Native.IsWindowVisible(windowHandle))Native.ShowWindow(windowHandle,Native.SW_HIDE);
            return;
        }
        AttachToDesktopOwner();
        if(!Native.IsWindowVisible(windowHandle)||Native.IsIconic(windowHandle))
            Native.ShowWindow(windowHandle,Native.SW_SHOWNOACTIVATE);
    }

    void ReleaseDesktopIntegration() {
        if(desktopTimer!=null)desktopTimer.Stop();
        if(windowHandle!=IntPtr.Zero)Native.SetOwner(windowHandle,IntPtr.Zero);
    }
}

public static class DesktopHostRunner {
    static System.Threading.Mutex singleton;
    public static void Run(DesktopHostOptions options,Func<Window> createWindow) {
        try {Native.SetProcessDpiAwarenessContext(new IntPtr(-4));}catch(EntryPointNotFoundException){}
        bool preview=Array.IndexOf(Environment.GetCommandLineArgs(),"--preview")>=0,created;
        singleton=new System.Threading.Mutex(true,"Local\\"+options.AppId+(preview?".Preview":".Desktop"),out created);
        if(!created)return;
        var app=new Application();
        try {app.Run(createWindow());}
        catch(Exception ex) {MessageBox.Show(options.WindowTitle+"未能启动，现有数据未被重置。\n"+ex.Message,options.WindowTitle);}
        GC.KeepAlive(singleton);
    }
}

static class Native {
    public const int GWL_EXSTYLE=-20;
    public const int WS_EX_TOOLWINDOW=0x80;
    public const int WS_EX_APPWINDOW=0x40000;
    public const int SW_HIDE=0;
    public const int SW_SHOWNOACTIVATE=4;
    public const uint GW_OWNER=4;
    [StructLayout(LayoutKind.Sequential)] public struct Rect {public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hwnd,out Rect rect);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateRoundRectRgn(int left,int top,int right,int bottom,int width,int height);
    [DllImport("user32.dll")] public static extern int SetWindowRgn(IntPtr hwnd,IntPtr region,bool redraw);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr value);
    [DllImport("user32.dll")] public static extern bool ReleaseCapture();
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hwnd,uint message,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hwnd,int index);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hwnd,int index,int value);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr64(IntPtr hwnd,int index,IntPtr value);
    [DllImport("user32.dll",EntryPoint="SetWindowLongW")] static extern int SetWindowLongPtr32(IntPtr hwnd,int index,int value);
    public static IntPtr SetOwner(IntPtr hwnd,IntPtr owner) {return IntPtr.Size==8?SetWindowLongPtr64(hwnd,-8,owner):new IntPtr(SetWindowLongPtr32(hwnd,-8,owner.ToInt32()));}
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd,int command);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string className,string windowName);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd,uint command);
}
}
