using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace Nocturne;
public partial class MainWindow
{
 async void AddRoot(object sender,RoutedEventArgs e){var d=new OpenFolderDialog{Title="选择媒体文件夹",Multiselect=true};if(d.ShowDialog(this)!=true)return;await ImportFolders(d.FolderNames);}
 internal async Task ImportFolders(IEnumerable<string> roots)
 {
  foreach(var root in roots){if(settings.Roots.Any(x=>WorkCatalog.Within(root,x)))continue;settings.Roots.RemoveAll(x=>WorkCatalog.Within(x,root));settings.Roots.Add(root);}
  await Scan(false);library.Watch(settings.Roots);Save();if(!lastScanSucceeded)return;
  ImportResult.Text=$"导入完成 · {settings.Roots.Count} 个媒体目录 · {works.Count} 部作品";Status.Text=ImportResult.Text;
 }
 bool lastScanSucceeded;
 readonly System.Threading.SemaphoreSlim scanGate=new(1,1);
 internal async Task<bool> Scan(bool force)
 {
  try{await scanGate.WaitAsync(cancellation.Token);}catch(OperationCanceledException){return false;}
  try{lastScanSucceeded=false;scanRunning=true;Status.Text="正在扫描本地目录…";var changed=await library.Scan(settings.Roots.ToArray(),new Progress<string>(s=>Status.Text=s),cancellation.Token,force);if(closing)return false;if(changed||force){await BuildTree();if(library.Folders.ContainsKey(folder)){PopulateTracks();UpdatePictures();}}Status.Text=$"{works.Count} 部作品 · {library.Folders.Values.Sum(x=>x.Audio.Count)} 个媒体文件 · 本次读取 {library.FilesEnumerated} 个文件";lastScanSucceeded=true;return changed||force;}
  catch(OperationCanceledException){}catch(Exception ex){Status.Text="扫描失败："+ex.Message;}
  finally{scanRunning=false;FeedbackChanged(this,EventArgs.Empty);scanGate.Release();}
  return false;
 }
 internal Task CatalogReady {get;private set;}=Task.CompletedTask;internal Task HomeReady {get;private set;}=Task.CompletedTask;
 int catalogRequest,homeRequest,catalogRevision;string? lastTrackCandidate,lastHomeSignature;string viewMode="works";bool filterLoading;
 string? homeNavigationKey;readonly Dictionary<string,(double offset,int columns)> homeScrollPositions=new();
 static ScrollViewer? BrowserScroll(DependencyObject element){for(int i=0;i<VisualTreeHelper.GetChildrenCount(element);i++){var child=VisualTreeHelper.GetChild(element,i);if(child is ScrollViewer scroll)return scroll;var found=BrowserScroll(child);if(found!=null)return found;}return null;}
 internal string? circleFilter,tagFilter;internal int HomeWorkerThread {get;private set;}internal int ShownWorks {get;private set;}internal long HomeRebuilds {get;private set;}
 public sealed record Facet(string Kind,string Name,int Count){public string Label=>$"{Name}  ·  {Count} 部作品";}
 internal Task BuildTree()=>CatalogReady=BuildCatalog();
 async Task BuildCatalog()
 {
  var request=++catalogRequest;var snapshot=MetadataStore.Snapshot(settings);var folders=library.Folders;
  var built=await Task.Run(()=>
  {
   var catalog=WorkCatalog.Build(folders,snapshot);
   bool CanResume(string path)=>snapshot.Roots.Any(root=>WorkCatalog.Within(path,root))&&Path.GetDirectoryName(path) is string directory&&folders.TryGetValue(directory,out var indexed)&&indexed.Audio.Contains(path,StringComparer.OrdinalIgnoreCase)&&File.Exists(path);
   var resume=snapshot.Tracks.Where(p=>p.Value.LastPlayed!=null).OrderByDescending(p=>p.Value.LastPlayed).Select(p=>p.Key).Concat(snapshot.Resume.Values.Select(r=>r.Track)).FirstOrDefault(CanResume);
   return(catalog,resume,tree:Organization.Build(catalog,snapshot),snapshot);
  });
  if(closing||request!=catalogRequest)return;foreach(var p in built.snapshot.WorkDetails)settings.WorkDetails.TryAdd(p.Key,p.Value);foreach(var p in built.snapshot.WorkCovers)settings.WorkCovers.TryAdd(p.Key,p.Value);foreach(var p in built.snapshot.WorkLocations)settings.WorkLocations.TryAdd(p.Key,p.Value);works=built.catalog;organization=built.tree;if(browserFolder.Length>0&&!organization.Folders.ContainsKey(browserFolder))browserFolder="";await RefreshOrganization();if(closing||request!=catalogRequest)return;catalogRevision++;lastTrackCandidate=built.resume;RefreshFilterChoices();await RenderHome();
  if(currentWork!=null){currentWork=works.FirstOrDefault(x=>x.Path==currentWork.Path);if(currentWork!=null)PopulateGroups();}
 }
 static bool MatchesBrowseQuery(Work work,string query,Organization tree)
 {
  if(work.SearchText.Contains(query,StringComparison.OrdinalIgnoreCase))return true;
  var id=tree.Locations.GetValueOrDefault(work.Path)??"";var visited=new HashSet<string>();while(visited.Add(id)&&tree.Folders.TryGetValue(id,out var f)){if(f.Name.Contains(query,StringComparison.OrdinalIgnoreCase))return true;id=f.Parent;}return false;
 }
 internal int Columns()=>Math.Clamp((int)Math.Floor(Math.Max(1,WorksList?.ActualWidth-18??ActualWidth-260)/240),1,6);
 void WorksViewportChanged(object sender,SizeChangedEventArgs e){if(ready&&Columns()!=homeColumns)_=RenderHome();}
 void SearchChanged(object sender,TextChangedEventArgs e){if(!ready)return;searchTimer.Stop();searchTimer.Start();}
 internal Task RenderHome()=>HomeReady=FilterHome();
 async Task FilterHome()
 {
  if(WorksList==null)return;var request=++homeRequest;var columns=Columns();var q=Search.Text.Trim();var circle=circleFilter;var tag=tagFilter;var mode=viewMode;var snapshot=works;var tree=organization;var location=browserFolder;var global=globalBrowse;var video=videoBrowse;
  var signature=string.Join("\0",catalogRevision.ToString(),columns.ToString(),q,circle,tag,mode,location,global.ToString(),video.ToString());if(signature==lastHomeSignature)return;
  var result=await Task.Run(()=>
  {
   HomeWorkerThread=Environment.CurrentManagedThreadId;
   var hasModeWorks=false;
   var shown=new List<Work>();var local=new List<Work>();var covers=new Dictionary<string,string?>(StringComparer.OrdinalIgnoreCase);
   foreach(var work in snapshot)
   {
    if(!ModeWork(work,video))continue;
    hasModeWorks=true;
    if((q.Length>0&&!MatchesBrowseQuery(work,q,tree))||(global&&circle!=null&&!work.Circle.Equals(circle,StringComparison.OrdinalIgnoreCase))||(tag!=null&&!work.Tags.Contains(tag,StringComparer.OrdinalIgnoreCase)))continue;
    if(global){shown.Add(work);local.Add(work);continue;}
    var parent=tree.Locations.GetValueOrDefault(work.Path)??"";
    if(parent==location){shown.Add(work);local.Add(work);continue;}
    while(tree.Folders.TryGetValue(parent,out var current))
    {
     if(current.Parent==location)
     {
      shown.Add(work);
      if(!covers.TryGetValue(parent,out var cover)||cover==null)covers[parent]=work.Cover;
      break;
     }
     parent=current.Parent;
    }
   }
   var children=global?Array.Empty<VirtualFolder>():tree.ChildFolders(location).Where(f=>covers.ContainsKey(f.Id)).ToArray();
   var tiles=children.Select(f=>new BrowserTile(null,f){FolderCover=covers[f.Id]}).Concat(local.Select(w=>new BrowserTile(w,null)));
   var rows=tiles.Chunk(columns).Select(x=>new HomeRow(x.ToList(),columns)).ToArray();
   var facets=mode=="works"?Array.Empty<Facet>():(mode=="circles"?shown.GroupBy(w=>w.Circle).Select(g=>new Facet("circles",g.Key,g.Count())):shown.SelectMany(w=>w.Tags).GroupBy(t=>t).Select(g=>new Facet("tags",g.Key,g.Count()))).OrderBy(f=>f.Name,Natural.Instance).ToArray();
   return(count:shown.Count,localCount:local.Count,rows,facets,children,hasModeWorks);
  });
  if(closing||request!=homeRequest||location!=browserFolder||global!=globalBrowse||tree!=organization)return;
  var navKey=string.Join("\0",q,circle,tag,mode,location,global.ToString(),videoBrowse.ToString());var viewer=BrowserScroll(WorksList);
  if(homeNavigationKey!=null&&viewer!=null)homeScrollPositions[homeNavigationKey]=(viewer.VerticalOffset,Math.Max(1,homeColumns));
  while(homeScrollPositions.Count>64)homeScrollPositions.Remove(homeScrollPositions.Keys.First());
  var restore=homeScrollPositions.GetValueOrDefault(navKey);homeNavigationKey=navKey;lastHomeSignature=signature;homeColumns=columns;ShownWorks=result.count;HomeRebuilds++;
  // No item-source mutation occurs during wheel scrolling or background animation.
  SmoothScroll.Stop(WorksList);WorksList.ItemsSource=result.rows;FacetsList.ItemsSource=result.facets;SmoothScroll.ToTop(FacetsList);WorksList.Visibility=mode=="works"?Visibility.Visible:Visibility.Collapsed;FacetsList.Visibility=mode=="works"?Visibility.Collapsed:Visibility.Visible;
  _=Dispatcher.BeginInvoke(()=>{if(!closing&&request==homeRequest)BrowserScroll(WorksList)?.ScrollToVerticalOffset(restore.offset*Math.Max(1,restore.columns)/columns);},System.Windows.Threading.DispatcherPriority.Loaded);
  EmptyLibrary.Visibility=(mode=="works"?result.rows.Length:result.facets.Length)==0?Visibility.Visible:Visibility.Collapsed;
  EmptyLibrary.Text=video&&!result.hasModeWorks?"这里还没有视频\n\n添加包含视频的文件夹后，即可在这里查看。":settings.Roots.Count==0?"从第一段声音开始\n\n添加本地文件夹，整理属于你的音声收藏。":"没有匹配的作品或分类。\n试试清除筛选条件，或换个关键词。";
  HomeTitle.Text=mode=="tags"?"按标签浏览":global?(circle??"全部作品"):browserFolder.Length==0?"分类文件夹":tree.Folders.GetValueOrDefault(browserFolder)?.Name??"分类文件夹";HomeTitle.ToolTip=HomeTitle.Text;CommitBrowserSelection();
  LibraryInfo.Text=$"{works.Count} 部作品  ·  "+(global?$"筛选 {result.count} 部":$"此处 {result.children.Length} 个文件夹 / {result.localCount} 部作品")+"  ·  仅存储在本机";ImportResult.Text=works.Count==0?"还没有媒体，先点“添加音频”开始。":$"本地收藏 · {works.Count} 部作品";ContinueButton.Visibility=lastTrackCandidate!=null?Visibility.Visible:Visibility.Collapsed;
  CurrentFilters.Text="当前筛选："+(circle==null?"全部社团":circle)+" / "+(tag==null?"全部标签":tag)+(q.Length==0?"":" / 搜索“"+q+"”");
 }
 void RefreshFilterChoices()
 {
  filterLoading=true;CircleFilter.ItemsSource=new[]{"全部社团"}.Concat(works.Select(w=>w.Circle).Distinct().OrderBy(x=>x,Natural.Instance)).ToArray();TagFilter.ItemsSource=new[]{"全部标签"}.Concat(settings.CustomTags.OrderBy(x=>x,Natural.Instance)).ToArray();
  CircleFilter.SelectedItem=circleFilter??"全部社团";TagFilter.SelectedItem=tagFilter??"全部标签";if(CircleFilter.SelectedIndex<0){if(globalBrowse)circleFilter=null;CircleFilter.SelectedIndex=0;}if(TagFilter.SelectedIndex<0){tagFilter=null;TagFilter.SelectedIndex=0;}filterLoading=false;RefreshBrowserChoices();
 }
 async void BrowseWorks(object sender,RoutedEventArgs e){ShowFolderNavigation();await EnterFolder(browserFolder);}
 async void BrowseCircles(object sender,RoutedEventArgs e){circleFilter=tagFilter=null;Search.Text="";RefreshFilterChoices();await EnterFolder("");}
 async void BrowseTags(object sender,RoutedEventArgs e){viewMode="tags";tagFilter=null;RefreshFilterChoices();await RenderHome();}
 async void FilterChanged(object sender,SelectionChangedEventArgs e){if(!ready||filterLoading)return;if(globalBrowse)circleFilter=CircleFilter.SelectedIndex<=0?null:CircleFilter.SelectedItem as string;tagFilter=TagFilter.SelectedIndex<=0?null:TagFilter.SelectedItem as string;RefreshBrowserChoices();viewMode="works";await RenderHome();}
 internal async Task FilterForTest(string? circle,string? tag,string search=""){circleFilter=circle;tagFilter=tag;folderBrowsing=false;browserFolder="";UpdateBrowserMode();globalBrowse=true;viewMode="works";RefreshFilterChoices();Search.Text=search;searchTimer.Stop();await RefreshOrganization();await RenderHome();}
 async void ClearFilters(object sender,RoutedEventArgs e){circleFilter=tagFilter=null;folderBrowsing=false;browserFolder="";UpdateBrowserMode();Search.Text="";searchTimer.Stop();globalBrowse=true;RefreshFilterChoices();viewMode="works";await RefreshOrganization();await RenderHome();}
 async void SelectFacet(object sender,RoutedEventArgs e){if(sender is Button{Tag:Facet f}){if(f.Kind=="circles")circleFilter=f.Name;else tagFilter=f.Name;viewMode="works";RefreshFilterChoices();await RenderHome();}}
 async void EditMetadata(object sender,RoutedEventArgs e){new MetadataWindow(works,settings){Owner=this}.ShowDialog();Save();await BuildTree();}
 void TrackClicked(object sender,MouseButtonEventArgs e)
 {
  var element=e.OriginalSource as DependencyObject;while(element!=null&&element is not ListBoxItem)element=System.Windows.Media.VisualTreeHelper.GetParent(element);
  if(element is not ListBoxItem{DataContext:FileItem file})return;
  if(currentTrack==file.Path){if(!PlaybackPlaying)RequestPlayback(true);Navigate("lyrics");UpdateSubtitle(true);}else OpenTrack(file.Path,true);
 }
 async void OpenWork(object sender,RoutedEventArgs e){if(sender is Button{Tag:BrowserTile tile}){if(tile.Work!=null)SelectWork(tile.Work);else if(tile.Folder!=null)await EnterFolder(tile.Folder.Id);}}
 internal void SelectWork(Work work)
 {
  RememberBrowse();currentWork=work;PopulateGroups();var last=work.Groups.SelectMany(g=>g.Audio).Where(ModeFile).Where(settings.Tracks.ContainsKey).OrderByDescending(x=>settings.Tracks[x].LastPlayed).FirstOrDefault();
  var group=last==null?work.Groups.FirstOrDefault(g=>g.Audio.Any(ModeFile)):work.Groups.FirstOrDefault(g=>g.Audio.Contains(last));if(group!=null)SelectFolder(group.Path);Navigate("detail");
 }
 void PopulateGroups(){if(currentWork==null)return;selectingGroup=true;string Label(Folder g)=>g.Path.Equals(currentWork.Path,StringComparison.OrdinalIgnoreCase)?"主音轨":WorkCatalog.Within(g.Path,currentWork.Path)?Path.GetRelativePath(currentWork.Path,g.Path).Replace('\\','／'):Path.GetFileName(Path.GetDirectoryName(g.Path))+"／"+g.Name;GroupList.ItemsSource=currentWork.Groups.Where(g=>g.Audio.Any(ModeFile)).Select(g=>new GroupItem(g,Label(g))).ToList();GroupList.SelectedItem=GroupList.Items.Cast<GroupItem>().FirstOrDefault(x=>x.Folder.Path==folder);selectingGroup=false;WorkTitle.Text=currentWork.Name;WorkTitle.ToolTip=currentWork.Name;WorkPath.Text=currentWork.Info+"  ·  "+currentWork.Classification;}
 void GroupSelected(object sender,SelectionChangedEventArgs e){if(!selectingGroup&&GroupList.SelectedItem is GroupItem group){folder=group.Folder.Path;PopulateTracks();UpdatePictures();}}
 internal void SelectFolder(string path)
 {
  var owner=works.FirstOrDefault(w=>w.Groups.Any(g=>g.Path.Equals(path,StringComparison.OrdinalIgnoreCase)))??works.FirstOrDefault(w=>w.Path.Equals(path,StringComparison.OrdinalIgnoreCase));
  if(owner==null){currentWork=null;folder=path;Tracks.ItemsSource=null;GroupList.ItemsSource=null;WorkTitle.Text=Path.GetFileName(path);WorkPath.Text=path;UpdatePictures();return;}currentWork=owner;folder=owner.Groups.Any(g=>g.Path==path)?path:owner.Groups[0].Path;PopulateGroups();PopulateTracks();UpdatePictures();if(CurrentPage!="lyrics")Navigate("detail");
 }
 void PopulateTracks()
 {
  if(!library.Folders.TryGetValue(folder,out var f))return;var q=Search.Text.Trim();bool all=q.Length==0||(currentWork!=null&&MatchesBrowseQuery(currentWork,q,organization))||(currentWork?.Name.Contains(q,StringComparison.OrdinalIgnoreCase)??false)||folder.Contains(q,StringComparison.OrdinalIgnoreCase);
  Tracks.ItemsSource=f.Audio.Where(ModeFile).Where(x=>all||Path.GetFileName(x).Contains(q,StringComparison.OrdinalIgnoreCase)).Select(x=>new FileItem(x,settings.Tracks.TryGetValue(x,out var p)?p.Position??0:0)).ToList();
  var selected=Tracks.Items.Cast<FileItem>().FirstOrDefault(x=>x.Path==currentTrack);
  if(selected==null&&settings.Resume.TryGetValue(folder,out var resume))selected=Tracks.Items.Cast<FileItem>().FirstOrDefault(x=>x.Path==resume.Track);Tracks.SelectedItem=selected;if(Tracks.SelectedItem==null&&Tracks.Items.Count>0)Tracks.SelectedIndex=0;
 }
 void TrackActivated(object sender,MouseButtonEventArgs e){if(Tracks.SelectedItem is FileItem file&&(currentTrack!=file.Path||!PlaybackPlaying))OpenTrack(file.Path,true);}
 void TrackKey(object sender,KeyEventArgs e){if(e.Key==Key.Enter&&Tracks.SelectedItem is FileItem file){OpenTrack(file.Path,true);e.Handled=true;}}
 internal string? ContinueTrack()=>lastTrackCandidate;
 void ContinueListening(object sender,RoutedEventArgs e){var path=ContinueTrack();if(path!=null)OpenTrack(path,true);else Status.Text="请选择作品中的一条音轨。";}
 void ManageRoots(object sender,RoutedEventArgs e)
 {
  var dialog=DialogShell.Create("媒体目录",620,420);var panel=new StackPanel{Margin=new Thickness(24)};dialog.Content=panel;panel.Children.Add(new TextBlock{Text="管理本地媒体目录",FontSize=22,Margin=new Thickness(0,0,0,18)});
  var list=new ListBox{ItemsSource=settings.Roots.ToList(),Height=180};panel.Children.Add(list);var buttons=new WrapPanel();panel.Children.Add(buttons);
  var refresh=new Button{Content="刷新目录"};refresh.Click+=async(_,_)=>{dialog.Close();await Scan(true);};buttons.Children.Add(refresh);
  var remove=new Button{Content="移除选中目录"};remove.Click+=async(_,_)=>{if(list.SelectedItem is string root){var removed=RemoveMediaRoot(root);list.ItemsSource=settings.Roots.ToList();await removed;}};buttons.Children.Add(remove);
  var done=new Button{Content="完成"};done.Click+=(_,_)=>dialog.Close();buttons.Children.Add(done);dialog.Owner=this;dialog.ShowDialog();
 }
 internal async Task RemoveMediaRoot(string root)
 {
  settings.Roots.RemoveAll(x=>x.Equals(root,StringComparison.OrdinalIgnoreCase));library.Watch(settings.Roots);
  if(lastTrackCandidate!=null&&WorkCatalog.Within(lastTrackCandidate,root)){lastTrackCandidate=null;ContinueButton.Visibility=Visibility.Collapsed;}
  if(WorkCatalog.Within(folder,root)){folder="";currentWork=null;Tracks.ItemsSource=null;pictures.Clear();Picture.Source=PicturePrevious.Source=Backdrop.Source=null;NoPicture.Visibility=Visibility.Visible;Navigate("home");}
  Save();var refreshed=await Scan(false);
  if(!closing&&!refreshed)await BuildTree();
 }
 async void EditWork(object sender,RoutedEventArgs e){if(currentWork==null)return;var window=new WorkEditor(currentWork,works,library,settings){Owner=this};if(window.ShowDialog()==true){var oldFolder=folder;await BuildTree();SelectFolder(oldFolder);Save();}}
 internal async Task AssignWork(IEnumerable<string> groups,string owner,string name){foreach(var g in groups)settings.WorkAssignments[g]=owner;settings.WorkNames[owner]=name;await BuildTree();Save();}
}
