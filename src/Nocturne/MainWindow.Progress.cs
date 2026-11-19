using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Nocturne;
public partial class MainWindow
{
 Slider? pointerSlider;double pointerValue;long lastPointerSeek;
 internal int PointerSeeks {get;private set;}
 internal readonly System.Collections.Generic.List<object> PointerTrace=new();
 bool MasterSlider(Slider slider)=>slider==Progress||directVideo||companionSync;
 // Use WPF track coordinates (DIP), not screen pixels or the enclosing panel width.
 internal static double ValueAtPointer(Slider slider,Point trackPoint)
 {
  var track=(Track)slider.Template.FindName("PART_Track",slider);
  var thumb=track.Thumb?.ActualWidth??0;var fraction=Math.Clamp((trackPoint.X-thumb/2)/Math.Max(1,track.ActualWidth-thumb),0,1);
  if(slider.IsDirectionReversed)fraction=1-fraction;
  return slider.Minimum+fraction*(slider.Maximum-slider.Minimum);
 }
 void BeginPointerSeek(Slider slider,MouseButtonEventArgs e)
 {
  if(MasterSlider(slider)?PlaybackPath==null||PlaybackDuration<=0:video.Path==null||video.Duration<=0)return;
  e.Handled=true;pointerSlider=slider;seeking=MasterSlider(slider);videoSeeking=slider==VideoSeek;
  slider.Focus();slider.CaptureMouse();MovePointerSeek(slider,e,true);
 }
 void MovePointerSeek(Slider slider,MouseEventArgs e,bool force=false)
 {
  if(pointerSlider!=slider)return;
  var track=(Track)slider.Template.FindName("PART_Track",slider);pointerValue=ValueAtPointer(slider,e.GetPosition(track));slider.Value=pointerValue;
  if(MasterSlider(slider))Elapsed.Text=Format(pointerValue);else VideoTime.Text=Format(pointerValue)+" / "+Format(video.Duration);
  var now=Stopwatch.GetTimestamp();if(!force&&Stopwatch.GetElapsedTime(lastPointerSeek,now).TotalMilliseconds<50)return;
  CommitPointerSeek();lastPointerSeek=now;e.Handled=true;
 }
 void CommitPointerSeek()
 {
  if(pointerSlider==null)return;if(MasterSlider(pointerSlider)){Seek(pointerValue);Progress.Value=PlaybackPosition;Elapsed.Text=Format(PlaybackPosition);}
  else{video.Seek(pointerValue);UpdateClock();VideoTime.Text=Format(video.Position)+" / "+Format(video.Duration);}
  PointerSeeks++;
  if(testing&&PointerTrace.Count<120)PointerTrace.Add(new{slider=pointerSlider?.Name,target=pointerValue,actual=PlaybackPosition,video=video.Position,seeking,captured=Mouse.Captured is FrameworkElement element?element.Name:null});
 }
 void EndPointerSeek(Slider slider,MouseButtonEventArgs? e=null)
 {
  if(pointerSlider!=slider)return;
  if(e!=null){MovePointerSeek(slider,e,true);e.Handled=true;}else CommitPointerSeek();
  pointerSlider=null;seeking=videoSeeking=false;slider.ReleaseMouseCapture();UpdateClock();
 }
 void SeekStart(object sender,MouseButtonEventArgs e)=>BeginPointerSeek(Progress,e);
 void SeekMove(object sender,MouseEventArgs e){if(pointerSlider==Progress&&e.LeftButton==MouseButtonState.Pressed)MovePointerSeek(Progress,e);}
 void SeekEnd(object sender,MouseButtonEventArgs e)=>EndPointerSeek(Progress,e);
 void SeekLostCapture(object sender,MouseEventArgs e)=>EndPointerSeek(Progress);
 void VideoSeekStart(object sender,MouseButtonEventArgs e)=>BeginPointerSeek(VideoSeek,e);
 void VideoSeekMove(object sender,MouseEventArgs e){if(pointerSlider==VideoSeek&&e.LeftButton==MouseButtonState.Pressed)MovePointerSeek(VideoSeek,e);}
 void VideoSeekEnd(object sender,MouseButtonEventArgs e)=>EndPointerSeek(VideoSeek,e);
 void VideoSeekLostCapture(object sender,MouseEventArgs e)=>EndPointerSeek(VideoSeek);
}
