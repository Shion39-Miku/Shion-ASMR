using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Nocturne;
public static class SubtitleMatcher
{
 public static List<string> Candidates(string track,Work? work,Library library,string? saved,IEnumerable<Work> catalog)
 {
  var stem=Path.GetFileNameWithoutExtension(track);var folder=Path.GetDirectoryName(track)!;
  bool Match(string p)=>Path.GetFileNameWithoutExtension(p).Equals(stem,StringComparison.OrdinalIgnoreCase)&&File.Exists(p);
  IEnumerable<string> Order(IEnumerable<string> values)=>values.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p=>Path.GetExtension(p).Equals(".vtt",StringComparison.OrdinalIgnoreCase)?0:1).ThenBy(p=>p,Natural.Instance);
  var result=new List<string>();if(saved!=null&&File.Exists(saved))result.Add(saved);
  // The current directory is checked directly, including files added since the last index update.
  // Two exact-name probes work with old indexes and files added while the application was closed.
  // They avoid enumerating every file in a large media directory on the UI thread.
  result.AddRange(Order(new[]{Path.Combine(folder,stem+".vtt"),Path.Combine(folder,stem+".lrc")}.Where(File.Exists)));
  if(library.Folders.TryGetValue(folder,out var indexed)&&indexed.SubtitleLinks.TryGetValue(stem,out var linked))result.AddRange(Order(linked.Where(Match)));
  if(work!=null)
  {
   // Only subtitle-only descendants belong to this work. Never cross into another catalog work.
   var others=catalog;
   var prefix=work.Path.TrimEnd('\\','/')+Path.DirectorySeparatorChar;
   var nested=others.Where(w=>!w.Path.Equals(work.Path,StringComparison.OrdinalIgnoreCase)&&w.Path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)).Select(w=>w.Path.TrimEnd('\\','/')+Path.DirectorySeparatorChar).ToList();
   result.AddRange(Order(library.Folders.Values.Where(f=>f.Audio.Count==0&&f.Path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)&&!nested.Any(p=>(f.Path+Path.DirectorySeparatorChar).StartsWith(p,StringComparison.OrdinalIgnoreCase))).SelectMany(f=>f.Subtitles).Where(Match)));
  }
  return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
 }
}
