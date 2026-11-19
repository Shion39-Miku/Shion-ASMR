using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Nocturne;
public partial class MainWindow
{
 internal Organization organization=new();internal string browserFolder="";bool globalBrowse=true;int treeRequest;
 internal async Task RefreshOrganization()
 {
  var request=++treeRequest;var tree=organization;var expanded=settings.ExpandedFolders.ToHashSet(StringComparer.OrdinalIgnoreCase);if(showFolderTree){var catalog=works.ToArray();var rows=await Task.Run(()=>tree.Visible(expanded).Where(row=>row.Track!=null?ModeFile(row.Track):row.Work!=null?ModeWork(row.Work):catalog.Any(w=>ModeWork(w)&&InSeries(w,row.Id,tree))).ToList());if(request!=treeRequest||closing)return;FolderTree.ItemsSource=rows;}if(request!=treeRequest||closing)return;
  Breadcrumb.Items.Clear();var chain=new List<VirtualFolder>();var id=browserFolder;while(tree.Folders.TryGetValue(id,out var f)){chain.Add(f);id=f.Parent;}chain.Reverse();
  void Crumb(string label,string target){var b=new Button{Content=new TextBlock{Text=label,TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis},Tag=target,Padding=new Thickness(10,6,10,6),MinHeight=30,MaxWidth=190,ToolTip=label};b.Click+=async(_,_)=>await EnterFolder(target);Breadcrumb.Items.Add(b);}
  Crumb(globalBrowse?"全部作品":"社团","");foreach(var f in chain)Crumb(f.Name,f.Id);
 }
 internal Task EnterFolder(string id)=>HomeReady=EnterFolderCore(id);
 async Task EnterFolderCore(string id,bool remember=true)
 {
  if(id.Length>0&&!organization.Folders.ContainsKey(id))return;if(remember&&id!=browserFolder)RememberBrowse();var request=++browseSelectionRequest;++homeRequest;browserFolder=id;folderBrowsing=true;globalBrowse=false;viewMode="works";circleFilter=null;
  var top=id;while(organization.Folders.TryGetValue(top,out var parent)){circleFilter=parent.Name;if(parent.Parent.Length==0)break;top=parent.Parent;}
  UpdateBrowserMode();
  var ancestor=id;while(organization.Folders.TryGetValue(ancestor,out var f)){if(!settings.ExpandedFolders.Contains(ancestor))settings.ExpandedFolders.Add(ancestor);ancestor=f.Parent;}QueueSave();
  if(CurrentPage!="home")Navigate("home");await RefreshOrganization();if(request!=browseSelectionRequest||closing)return;await RenderHome();
 }
 async void ParentFolder(object sender,RoutedEventArgs e)
 {
  if(organization.Folders.TryGetValue(browserFolder,out var current))
  {
   if(browseHistory.Count>0&&browseHistory[^1].Location==current.Parent)await ReturnBrowse();
   else await (HomeReady=EnterFolderCore(current.Parent,remember:false));
  }
  else if(browseHistory.Count>0)await ReturnBrowse();
  else await EnterFolder("");
 }
 async void ToggleFolder(object sender,RoutedEventArgs e)
 {
  e.Handled=true;if(sender is not Button {Tag:TreeEntry row}||!row.Expandable)return;
  if(!settings.ExpandedFolders.Remove(row.Id))settings.ExpandedFolders.Add(row.Id);QueueSave();await RefreshOrganization();
 }
 async void TreeOpen(object sender,RoutedEventArgs e)
 {
  if(sender is not Button {Tag:TreeEntry row})return;
  if(row.Track!=null)OpenTrack(row.Track,true);else if(row.Work!=null)SelectWork(row.Work);else await EnterFolder(row.Id);
 }
 void ShowOrganizationMenu(object sender,RoutedEventArgs e){if(sender is Button b&&b.ContextMenu!=null){b.ContextMenu.PlacementTarget=b;b.ContextMenu.IsOpen=true;}}
 async void EditOrganization(object sender,RoutedEventArgs e)
 {
  new OrganizationWindow(organization,works,settings,browserFolder){Owner=this}.ShowDialog();Save();await BuildTree();
 }
}
