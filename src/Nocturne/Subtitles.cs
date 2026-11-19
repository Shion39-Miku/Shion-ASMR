using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Globalization;

namespace Nocturne;
public sealed record Cue(double Start,double End,string Text) { public bool Spacer {get;init;} public override string ToString()=>Text; }
public sealed class SubtitleDocument
{
 public List<Cue> Cues {get;private set;}=new(); double[] prefixEnd=Array.Empty<double>();
 public static string Read(string path,string encoding)
 {
  if(new FileInfo(path).Length>16*1024*1024)throw new IOException("字幕超过 16 MB 限制。");var bytes=File.ReadAllBytes(path);
  if(encoding!="自动")return Encoding.GetEncoding(encoding).GetString(bytes).TrimStart('\uFEFF');
  if(bytes.Length>=2&&bytes[0]==0xff&&bytes[1]==0xfe)return Encoding.Unicode.GetString(bytes).TrimStart('\uFEFF');
  if(bytes.Length>=2&&bytes[0]==0xfe&&bytes[1]==0xff)return Encoding.BigEndianUnicode.GetString(bytes).TrimStart('\uFEFF');
  try{return new UTF8Encoding(false,true).GetString(bytes).TrimStart('\uFEFF');}catch(DecoderFallbackException){return Encoding.GetEncoding(932).GetString(bytes);}
 }
 public static SubtitleDocument Load(string path,string encoding)=>Parse(Read(path,encoding),Path.GetExtension(path).Equals(".vtt",StringComparison.OrdinalIgnoreCase));
 static double Time(string value){var pieces=value.Replace(',','.').Split(':');double t=0;foreach(var p in pieces)t=t*60+double.Parse(p,CultureInfo.InvariantCulture);return t;}
 static string PlainText(string text)=>WebUtility.HtmlDecode(Regex.Replace(Regex.Replace(text,@"\{\\[^}]*\}",""),@"<[^>]*>","")).Replace("\\N","\n").Replace("\\n","\n");
 public static SubtitleDocument Parse(string text,bool vtt)
 {
  var result=new SubtitleDocument();text=text.Replace("\r","").TrimStart('\uFEFF');
  if(vtt)
  {
   foreach(var block in Regex.Split(text,@"\n[\t ]*\n"))
   {
    var lines=block.Split('\n');if(lines[0].StartsWith("NOTE")||lines[0]=="STYLE"||lines[0]=="REGION")continue;
    for(int i=0;i<lines.Length;i++){var m=Regex.Match(lines[i],@"(?<a>(?:\d+:)?\d{2}:\d{2}[.,]\d+)\s+-->\s+(?<b>(?:\d+:)?\d{2}:\d{2}[.,]\d+)");if(!m.Success)continue;
     var content=PlainText(string.Join("\n",lines.Skip(i+1)).TrimEnd('\n'));
     var a=Time(m.Groups["a"].Value);var b=Time(m.Groups["b"].Value);if(b>a)result.Cues.Add(new Cue(a,b,content));break;
    }
   }
  }
  else
  {
   var offset=Regex.Matches(text,@"\[offset:\s*([+-]?\d+)\]",RegexOptions.IgnoreCase).Cast<Match>().LastOrDefault();double shift=offset==null?0:double.Parse(offset.Groups[1].Value,CultureInfo.InvariantCulture)/1000;
   foreach(var line in text.Split('\n')){var times=Regex.Matches(line,@"\[(\d+:\d{2}(?:[.:]\d+)?)\]");var content=PlainText(Regex.Replace(line,@"\[[^\]]*\]","")).Trim();foreach(Match m in times){var t=m.Groups[1].Value;var last=t.LastIndexOf(':');if(last!=t.IndexOf(':'))t=t[..last]+"."+t[(last+1)..];result.Cues.Add(new Cue(Math.Max(0,Time(t)-shift),double.PositiveInfinity,content));}}
   result.Cues=result.Cues.OrderBy(x=>x.Start).ToList();double nextStart=double.PositiveInfinity;for(int i=result.Cues.Count-1;i>=0;i--){if(i<result.Cues.Count-1&&result.Cues[i+1].Start>result.Cues[i].Start)nextStart=result.Cues[i+1].Start;result.Cues[i]=result.Cues[i] with{End=nextStart};}
  }
  result.Cues=result.Cues.OrderBy(x=>x.Start).ToList();double end=0;result.prefixEnd=result.Cues.Select(x=>end=Math.Max(end,x.End)).ToArray();return result;
 }
 public List<int> Active(double seconds,bool holdPrevious=false)
 {
  var list=new List<int>();int lo=0,hi=Cues.Count;while(lo<hi){int mid=(lo+hi)/2;if(Cues[mid].Start<=seconds)lo=mid+1;else hi=mid;}
  for(int i=lo-1;i>=0&&prefixEnd[i]>seconds;i--)if(Cues[i].End>seconds)list.Add(i);
  if(holdPrevious&&list.Count==0&&lo>0)list.Add(lo-1);
  list.Reverse();return list;
 }
}
