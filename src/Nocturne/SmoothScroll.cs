using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Nocturne;
// One short-lived integrator per viewport. New input replaces its target; no animation queue.
public static class SmoothScroll
{
 public static readonly DependencyProperty EnabledProperty=DependencyProperty.RegisterAttached("Enabled",typeof(bool),typeof(SmoothScroll),new PropertyMetadata(false,Changed));
 static readonly DependencyProperty StateProperty=DependencyProperty.RegisterAttached("State",typeof(ScrollState),typeof(SmoothScroll));
 public static void SetEnabled(DependencyObject o,bool value)=>o.SetValue(EnabledProperty,value);public static bool GetEnabled(DependencyObject o)=>(bool)o.GetValue(EnabledProperty);
 static void Changed(DependencyObject d,DependencyPropertyChangedEventArgs e){if(d is FrameworkElement f&&(bool)e.NewValue){var state=new ScrollState(f);f.SetValue(StateProperty,state);}}
 public static void Stop(DependencyObject o){if(o.GetValue(StateProperty) is ScrollState s)s.Stop();}
 public static void ToTop(DependencyObject o){if(o.GetValue(StateProperty) is ScrollState s)s.ToTop();}
 public static bool Running(DependencyObject o)=>o.GetValue(StateProperty) is ScrollState s&&s.Running;
 public static void Follow(ListBox list,object item,bool animate)
 {
  if(list.GetValue(StateProperty) is not ScrollState state||!animate){if(list.GetValue(StateProperty) is ScrollState direct){direct.Follow(list,item,false);}else list.ScrollIntoView(item);return;}
  state.Follow(list,item);
 }
 public static bool CenterNow(ListBox list,object item)
 {
  if(list.GetValue(StateProperty) is not ScrollState state)return false;
  state.Follow(list,item,false,false);list.UpdateLayout();
  var viewer=ScrollState.Find(list);var row=list.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
  if(viewer==null||row==null||viewer.ViewportHeight<1||row.ActualHeight<1)return false;
  var bounds=row.TransformToAncestor(viewer).TransformBounds(new Rect(0,0,row.ActualWidth,row.ActualHeight));
  return Math.Abs(bounds.Top+row.ActualHeight/2-viewer.ViewportHeight/2)<1.5;
 }
 internal static Action<int,double,double,string>? Trace;
 sealed class ScrollState
 {
  readonly FrameworkElement owner;readonly FrameTicker timer=new();readonly Stopwatch clock=new();ScrollViewer? viewer;double target;int direction,followVersion;bool applying,wired;
  public bool Running=>timer.IsEnabled;
  public ScrollState(FrameworkElement owner){this.owner=owner;owner.PreviewMouseWheel+=Wheel;owner.Unloaded+=(_,_)=>Stop();owner.IsVisibleChanged+=(_,_)=>{if(!owner.IsVisible)Stop();};timer.Tick+=(_,_)=>Tick();owner.Loaded+=(_,_)=>{viewer=Find(owner);if(viewer!=null&&!wired){wired=true;viewer.ScrollChanged+=(_,e)=>{if(!applying&&(e.ExtentHeightChange!=0||e.ViewportHeightChange!=0)){target=Math.Clamp(target,0,viewer.ScrollableHeight);}};}};}
  internal static ScrollViewer? Find(DependencyObject p){for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++){var c=VisualTreeHelper.GetChild(p,i);if(c is ScrollViewer s)return s;var found=Find(c);if(found!=null)return found;}return null;}
  void Wheel(object sender,MouseWheelEventArgs e)
  {
   viewer??=Find(owner);if(viewer==null||viewer.ScrollableHeight<=0)return;
   Trace?.Invoke(e.Delta,viewer.VerticalOffset,target,"input");
   // A nested scrollable owns its own event. Empty/boundary viewports let the event bubble.
   var origin=e.OriginalSource as DependencyObject;while(origin!=null&&origin!=viewer){if(origin is ScrollViewer nested&&nested.ScrollableHeight>0)return;origin=VisualTreeHelper.GetParent(origin);}
   int sign=Math.Sign(e.Delta);if((viewer.VerticalOffset<=.1&&sign>0)||(viewer.VerticalOffset>=viewer.ScrollableHeight-.1&&sign<0)){Stop();return;}
   if(!timer.IsEnabled||direction!=sign)target=viewer.VerticalOffset;direction=sign;
   var lines=SystemParameters.WheelScrollLines;var distance=lines<0?viewer.ViewportHeight:Math.Max(1,lines)*18;
   target=Math.Clamp(target-e.Delta/120.0*distance,0,viewer.ScrollableHeight);e.Handled=true;Trace?.Invoke(e.Delta,viewer.VerticalOffset,target,"target");
   if(!SystemParameters.ClientAreaAnimation||Application.Current.MainWindow is MainWindow w&&w.settings.ReduceMotion){viewer.ScrollToVerticalOffset(target);return;}
   if(!timer.IsEnabled){clock.Restart();timer.Start();}
  }
  void Tick(){if(viewer==null||!owner.IsVisible){Stop();return;}var dt=Math.Clamp(clock.Elapsed.TotalSeconds,.001,.05);clock.Restart();var current=viewer.VerticalOffset;var next=current+(target-current)*(1-Math.Exp(-dt/.055));if(Math.Abs(target-next)<.4){next=target;timer.Stop();}applying=true;viewer.ScrollToVerticalOffset(next);applying=false;}
  public void Stop(){timer.Stop();direction=0;followVersion++;}
  public void ToTop(){Stop();viewer??=Find(owner);viewer?.ScrollToTop();target=0;}
  public void Follow(ListBox list,object item,bool animate=true,bool refine=true)
  {
   viewer??=Find(owner);if(viewer==null)return;var before=viewer.VerticalOffset;var row=list.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;
   if(row==null){list.ScrollIntoView(item);list.UpdateLayout();row=list.ItemContainerGenerator.ContainerFromItem(item) as FrameworkElement;}if(row==null)return;
   var bounds=row.TransformToAncestor(viewer).TransformBounds(new Rect(0,0,row.ActualWidth,row.ActualHeight));var next=Math.Clamp(viewer.VerticalOffset+bounds.Top+row.ActualHeight/2-viewer.ViewportHeight/2,0,viewer.ScrollableHeight);
   if(timer.IsEnabled&&Math.Abs(target-next)<.75)return;
   var version=++followVersion;if(refine)_=list.Dispatcher.BeginInvoke(()=>{if(version==followVersion)Follow(list,item,animate,false);},DispatcherPriority.ContextIdle);
   if(Math.Abs(next-before)<.75){timer.Stop();return;}
   target=next;direction=0;if(!animate||Math.Abs(next-before)>viewer.ViewportHeight*1.5){timer.Stop();viewer.ScrollToVerticalOffset(next);return;}
   if(Math.Abs(viewer.VerticalOffset-before)>.75)viewer.ScrollToVerticalOffset(before);if(!timer.IsEnabled){clock.Restart();timer.Start();}
  }
 }
}
