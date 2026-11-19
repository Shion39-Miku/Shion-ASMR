using System;
using System.IO;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.Vorbis;
using System.Threading;

namespace Nocturne;
public sealed class AudioEngine : IDisposable
{
 WaveStream? reader; WaveOutEvent? output; VolumeSampleProvider? volume; FadeProvider? envelope; double anchor; float gain=0.65f; int version;
 public float TemporaryGain=>envelope?.Gain??1;
 public void Fade(float value,int milliseconds=0)=>envelope?.Set(value,milliseconds);
 public System.Threading.Tasks.Task FadeComplete=>envelope?.Completion??System.Threading.Tasks.Task.CompletedTask;
 public event Action<Exception?>? Ended;
 public AudioAnalysis Analysis {get;}=new();
 public int Generation=>Volatile.Read(ref version);
 public string? FilePath {get;private set;} public bool Playing=>output?.PlaybackState==PlaybackState.Playing;
 public double Duration=>reader?.TotalTime.TotalSeconds??0;
 public int Channels=>reader?.WaveFormat.Channels??0;
 public double Position
 {
  get
  {
   if(output==null||reader==null)return 0;
   var bytesPerSecond=output.OutputWaveFormat.AverageBytesPerSecond;var raw=output.GetPosition();
   // WinMM reports a 32-bit byte counter. Infer its epoch from the decoder position (only ~120 ms ahead),
   // so playback beyond a counter wrap remains correct even when no visible window is polling.
   var decodedBytes=(reader.CurrentTime.TotalSeconds-anchor)*bytesPerSecond;
   var wraps=Math.Max(0,Math.Round((decodedBytes-raw)/4294967296.0));
   return Math.Clamp(anchor+(raw+wraps*4294967296.0)/bytesPerSecond,0,Duration);
  }
 }
 public float Volume {get=>gain;set{gain=Math.Clamp(value,0,1);if(volume!=null)volume.Volume=gain;}}
 public void Open(string path,double seconds=0)
 {
  DisposeStream();try {reader=Path.GetExtension(path).Equals(".ogg",StringComparison.OrdinalIgnoreCase)?new VorbisWaveReader(path):new AudioFileReader(path);FilePath=path;Seek(seconds,false);}catch{DisposeStream();throw;}
 }
 public void Seek(double seconds,bool play)
 {
  if(reader==null)return;version++;output?.Stop();output?.Dispose();output=null;
  anchor=Math.Clamp(seconds,0,Math.Max(0,Duration-0.001));reader.CurrentTime=TimeSpan.FromSeconds(anchor);
  // Anchor at the decoder's actual seek result, accounting for block-aligned formats.
  anchor=reader.CurrentTime.TotalSeconds;
  volume=new VolumeSampleProvider(reader.ToSampleProvider()){Volume=gain};output=new WaveOutEvent{DesiredLatency=120,NumberOfBuffers=3};
  envelope=new FadeProvider(volume);Analysis.Clear();output.Init(new AnalysisProvider(envelope,Analysis));var current=version;output.PlaybackStopped+=(_,e)=>{if(current==version)Ended?.Invoke(e.Exception);};if(play)output.Play();
 }
 public void Play(){if(reader!=null&&Position>=Duration-0.05){var temporary=TemporaryGain;Seek(0,false);Fade(temporary);output?.Play();}else output?.Play();}
 public void Pause()=>output?.Pause();
 void DisposeStream(){version++;output?.Stop();output?.Dispose();output=null;envelope=null;reader?.Dispose();reader=null;FilePath=null;anchor=0;}
 public void Dispose()=>DisposeStream();
}
