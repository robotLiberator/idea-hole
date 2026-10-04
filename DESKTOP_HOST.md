# 可复用桌面网页容器

`DesktopAppHost.cs` 是与产品业务无关的 Windows 桌面外壳。它负责把一个本地 HTML 页面变成独立桌面应用：

- WPF + WebView2 本地网页容器；
- 正式模式铺满工作区、无边框、不进入任务栏；
- 窗口归属于启动它的 Windows 虚拟桌面，不做全局置顶或跨桌面固定；
- 每个产品独立单实例、独立 `%LOCALAPPDATA%` 数据目录和 WebView2 用户目录；
- `--preview` 普通可缩放预览窗口；
- `--capture <绝对PNG路径>` 保存真实 WebView 渲染截图；
- JavaScript → C# 消息入口与 C# → JavaScript 回传通道。

它不包含便签、拖拽、日程、循环任务、Markdown归档等业务功能。

## 创建第二个产品

让产品窗口继承 `DesktopWebHostWindow`，只配置身份和网页目录：

```csharp
public class PortfolioWorkspace : DesktopWebHostWindow {
    static readonly DesktopHostOptions Options=new DesktopHostOptions {
        AppId="PersonalPortfolio",
        WindowTitle="作品 · 展示桌面",
        DataFolderName="PersonalPortfolio",
        WebFolderName="portfolio-web",
        VirtualHostName="portfolio.local",
        BackgroundR=248,BackgroundG=245,BackgroundB=241
    };

    public PortfolioWorkspace() : base(Options) {}
    protected override void OnWebReady() { /* 发送作品数据 */ }
    protected override void OnWebMessage(string json) { /* 处理作品业务 */ }

    [STAThread] public static void Main() {
        DesktopHostRunner.Run(Options,delegate{return new PortfolioWorkspace();});
    }
}
```

`AppId`、`DataFolderName` 和 `VirtualHostName` 必须与灵感池不同，这样两个产品可以同时运行且数据完全隔离。

## 共用视觉语言

`web/shared/soft-theme.css` 保存柔和色调、纸张圆角和阴影变量；`web/shared/soft-theme.js` 提供同一套颜色名称。第二个产品可以复制或链接这两个文件，得到同一视觉家族，但无需复用灵感池布局。

推荐把共享范围保持在：桌面外壳、通信方式、主题色和基础材质参数。作品集的数据结构、导航和展示方式应留在第二个产品内部。
