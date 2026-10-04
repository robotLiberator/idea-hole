using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace DesktopGrowth {
// Entirely disposable UI; controller and archive do not depend on this file.
public class DebugOperations : Expander {
    class Choice {public string Id;public string Label;public override string ToString(){return Label;}}
    static TextBlock Label(string s) {return new TextBlock{Text=s,Margin=new Thickness(0,8,0,4),Foreground=Brushes.LightGray,TextWrapping=TextWrapping.Wrap};}
    static Button Button(string s) {return new Button{Content=s,Height=28,Margin=new Thickness(0,7,0,0)};}
    public DebugOperations(TaskController controller) {
        Header="记录 / 删除 / 每日归档";Foreground=Brushes.LightGray;Margin=new Thickness(0,12,0,0);IsExpanded=Environment.GetCommandLineArgs().Contains("--snapshot-details");
        var stack=new StackPanel();Content=new ScrollViewer{Content=stack,MaxHeight=245,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var message=Label("");message.Visibility=Visibility.Collapsed;
        Action<Action> run=action=>{try{action();}catch(Exception ex){message.Text=ex.Message;message.Visibility=Visibility.Visible;}};
        Action<string> say=text=>{message.Text=text;message.Visibility=Visibility.Visible;};
        stack.Children.Add(Label("选择任务"));var task=new ComboBox{Height=28};stack.Children.Add(task);
        stack.Children.Add(Label("实际分钟（可空） / 小总结"));var minutes=new TextBox{Height=26};stack.Children.Add(minutes);
        var note=new TextBox{MinHeight=45,MaxHeight=80,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,0)};stack.Children.Add(note);
        var save=Button("保存这条记录");stack.Children.Add(save);
        var scope=new ComboBox{Height=28,Margin=new Thickness(0,10,0,0),SelectedIndex=0};
        scope.Items.Add("删除这一条");scope.Items.Add("删除整个循环（保留过去）");stack.Children.Add(scope);
        var delete=Button("删除选中任务");stack.Children.Add(delete);
        stack.Children.Add(Label("日终归档 · 已写出的 MD 保持不变"));
        var day=new ComboBox{Height=28};stack.Children.Add(day);
        var review=new TextBox{MinHeight=55,MaxHeight=110,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,5,0,0)};stack.Children.Add(review);
        var draft=Button("保存复盘草稿（只存 JSON）");stack.Children.Add(draft);
        var finish=Button("结束这一天 · 写入当月 MD");stack.Children.Add(finish);
        var folder=Button("打开月度记录文件夹");stack.Children.Add(folder);stack.Children.Add(message);
        Action refresh=delegate {
            var selected=task.SelectedItem as Choice;string id=selected==null?null:selected.Id;
            task.ItemsSource=controller.Today().Select(t=>new Choice{Id=t.Id,Label=t.Time+"  "+t.Title}).ToList();
            task.SelectedItem=task.Items.Cast<Choice>().FirstOrDefault(c=>c.Id==id);if(task.SelectedIndex<0&&task.Items.Count>0)task.SelectedIndex=0;
            string date=day.SelectedItem as string;day.ItemsSource=controller.UnarchivedDays();day.SelectedItem=date;if(day.SelectedIndex<0&&day.Items.Count>0)day.SelectedIndex=day.Items.Count-1;
            finish.IsEnabled=draft.IsEnabled=day.Items.Count>0;
        };
        task.SelectionChanged+=delegate {var c=task.SelectedItem as Choice;if(c==null)return;var t=controller.Today().Single(x=>x.Id==c.Id);minutes.Text=t.ActualMinutes.HasValue?t.ActualMinutes.ToString():"";note.Text=t.Note;scope.SelectedIndex=0;};
        day.SelectionChanged+=delegate {string d=day.SelectedItem as string;if(d!=null)review.Text=controller.Review(d);};
        save.Click+=delegate {run(delegate {var c=task.SelectedItem as Choice;if(c==null)throw new ArgumentException("请先选择任务。");int n;int? value=null;if(!String.IsNullOrWhiteSpace(minutes.Text)){if(!Int32.TryParse(minutes.Text,out n)||n<0)throw new ArgumentException("耗时请输入非负整数。");value=n;}controller.SetDetails(c.Id,value,note.Text);say("记录已保存到 JSON。");});};
        delete.Click+=delegate {run(delegate {var c=task.SelectedItem as Choice;if(c==null)throw new ArgumentException("请先选择任务。");controller.Delete(c.Id,(DeleteScope)scope.SelectedIndex);say("已删除；已经写出的月度 MD 不变。");});};
        draft.Click+=delegate {run(delegate {string d=day.SelectedItem as string;if(d==null)return;controller.SaveReview(d,review.Text);say("复盘草稿已保存，尚未写入 MD。");});};
        finish.Click+=delegate {run(delegate {string d=day.SelectedItem as string;if(d==null)return;string path=controller.FinishDay(d,review.Text);say("已归档："+System.IO.Path.GetFileName(path));});};
        folder.Click+=delegate {run(delegate {System.IO.Directory.CreateDirectory(controller.ArchiveFolder);System.Diagnostics.Process.Start("explorer.exe",controller.ArchiveFolder);});};
        refresh();controller.Changed+=refresh;
    }
}
}
