using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Threading;
namespace Nocturne;
// A single WPF composition callback drives visible 60 Hz animations. Slow, explicitly
// selected economy effects do not keep the compositor awake at 60 Hz.
public sealed class FrameTicker
{
 static readonly List<FrameTicker> active=new();static FrameTicker[] snapshot=Array.Empty<FrameTicker>();static TimeSpan lastRendering=TimeSpan.MinValue;
 [DllImport("winmm.dll")]static extern uint timeBeginPeriod(uint period);
 [DllImport("winmm.dll")]static extern uint timeEndPeriod(uint period);
 internal static bool HighResolutionActive{get;private set;}
 readonly DispatcherTimer slow=new(DispatcherPriority.Render);TimeSpan interval=TimeSpan.FromSeconds(1d/60);double due,last;
 public event EventHandler? Tick;public bool IsEnabled{get;private set;}
 public TimeSpan Interval{get=>interval;set{if(interval==value)return;bool restart=IsEnabled;Stop();interval=value;if(restart)Start();}}
 internal static double LastWorkMs{get;private set;}
 public FrameTicker(){slow.Tick+=(_,e)=>Tick?.Invoke(this,e);}
 public void Start(){if(IsEnabled)return;IsEnabled=true;if(interval.TotalMilliseconds>=100){slow.Interval=interval;slow.Start();return;}last=Stopwatch.GetTimestamp()/(double)Stopwatch.Frequency;due=last;active.Add(this);snapshot=active.ToArray();if(active.Count==1){HighResolutionActive=timeBeginPeriod(1)==0;CompositionTarget.Rendering+=Render;}}
 public void Stop(){if(!IsEnabled)return;IsEnabled=false;slow.Stop();if(active.Remove(this)){snapshot=active.ToArray();if(active.Count==0){CompositionTarget.Rendering-=Render;lastRendering=TimeSpan.MinValue;if(HighResolutionActive)timeEndPeriod(1);HighResolutionActive=false;}}}
 static void Render(object? sender,EventArgs e){if(e is RenderingEventArgs frame){if(frame.RenderingTime==lastRendering)return;lastRendering=frame.RenderingTime;}var begin=Stopwatch.GetTimestamp();double now=begin/(double)Stopwatch.Frequency;foreach(var timer in snapshot){if(!timer.IsEnabled||now+.0008<timer.due)continue;timer.last=now;timer.due=Math.Max(timer.due+timer.interval.TotalSeconds,now-.002);timer.Tick?.Invoke(timer,EventArgs.Empty);}LastWorkMs=Stopwatch.GetElapsedTime(begin).TotalMilliseconds;}
 internal static int ActiveCount=>active.Count;
}
