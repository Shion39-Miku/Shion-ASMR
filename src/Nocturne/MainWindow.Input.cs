using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Nocturne;
public partial class MainWindow
{
 CancellationTokenSource? fadeCancel;bool? fadeDesired;double videoGain=1;long lastArrow;bool spaceHeld;
 internal Task FadeReady {get;private set;}=Task.CompletedTask;
 internal int LyricSeeks {get;private set;}
 internal readonly System.Collections.Generic.List<object> KeyboardTrace=new();
 void CancelFade(){fadeCancel?.Cancel();fadeCancel?.Dispose();fadeCancel=null;fadeDesired=null;engine.Fade(1);videoGain=1;if(directVideo)video.Volume(engine.Volume);}
 void RawPause(){if(directVideo)video.Pause();else engine.Pause();SynchronizeCompanion(true);}
 void RawPlay(){if(directVideo)video.Play();else engine.Play();SynchronizeCompanion(true);}
 internal void RequestPlayback(bool playing)=>FadeReady=TransitionPlayback(playing);
 async Task TransitionPlayback(bool playing)
 {
  fadeCancel?.Cancel();fadeCancel?.Dispose();var cancel=fadeCancel=new();var token=cancel.Token;fadeDesired=playing;
  if(!settings.PauseFade){CancelFade();if(playing)PlayMaster();else PauseMaster();UpdateClock();UpdateTimer();Save();return;}
  try
  {
   if(!playing&&!PlaybackPlaying){engine.Fade(0);videoGain=0;fadeDesired=null;UpdateClock();UpdateTimer();Save();return;}
   if(playing){if(currentTrack!=null)LoadSubtitleChoices();if(!PlaybackPlaying||PlaybackDuration>0&&PlaybackPosition>=PlaybackDuration-.05){engine.Fade(0);videoGain=0;if(directVideo)video.Volume(0);RawPlay();}}
   UpdateTimer();
   if(directVideo)
   {
    var start=videoGain;var target=playing?1d:0d;var watch=Stopwatch.StartNew();
    do{token.ThrowIfCancellationRequested();var t=Math.Clamp(watch.Elapsed.TotalMilliseconds/500,0,1);videoGain=start+(target-start)*t;video.Volume(engine.Volume*videoGain);if(t>=1)break;await Task.Delay(16,token);}while(true);
   }
   else{engine.Fade(playing?1:0,500);await engine.FadeComplete.WaitAsync(token);if(!playing)await Task.Delay(120,token);} // Allow queued WinMM buffers to drain the fade before pausing.
   token.ThrowIfCancellationRequested();if(!playing)RawPause();fadeDesired=null;UpdateClock();UpdateTimer();Save();
  }
  catch(OperationCanceledException){}
 }
 static bool TextEditing(DependencyObject? source)
 {
  while(source!=null){if(source is TextBoxBase or PasswordBox||source is ComboBox {IsEditable:true})return true;source=source is Visual||source is System.Windows.Media.Media3D.Visual3D?VisualTreeHelper.GetParent(source):LogicalTreeHelper.GetParent(source);}return false;
 }
 void PlayerKey(object sender,KeyEventArgs e)
 {
  if(testing&&e.Key==Key.Space&&KeyboardTrace.Count<40)KeyboardTrace.Add(new{eventType="down",e.IsRepeat,spaceHeld,playing=PlaybackPlaying,focus=Keyboard.FocusedElement?.GetType().Name});
  if(!(IsActive||fullscreen?.IsActive==true)||Keyboard.Modifiers!=ModifierKeys.None||TextEditing(Keyboard.FocusedElement as DependencyObject))return;
  if(e.Key is not (Key.Left or Key.Right or Key.Space or Key.Up or Key.Down))return;
  e.Handled=true;if(e.Key==Key.Space){if(!spaceHeld&&!e.IsRepeat){spaceHeld=true;TogglePlay(this,new());}return;}
  // Native key-repeat is accepted at a bounded rate; no delayed commands are queued.
  var now=Stopwatch.GetTimestamp();if(e.IsRepeat&&Stopwatch.GetElapsedTime(lastArrow,now).TotalMilliseconds<65)return;lastArrow=now;
  if(e.Key==Key.Left)Seek(Math.Clamp(PlaybackPosition-3,0,PlaybackDuration));
  else if(e.Key==Key.Right)Seek(Math.Clamp(PlaybackPosition+5,0,PlaybackDuration));
  else Volume.Value=Math.Clamp(Volume.Value+(e.Key==Key.Up?.05:-.05),0,1);
 }
 void PlayerKeyUp(object sender,KeyEventArgs e){if(e.Key==Key.Space){if(testing&&KeyboardTrace.Count<40)KeyboardTrace.Add(new{eventType="up",spaceHeld,playing=PlaybackPlaying});spaceHeld=false;}}
 Cue? lastClickedCue;int lastLyricTimestamp,lastCueDown;Point cueDownPoint;bool cueClickArmed,cueReleased,cueDragged;
 void CancelLyricClick(){cueClickArmed=false;lastClickedCue=null;}
 void LyricMouseScope(object sender,MouseButtonEventArgs e)
 {
  DependencyObject? source=e.OriginalSource as DependencyObject;while(source!=null){if(ReferenceEquals(source,SubtitleLines))return;source=source is Visual?VisualTreeHelper.GetParent(source):LogicalTreeHelper.GetParent(source);}CancelLyricClick();
 }
 void LyricPointerMoved(object sender,MouseEventArgs e)
 {
  if(!cueClickArmed||e.LeftButton!=MouseButtonState.Pressed)return;var point=e.GetPosition(SubtitleLines);
  if(Math.Abs(point.X-cueDownPoint.X)>SystemParameters.MinimumHorizontalDragDistance||Math.Abs(point.Y-cueDownPoint.Y)>SystemParameters.MinimumVerticalDragDistance)cueDragged=true;
 }
 void LyricReleased(object sender,MouseButtonEventArgs e){if(e.ChangedButton==MouseButton.Left){var point=e.GetPosition(SubtitleLines);if(Math.Abs(point.X-cueDownPoint.X)>SystemParameters.MinimumHorizontalDragDistance||Math.Abs(point.Y-cueDownPoint.Y)>SystemParameters.MinimumVerticalDragDistance)cueDragged=true;cueReleased=true;}}
 internal readonly System.Collections.Generic.List<object> LyricClickTrace=new();
 void LyricDoubleClick(object sender,MouseButtonEventArgs e)
 {
  if(e.ChangedButton!=MouseButton.Left)return;
  DependencyObject? source=e.OriginalSource as DependencyObject;
  while(source!=null&&source is not ListBoxItem){if(source is ScrollBar or Thumb){CancelLyricClick();return;}source=VisualTreeHelper.GetParent(source);}
  if(source is not ListBoxItem {DataContext:Cue cue}||cue.Spacer)return;
  if(testing&&LyricClickTrace.Count<80)LyricClickTrace.Add(new{e.ClickCount,e.Timestamp,cue=cue.Text,position=PlaybackPosition,last=lastClickedCue?.Text,point=e.GetPosition(SubtitleLines),cueDownPoint,cueClickArmed,cueReleased,cueDragged,elapsed=unchecked(e.Timestamp-lastCueDown)});
  if(lastLyricTimestamp==e.Timestamp)return;lastLyricTimestamp=e.Timestamp;
  var point=e.GetPosition(SubtitleLines);var elapsed=unchecked(e.Timestamp-lastCueDown);
  // WPF ClickCount can restart when glyph/border hit targets change after a row
  // selection. Recognize the same cue pair using OS timing/distance, and require
  // a completed, non-dragging first click. No global hook or delayed single-click.
  var size=System.Windows.Forms.SystemInformation.DoubleClickSize;var dpi=VisualTreeHelper.GetDpi(SubtitleLines);
  var pair=cueClickArmed&&cueReleased&&!cueDragged&&ReferenceEquals(lastClickedCue,cue)&&elapsed>0&&elapsed<=System.Windows.Forms.SystemInformation.DoubleClickTime&&Math.Abs(point.X-cueDownPoint.X)<=size.Width/dpi.DpiScaleX/2&&Math.Abs(point.Y-cueDownPoint.Y)<=size.Height/dpi.DpiScaleY/2;
  lastClickedCue=cue;lastCueDown=e.Timestamp;cueDownPoint=point;cueReleased=false;cueDragged=false;cueClickArmed=!pair;
  if(!pair)return;
  e.Handled=true;LyricSeeks++;Seek(Math.Clamp(cue.Start+Preference().Offset,0,PlaybackDuration));SetLyricsFollow(true);UpdateSubtitle(true);
 }
 void ShowFullName(object sender,RoutedEventArgs e)
 {
  if(sender is not MenuItem item||ItemsControl.ItemsControlFromItemContainer(item) is not ContextMenu menu||menu.PlacementTarget is not FrameworkElement target)return;
  var name=target.DataContext switch{BrowserTile t=>t.Name,TreeEntry t=>t.Name,FileItem f=>f.Name,_=>target.DataContext?.ToString()??""};
  var window=DialogShell.Create("完整名称",600,220);window.Owner=this;var panel=new StackPanel{Margin=new Thickness(24)};window.Content=panel;panel.Children.Add(new TextBox{Text=name,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,MaxHeight=110,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});var done=new Button{Content="关闭"};done.Click+=(_,_)=>window.Close();panel.Children.Add(done);window.ShowDialog();
 }
}
