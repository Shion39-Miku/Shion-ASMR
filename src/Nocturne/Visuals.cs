using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using NAudio.Wave;
using System.Windows.Input;
using System.Windows.Controls.Primitives;

namespace Nocturne;
public sealed class Symbol : Control
{
 public static readonly DependencyProperty KindProperty=DependencyProperty.Register(nameof(Kind),typeof(string),typeof(Symbol),new FrameworkPropertyMetadata("Play",FrameworkPropertyMetadataOptions.AffectsRender));
 public string Kind {get=>(string)GetValue(KindProperty);set=>SetValue(KindProperty,value);}
 static readonly Dictionary<string,string> shapes=new(){["Folder"]="M3,7 L3,19 Q3,21 5,21 L19,21 Q21,21 21,19 L21,8 Q21,6 19,6 L12,6 10,3 5,3 Q3,3 3,5 Z",["Lock"]="M6,10 L18,10 18,21 6,21 Z M8,10 L8,6 C8,1 16,1 16,6 L16,10",["Play"]="M8,5 L19,12 8,19 Z",["Pause"]="M8,5 L8,19 M16,5 L16,19",["Previous"]="M6,5 L6,19 M18,5 L9,12 18,19 Z",["Next"]="M18,5 L18,19 M6,5 L15,12 6,19 Z",["Back"]="M14,5 L7,12 14,19 M7,12 L21,12",["Close"]="M6,6 L18,18 M18,6 L6,18",["Minus"]="M5,12 L19,12",["Plus"]="M12,5 L12,19 M5,12 L19,12",["Maximize"]="M5,5 L19,5 19,19 5,19 Z",["Lyrics"]="M5,6 L19,6 M5,12 L16,12 M5,18 L19,18",["Home"]="M3,11 L12,4 21,11 M6,10 L6,20 18,20 18,10 M10,20 L10,15 14,15 14,20",["Settings"]="M4,6 L20,6 M4,12 L20,12 M4,18 L20,18 M9,3 L9,9 M16,9 L16,15 M8,15 L8,21",["Overlay"]="M3,5 L21,5 21,19 3,19 Z M7,14 L17,14 M9,10 L15,10",["ChevronLeft"]="M15,5 L8,12 15,19",["ChevronRight"]="M9,5 L16,12 9,19",["Fit"]="M9,4 L4,4 4,9 M15,4 L20,4 20,9 M4,15 L4,20 9,20 M20,15 L20,20 15,20",["Cover"]="M4,4 L20,4 20,20 4,20 Z M5,17 L10,12 14,16 18,11 20,13",["Edit"]="M5,16 L16,5 20,9 9,20 4,21 Z",["Moon"]="M17,3 C6,0 1,12 8,19 C14,25 24,18 21,13 C16,18 8,10 17,3 Z",["Refresh"]="M19,8 C14,0 3,5 4,13 C5,21 17,23 20,15 M20,3 L20,9 14,9",["Follow"]="M4,8 L4,4 8,4 M16,4 L20,4 20,8 M20,16 L20,20 16,20 M8,20 L4,20 4,16 M8,12 L16,12 M12,8 L12,16"};
 static readonly Dictionary<string,Geometry> geometries=new();
 public Symbol(){Width=20;Height=20;Focusable=false;IsHitTestVisible=false;}
 protected override void OnRender(DrawingContext dc){base.OnRender(dc);if(!geometries.TryGetValue(Kind,out var geometry)){geometry=Geometry.Parse(shapes.TryGetValue(Kind,out var d)?d:shapes["Play"]);geometry.Freeze();geometries[Kind]=geometry;}dc.PushTransform(new ScaleTransform(ActualWidth/24,ActualHeight/24));var pen=new Pen(Foreground??Brushes.White,1.65){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};dc.DrawGeometry(Kind is "Play"?Foreground:null,pen,geometry);dc.Pop();}
}
public static class Motion
{
 static Settings? current;static bool wired;
 public static void Use(Settings s)
 {
  current=s;if(wired)return;wired=true;
  void Hover(object sender,MouseEventArgs args){if(current==null||!Enabled(current)||sender is not Button b||!b.IsEnabled)return;b.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(b.Opacity,b.IsMouseOver?0.86:1,TimeSpan.FromMilliseconds(220)));}
  EventManager.RegisterClassHandler(typeof(Button),Mouse.MouseEnterEvent,new MouseEventHandler(Hover));EventManager.RegisterClassHandler(typeof(Button),Mouse.MouseLeaveEvent,new MouseEventHandler(Hover));
  EventManager.RegisterClassHandler(typeof(ListBox),Selector.SelectionChangedEvent,new SelectionChangedEventHandler((sender,e)=>{if(current==null||!Enabled(current)||sender is not ListBox list)return;foreach(var added in e.AddedItems)if(list.ItemContainerGenerator.ContainerFromItem(added) is ListBoxItem item)item.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(0.65,1,TimeSpan.FromMilliseconds(240)));}));
 }
 public static bool Enabled(Settings s)=>!s.ReduceMotion&&SystemParameters.ClientAreaAnimation;
 public static void Enter(UIElement view,Settings settings,double opacity,double offset)
 {
  view.Opacity=1;
  var translate=new TranslateTransform();view.RenderTransform=translate;if(!Enabled(settings))return;
  view.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(opacity,1,TimeSpan.FromMilliseconds(400)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseInOut},FillBehavior=FillBehavior.Stop});
  translate.BeginAnimation(TranslateTransform.YProperty,new DoubleAnimation(opacity>0?offset:7,0,TimeSpan.FromMilliseconds(400)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseInOut},FillBehavior=FillBehavior.Stop});
 }
}
// Samples are observed without being modified. Bounded waveform history, no FFT workers or file rereads.
public sealed class AudioAnalysis
{
 readonly object gate=new();readonly float[] history=new float[128];int head;double energy;int count;public volatile bool Enabled;public long Blocks {get;private set;}
 public void Observe(float[] data,int offset,int samples,int channels)
 {
  if(!Enabled)return;
  for(int i=offset;i<offset+samples;i+=channels){double e=0;for(int c=0;c<channels&&i+c<offset+samples;c++)e+=data[i+c]*data[i+c];energy+=e/channels;if(++count<384)continue;var rms=(float)Math.Sqrt(energy/count);lock(gate){history[head]=rms;head=(head+1)%history.Length;Blocks++;}energy=0;count=0;}
 }
 public void Copy(float[] target){lock(gate){for(int i=0;i<target.Length;i++)target[i]=history[(head+i*history.Length/target.Length)%history.Length];}}
 public void Clear(){lock(gate){Array.Clear(history);head=0;}energy=0;count=0;}
}
sealed class AnalysisProvider : ISampleProvider
{
 readonly ISampleProvider source;readonly AudioAnalysis analysis;public AnalysisProvider(ISampleProvider source,AudioAnalysis analysis){this.source=source;this.analysis=analysis;}public WaveFormat WaveFormat=>source.WaveFormat;
 public int Read(float[] buffer,int offset,int count){var read=source.Read(buffer,offset,count);analysis.Observe(buffer,offset,read,WaveFormat.Channels);return read;}
}
public sealed class AudioWave : FrameworkElement
{
 readonly FrameTicker timer=new();readonly float[] values=new float[72],source=new float[72];readonly Pen pen=new(new SolidColorBrush(Color.FromArgb(220,200,175,233)),1.6){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};AudioAnalysis? analysis;bool playing;DateTime stopAt;
 readonly DrawingVisual waveform=new();
 public bool Running=>timer.IsEnabled;public long Frames {get;private set;}public long DrawFrames{get;private set;}
 internal static float Map(float rms)=>rms<=.00025f?0:(float)Math.Pow(Math.Clamp((20*Math.Log10(rms)+72)/54,0,1),.9);
 internal float Peak=>System.Linq.Enumerable.Max(values);
 public AudioWave(){IsHitTestVisible=false;AddVisualChild(waveform);pen.Freeze();timer.Tick+=(_,_)=>{if(playing)analysis?.Copy(source);else Array.Clear(source);for(int i=0;i<values.Length;i++){var target=Map(source[i]);values[i]+=(target-values[i])*(target>values[i]?.78f:.30f);}if(!playing&&DateTime.UtcNow>=stopAt){Array.Clear(values);timer.Stop();}DrawFrame();Frames++;};}
 protected override int VisualChildrenCount=>1;
 protected override Visual GetVisualChild(int index)=>index==0?waveform:throw new ArgumentOutOfRangeException(nameof(index));
 public void Configure(AudioAnalysis data,bool isPlaying,bool visible,bool enabled){analysis=data;if(playing&&!isPlaying)stopAt=DateTime.UtcNow.AddMilliseconds(450);playing=isPlaying;analysis.Enabled=visible&&enabled&&isPlaying;if(!visible||!enabled){timer.Stop();return;}if(isPlaying||DateTime.UtcNow<stopAt&&Array.Exists(values,x=>x>0.0001f))timer.Start();else timer.Stop();}
 protected override void OnRender(DrawingContext dc){base.OnRender(dc);DrawFrame();}
 void DrawFrame()
 {
  if(!IsVisible||ActualWidth<=0||ActualHeight<=0)return;
  var begin=DrawTiming.Begin();DrawFrames++;
  using(var dc=waveform.RenderOpen())
  {
   double step=ActualWidth/values.Length;
   for(int i=0;i<values.Length;i++){double height=Math.Min(ActualHeight,1+values[i]*(ActualHeight-1));var x=(i+.5)*step;dc.DrawLine(pen,new Point(x,(ActualHeight-height)/2),new Point(x,(ActualHeight+height)/2));}
  }
  DrawTiming.End("wave",begin);
 }
}
