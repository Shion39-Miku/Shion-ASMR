using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Nocturne;
public sealed class MetadataWindow : Window
{
 public MetadataWindow(List<Work> works,Settings settings)
 {
  Title="社团与自定义标签";Width=900;Height=640;MinWidth=740;MinHeight=540;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=new SolidColorBrush(Color.FromRgb(15,21,38));Foreground=(Brush)Application.Current.FindResource("Ink");FontFamily=new FontFamily("Microsoft YaHei UI");NativeTheme.Attach(this);
  var grid=new Grid{Margin=new Thickness(24)};Content=grid;grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});grid.RowDefinitions.Add(new(){Height=GridLength.Auto});grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(340)});
  var header=new TextBlock{Text="管理社团与标签",FontSize=24,Margin=new Thickness(0,0,0,16)};grid.Children.Add(header);Grid.SetColumnSpan(header,2);
  var left=new DockPanel{Margin=new Thickness(0,0,16,0)};grid.Children.Add(left);Grid.SetRow(left,1);var search=new TextBox{ToolTip="搜索后可全选结果"};DockPanel.SetDock(search,Dock.Top);left.Children.Add(search);var select=new Button{Content="全选当前搜索结果"};DockPanel.SetDock(select,Dock.Top);left.Children.Add(select);
  var list=new ListBox{SelectionMode=SelectionMode.Extended,DisplayMemberPath="Summary",ItemsSource=works};SmoothScroll.SetEnabled(list,true);left.Children.Add(list);var selected=new TextBlock{Text="选择一个或多个作品（Ctrl / Shift 多选）",FontSize=11,Margin=new Thickness(5)};DockPanel.SetDock(selected,Dock.Bottom);left.Children.Insert(2,selected);list.SelectionChanged+=(_,_)=>selected.Text=$"已选 {list.SelectedItems.Count} 部作品";
  search.TextChanged+=(_,_)=>list.ItemsSource=works.Where(w=>w.Name.Contains(search.Text.Trim(),StringComparison.OrdinalIgnoreCase)).ToArray();select.Click+=(_,_)=>list.SelectAll();
  var right=new StackPanel();grid.Children.Add(right);Grid.SetRow(right,1);Grid.SetColumn(right,1);var status=new TextBlock{Text="修改立即写入当前设置，关闭后刷新分类。",Foreground=(Brush)Application.Current.FindResource("Muted"),FontSize=11,Margin=new Thickness(4,12,4,0)};
  string[] Selection()=>list.SelectedItems.Cast<Work>().Select(w=>w.Path).ToArray();
  void Persist(string message){foreach(var w in works)if(settings.WorkDetails.TryGetValue(w.Path,out var d)){if(d.CircleManual)w.Circle=string.IsNullOrWhiteSpace(d.Circle)?MetadataStore.Unset:d.Circle;w.Tags=d.Tags.ToArray();w.NotifyMetadata();}State.Write("settings.json",settings);status.Text=message;}
  void Button(string title,Action action){var b=new Button{Content=title,HorizontalAlignment=HorizontalAlignment.Stretch};b.Click+=(_,_)=>action();right.Children.Add(b);}
  right.Children.Add(new TextBlock{Text="批量设置社团",FontSize=18});var circle=new TextBox{MaxLength=120,ToolTip="留空表示未设置社团"};right.Children.Add(circle);
  Button("应用社团到所选作品",()=>{var chosen=Selection();if(chosen.Length==0){status.Text="请先选择作品。";return;}MetadataStore.SetCircle(settings,chosen,circle.Text);Persist($"已设置 {chosen.Length} 部作品的社团。");});
  right.Children.Add(new TextBlock{Text="自定义标签",FontSize=18,Margin=new Thickness(0,20,0,10)});var tags=new ComboBox();right.Children.Add(tags);var name=new TextBox{MaxLength=80,ToolTip="新标签名称，或重命名后的名称"};right.Children.Add(name);
  void Refresh(string? selectedTag=null){tags.ItemsSource=settings.CustomTags.OrderBy(x=>x,Natural.Instance).ToArray();tags.SelectedItem=selectedTag;if(tags.SelectedIndex<0&&tags.Items.Count>0)tags.SelectedIndex=0;}
  Refresh();Button("创建标签",()=>{var text=name.Text.Trim();if(text.Length==0)return;MetadataStore.AddTag(settings,Array.Empty<string>(),text);Refresh(text);Persist("标签已创建。");});
  Button("重命名选中标签",()=>{if(tags.SelectedItem is string old&&!string.IsNullOrWhiteSpace(name.Text)){MetadataStore.RenameTag(settings,old,name.Text);Refresh(name.Text.Trim());Persist("已重命名并更新所有关联。");}});
  Button("删除选中标签及其关联",()=>{if(tags.SelectedItem is string tag){MetadataStore.DeleteTag(settings,tag);Refresh();Persist("标签及关联已删除，作品和音轨保持原样。");}});
  Button("给所选作品添加标签",()=>{if(tags.SelectedItem is string tag){var chosen=Selection();MetadataStore.AddTag(settings,chosen,tag);Persist($"已为 {chosen.Length} 部作品添加标签。");}});
  Button("从所选作品移除标签",()=>{if(tags.SelectedItem is string tag){var chosen=Selection();MetadataStore.RemoveTag(settings,chosen,tag);Persist($"已从 {chosen.Length} 部作品移除标签。");}});right.Children.Add(status);
  var done=new Button{Content="完成",HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,14,0,0)};grid.Children.Add(done);Grid.SetRow(done,2);Grid.SetColumnSpan(done,2);done.Click+=(_,_)=>Close();
 }
}
