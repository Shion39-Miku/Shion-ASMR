using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Media;
namespace Nocturne;
public static partial class Verification
{
 static readonly List<object> frameReports=new();
 static async Task FramesOnly11(MainWindow w)
 {
  w.Width=1280;w.Height=900;w.Left=30;w.Top=20;w.Topmost=true;w.settings.ReduceMotion=false;w.settings.Stars=2;w.settings.Visualizer=true;await w.SetBrowseMode(false);await Delay(300);await MeasureFrames11(w,"诊断_分类星空",6000);var audio=w.works.SelectMany(x=>x.Groups).SelectMany(g=>g.Audio).First(x=>x.EndsWith("01 晚安.part.1.WAV"));w.OpenTrack(audio,true,10.1);await w.SubtitleReady;await w.ImageReady;await MeasureFrames11(w,"诊断_歌词波形星空",6000);w.PausePlayback();
 }
 static async Task MeasureFrames11(MainWindow w,string scene,int milliseconds,Func<Task>? interaction=null)
 {
  await Delay(500);var intervals=new List<double>();var work=new List<double>();var watch=Stopwatch.StartNew();double last=-1;TimeSpan rendered=TimeSpan.MinValue;var drawIntervals=new Dictionary<string,List<double>>{{"stars",new()},{"wave",new()}};var drawCpu=new Dictionary<string,List<double>>{{"stars",new()},{"wave",new()}};var drawLast=new Dictionary<string,double>();
  var process=Process.GetCurrentProcess();process.Refresh();var cpu=process.TotalProcessorTime;var stars=w.Stars.DrawFrames;var wave=w.Wave.DrawFrames;
  EventHandler callback=(_,e)=>{if(e is RenderingEventArgs r){if(r.RenderingTime==rendered)return;rendered=r.RenderingTime;}var now=watch.Elapsed.TotalMilliseconds;if(last>=0)intervals.Add(now-last);last=now;work.Add(FrameTicker.LastWorkMs);};
  void Draw(string name,double cpuMs){var now=watch.Elapsed.TotalMilliseconds;if(drawLast.TryGetValue(name,out var before))drawIntervals[name].Add(now-before);drawLast[name]=now;drawCpu[name].Add(cpuMs);}
  State.Write("frame-phase.json",new{scene,pid=Environment.ProcessId,started=DateTimeOffset.Now});CompositionTarget.Rendering+=callback;DrawTiming.Sample+=Draw;try{if(interaction!=null)await Task.WhenAll(Task.Delay(milliseconds),interaction());else await Task.Delay(milliseconds);}finally{CompositionTarget.Rendering-=callback;DrawTiming.Sample-=Draw;}
  watch.Stop();process.Refresh();var sorted=intervals.Order().ToArray();double Percent(double p)=>sorted.Length==0?0:sorted[Math.Min(sorted.Length-1,(int)Math.Floor((sorted.Length-1)*p))];
  var result=new{scene,seconds=watch.Elapsed.TotalSeconds,frames=intervals.Count+1,wpfCompositionFps=(intervals.Count+1)/watch.Elapsed.TotalSeconds,frameIntervalMeanMs=intervals.Count==0?0:intervals.Average(),frameIntervalP95Ms=Percent(.95),frameIntervalP99Ms=Percent(.99),frameIntervalMaxMs=intervals.Count==0?0:intervals.Max(),gapsOver25ms=intervals.Count(x=>x>25),gapsOver50ms=intervals.Count(x=>x>50),schedulerCpuMeanMs=work.Count==0?0:work.Average(),starDrawFps=(w.Stars.DrawFrames-stars)/watch.Elapsed.TotalSeconds,waveDrawFps=(w.Wave.DrawFrames-wave)/watch.Elapsed.TotalSeconds,cpuPercentAllLogicalCores=(process.TotalProcessorTime-cpu).TotalSeconds/watch.Elapsed.TotalSeconds/Environment.ProcessorCount*100,workingSetMiB=process.WorkingSet64/1048576d,privateMiB=process.PrivateMemorySize64/1048576d,intervalsMs=intervals};
  object DrawReport(string kind){var times=drawIntervals[kind].Order().ToArray();return new{kind,frames=drawCpu[kind].Count,fps=drawCpu[kind].Count/watch.Elapsed.TotalSeconds,intervalMeanMs=times.Length==0?0:times.Average(),intervalP95Ms=times.Length==0?0:times[(int)((times.Length-1)*.95)],intervalMaxMs=times.Length==0?0:times.Max(),gapsOver25ms=times.Count(x=>x>25),gapsOver50ms=times.Count(x=>x>50),drawCommandCpuMeanMs=drawCpu[kind].Count==0?0:drawCpu[kind].Average(),rawIntervalsMs=drawIntervals[kind]};}
  frameReports.Add(new{summary=result,actualDraws=new[]{DrawReport("stars"),DrawReport("wave")}});State.Write("frame-performance.json",new{method="Stopwatch wall-clock intervals of WPF CompositionTarget.Rendering callbacks (can repeat per display frame), plus actual StarField/AudioWave.OnRender command updates and their intervals/CPU cost. This is not GPU presentation timing. No recording frame rate is used. Video fixture is native 24 fps; no video interpolation.",environment=new{os=Environment.OSVersion.ToString(),runtime=Environment.Version.ToString(),cpu=Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),logicalProcessors=Environment.ProcessorCount,renderTier=RenderCapability.Tier>>16},scenes=frameReports});
 }
}
