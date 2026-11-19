using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
namespace Nocturne;
public sealed class HalfViewport:IValueConverter
{
 public object Convert(object value,Type t,object p,CultureInfo c)=>value is double d?Math.Max(0,d/2):0;
 public object ConvertBack(object v,Type t,object p,CultureInfo c)=>Binding.DoNothing;
}
public sealed class StartupWindow:Window
{
 readonly TextBlock status=new(){Text="正在读取本地设置与媒体索引…",FontSize=12,Foreground=new SolidColorBrush(Color.FromRgb(181,187,214)),TextWrapping=TextWrapping.Wrap};readonly StarField stars=new();readonly FrameTicker motion=new();readonly System.Diagnostics.Stopwatch time=new();readonly TranslateTransform floatPortrait=new(),lineMotion=new();bool reduce;internal long MotionFrames {get;private set;}internal bool MotionRunning=>motion.IsEnabled;
 public StartupWindow()
 {
  Title=Brand.Name+" · 启动";Icon=Brand.Artwork;Width=640;Height=390;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;Foreground=Brushes.White;
  var outer=new Border{CornerRadius=new CornerRadius(24),Background=new SolidColorBrush(Color.FromRgb(15,19,38))};var g=new Grid{Clip=new RectangleGeometry(new Rect(0,0,640,390),24,24)};outer.Child=g;Content=outer;g.Children.Add(stars);
  g.Children.Add(new Image{Source=Brand.Artwork,Stretch=Stretch.Uniform,Width=310,Height=320,Margin=new Thickness(0,0,22,0),RenderTransform=floatPortrait,HorizontalAlignment=HorizontalAlignment.Right});
  g.Children.Add(new Border{Background=new LinearGradientBrush(new GradientStopCollection{new GradientStop(Color.FromArgb(230,15,19,38),0),new GradientStop(Color.FromArgb(210,15,19,38),.36),new GradientStop(Colors.Transparent,.76)},0)});
  var copy=new StackPanel{Width=260,Margin=new Thickness(34,0,0,34),VerticalAlignment=VerticalAlignment.Bottom,HorizontalAlignment=HorizontalAlignment.Left};copy.Children.Add(new TextBlock{Text=Brand.Name,FontFamily=FontBook.ArtWithFallback,FontSize=38,Foreground=new SolidColorBrush(Color.FromRgb(244,215,236))});copy.Children.Add(new TextBlock{Text="让声音，在夜里轻轻盛开。",FontSize=14,Margin=new Thickness(0,12,0,34)});copy.Children.Add(status);var rail=new Grid{Height=2,Width=218,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,18,0,0),ClipToBounds=true,Background=new SolidColorBrush(Color.FromRgb(48,44,67))};rail.Children.Add(new Border{Height=2,Width=54,HorizontalAlignment=HorizontalAlignment.Left,Background=new SolidColorBrush(Color.FromRgb(211,175,216)),RenderTransform=lineMotion,CornerRadius=new CornerRadius(1)});copy.Children.Add(rail);g.Children.Add(copy);
  motion.Tick+=(_,_)=>{var t=time.Elapsed.TotalSeconds;floatPortrait.Y=2*Math.Sin(t*1.4);lineMotion.X=82+82*Math.Sin(t*1.9-1.57);MotionFrames++;};Loaded+=(_,_)=>UpdateMotion();StateChanged+=(_,_)=>UpdateMotion();IsVisibleChanged+=(_,_)=>UpdateMotion();Closed+=(_,_)=>{motion.Stop();stars.Configure(0,false);};
 }
 public void SetReducedMotion(bool value,bool sakura=true,double strength=.65){reduce=value;stars.ConfigurePetals(sakura&&!value,strength);UpdateMotion();}
 void UpdateMotion(){var visible=IsVisible&&WindowState!=WindowState.Minimized;stars.Configure(reduce?0:2,visible);if(visible&&!reduce&&SystemParameters.ClientAreaAnimation){if(!time.IsRunning)time.Start();motion.Start();}else motion.Stop();}
 public void SetStatus(string text)=>status.Text=text;
}
