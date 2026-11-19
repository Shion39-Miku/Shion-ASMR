using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Nocturne;
public static class DialogShell
{
 public static Window Create(string title,double width,double height)
 {
  var window=new Window{Title="月咲 Shion · "+title,Width=width,Height=height,Background=new SolidColorBrush(Color.FromRgb(15,21,38)),Foreground=(Brush)Application.Current.FindResource("Ink"),FontFamily=new FontFamily("Microsoft YaHei UI, Yu Gothic UI, Segoe UI"),WindowStartupLocation=WindowStartupLocation.CenterOwner,ResizeMode=ResizeMode.CanResize};NativeTheme.Attach(window);return window;
 }
}
public sealed class WorkEditor : Window
{
 sealed record Destination(string Path,string Name){public override string ToString()=>Name;}
 public WorkEditor(Work work,List<Work> works,Library library,Settings s)
 {
  Title="调整分类";Width=660;Height=580;MinWidth=550;MinHeight=480;Background=new SolidColorBrush(Color.FromRgb(15,21,38));Foreground=(Brush)Application.Current.FindResource("Ink");FontFamily=new FontFamily("Microsoft YaHei UI");WindowStartupLocation=WindowStartupLocation.CenterOwner;NativeTheme.Attach(this);
  var p=new StackPanel{Margin=new Thickness(28)};Content=new ScrollViewer{Content=p};p.Children.Add(new TextBlock{Text="整理作品分类",FontSize=24,Margin=new Thickness(0,0,0,18)});
  p.Children.Add(new TextBlock{Text="当前分类名称",Margin=new Thickness(0,8,0,8)});var name=new TextBox{Text=work.Name};p.Children.Add(name);
  p.Children.Add(new TextBlock{Text="要调整归属的子分组（可多选）",Margin=new Thickness(0,18,0,8)});var groups=new ListBox{ItemsSource=work.Groups,DisplayMemberPath="Path",SelectionMode=SelectionMode.Multiple,Height=140};p.Children.Add(groups);
  var candidates=works.Select(w=>w.Path).Concat(work.Groups.Select(g=>g.Path)).Concat(work.Groups.SelectMany(g=>Ancestors(g.Path,g.Root))).Distinct(StringComparer.OrdinalIgnoreCase).Where(library.Folders.ContainsKey).Select(x=>new Destination(x,Path.GetFileName(x)+"  ·  "+x)).ToList();
  p.Children.Add(new TextBlock{Text="选中子分组的所属作品目录",Margin=new Thickness(0,18,0,8)});var target=new ComboBox{ItemsSource=candidates,SelectedItem=candidates.FirstOrDefault(x=>x.Path==work.Path)};p.Children.Add(target);
  p.Children.Add(new TextBlock{Text="仅修改分类映射，不移动媒体。未选中子分组时，只修改当前分类名称；修改归属后保留各音轨的进度和字幕设置。",FontSize=11,Foreground=(Brush)Application.Current.FindResource("Muted"),Margin=new Thickness(4,15,0,10)});
  var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};p.Children.Add(actions);var cancel=new Button{Content="取消"};cancel.Click+=(_,_)=>Close();actions.Children.Add(cancel);var apply=new Button{Content="保存修正"};apply.Click+=(_,_)=>{if(!string.IsNullOrWhiteSpace(name.Text))s.WorkNames[work.Path]=name.Text.Trim();if(target.SelectedItem is Destination destination){foreach(Folder f in groups.SelectedItems)s.WorkAssignments[f.Path]=destination.Path;}DialogResult=true;};actions.Children.Add(apply);
 }
 static IEnumerable<string> Ancestors(string path,string root){var p=Path.GetDirectoryName(path);while(p!=null&&WorkCatalog.Within(p,root)){yield return p;if(p==root)break;p=Path.GetDirectoryName(p);}}
}
