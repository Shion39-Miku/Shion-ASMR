using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Nocturne;
public sealed class Work : System.ComponentModel.INotifyPropertyChanged
{
 public string Path {get;init;}=""; public string Name {get;set;}="";public List<Folder> Groups {get;}=new();public List<string> Images {get;set;}=new();
 public int TrackCount=>Groups.Sum(g=>g.Audio.Count);
 public string Info=>$"{TrackCount} 条音轨  ·  {Groups.Count} 个分组";
 string? cover;public string? Cover {get=>cover;set{if(cover==value)return;cover=value;PropertyChanged?.Invoke(this,new(nameof(Cover)));}}public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
 public string Circle {get;set;}=MetadataStore.Unset;public string CircleSource {get;set;}="未设置";public string[] Tags {get;set;}=Array.Empty<string>();public string SearchText {get;set;}="";
 public string Classification=>Circle+(Tags.Length==0?"":"  ·  "+string.Join(" / ",Tags));
 public string Summary=>Name+"\n"+Classification;
 public string HomeInfo=>Info+"  ·  "+Classification;
 public void NotifyMetadata(){PropertyChanged?.Invoke(this,new(nameof(Summary)));PropertyChanged?.Invoke(this,new(nameof(Classification)));}
}
public static class WorkCatalog
{
 public static bool Within(string path,string parent)=>path.Equals(parent,StringComparison.OrdinalIgnoreCase)||path.StartsWith(parent.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase);
 static readonly Regex MediaName=new(@"^(?:cd|disc|disk|vol|track[s]?|audio|voice|音轨|音軌|音声|音聲|音频|音頻|本編|本篇|特典|ボイス|トラック|wav|mp3|flac|ogg|m4a)[\s._\-\d一二三四五六七八九十]*$|^\d+$",RegexOptions.IgnoreCase|RegexOptions.Compiled);
 static readonly Regex CollectionName=new(@"合集|合輯|合辑|收藏|分类|分類|全年龄|全年齡|成人向|作品集|collection|anthology|^20\d{2}$",RegexOptions.IgnoreCase|RegexOptions.Compiled);
 static readonly HashSet<string> CircleContainers=new(new[]{"社团","サークル","Circles"},StringComparer.OrdinalIgnoreCase);
 static bool CircleCollection(Folder f)=>CircleContainers.Contains(f.Name)||CircleContainers.Contains(System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(f.Path))??"");
 public static List<Work> Build(Library library,Settings settings)=>Build(library.Folders,settings);
 public static List<Work> Build(IReadOnlyDictionary<string,Folder> folders,Settings settings)
 {
  var counts=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
  int Count(string path){if(counts.TryGetValue(path,out var c))return c;if(!folders.TryGetValue(path,out var f))return 0;return counts[path]=f.Audio.Count+f.Children.Sum(Count);}
  var auto=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
  void Assign(string path,string work){if(!folders.TryGetValue(path,out var f))return;if(f.Audio.Count>0)auto[path]=work;foreach(var child in f.Children)Assign(child,work);}
  bool Sequential(List<Folder> children)=>children.Count>1&&children.Select(x=>Regex.Replace(x.Name,@"\d+","#")).Distinct(StringComparer.OrdinalIgnoreCase).Count()==1;
  void Visit(string path)
  {
   if(!folders.TryGetValue(path,out var f)||Count(path)==0)return;
   var children=f.Children.Where(x=>Count(x)>0).Select(x=>folders[x]).ToList();
   var structural=children.Count>0&&(children.All(x=>MediaName.IsMatch(x.Name))||Sequential(children)&&children.All(x=>x.Images.Count==0));
   var independentCovers=children.Count(x=>x.Images.Count>0);
   var branchingWorks=children.Count(x=>x.Children.Any(c=>Count(c)>0));
   bool ClearSeries(){var p=Directory.GetParent(f.Path);while(p!=null&&Within(p.FullName,f.Root)){if(p.Parent!=null&&CircleContainers.Contains(p.Parent.Name))return true;if(p.FullName.Equals(f.Root,StringComparison.OrdinalIgnoreCase))break;p=p.Parent;}return Regex.IsMatch(f.Name,@"^(?:系列|子系列|series|subseries)",RegexOptions.IgnoreCase);}
   var collection=f.Audio.Count==0&&children.Count>0&&(CircleCollection(f)||!structural&&ClearSeries()||!structural&&((independentCovers>=2)||(f.Images.Count==0&&children.Count>=2&&branchingWorks>=2)||CollectionName.IsMatch(f.Name)));
   if(collection){foreach(var child in children)Visit(child.Path);}else Assign(path,path);
  }
  foreach(var root in settings.Roots)
  {
   if(!folders.TryGetValue(root,out var f))continue;var children=f.Children.Where(x=>Count(x)>0).Select(x=>folders[x]).ToList();
   var rootIsWork=!CircleCollection(f)&&(children.Count==0||children.All(x=>MediaName.IsMatch(x.Name))&&(f.Audio.Count>0||f.Images.Count>0||!CollectionName.IsMatch(f.Name)));
   if(rootIsWork)Assign(root,root);else{if(f.Audio.Count>0)auto[root]=root;foreach(var child in children)Visit(child.Path);}
  }
  var result=new Dictionary<string,Work>(StringComparer.OrdinalIgnoreCase);
  foreach(var pair in auto)
  {
   var owner=pair.Value;string? assignment=pair.Key;
   while(assignment!=null&&Within(assignment,folders[pair.Key].Root)){if(settings.WorkAssignments.TryGetValue(assignment,out var correction)&&folders.ContainsKey(correction)){owner=correction;break;}assignment=System.IO.Path.GetDirectoryName(assignment);}
   if(!result.TryGetValue(owner,out var work))result[owner]=work=new Work{Path=owner,Name=settings.WorkNames.TryGetValue(owner,out var name)?name:folders[owner].Name};
   work.Groups.Add(folders[pair.Key]);
  }
  var groupOwners=result.Values.SelectMany(w=>w.Groups.Select(g=>(g.Path,w))).ToDictionary(x=>x.Path,x=>x.w,StringComparer.OrdinalIgnoreCase);
  foreach(var imageFolder in folders.Values.Where(f=>f.Images.Count>0))
  {
   string? p=imageFolder.Path;Work? owner=null;
   while(p!=null){if(groupOwners.TryGetValue(p,out owner)||result.TryGetValue(p,out owner))break;if(p==imageFolder.Root)break;p=System.IO.Path.GetDirectoryName(p);}
   if(owner!=null)owner.Images.AddRange(imageFolder.Images);
  }
  foreach(var work in result.Values)
  {
   MetadataStore.Inherit(work.Path,work.Groups[0].Root,settings);
   work.Groups.Sort((a,b)=>Natural.Instance.Compare(a.Path,b.Path));
   work.Images=work.Images.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>System.IO.Path.GetFileName(x),Natural.Instance).ThenBy(x=>x,Natural.Instance).ToList();
   if(!settings.WorkCovers.ContainsKey(work.Path))
   {
    var legacy=settings.Covers.Where(x=>x.Key.Equals(work.Path,StringComparison.OrdinalIgnoreCase)||work.Groups.Any(g=>g.Path.Equals(x.Key,StringComparison.OrdinalIgnoreCase))).Select(x=>x.Value).FirstOrDefault(File.Exists);
    if(legacy!=null)settings.WorkCovers[work.Path]=legacy;
   }
   work.Cover=settings.WorkCovers.TryGetValue(work.Path,out var selected)&&File.Exists(selected)?selected:work.Images.FirstOrDefault();
   MetadataStore.Enrich(work,settings);work.SearchText=string.Join("\n",new[]{work.Name,work.Path,work.Circle,string.Join(" ",work.Tags),string.Join("\n",work.Groups.SelectMany(g=>g.Audio))});
  }
  return result.Values.OrderBy(w=>w.Name,Natural.Instance).ToList();
 }
 public static List<string> Pictures(Library library,Work? work,string folder)
 {
  if(library.Folders.ContainsKey(folder)){var local=ImageDiscovery.Read(folder);if(local.Count>0)return local;}
  if(work!=null&&library.Folders.TryGetValue(work.Path,out var owner))return ImageDiscovery.Read(owner.Path);return new();
 }
}
