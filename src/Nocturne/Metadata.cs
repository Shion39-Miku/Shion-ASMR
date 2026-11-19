using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;

namespace Nocturne;
public sealed class WorkMetadata
{
 public string? Circle {get;set;} public bool CircleManual {get;set;}
 public List<string> Tags {get;set;}=new();
 public WorkMetadata Copy()=>new(){Circle=Circle,CircleManual=CircleManual,Tags=Tags.ToList()};
}
public static class MetadataStore
{
 public const string Unset="未设置社团";
 public static void Inherit(string path,string root,Settings settings)
 {
  var parent=Path.GetDirectoryName(path);
  while(parent!=null&&WorkCatalog.Within(parent,root))
  {
   if(!settings.WorkDetails.ContainsKey(path)&&settings.WorkDetails.TryGetValue(parent,out var detail))settings.WorkDetails[path]=detail.Copy();
   if(!settings.WorkCovers.ContainsKey(path)&&settings.WorkCovers.TryGetValue(parent,out var cover))settings.WorkCovers[path]=cover;
   if(!settings.WorkLocations.ContainsKey(path)&&settings.WorkLocations.TryGetValue(parent,out var location))settings.WorkLocations[path]=location;
   if(parent.Equals(root,StringComparison.OrdinalIgnoreCase))break;parent=Path.GetDirectoryName(parent);
  }
 }
 public static WorkMetadata Detail(Settings s,string work){if(!s.WorkDetails.TryGetValue(work,out var d))s.WorkDetails[work]=d=new();return d;}
 public static void SetCircle(Settings s,IEnumerable<string> works,string value){foreach(var w in works){var d=Detail(s,w);d.Circle=value.Trim();d.CircleManual=true;}}
 public static void AddTag(Settings s,IEnumerable<string> works,string name){name=name.Trim();if(name.Length==0)return;if(!s.CustomTags.Contains(name,StringComparer.OrdinalIgnoreCase))s.CustomTags.Add(name);foreach(var w in works){var d=Detail(s,w);if(!d.Tags.Contains(name,StringComparer.OrdinalIgnoreCase))d.Tags.Add(name);}}
 public static void RemoveTag(Settings s,IEnumerable<string> works,string name){foreach(var w in works)Detail(s,w).Tags.RemoveAll(t=>t.Equals(name,StringComparison.OrdinalIgnoreCase));}
 public static void RenameTag(Settings s,string old,string name){name=name.Trim();if(name.Length==0)return;foreach(var d in s.WorkDetails.Values)d.Tags=d.Tags.Select(t=>t.Equals(old,StringComparison.OrdinalIgnoreCase)?name:t).Distinct(StringComparer.OrdinalIgnoreCase).ToList();s.CustomTags=s.CustomTags.Select(t=>t.Equals(old,StringComparison.OrdinalIgnoreCase)?name:t).Distinct(StringComparer.OrdinalIgnoreCase).ToList();}
 public static void DeleteTag(Settings s,string name){s.CustomTags.RemoveAll(t=>t.Equals(name,StringComparison.OrdinalIgnoreCase));foreach(var d in s.WorkDetails.Values)d.Tags.RemoveAll(t=>t.Equals(name,StringComparison.OrdinalIgnoreCase));}
 static IEnumerable<string> Names(JsonElement value)=>value.ValueKind==JsonValueKind.Array?value.EnumerateArray().Where(t=>t.ValueKind==JsonValueKind.String).Select(t=>t.GetString()!).ToArray():value.ValueKind==JsonValueKind.String?new[]{value.GetString()!}:Array.Empty<string>();
 public static void Migrate(Settings s)
 {
  s.WorkDetails=new(s.WorkDetails??new(),StringComparer.OrdinalIgnoreCase);
  if(s.SchemaVersion<3)
  {
   var file=Path.Combine(State.Home,"settings.json");if(File.Exists(file)&&!File.Exists(file+".v1.2.bak"))File.Copy(file,file+".v1.2.bak");
   var legacy=s.LegacyData??new();
   foreach(var pair in legacy)
   {
    if(pair.Key.Equals("tags",StringComparison.OrdinalIgnoreCase)&&pair.Value.ValueKind==JsonValueKind.Array)s.CustomTags.AddRange(Names(pair.Value));
    if(pair.Key.Equals("workTags",StringComparison.OrdinalIgnoreCase)&&pair.Value.ValueKind==JsonValueKind.Object)
     foreach(var work in pair.Value.EnumerateObject())foreach(var tag in Names(work.Value))AddTag(s,new[]{work.Name},tag);
    if(pair.Key.Equals("workCircles",StringComparison.OrdinalIgnoreCase)&&pair.Value.ValueKind==JsonValueKind.Object)
     foreach(var work in pair.Value.EnumerateObject())if(work.Value.ValueKind==JsonValueKind.String)SetCircle(s,new[]{work.Name},work.Value.GetString()!);
   }
   // Older optional tags.json: {"work path": ["tag", ...]}. Keep its source untouched.
   foreach(var pair in State.Read<Dictionary<string,JsonElement>>("tags.json"))foreach(var tag in Names(pair.Value))if(Path.IsPathFullyQualified(pair.Key))AddTag(s,new[]{pair.Key},tag);
   s.SchemaVersion=3;
  }
  s.CustomTags=s.CustomTags.Concat(s.WorkDetails.Values.SelectMany(d=>d.Tags)).Where(t=>!string.IsNullOrWhiteSpace(t)).Select(t=>t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
  foreach(var d in s.WorkDetails.Values)d.Tags=d.Tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
 }
 public static void Enrich(Work w,Settings s)
 {
  if(s.WorkDetails.TryGetValue(w.Path,out var d)){w.Tags=d.Tags.ToArray();if(d.CircleManual){w.Circle=string.IsNullOrWhiteSpace(d.Circle)?Unset:d.Circle;w.CircleSource="手动";return;}}
  foreach(var name in new[]{"nocturne.json","metadata.json"})try
  {
   var file=Path.Combine(w.Path,name);if(!File.Exists(file)||new FileInfo(file).Length>131072)continue;using var json=JsonDocument.Parse(File.ReadAllText(file));
   foreach(var prop in json.RootElement.EnumerateObject())if(new[]{"circle","circleName","circle_name","社团","サークル"}.Contains(prop.Name,StringComparer.OrdinalIgnoreCase))
   {
    var value=prop.Value;if(value.ValueKind==JsonValueKind.Object&&value.TryGetProperty("name",out var n))value=n;
    if(value.ValueKind==JsonValueKind.String&&!string.IsNullOrWhiteSpace(value.GetString())){w.Circle=value.GetString()!.Trim();w.CircleSource="作品元数据";return;}
   }
  }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException){}
  var root=w.Groups.FirstOrDefault()?.Root;var current=Directory.GetParent(w.Path);
  while(current!=null&&root!=null&&WorkCatalog.Within(current.FullName,root))
  {
   if(current.Parent!=null&&new[]{"社团","サークル","Circles"}.Contains(current.Parent.Name,StringComparer.OrdinalIgnoreCase)){w.Circle=current.Name;w.CircleSource="明确社团目录";return;}
   if(current.FullName.Equals(root,StringComparison.OrdinalIgnoreCase))break;current=current.Parent;
  }
  w.Circle=Unset;w.CircleSource="未设置";
 }
 public static Settings Snapshot(Settings s)=>new(){SchemaVersion=4,Organization=s.Organization.ToDictionary(p=>p.Key,p=>p.Value.Copy(),StringComparer.OrdinalIgnoreCase),WorkLocations=new(s.WorkLocations,StringComparer.OrdinalIgnoreCase),ExpandedFolders=s.ExpandedFolders.ToList(),Roots=s.Roots.ToList(),Tracks=s.Tracks.ToDictionary(p=>p.Key,p=>new TrackPreference{LastPlayed=p.Value.LastPlayed},StringComparer.OrdinalIgnoreCase),Resume=new(s.Resume,StringComparer.OrdinalIgnoreCase),LastFolder=s.LastFolder,WorkAssignments=new(s.WorkAssignments,StringComparer.OrdinalIgnoreCase),WorkNames=new(s.WorkNames,StringComparer.OrdinalIgnoreCase),WorkCovers=new(s.WorkCovers,StringComparer.OrdinalIgnoreCase),Covers=new(s.Covers,StringComparer.OrdinalIgnoreCase),WorkDetails=s.WorkDetails.ToDictionary(p=>p.Key,p=>p.Value.Copy(),StringComparer.OrdinalIgnoreCase),CustomTags=s.CustomTags.ToList()};
}
