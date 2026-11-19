using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Nocturne;
public sealed class CoverView : Border
{
 static readonly ImageCache cache=new(24,360);
 internal static ImageCache Cache=>cache;internal static long Paints {get;private set;}
 static readonly Brush placeholder=new SolidColorBrush(Color.FromArgb(146,34,44,69));
 public static readonly DependencyProperty PathProperty=DependencyProperty.Register(nameof(Path),typeof(string),typeof(CoverView),new PropertyMetadata(null,(sender,_)=>((CoverView)sender).Load()));
 public string? Path{get=>(string?)GetValue(PathProperty);set=>SetValue(PathProperty,value);}
 readonly Image image=new(){Stretch=Stretch.Uniform,Margin=new Thickness(4),IsHitTestVisible=false};
 readonly Symbol empty=new(){Kind="Moon",Width=38,Height=38,Foreground=new SolidColorBrush(Color.FromRgb(104,116,147)),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
 CancellationTokenSource? pending;string? displayedPath;
 public CoverView(){CornerRadius=new CornerRadius(18);Background=placeholder;var grid=new Grid();grid.Children.Add(image);grid.Children.Add(empty);Child=grid;Loaded+=(_,_)=>Load();Unloaded+=(_,_)=>pending?.Cancel();}
 async void Load()
 {
  pending?.Cancel();pending?.Dispose();pending=null;
  if(!IsLoaded)return;var path=Path;if(string.IsNullOrEmpty(path)){displayedPath=null;image.Source=null;empty.Visibility=Visibility.Visible;return;}
  if(!string.Equals(displayedPath,path,StringComparison.OrdinalIgnoreCase)){image.Source=null;empty.Visibility=Visibility.Visible;}
  var cts=new CancellationTokenSource();pending=cts;
  try{var source=await cache.LoadAsync(path,cts.Token);if(cts.IsCancellationRequested||!IsLoaded||path!=Path)return;displayedPath=path;image.Source=source;empty.Visibility=Visibility.Collapsed;}
  catch(OperationCanceledException){}catch(Exception ex){ImageDiscovery.Log(path,ex);ToolTip="图片存在但读取失败："+ex.Message;}
 }
 protected override void OnRender(DrawingContext dc){base.OnRender(dc);Paints++;}
}
