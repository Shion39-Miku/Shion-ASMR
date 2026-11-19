using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
namespace Nocturne;
public static class ImageDiscovery
{
 public sealed record Result(List<string> Files,string? Error);
 public static Result Find(string audio,Work? work,IReadOnlyList<Work>? catalog=null,CancellationToken token=default)
 {
  var searched=new List<string>();var skipped=new List<string>();var errors=new List<string>();
  List<string> Scan(string dir){token.ThrowIfCancellationRequested();searched.Add(dir);try{return Directory.EnumerateFiles(dir).Where(p=>{token.ThrowIfCancellationRequested();return Library.ImageTypes.Contains(Path.GetExtension(p));}).ToList();}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){errors.Add(dir+": "+ex.Message);return new();}}
  var actual=Path.GetDirectoryName(Path.GetFullPath(audio))!;work=catalog?.FirstOrDefault(w=>w.Groups.Any(g=>g.Path.Equals(actual,StringComparison.OrdinalIgnoreCase)))??work;var owner=work?.Path??actual;var root=work?.Groups.FirstOrDefault()?.Root??actual;
  if(!WorkCatalog.Within(actual,owner)||!WorkCatalog.Within(owner,root))owner=actual;
  var others=(catalog??Array.Empty<Work>()).Where(w=>w.Path!=owner&&WorkCatalog.Within(w.Path,owner)).Select(w=>w.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
  var found=Scan(actual);
  if(found.Count==0)
  {
   if(!owner.Equals(actual,StringComparison.OrdinalIgnoreCase))found.AddRange(Scan(owner));
   var stack=new Stack<(string path,int depth)>();stack.Push((owner,0));int visited=0;
   while(stack.Count>0&&visited++<8192)
   {
    token.ThrowIfCancellationRequested();var (parent,depth)=stack.Pop();if(depth>=64){errors.Add("超过64层图片目录安全限制："+parent);continue;}
    try{foreach(var child in Directory.EnumerateDirectories(parent))
    {
     if((File.GetAttributes(child)&FileAttributes.ReparsePoint)!=0||others.Contains(child)){skipped.Add(child);continue;}
     if(!child.Equals(actual,StringComparison.OrdinalIgnoreCase))found.AddRange(Scan(child));stack.Push((child,depth+1));
    }}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){errors.Add(parent+": "+ex.Message);}
   }
   if(stack.Count>0)errors.Add("超过8192个图片目录安全限制");
  }
  found=found.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p=>Path.GetFileName(p),Natural.Instance).ThenBy(p=>p,Natural.Instance).ToList();
  Log(audio,new IOException($"图片检索：owner={owner}; directories=[{string.Join(" | ",searched)}]; count={found.Count}; excluded=[{string.Join(" | ",skipped)}]; errors={string.Join(" | ",errors)}"));return new(found,errors.Count>0?string.Join("; ",errors):null);
 }
 public static List<string> Read(string path){try{return Directory.EnumerateFiles(path).Where(p=>Library.ImageTypes.Contains(Path.GetExtension(p))).OrderBy(p=>Path.GetFileName(p),Natural.Instance).ToList();}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){Log(path,ex);return new();}}
 public static void Log(string? path,Exception error){try{var log=Path.Combine(State.Home,"image-errors.log");Directory.CreateDirectory(State.Home);if(File.Exists(log)&&new FileInfo(log).Length>262144)File.Move(log,log+".previous",true);File.AppendAllText(log,$"{DateTimeOffset.Now:O} | {path} | {error.GetType().Name}: {error.Message}\n");}catch{}}
}
