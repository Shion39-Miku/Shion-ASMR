using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nocturne;
public sealed class TrackPreference { public string? Subtitle {get;set;} public string Encoding {get;set;}="自动"; public double Offset {get;set;} public double? Position {get;set;} public bool FollowLyrics {get;set;}=true; public bool LyricsOpen {get;set;} public DateTimeOffset? LastPlayed {get;set;} }
public sealed class ResumePoint { public string Track {get;set;}=""; public double Seconds {get;set;} }
public sealed class Settings
{
 public bool Sakura {get;set;}=true;public double SakuraStrength {get;set;}=.65;
 public bool ShowPictures {get;set;}=true;public bool ShowVideo {get;set;}=true;
 public bool PictureOnRight {get;set;} public bool VideoOnRight {get;set;}=true;
 public double LeftMediaShare {get;set;}=.72;public double RightMediaShare {get;set;}=.85;public double CompactMediaHeight {get;set;}=140;
 public double WaveHeight {get;set;}=32;public double StarIntensity {get;set;}=1;
 public bool OverlayArt {get;set;}=true;public bool VideoSubtitles {get;set;}=true;public double VideoSubtitleScale {get;set;}=1;
 public bool PauseFade {get;set;}=true;
 public Dictionary<string,VirtualFolder> Organization {get;set;}=new(StringComparer.OrdinalIgnoreCase);
 public Dictionary<string,string> WorkLocations {get;set;}=new(StringComparer.OrdinalIgnoreCase);
 public List<string> ExpandedFolders {get;set;}=new();
 [JsonExtensionData] public Dictionary<string,JsonElement>? LegacyData {get;set;}
 public bool ArtLyrics {get;set;}=true;
 public Dictionary<string,WorkMetadata> WorkDetails {get;set;}=new(StringComparer.OrdinalIgnoreCase);
 public List<string> CustomTags {get;set;}=new();
 public int SchemaVersion {get;set;}=1;
 public double VideoWidth {get;set;}=1.25;
 public Dictionary<string,string> WorkAssignments {get;set;}=new(StringComparer.OrdinalIgnoreCase);
 public Dictionary<string,string> WorkNames {get;set;}=new(StringComparer.OrdinalIgnoreCase);
 public Dictionary<string,string> WorkCovers {get;set;}=new(StringComparer.OrdinalIgnoreCase);
 public bool ReduceMotion {get;set;} public bool Visualizer {get;set;}=true;
 public string LyricFontFamily {get;set;}="Microsoft YaHei, Yu Gothic UI, Microsoft JhengHei UI, Segoe UI";
 public int LyricWeight {get;set;}=500; public double LyricLineSpacing {get;set;}=1.7; public bool LyricShadow {get;set;}=true;
 public int OverlayWeight {get;set;}=600; public double OverlayLineSpacing {get;set;}=1.45; public bool OverlayShadow {get;set;}=true;
 public List<string> Roots {get;set;}=new(); public Dictionary<string,TrackPreference> Tracks {get;set;}=new(StringComparer.OrdinalIgnoreCase);
 public Dictionary<string,ResumePoint> Resume {get;set;}=new(StringComparer.OrdinalIgnoreCase);
 public Dictionary<string,string> Covers {get;set;}=new(StringComparer.OrdinalIgnoreCase);
 public string LastFolder {get;set;}=""; public double Volume {get;set;}=0.65; public int Loop {get;set;}=0; public int Stars {get;set;}=2; public int Background {get;set;}=0; public string FixedImage {get;set;}="";
 public double Width {get;set;}=1160; public double Height {get;set;}=760; public double Left {get;set;}=80; public double Top {get;set;}=70;
 public bool SubtitlesExpanded {get;set;}=true; public double FontSize {get;set;}=22; public double Sidebar {get;set;}=250;
 public double OverlayLeft {get;set;}=200; public double OverlayTop {get;set;}=650; public double OverlayWidth {get;set;}=850; public double OverlayHeight {get;set;}=150;
 public double OverlayFont {get;set;}=30; public string OverlayFontFamily {get;set;}="Microsoft YaHei UI, Yu Gothic UI, Microsoft JhengHei UI, Segoe UI"; public string OverlayColor {get;set;}="#FFB2D9"; public double OverlayOpacity {get;set;}=1; public bool OverlayBackground {get;set;}=false; public bool OverlayTopmost {get;set;}=true; public bool OverlayLocked {get;set;}=true; public bool OverlayClickThrough {get;set;}=true;
 public void Migrate()
 {
  Tracks=new(Tracks,StringComparer.OrdinalIgnoreCase);Resume=new(Resume,StringComparer.OrdinalIgnoreCase);Covers=new(Covers,StringComparer.OrdinalIgnoreCase);
  WorkAssignments=new(WorkAssignments,StringComparer.OrdinalIgnoreCase);WorkNames=new(WorkNames,StringComparer.OrdinalIgnoreCase);WorkCovers=new(WorkCovers,StringComparer.OrdinalIgnoreCase);
  if(SchemaVersion<2)
  {
   var path=Path.Combine(State.Home,"settings.json");if(File.Exists(path)&&!File.Exists(path+".v1.bak"))File.Copy(path,path+".v1.bak");
   foreach(var r in Resume.Values){if(!Tracks.TryGetValue(r.Track,out var p))Tracks[r.Track]=p=new();p.Position??=r.Seconds;}
   SchemaVersion=2;
  }
  // Edit mode never survives a restart, including settings imported from v1.
  OverlayClickThrough=OverlayLocked;
  MetadataStore.Migrate(this);
  Organization=new(Organization,StringComparer.OrdinalIgnoreCase);WorkLocations=new(WorkLocations,StringComparer.OrdinalIgnoreCase);
  if(SchemaVersion<4){var file=Path.Combine(State.Home,"settings.json");if(File.Exists(file)&&!File.Exists(file+".v1.3.bak"))File.Copy(file,file+".v1.3.bak");SchemaVersion=4;}
  if(SchemaVersion<5){var file=Path.Combine(State.Home,"settings.json");if(File.Exists(file)&&!File.Exists(file+".v1.4.bak"))File.Copy(file,file+".v1.4.bak");OverlayBackground=false;OverlayArt=true;if(OverlayColor=="#E6E0FF")OverlayColor="#FFB2D9";SchemaVersion=5;}
  if(SchemaVersion<6){var file=Path.Combine(State.Home,"settings.json");if(File.Exists(file)&&!File.Exists(file+".v1.5.bak"))File.Copy(file,file+".v1.5.bak");WaveHeight=WaveHeight>=64?32:Math.Clamp(WaveHeight,16,48);SchemaVersion=6;}
 }
}
public static class State
{
 public static Settings? BootSettings;public static Dictionary<string,Folder>? BootFolders;
 public static readonly string DefaultHome=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Nocturne");
 public static string Home=DefaultHome;
 public static T Read<T>(string name) where T:new() {try{return JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(Home,name))) ?? new T();}catch{return new T();}}
 public static void Write<T>(string name,T value) {Directory.CreateDirectory(Home); var file=Path.Combine(Home,name); File.WriteAllText(file+".tmp",JsonSerializer.Serialize(value,new JsonSerializerOptions{WriteIndented=true})); File.Move(file+".tmp",file,true);}
}
