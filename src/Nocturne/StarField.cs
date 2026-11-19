using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
namespace Nocturne;
public sealed class StarField : FrameworkElement
{
 readonly (double X,double Y,double R,double Speed,double Period,double Offset,double Light)[] stars=new (double,double,double,double,double,double,double)[92];
 readonly FrameTicker timer=new();readonly Stopwatch clock=new();readonly Brush[] palette=new Brush[48];double phase,intensity=1;bool petals=true;double petalStrength=.65;static readonly Geometry petal=MakePetal();static readonly Brush petalInk=MakePetalInk();
 readonly DrawingVisual animated=new();
 static readonly Brush clouds=CloudLayer();
 static readonly Brush violet=Nebula(Color.FromArgb(40,122,92,174)),pink=Nebula(Color.FromArgb(24,209,126,172));
 static Brush CloudLayer()
 {
  var group=new DrawingGroup();group.Children.Add(new GeometryDrawing(Brushes.Transparent,null,new RectangleGeometry(new Rect(0,0,1,1))));
  for(int i=0;i<18;i++){var j=i%9;var x=i<9?.01+j*.026:.78+j*.027;var y=i<9?.87-j*.026:.35-j*.018;var b=new RadialGradientBrush(Color.FromArgb((byte)(12+j%3*4),152,112,199),Colors.Transparent);b.Freeze();group.Children.Add(new GeometryDrawing(b,null,new EllipseGeometry(new Point(x,y),.045+(j%3)*.018,.05+(j%4)*.014)));}group.Freeze();var brush=new DrawingBrush(group){ViewboxUnits=BrushMappingMode.Absolute,Viewbox=new Rect(0,0,1,1),Stretch=Stretch.Fill};RenderOptions.SetCachingHint(brush,CachingHint.Cache);brush.Freeze();return brush;
 }
 static Brush Nebula(Color color){var brush=new RadialGradientBrush(color,Colors.Transparent){RadiusX=.5,RadiusY=.5};brush.Freeze();return brush;}
 public bool Running=>timer.IsEnabled;public long Frames {get;private set;}public long DrawFrames{get;private set;}internal double Phase=>phase;
 public StarField(){IsHitTestVisible=false;AddVisualChild(animated);var random=new Random(842);for(int i=0;i<stars.Length;i++)stars[i]=(random.NextDouble(),random.NextDouble(),.6+random.NextDouble()*1.2,.5+random.NextDouble()*1.2,4+random.NextDouble()*9,random.NextDouble()*Math.PI*2,.35+random.NextDouble()*.65);for(int i=0;i<palette.Length;i++){var b=new SolidColorBrush(Color.FromArgb((byte)(i*4),184,197,237));b.Freeze();palette[i]=b;}timer.Tick+=(_,_)=>{phase+=Math.Min(.3,clock.Elapsed.TotalSeconds);clock.Restart();Frames++;DrawFrame();};}
 protected override int VisualChildrenCount=>1;
 protected override Visual GetVisualChild(int index)=>index==0?animated:throw new ArgumentOutOfRangeException(nameof(index));
 static Geometry MakePetal(){var g=Geometry.Parse("M0,-5 C7,-6 7,2 0,6 C-6,2 -6,-4 0,-5 Z");g.Freeze();return g;}
 static Brush MakePetalInk(){var b=new SolidColorBrush(Color.FromArgb(85,247,177,207));b.Freeze();return b;}
 public void ConfigurePetals(bool enabled,double strength){petals=enabled;petalStrength=Math.Clamp(strength,.2,1.5);}
 void DrawPetals(DrawingContext dc)
 {
  if(!petals)return;var count=Math.Clamp((int)Math.Round(petalStrength*6),1,9);
  for(int i=0;i<count;i++){var y=(i*.173+phase*(.012+i*.0007))%1;var edge=.035+(i%3)*.035+.014*Math.Sin(phase*.35+i);var x=(i%2==0?edge:1-edge)*ActualWidth;var scale=.6+(i%4)*.16;var m=Matrix.Identity;m.Scale(scale,scale);m.Rotate(i*57+phase*(7+i));m.Translate(x,y*ActualHeight);dc.PushTransform(new MatrixTransform(m));dc.DrawGeometry(petalInk,null,petal);dc.Pop();}
 } public void Configure(int mode,bool visible,double strength=1){intensity=Math.Clamp(strength,.25,1.6);bool animate=mode>=1&&visible&&SystemParameters.ClientAreaAnimation;if(animate){timer.Interval=TimeSpan.FromMilliseconds(mode==1?200:1000d/60);if(!timer.IsEnabled){clock.Restart();timer.Start();}}else{timer.Stop();clock.Stop();}if(visible)DrawFrame();}
 internal (double x,double light) Sample(int index){var s=stars[index];return((s.X+phase*.003*s.Speed)%1,s.Light*(.72+.28*Math.Sin(phase*Math.PI*2/s.Period+s.Offset)));}
 protected override void OnRender(DrawingContext dc){base.OnRender(dc);dc.DrawRectangle(clouds,null,new Rect(0,0,ActualWidth,ActualHeight));dc.DrawEllipse(violet,null,new Point(ActualWidth*.82,ActualHeight*.24),ActualWidth*.5,ActualHeight*.6);dc.DrawEllipse(pink,null,new Point(ActualWidth*.08,ActualHeight*.8),ActualWidth*.45,ActualHeight*.6);DrawFrame();}
 void DrawFrame()
 {
  if(!IsVisible||ActualWidth<=0||ActualHeight<=0)return;
  var begin=DrawTiming.Begin();DrawFrames++;
  using(var dc=animated.RenderOpen())
  {
   for(int i=0;i<stars.Length;i++){var s=stars[i];var (x,light)=Sample(i);var y=(s.Y+phase*.00065*s.Speed)%1;var brush=palette[Math.Clamp((int)(light*intensity*43),3,47)];dc.DrawEllipse(brush,null,new Point(x*ActualWidth,y*ActualHeight),s.R,s.R);}
   DrawPetals(dc);
  }
  DrawTiming.End("stars",begin);
 }
}
