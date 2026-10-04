using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopGrowth {
// Disposable test UI. Only calls the controller; no persistence or task rendering here.
public class DebugControls : Border {
    static Brush B(string color) {return (Brush)new BrushConverter().ConvertFromString(color);}
    public DebugControls(TaskController controller) {
        CornerRadius=new CornerRadius(13);Background=B("#192321");BorderBrush=B("#384642");BorderThickness=new Thickness(1);Padding=new Thickness(14);Margin=new Thickness(0,12,0,0);
        var stack=new StackPanel();Child=stack;
        stack.Children.Add(new TextBlock{Text="调试控制",Foreground=B("#96A99F"),FontSize=12,Margin=new Thickness(0,0,0,10)});
        var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(105)});grid.ColumnDefinitions.Add(new ColumnDefinition());
        var time=new ComboBox{IsEditable=true,Text="09:00",Height=30,Margin=new Thickness(0,0,10,0)};
        for(int h=0;h<24;h++) {time.Items.Add(h.ToString("00")+":00");time.Items.Add(h.ToString("00")+":30");}
        var title=new TextBox{Height=30,Padding=new Thickness(6,4,6,4),MaxLength=120};
        System.Windows.Automation.AutomationProperties.SetName(time,"时间");System.Windows.Automation.AutomationProperties.SetName(title,"做什么");
        var fields=new StackPanel();fields.Children.Add(new TextBlock{Text="时间",Foreground=B("#BBC9C1"),Margin=new Thickness(0,0,0,5)});fields.Children.Add(time);grid.Children.Add(fields);
        var titleFields=new StackPanel();titleFields.Children.Add(new TextBlock{Text="做什么",Foreground=B("#BBC9C1"),Margin=new Thickness(0,0,0,5)});titleFields.Children.Add(title);Grid.SetColumn(titleFields,1);grid.Children.Add(titleFields);stack.Children.Add(grid);
        var lower=new Grid{Margin=new Thickness(0,12,0,0)};lower.ColumnDefinitions.Add(new ColumnDefinition());lower.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(92)});
        var repeat=new ComboBox{Height=30,Margin=new Thickness(0,0,10,0),SelectedIndex=0};repeat.Items.Add("不循环 · 仅今天");repeat.Items.Add("每日循环");System.Windows.Automation.AutomationProperties.SetName(repeat,"是否循环");lower.Children.Add(repeat);
        var add=new Button{Content="添加",Height=30,Background=B("#91CDA2"),Foreground=B("#14291D"),BorderThickness=new Thickness(0)};Grid.SetColumn(add,1);lower.Children.Add(add);stack.Children.Add(lower);
        var status=new TextBlock{Foreground=B("#AEC8B7"),FontSize=12,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,0),Visibility=Visibility.Collapsed};stack.Children.Add(status);
        add.Click+=delegate {try {controller.Add(time.Text,title.Text,repeat.SelectedIndex==1);title.Clear();status.Text="已添加";}catch(Exception ex){status.Text=ex is ArgumentException?ex.Message:"添加未完成："+ex.Message;}status.Visibility=Visibility.Visible;};
        stack.Children.Add(new DebugOperations(controller));
    }
}
}
