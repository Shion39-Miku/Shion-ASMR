using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;

namespace Nocturne;
public sealed class VirtualFolder
{
 public string Id {get;set;}="";public string Parent {get;set;}="";public string Name {get;set;}="";
 public VirtualFolder Copy()=>new(){Id=Id,Parent=Parent,Name=Name};
}
public sealed class BrowserTile : System.ComponentModel.INotifyPropertyChanged
{
 public Work? Work {get;}public VirtualFolder? Folder {get;}
 public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
 public BrowserTile(Work? work,VirtualFolder? folder){Work=work;Folder=folder;if(work!=null)System.ComponentModel.PropertyChangedEventManager.AddHandler(work,Changed,"");}
 void Changed(object? sender,System.ComponentModel.PropertyChangedEventArgs e){PropertyChanged?.Invoke(this,new(nameof(Cover)));PropertyChanged?.Invoke(this,new(nameof(HomeInfo)));}
 public string Name=>Work?.Name??Folder!.Name;public string? Cover=>Work?.Cover??FolderCover;public string? FolderCover {get;init;}
 public string HomeInfo=>Work?.HomeInfo??(Folder!.Parent.Length==0?"社团文件夹":"系列文件夹")+" · 点击进入";
 public Visibility FolderVisibility=>Folder==null?Visibility.Collapsed:Visibility.Visible;
 public Visibility CoverVisibility=>Folder==null?Visibility.Visible:Visibility.Collapsed;
}
public sealed record TreeEntry(string Id,string Name,int Depth,bool Expanded,bool Expandable,Work? Work=null,string? Track=null)
{
 public Thickness Indent=>new(Depth*16,0,0,0);
 public string Arrow=>Expandable?(Expanded?"⌄":"›"):"·";
 public string Icon=>Track!=null?"Play":Work!=null?"Cover":"Folder";
}
public sealed class Organization
{
 public Dictionary<string,VirtualFolder> Folders {get;}=new(StringComparer.OrdinalIgnoreCase);
 public Dictionary<string,string> Locations {get;}=new(StringComparer.OrdinalIgnoreCase);
 public Dictionary<string,VirtualFolder[]> Children {get;private set;}=new(StringComparer.OrdinalIgnoreCase);
 public Dictionary<string,Work[]> Works {get;private set;}=new(StringComparer.OrdinalIgnoreCase);
 public static string CircleId(string name)=>"circle:"+name.ToUpperInvariant();
 public static Organization Build(IEnumerable<Work> works,Settings settings)
 {
  var tree=new Organization();var all=works.ToArray();
  foreach(var w in all)
  {
   var circle=CircleId(w.Circle);tree.Folders.TryAdd(circle,new(){Id=circle,Name=w.Circle});var location=circle;
   // Only a positively identified circle directory, or a parent matching reliable circle metadata,
   // establishes series ancestry. Uncertain ordinary folders remain a work, without invented metadata.
   var ancestors=new List<DirectoryInfo>();var parent=Directory.GetParent(w.Path);var root=w.Groups.FirstOrDefault()?.Root;
   while(parent!=null&&root!=null&&WorkCatalog.Within(parent.FullName,root))
   {
    if(parent.Name.Equals(w.Circle,StringComparison.OrdinalIgnoreCase)&&w.Circle!=MetadataStore.Unset)
    {
     foreach(var series in ancestors.AsEnumerable().Reverse()){var id="directory:"+series.FullName.ToUpperInvariant();tree.Folders.TryAdd(id,new(){Id=id,Parent=location,Name=settings.WorkNames.GetValueOrDefault(series.FullName)??series.Name});location=id;}break;
    }
    if(parent.FullName.Equals(root,StringComparison.OrdinalIgnoreCase))break;ancestors.Add(parent);parent=parent.Parent;
   }
   tree.Locations[w.Path]=location;
  }
  foreach(var f in settings.Organization.Values.Where(f=>!string.IsNullOrWhiteSpace(f.Id)))tree.Folders[f.Id]=f.Copy();
  foreach(var f in tree.Folders.Values)
  {
   var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase){f.Id};var p=f.Parent;
   while(p.Length>0){if(!visited.Add(p)||!tree.Folders.TryGetValue(p,out var up)){f.Parent="";break;}p=up.Parent;}
  }
  foreach(var w in all)if(settings.WorkLocations.TryGetValue(w.Path,out var saved)&&(saved.Length==0||tree.Folders.ContainsKey(saved)))tree.Locations[w.Path]=saved;
  var used=new HashSet<string>(tree.Locations.Values,StringComparer.OrdinalIgnoreCase);
  foreach(var id in used.Concat(settings.Organization.Keys).ToArray()){var p=id;while(tree.Folders.TryGetValue(p,out var f)){used.Add(p);p=f.Parent;}}
  foreach(var id in tree.Folders.Keys.Where(id=>!used.Contains(id)).ToArray())tree.Folders.Remove(id);
  tree.Children=tree.Folders.Values.GroupBy(f=>f.Parent,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.OrderBy(f=>f.Name,Natural.Instance).ToArray(),StringComparer.OrdinalIgnoreCase);
  tree.Works=all.GroupBy(w=>tree.Locations[w.Path],StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.OrdinalIgnoreCase);return tree;
 }
 public VirtualFolder[] ChildFolders(string id)=>Children.GetValueOrDefault(id)??Array.Empty<VirtualFolder>();
 public Work[] ChildWorks(string id)=>Works.GetValueOrDefault(id)??Array.Empty<Work>();
 public List<TreeEntry> Visible(HashSet<string> expanded)
 {
  var rows=new List<TreeEntry>();void Visit(string id,int depth)
  {
   foreach(var f in ChildFolders(id)){var open=expanded.Contains(f.Id);rows.Add(new(f.Id,f.Name,depth,open,ChildFolders(f.Id).Length+ChildWorks(f.Id).Length>0));if(open)Visit(f.Id,depth+1);}
   foreach(var w in ChildWorks(id)){var wid="work:"+w.Path;var open=expanded.Contains(wid);rows.Add(new(wid,w.Name,depth,open,true,w));if(open)foreach(var track in w.Groups.SelectMany(g=>g.Audio))rows.Add(new("track:"+track,Path.GetFileName(track),depth+1,false,false,w,track));}
  }Visit("",0);return rows;
 }
 public string Label(string id){var names=new List<string>();while(Folders.TryGetValue(id,out var f)){names.Add(f.Name);id=f.Parent;}names.Reverse();return "分类 / "+string.Join(" / ",names);}
 public void Pin(Settings settings,string id)
 {
  while(Folders.TryGetValue(id,out var f)){settings.Organization[id]=f.Copy();id=f.Parent;}
 }
}
