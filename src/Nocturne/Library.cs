using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Nocturne;
public sealed class Natural : IComparer<string>
{
 [DllImport("shlwapi.dll",CharSet=CharSet.Unicode)] static extern int StrCmpLogicalW(string a,string b);
 public static readonly Natural Instance=new(); public int Compare(string? a,string? b)=>StrCmpLogicalW(a??"",b??"");
}
public sealed class Folder
{
 public string Path {get;set;}=""; public string Root {get;set;}=""; public long Stamp {get;set;}
 public List<string> Audio {get;set;}=new(); public List<string> Images {get;set;}=new(); public List<string> Subtitles {get;set;}=new();
 public List<string> Children {get;set;}=new();
 public Dictionary<string,List<string>> SubtitleLinks {get;set;}=new(StringComparer.OrdinalIgnoreCase);
 public void LinkSubtitles()=>SubtitleLinks=Subtitles.GroupBy(p=>System.IO.Path.GetFileNameWithoutExtension(p),StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.OrderBy(p=>System.IO.Path.GetExtension(p).Equals(".vtt",StringComparison.OrdinalIgnoreCase)?0:1).ThenBy(p=>p,Natural.Instance).ToList(),StringComparer.OrdinalIgnoreCase);
 public string Name=>System.IO.Path.GetFileName(Path.TrimEnd('\\')) is string s && s.Length>0?s:Path;
}
public sealed class Library : IDisposable
{
 public static readonly HashSet<string> AudioTypes=new(new[]{".mp3",".wav",".flac",".m4a",".ogg"},StringComparer.OrdinalIgnoreCase);
 public static readonly HashSet<string> VideoTypes=new(new[]{".mp4",".m4v",".mov",".wmv",".avi",".mkv"},StringComparer.OrdinalIgnoreCase);
 public static readonly HashSet<string> ImageTypes=new(new[]{".jpg",".jpeg",".png",".webp",".bmp"},StringComparer.OrdinalIgnoreCase);
 public Dictionary<string,Folder> Folders {get;private set;}=new(StringComparer.OrdinalIgnoreCase);
 readonly List<FileSystemWatcher> watchers=new(); readonly object gate=new(); readonly HashSet<string> dirty=new(StringComparer.OrdinalIgnoreCase);
 System.Threading.Timer? debounce;volatile bool disposed; public event Action? Changed; public int FilesEnumerated {get;private set;} public int ScanThreadId {get;private set;}
 public string CacheDiagnostics {get;private set;}="";
 public Library(){Folders=new(State.BootFolders??State.Read<Dictionary<string,Folder>>("index.json"),StringComparer.OrdinalIgnoreCase);if(!File.Exists(System.IO.Path.Combine(State.Home,"video-index-v1")))foreach(var f in Folders.Values)dirty.Add(f.Path);}
 public Task<bool> Scan(IReadOnlyList<string> roots,IProgress<string>? progress,CancellationToken token,bool force=false)=>Task.Run(()=>
 {
  ScanThreadId=Environment.CurrentManagedThreadId;var next=new Dictionary<string,Folder>(StringComparer.OrdinalIgnoreCase); var invalid=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  lock(gate){invalid.UnionWith(dirty);dirty.Clear();} FilesEnumerated=0; int count=0,miss=0,changedStamp=0;
  var cachedFolders=Folders;
  void PreserveOfflineRoot(string root)
  {
   var pending=new Stack<string>();pending.Push(root);
   while(pending.TryPop(out var path))
   {
    token.ThrowIfCancellationRequested();
    if(cachedFolders.TryGetValue(path,out var cached)&&cached.Root==root&&next.TryAdd(path,cached))
     foreach(var child in cached.Children)pending.Push(child);
   }
  }
  try
  {
  foreach(var root in roots)
  {
   var stack=new Stack<string>(); stack.Push(root);
   while(stack.Count>0)
   {
    token.ThrowIfCancellationRequested(); var path=stack.Pop();
    try
    {
     var di=new DirectoryInfo(path); if(!di.Exists){progress?.Report("目录不可用："+path);if(path==root)PreserveOfflineRoot(root);continue;}
     if((di.Attributes&FileAttributes.ReparsePoint)!=0)continue;
     Folder folder;
     if(!force&&!invalid.Contains(path)&&cachedFolders.TryGetValue(path,out var old)&&old.Stamp==di.LastWriteTimeUtc.Ticks&&old.Root==root&&(old.Subtitles.Count==0||old.SubtitleLinks.Count>0))folder=old;
     else
     {
      miss++;if(cachedFolders.TryGetValue(path,out var previous)&&previous.Stamp!=di.LastWriteTimeUtc.Ticks)changedStamp++;
      folder=new Folder{Path=path,Root=root,Stamp=di.LastWriteTimeUtc.Ticks};
      foreach(var file in di.EnumerateFiles()){token.ThrowIfCancellationRequested();FilesEnumerated++;var ext=file.Extension;if(AudioTypes.Contains(ext)||VideoTypes.Contains(ext))folder.Audio.Add(file.FullName);else if(ImageTypes.Contains(ext))folder.Images.Add(file.FullName);else if(ext.Equals(".lrc",StringComparison.OrdinalIgnoreCase)||ext.Equals(".vtt",StringComparison.OrdinalIgnoreCase))folder.Subtitles.Add(file.FullName);}
      folder.Children=di.EnumerateDirectories().Where(x=>(x.Attributes&FileAttributes.ReparsePoint)==0).Select(x=>x.FullName).ToList();
      folder.Audio.Sort(Natural.Instance);folder.Images.Sort(Natural.Instance);folder.Subtitles.Sort(Natural.Instance);folder.Children.Sort(Natural.Instance);folder.LinkSubtitles();
     }
     next[path]=folder; foreach(var child in folder.Children)stack.Push(child);
     if(++count%30==0)progress?.Report($"扫描 {count} 个文件夹 · {System.IO.Path.GetFileName(path)}");
    }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
    {
     progress?.Report("无法读取："+path);
     if(cachedFolders.TryGetValue(path,out var cached)&&cached.Root==root)
     {
      next[path]=cached;
      foreach(var child in cached.Children)stack.Push(child);
     }
    }
   }
  }
  token.ThrowIfCancellationRequested();
  var changed=next.Count!=cachedFolders.Count||next.Any(pair=>!cachedFolders.TryGetValue(pair.Key,out var cached)||!ReferenceEquals(pair.Value,cached));
  var marker=System.IO.Path.Combine(State.Home,"video-index-v1");
  if(changed||!File.Exists(marker)){State.Write("index.json",next);File.WriteAllText(marker,"1");}
  if(changed)Folders=next;
  CacheDiagnostics=$"force={force}; dirty={invalid.Count}; misses={miss}; changed stamps={changedStamp}; changed={changed}";
  return changed;
  }
  catch
  {
   lock(gate)dirty.UnionWith(invalid);
   throw;
  }
 },token);
 public void Watch(IEnumerable<string> roots)
 {
  foreach(var w in watchers)w.Dispose();watchers.Clear();debounce?.Dispose();
  debounce=new System.Threading.Timer(_=>{if(!disposed)Changed?.Invoke();},null,Timeout.Infinite,Timeout.Infinite);
  foreach(var root in roots)try{var w=new FileSystemWatcher(root){IncludeSubdirectories=true,NotifyFilter=NotifyFilters.FileName|NotifyFilters.DirectoryName|NotifyFilters.LastWrite};
   FileSystemEventHandler mark=(_,e)=>Mark(e.FullPath); w.Changed+=mark;w.Created+=mark;w.Deleted+=mark;w.Renamed+=(_,e)=>{Mark(e.OldFullPath);Mark(e.FullPath);};w.Error+=(_,_)=>{lock(gate){foreach(var path in Folders.Keys)dirty.Add(path);}ScheduleChange();};w.EnableRaisingEvents=true;watchers.Add(w);
  }catch(IOException){}catch(ArgumentException){}catch(UnauthorizedAccessException){}
 }
 void ScheduleChange(){try{if(!disposed)debounce?.Change(1200,Timeout.Infinite);}catch(ObjectDisposedException){}}
 void Mark(string path){if(disposed)return;lock(gate){dirty.Add(path);var parent=System.IO.Path.GetDirectoryName(path);if(parent!=null)dirty.Add(parent);}ScheduleChange();}
 public List<string> ImagesFor(string folder)
 {
  if(!Folders.TryGetValue(folder,out var start))return new();var path=folder;
  while(true){if(Folders.TryGetValue(path,out var f)&&f.Images.Count>0)return f.Images.OrderBy(x=>CoverRank(x)).ThenBy(x=>x,Natural.Instance).ToList();if(path.Equals(start.Root,StringComparison.OrdinalIgnoreCase))break;var p=System.IO.Path.GetDirectoryName(path);if(p==null)break;path=p;}
  return new();
 }
 static int CoverRank(string path)=>System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant() switch{"cover"=>0,"folder"=>1,"front"=>2,_=>3};
 public void Dispose(){disposed=true;foreach(var w in watchers)w.Dispose();debounce?.Dispose();}
}
