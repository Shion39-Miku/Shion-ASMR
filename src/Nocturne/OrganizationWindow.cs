using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Nocturne;
public sealed class OrganizationWindow : Window
{
 public OrganizationWindow(Organization tree,List<Work> works,Settings settings,string current)
 {
  Title="整理分类文件夹";Width=880;Height=620;MinWidth=720;MinHeight=500;WindowStartupLocation=WindowStartupLocation.CenterOwner;NativeTheme.Attach(this);Background=new SolidColorBrush(Color.FromRgb(15,21,38));Foreground=(Brush)Application.Current.FindResource("Ink");FontFamily=new FontFamily("Microsoft YaHei UI");
  var grid=new Grid{Margin=new Thickness(24)};Content=grid;grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(330)});
  var left=new DockPanel{Margin=new Thickness(0,0,20,0)};grid.Children.Add(left);var search=new TextBox{ToolTip="搜索需要调整位置的作品"};DockPanel.SetDock(search,Dock.Top);left.Children.Add(search);var all=new Button{Content="全选搜索结果"};DockPanel.SetDock(all,Dock.Top);left.Children.Add(all);var list=new ListBox{ItemsSource=works,SelectionMode=SelectionMode.Extended,DisplayMemberPath="Summary"};left.Children.Add(list);all.Click+=(_,_)=>list.SelectAll();search.TextChanged+=(_,_)=>list.ItemsSource=works.Where(w=>w.SearchText.Contains(search.Text.Trim(),StringComparison.OrdinalIgnoreCase)).ToArray();
  var right=new StackPanel();Grid.SetColumn(right,1);grid.Children.Add(new ScrollViewer{Content=right,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Grid.SetColumn(grid.Children[^1],1);
  right.Children.Add(new TextBlock{Text="分类文件夹",FontSize=24,FontWeight=FontWeights.SemiBold});right.Children.Add(new TextBlock{Text="社团、系列与子系列均可使用文件夹组织。仅修改软件内关系，不改动媒体文件。",Margin=new Thickness(0,12,0,14)});
  var folder=new ComboBox{DisplayMemberPath="Label",SelectedValuePath="Id"};right.Children.Add(folder);
  void Refresh(string selected){folder.ItemsSource=new[]{new Choice("","分类根目录")}.Concat(tree.Folders.Values.OrderBy(f=>tree.Label(f.Id),Natural.Instance).Select(f=>new Choice(f.Id,tree.Label(f.Id)))).ToArray();folder.SelectedValue=selected;}
  Refresh(current);var name=new TextBox{MaxLength=160,ToolTip="文件夹名称"};right.Children.Add(name);var status=new TextBlock{Margin=new Thickness(4,12,4,8)};
  void Persist(string text){State.Write("settings.json",settings);status.Text=text;}
  void Button(string text,Action action){var b=new Button{Content=text};b.Click+=(_,_)=>action();right.Children.Add(b);}
  Button("在所选位置创建文件夹",()=>{var text=name.Text.Trim();if(text.Length==0){status.Text="请输入文件夹名称。";return;}var f=new VirtualFolder{Id="manual:"+Guid.NewGuid().ToString("N"),Parent=folder.SelectedValue as string??"",Name=text};settings.Organization[f.Id]=f;tree.Folders[f.Id]=f.Copy();Refresh(f.Id);Persist("已创建，支持继续创建下一级。");});
  Button("重命名所选文件夹",()=>{if(folder.SelectedValue is not string id||!tree.Folders.TryGetValue(id,out var f)||name.Text.Trim().Length==0){status.Text="请选择文件夹并输入名称。";return;}f.Name=name.Text.Trim();settings.Organization[id]=f.Copy();Refresh(id);Persist("名称已保存。");});
  Button("将所选作品移入此文件夹",()=>{var target=folder.SelectedValue as string??"";tree.Pin(settings,target);var selected=list.SelectedItems.Cast<Work>().ToArray();foreach(var w in selected)settings.WorkLocations[w.Path]=target;Persist($"已调整 {selected.Length} 部作品的位置。");});
  right.Children.Add(new TextBlock{Text="移动文件夹（包含其作品与下级）",FontSize=15,Margin=new Thickness(0,16,0,4)});var destination=new ComboBox{DisplayMemberPath="Label",SelectedValuePath="Id",ItemsSource=new[]{new Choice("","分类根目录")}.Concat(tree.Folders.Values.Select(f=>new Choice(f.Id,tree.Label(f.Id)))).ToArray(),SelectedIndex=0};right.Children.Add(destination);
  Button("将所选文件夹移至目标位置",()=>{if(folder.SelectedValue is not string id||!tree.Folders.TryGetValue(id,out var f))return;var target=destination.SelectedValue as string??"";var p=target;while(p.Length>0){if(p==id){status.Text="不能移入自身或自己的下级。";return;}p=tree.Folders.TryGetValue(p,out var parent)?parent.Parent:"";}foreach(var w in works){var location=tree.Locations.GetValueOrDefault(w.Path)??"";var ancestor=location;while(tree.Folders.TryGetValue(ancestor,out var node)){if(ancestor==id){settings.WorkLocations[w.Path]=location;tree.Pin(settings,location);break;}ancestor=node.Parent;}}f.Parent=target;settings.Organization[id]=f.Copy();tree.Pin(settings,target);Refresh(id);Persist("文件夹及其内容的位置已保存。");});
  right.Children.Add(status);Button("完成",Close);
 }
 sealed record Choice(string Id,string Label);
}
