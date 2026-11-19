using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
namespace Nocturne;
public partial class MainWindow
{
 public sealed record CircleOption(string? Name,int Count,string? FolderId=null){public string Id=>FolderId??"all";public string Label=>Name??"全部作品";}
 bool folderBrowsing,showFolderTree;
 CircleOption[] circleOptions=Array.Empty<CircleOption>();int circleScrollRequest,browseSelectionRequest;string selectedCircleId="all";
 void CircleSearchChanged(object sender,TextChangedEventArgs e){if(ready)ApplyCircleSearch();}
 void ApplyCircleSearch()
 {
  var wasLoading=filterLoading;filterLoading=true;
  var query=CircleSearch.Text.Trim();var filtered=circleOptions.Where(c=>c.Name==null||query.Length==0||c.Label.Contains(query,StringComparison.OrdinalIgnoreCase)).ToArray();
  if(!CircleChoices.Items.Cast<CircleOption>().SequenceEqual(filtered))CircleChoices.ItemsSource=filtered;
  var chosen=filtered.FirstOrDefault(c=>c.Id==selectedCircleId);if(!ReferenceEquals(CircleChoices.SelectedItem,chosen))CircleChoices.SelectedItem=chosen;
  var scrollRequest=++circleScrollRequest;if(CircleChoices.SelectedItem is object selected)
  {
   Dispatcher.BeginInvoke(()=>{if(!closing&&scrollRequest==circleScrollRequest&&ReferenceEquals(CircleChoices.SelectedItem,selected)){var row=CircleChoices.ItemContainerGenerator.ContainerFromItem(selected) as FrameworkElement;var viewport=BrowserScroll(CircleChoices);if(row==null){CircleChoices.ScrollIntoView(selected);return;}if(viewport!=null){var bounds=row.TransformToAncestor(viewport).TransformBounds(new Rect(0,0,row.ActualWidth,row.ActualHeight));if(bounds.Top<0||bounds.Bottom>viewport.ViewportHeight)CircleChoices.ScrollIntoView(selected);}}},System.Windows.Threading.DispatcherPriority.ContextIdle);
  }
  filterLoading=wasLoading;
 }
 void CommitBrowserSelection()
 {
  var root=browserFolder;while(organization.Folders.TryGetValue(root,out var parent)&&parent.Parent.Length>0)root=parent.Parent;
  selectedCircleId=globalBrowse?"all":root;ApplyCircleSearch();
 }
 async void CircleMouseDown(object sender,System.Windows.Input.MouseButtonEventArgs e)
 {
  if(e.ChangedButton!=System.Windows.Input.MouseButton.Left)return;
  var node=e.OriginalSource as DependencyObject;while(node!=null&&node is not ListBoxItem){if(node is System.Windows.Controls.Primitives.ScrollBar or System.Windows.Controls.Primitives.Thumb)return;node=System.Windows.Media.VisualTreeHelper.GetParent(node);}
  if(node is not ListBoxItem{DataContext:CircleOption option})return;
  // Capture identity before selection/layout can recycle or move the clicked row.
  var id=option.Id;e.Handled=true;await SelectCircleId(id);
 }
 internal System.Threading.Tasks.Task SelectCircleId(string id)=>id=="all"?(HomeReady=ShowAllWorks()):EnterFolder(id);
 internal void ShowFolderNavigation(){showFolderTree=true;UpdateBrowserMode();}
 bool InSeries(Work work,string? series,Organization tree)
 {
  if(series==null)return true;var id=tree.Locations.GetValueOrDefault(work.Path)??"";while(tree.Folders.TryGetValue(id,out var f)){if(id==series)return true;id=f.Parent;}return false;
 }
 void RefreshBrowserChoices()
 {
  filterLoading=true;
  var root=browserFolder;while(organization.Folders.TryGetValue(root,out var parent)&&parent.Parent.Length>0)root=parent.Parent;
  var counts=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);foreach(var work in works.Where(ModeWork)){var location=organization.Locations.GetValueOrDefault(work.Path)??"";while(organization.Folders.TryGetValue(location,out var f)&&f.Parent.Length>0)location=f.Parent;counts[location]=counts.GetValueOrDefault(location)+1;}
  var old=circleOptions.ToDictionary(c=>c.Id);circleOptions=new[]{new CircleOption(null,works.Count(ModeWork))}.Concat(organization.ChildFolders("").Where(f=>counts.GetValueOrDefault(f.Id)>0).Select(f=>new CircleOption(f.Name,counts.GetValueOrDefault(f.Id),f.Id))).Select(c=>old.TryGetValue(c.Id,out var existing)&&existing==c?existing:c).ToArray();ApplyCircleSearch();
  filterLoading=false;
 }
 async void CircleSelected(object sender,SelectionChangedEventArgs e)
 {
  if(!ready||filterLoading||CircleChoices.SelectedItem is not CircleOption circle||circle.Id==selectedCircleId)return;
  await SelectCircleId(circle.Id);
 }
 async System.Threading.Tasks.Task ShowAllWorks()
 {
  var request=++browseSelectionRequest;++homeRequest;folderBrowsing=false;globalBrowse=true;viewMode="works";browserFolder="";circleFilter=null;UpdateBrowserMode();await RefreshOrganization();if(request!=browseSelectionRequest)return;await RenderHome();
 }
 void ToggleTagFilters(object sender,RoutedEventArgs e){TagFiltersPanel.Visibility=TagFiltersPanel.Visibility==Visibility.Visible?Visibility.Collapsed:Visibility.Visible;TagFilterButton.Background=(System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(TagFiltersPanel.Visibility==Visibility.Visible?"#A05F5076":"#30313E58")!;}
 void UpdateBrowserMode()
 {
  var tree=folderBrowsing&&showFolderTree;FolderBreadcrumbBar.Visibility=Visibility.Visible;FolderTree.Visibility=tree?Visibility.Visible:Visibility.Collapsed;
  CircleChoices.VerticalAlignment=tree?VerticalAlignment.Top:VerticalAlignment.Stretch;CircleChoices.Height=tree?170:double.NaN;FolderTree.Margin=new Thickness(0,tree?178:0,0,0);
 }
}
