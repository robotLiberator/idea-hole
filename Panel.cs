using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace DesktopGrowth {
public class Panel : Window {
    readonly TaskController controller;
    readonly bool workspaceMode;
    DispatcherTimer dayTimer;
    readonly string statePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopGrowth", "preview-state.txt");
    readonly StackPanel root = new StackPanel();
    readonly StackPanel tasks = new StackPanel();
    readonly TextBlock feedback = new TextBlock();
    IntPtr handle;
    bool positionLocked=true;
    DispatcherTimer desktopTimer;
    static System.Threading.EventWaitHandle showRequest;
    bool collapsed;
    static System.Threading.Mutex singleton;
    static Brush B(string value) { return (Brush)new BrushConverter().ConvertFromString(value); }
    static Border Surface(UIElement child, int radius) {
        return new Border { Child=child, CornerRadius=new CornerRadius(radius), Background=new LinearGradientBrush(Color.FromArgb(248,27,37,35),Color.FromArgb(248,17,24,25),90), BorderBrush=B("#384642"), BorderThickness=new Thickness(1), Padding=new Thickness(13) };
    }
    static TextBlock Label(string text, string color, double size) {
        return new TextBlock {Text=text, Foreground=B(color), FontSize=size, VerticalAlignment=VerticalAlignment.Center};
    }
    static ControlTemplate RoundToggle() {
        return (ControlTemplate)XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='CheckBox'><Grid Width='30' Height='30' Background='Transparent'><Ellipse x:Name='ring' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Width='22' Height='22' Stroke='#70847B' StrokeThickness='1.6' Fill='Transparent'/><Path x:Name='tick' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' Data='M 10,15 L 13,18 L 20,11' Stroke='#173221' StrokeThickness='2' StrokeStartLineCap='Round' StrokeEndLineCap='Round' Visibility='Collapsed'/></Grid><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='ring' Property='Fill' Value='#8ED89C'/><Setter TargetName='ring' Property='Stroke' Value='#8ED89C'/><Setter TargetName='tick' Property='Visibility' Value='Visible'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='ring' Property='Stroke' Value='#B8F5C4'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='ring' Property='Stroke' Value='White'/><Setter TargetName='ring' Property='StrokeThickness' Value='2.5'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
    }
    new static Button Icon(string text, string tip) {
        var button = new Button {Content=text, Width=26, Height=26, FontSize=17, Foreground=B("#A5B4AE"), Background=Brushes.Transparent, BorderThickness=new Thickness(0), Cursor=Cursors.Hand, ToolTip=tip};
        button.Template=(ControlTemplate)XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='Button'><Border Background='{TemplateBinding Background}' CornerRadius='6'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate>");
        return button;
    }
    public Panel() {
        var args=Environment.GetCommandLineArgs();
        workspaceMode=!args.Contains("--widget") && !args.Contains("--preview") && !args.Contains("--snapshot");
        Title="成长 · 计划桌面"; Width=344; SizeToContent=SizeToContent.Height;
        WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; AllowsTransparency=true;
        Background=Brushes.Transparent; ShowInTaskbar=args.Contains("--preview"); ShowActivated=workspaceMode; FontFamily=new FontFamily("Microsoft YaHei UI");
        UseLayoutRounding=true; SnapsToDevicePixels=true;
        Left=SystemParameters.WorkArea.Right-Width-24; Top=SystemParameters.WorkArea.Top+32;
        controller=new TaskController(Path.GetDirectoryName(statePath),DateTime.Today);
        if(workspaceMode) {
            AllowsTransparency=false; Background=B("#070B0A"); SizeToContent=SizeToContent.Manual;
            Left=SystemParameters.WorkArea.Left; Top=SystemParameters.WorkArea.Top;
            Width=SystemParameters.WorkArea.Width; Height=SystemParameters.WorkArea.Height;
            var workspace=new Grid {Background=new LinearGradientBrush(Color.FromRgb(5,8,7),Color.FromRgb(10,17,14),0)};
            workspace.ColumnDefinitions.Add(new ColumnDefinition());
            workspace.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(380)});
            root.Width=344; root.HorizontalAlignment=HorizontalAlignment.Center; root.VerticalAlignment=VerticalAlignment.Top; root.Margin=new Thickness(0,32,0,24);
            Grid.SetColumn(root,1); workspace.Children.Add(root); Content=workspace;
        } else Content=root;
        var frame=new Grid();
        var taskScroll=new ScrollViewer {Content=tasks,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=Math.Max(180,SystemParameters.WorkArea.Height-370)};
        frame.Children.Add(taskScroll);
        var menu=Icon("⋮","面板选项 · 空白处可拖动"); menu.VerticalAlignment=VerticalAlignment.Top; menu.HorizontalAlignment=HorizontalAlignment.Right; menu.Margin=new Thickness(0,-8,-10,0);
        var context=new ContextMenu();
        AddMenu(context,"折叠 / 展开任务",delegate {collapsed=!collapsed; tasks.Visibility=collapsed?Visibility.Collapsed:Visibility.Visible; frame.MinHeight=collapsed?20:0;});
        AddMenu(context,"回到桌面右上角",delegate { PositionAtCorner(); });
        var lockItem=new MenuItem {Header="固定位置",IsCheckable=true,IsChecked=true};
        lockItem.Click+=delegate {positionLocked=lockItem.IsChecked;}; context.Items.Add(lockItem);
        AddMenu(context,"退出面板",delegate {Close();});
        menu.Click+=delegate {context.PlacementTarget=menu; context.IsOpen=true;};
        frame.Children.Add(menu);
        var taskSurface=Surface(frame,16); taskSurface.Padding=new Thickness(12,15,17,11); root.Children.Add(taskSurface);
        taskSurface.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e) {if(!positionLocked && (e.OriginalSource is Border || e.OriginalSource is Grid)) DragMove();};
        RenderTasks(); controller.Changed+=RenderTasks;
        var command=new Grid(); command.ColumnDefinitions.Add(new ColumnDefinition()); command.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(28)});
        var input=new TextBox {FontSize=14, Foreground=B("#E2EAE5"), Background=Brushes.Transparent, BorderThickness=new Thickness(0), CaretBrush=B("#A8E5B5"), VerticalContentAlignment=VerticalAlignment.Center, Padding=new Thickness(2,7,4,7), MaxLength=2000};
        System.Windows.Automation.AutomationProperties.SetName(input,"自然语言输入（接口待接入）");
        var placeholder=Label("说点什么…","#8E9F97",14); placeholder.Margin=new Thickness(3,0,0,0); placeholder.IsHitTestVisible=false;
        command.Children.Add(input); command.Children.Add(placeholder);
        input.TextChanged+=delegate {placeholder.Visibility=input.Text.Length==0?Visibility.Visible:Visibility.Collapsed; feedback.Visibility=Visibility.Collapsed;};
        input.KeyDown+=delegate(object sender,KeyEventArgs e) {if(e.Key==Key.Enter) {if(!String.IsNullOrWhiteSpace(input.Text)) Notify("对话功能待接入，任务未修改。"); e.Handled=true;} if(e.Key==Key.Escape) {input.Clear(); Keyboard.ClearFocus();}};
        var mic=Icon("\uE720","语音入口 · 待接入"); mic.FontFamily=new FontFamily("Segoe MDL2 Assets"); mic.FontSize=17;
        mic.Click+=delegate {Notify("语音功能待接入。");}; Grid.SetColumn(mic,1); command.Children.Add(mic);
        var commandSurface=Surface(command,13); commandSurface.Padding=new Thickness(12,4,10,4); commandSurface.Margin=new Thickness(0,7,0,0); root.Children.Add(commandSurface);
        feedback.FontSize=12; feedback.Foreground=B("#C4D4CA"); feedback.TextWrapping=TextWrapping.Wrap; feedback.Margin=new Thickness(10,7,10,0); feedback.Visibility=Visibility.Collapsed; root.Children.Add(feedback);
#if DEBUG_CONTROLS
        root.Children.Add(new ScrollViewer{Content=new DebugControls(controller),MaxHeight=300,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
#endif
        dayTimer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(15)};
        dayTimer.Tick+=delegate {try {controller.ChangeDay(DateTime.Today);}catch(Exception){Notify("当天任务生成失败，请稍后重试。");}};
        dayTimer.Start();Closed+=delegate {dayTimer.Stop();};
        SourceInitialized+=delegate {
            handle=new WindowInteropHelper(this).Handle;
            if(!ShowInTaskbar && !workspaceMode) Native.SetWindowLong(handle,-20,Native.GetWindowLong(handle,-20)|0x80);
        };
        Loaded+=delegate {
            if(args.Contains("--snapshot")) {Dispatcher.BeginInvoke(new Action(delegate {Snapshot(); Close();}),DispatcherPriority.ApplicationIdle); return;}
            if(workspaceMode) StartWorkspaceWindow();
            else if(!args.Contains("--preview")) StartDesktopWindow();
        };
    }
    void AddMenu(ContextMenu menu,string label,Action action) {var item=new MenuItem{Header=label};item.Click+=delegate {action();};menu.Items.Add(item);}
    void RenderTasks() {tasks.Children.Clear();var rows=controller.Today();for(int i=0;i<rows.Count;i++) AddRow(rows[i],i,rows.Count);}
    void AddRow(TaskRecord task,int index,int count) {
        var row=new Grid {Height=47, Margin=new Thickness(0,0,0,index==count-1?0:4)};
        row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(53)});
        row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(16)});
        row.ColumnDefinitions.Add(new ColumnDefinition());
        var time=Label(task.Time,"#9FAEA7",13);time.TextTrimming=TextTrimming.CharacterEllipsis;time.ToolTip=task.Time; row.Children.Add(time);
        var track=new Grid(); Grid.SetColumn(track,1); row.Children.Add(track);
        if(index>0) track.Children.Add(new Border {Width=1,Height=27,Background=B("#495C51"),VerticalAlignment=VerticalAlignment.Top});
        if(index<count-1) track.Children.Add(new Border {Width=1,Height=27,Background=B("#495C51"),VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,-4)});
        var dot=new System.Windows.Shapes.Ellipse {Width=7,Height=7,Fill=B(task.Done?"#7CC48A":"#64746D")}; track.Children.Add(dot);
        var card=new Border {CornerRadius=new CornerRadius(11),Background=B("#27332F"),Margin=new Thickness(4,0,0,0)}; Grid.SetColumn(card,2); row.Children.Add(card);
        var body=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(7,0,4,0)}; card.Child=body;
        var check=new CheckBox {Template=RoundToggle(),IsChecked=task.Done,VerticalAlignment=VerticalAlignment.Center,Cursor=Cursors.Hand,ToolTip="标记完成 / 撤销"};
        System.Windows.Automation.AutomationProperties.SetName(check,task.Title+" 完成状态");
        body.Children.Add(check); var name=Label(task.Title,"#E3EBE6",15);name.MaxWidth=165;name.TextTrimming=TextTrimming.CharacterEllipsis;name.ToolTip=task.Title; name.Margin=new Thickness(8,0,0,0); body.Children.Add(name);
        check.Click+=delegate {try{controller.SetDone(task,check.IsChecked==true);}catch(Exception){Notify("保存未完成，请重试。");check.IsChecked=task.Done;}dot.Fill=B(task.Done?"#7CC48A":"#64746D");};
        card.MouseEnter+=delegate {card.Background=B("#35443D");}; card.MouseLeave+=delegate {card.Background=B("#27332F");};
        tasks.Children.Add(row);
    }
    void Notify(string text) {feedback.Text=text;feedback.Visibility=Visibility.Visible;}
    void StartWorkspaceWindow() {
        handle=new WindowInteropHelper(this).EnsureHandle();
        Left=SystemParameters.WorkArea.Left; Top=SystemParameters.WorkArea.Top;
        Width=SystemParameters.WorkArea.Width; Height=SystemParameters.WorkArea.Height;
        // Keep this as an ordinary top-level window. Windows virtual desktops then
        // own it like any other application window instead of treating it as part
        // of Explorer's shared desktop surface.
        var style=Native.GetWindowLong(handle,-20);
        Native.SetWindowLong(handle,-20,style&~0x80);
        Activate();
    }
    void StartDesktopWindow() {
        handle=new WindowInteropHelper(this).EnsureHandle(); PositionAtCorner();
        // WPF remains a top-level surface; it must not be reparented into Explorer.
        Native.SetWindowLong(handle,-20,(Native.GetWindowLong(handle,-20)|0x80)&~0x40000);
        Deactivated+=delegate {PlaceOnDesktop();};
        desktopTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(300)};
        desktopTimer.Tick+=delegate {if(showRequest!=null && showRequest.WaitOne(0)) PositionAtCorner(); PlaceOnDesktop();};
        desktopTimer.Start(); Closed+=delegate {desktopTimer.Stop();};
        Dispatcher.BeginInvoke(new Action(PlaceOnDesktop),DispatcherPriority.ApplicationIdle);
    }
    string lastDesktopStatus;
    void PlaceOnDesktop() {
        var foreground=Native.GetForegroundWindow();
        uint process; Native.GetWindowThreadProcessId(foreground,out process);
        if(process==(uint)System.Diagnostics.Process.GetCurrentProcess().Id) return;
        var cls=new System.Text.StringBuilder(256);Native.GetClassName(foreground,cls,cls.Capacity);
        bool desktopActive=cls.ToString()=="Progman" || cls.ToString()=="WorkerW";
        if(desktopActive) Native.ShowWindow(handle,4); // SW_SHOWNOACTIVATE, also survives Show Desktop.
        IntPtr desktop=IntPtr.Zero;
        Native.EnumWindows(delegate(IntPtr window,IntPtr unused) {
            if(Native.FindWindowEx(window,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero) {desktop=window;return false;}
            return true;
        },IntPtr.Zero);
        if(desktop!=IntPtr.Zero) {
            // Place immediately above the desktop, below every ordinary app.
            // Do not parent the WPF layered surface to Explorer or activate it.
            var preceding=Native.GetWindow(desktop,3);
            if(preceding==handle) preceding=Native.GetWindow(handle,3);
            if(preceding!=IntPtr.Zero && (Native.GetWindowLong(preceding,-20)&8)!=0) preceding=IntPtr.Zero;
            Native.SetWindowPos(handle,preceding,0,0,0,0,0x13);
        } else Native.SetWindowPos(handle,new IntPtr(1),0,0,0,0,0x13);
        string status="Mode=Desktop widget\nShowInTaskbar="+ShowInTaskbar+"\nToolWindow="+((Native.GetWindowLong(handle,-20)&0x80)!=0)+"\nTopmost="+((Native.GetWindowLong(handle,-20)&8)!=0)+"\nDesktopFound="+(desktop!=IntPtr.Zero)+"\nDesktopActive="+desktopActive+"\nVisible="+Native.IsWindowVisible(handle);
        if(status!=lastDesktopStatus) {lastDesktopStatus=status;LogDesktop(status);}
    }
    void LogDesktop(string message) {try {Directory.CreateDirectory(Path.GetDirectoryName(statePath));File.WriteAllText(Path.Combine(Path.GetDirectoryName(statePath),"desktop-status.txt"),message);}catch(Exception){}}
    void PositionAtCorner() {
        Left=SystemParameters.WorkArea.Right-ActualWidth-24;Top=SystemParameters.WorkArea.Top+32;
    }
    void Snapshot() {UpdateLayout();var image=new RenderTargetBitmap((int)Math.Ceiling(ActualWidth*2),(int)Math.Ceiling(ActualHeight*2),192,192,PixelFormats.Pbgra32);image.Render(this);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"panel-preview.png"))) encoder.Save(file);}
    [STAThread] public static void Main() {
        try {Native.SetProcessDpiAwarenessContext(new IntPtr(-4));} catch(EntryPointNotFoundException) {}
        bool created; singleton=new System.Threading.Mutex(true,"Local\\DesktopGrowthPanel",out created);
        showRequest=new System.Threading.EventWaitHandle(false,System.Threading.EventResetMode.AutoReset,"Local\\DesktopGrowthShow");
        if(!created && !Environment.GetCommandLineArgs().Contains("--snapshot")) {showRequest.Set();return;}
        var app=new Application();
        try {app.Run(new Panel());} catch(Exception ex) {MessageBox.Show("面板未能启动，现有数据未被重置。\n"+ex.Message,"成长面板");}
        GC.KeepAlive(singleton);
    }
}
static class Native {
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd,uint command);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint process);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd,System.Text.StringBuilder text,int count);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd,int cmd);
    [DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    public delegate bool EnumProc(IntPtr hwnd,IntPtr param);
    [StructLayout(LayoutKind.Sequential)] public struct POINT {public int X;public int Y;}
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc proc,IntPtr param);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string name);
    [DllImport("user32.dll",SetLastError=true)] public static extern IntPtr SetParent(IntPtr child,IntPtr parent);
    [DllImport("user32.dll")] public static extern IntPtr GetParent(IntPtr child);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hwnd,int index);
    [DllImport("user32.dll")] public static extern int SetWindowLong(IntPtr hwnd,int index,int value);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hwnd,IntPtr after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr hwnd,ref POINT point);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hwnd,uint msg,IntPtr wp,IntPtr lp);
    [DllImport("kernel32.dll")] public static extern void SetLastError(uint error);
}
}
