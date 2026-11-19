using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace Nocturne;
public partial class MainWindow
{
 internal readonly VideoSession video=new();internal bool directVideo;int videoRequest;bool companionSync,hiddenVideoResume,videoSeeking;
 Window? fullscreen;Image? fullscreenImage;
 internal Task<bool> VideoReady {get;private set;}=Task.FromResult(false);
 internal double PlaybackPosition=>directVideo?video.Position:engine.Position;
 internal double PlaybackDuration=>directVideo?video.Duration:engine.Duration;
 internal bool PlaybackPlaying=>directVideo?video.Playing:engine.Playing;
 string? PlaybackPath=>directVideo?video.Path:engine.FilePath;
 internal bool VideoDrawingVisible=>VideoImage.Source!=null||fullscreenImage?.Source!=null;
 internal bool VideoFullscreen=>fullscreen!=null;
 internal IntPtr VideoFullscreenHandle=>fullscreen==null?IntPtr.Zero:new System.Windows.Interop.WindowInteropHelper(fullscreen).Handle;
 const string VideoFilter="本地视频|*.mp4;*.m4v;*.mov;*.wmv;*.avi;*.mkv|所有文件|*.*";
 void ChooseVideo(object sender,RoutedEventArgs e)
 {
  var shell=DialogShell.Create("打开本地视频",440,230);shell.Owner=this;var panel=new StackPanel{Margin=new Thickness(24)};shell.Content=panel;
  panel.Children.Add(new TextBlock{Text="选择播放方式",FontSize=22,Margin=new Thickness(0,0,0,16)});
  var direct=new Button{Content="直接播放视频（使用视频声音）"};direct.Click+=(_,_)=>{shell.Close();PickVideo(true);};panel.Children.Add(direct);
  var companion=new Button{Content="配套视频（静音，音频继续）",IsEnabled=engine.FilePath!=null&&!directVideo};companion.Click+=(_,_)=>{shell.Close();PickVideo(false);};panel.Children.Add(companion);shell.ShowDialog();
 }
 async void PickVideo(bool direct)
 {
  var picker=new OpenFileDialog{Title=direct?"直接播放视频":"打开静音配套视频",Filter=VideoFilter};
  if(picker.ShowDialog(this)==true){VideoReady=OpenVideo(picker.FileName,direct,true);await VideoReady;}
 }
 internal async Task<bool> OpenVideo(string path,bool direct,bool play,double? position=null,bool reveal=true)
 {
  if(!direct&&(engine.FilePath==null||directVideo)){Status.Text="请先播放音频，再选择配套视频。";return false;}
  var releasingAudio=direct&&engine.FilePath!=null;Save();if(reveal)settings.ShowVideo=true;if(direct)CancelFade();var navigationAtOpen=navigationVersion;var request=++videoRequest;ExitVideoFullscreen();video.Close();hiddenVideoResume=false;companionSync=false;VideoSync.IsChecked=false;
  if(direct){engine.Dispose();currentTrack=null;ClearSubtitles();}else if(engine.FilePath==null){Status.Text="请先播放音频，再选择配套视频。";return false;}
  directVideo=direct;UpdateVideoSurface();Status.Text="正在打开本地视频…";
  try
  {
   // WinMM releases its queued output asynchronously. Let it settle before the
   // system video graph opens its audio renderer, rather than after MediaOpened.
   if(releasingAudio)await Task.Delay(150);if(request!=videoRequest||closing)return false;
   await video.Open(path,!direct,settings.Volume);if(request!=videoRequest||closing)return false;
   var page=CurrentPage;var show=reveal&&navigationVersion==navigationAtOpen;
   if(direct)
   {
    currentTrack=path;CaptureQueue(path);UpdatePictures();lastTrackCandidate=path;ContinueButton.Visibility=Visibility.Visible;var p=Preference();p.LastPlayed=DateTimeOffset.Now;SelectFolder(Path.GetDirectoryName(path)!);
    video.Seek(position??p.Position??0);LoadSubtitleChoices();await SubtitleReady;if(request!=videoRequest||closing)return false;NowPlaying.Text=Path.GetFileNameWithoutExtension(path);LyricTrackName.Text=NowPlaying.Text;
    AudioInfo.Text=Path.GetExtension(path).TrimStart('.').ToUpperInvariant()+" · 视频原声";Progress.Maximum=Math.Max(1,video.Duration);Total.Text=Format(video.Duration);
   }
   VideoTitle.Text=Path.GetFileName(path)+(direct?" · 视频原声":" · 配套视频 / 静音");
   if(!direct){video.Seek(position??0);await LoadCompanionCaptions(path,request);}if(request!=videoRequest||closing)return false;
   VideoSync.Visibility=direct?Visibility.Collapsed:Visibility.Visible;VideoToggle.IsEnabled=true;VideoSeek.IsEnabled=true;
   Navigate(show?"lyrics":page);Status.Text=direct?"视频原声 · 字幕跟随视频时间":"音频保持原声 · 配套视频静音且独立播放";
   UpdateVideoSurface();
   // Connect the native drawing before starting its graph. Starting before the WPF render
   // target is attached can leave the independent MediaPlayer at its first frame.
   if(VideoDrawingVisible)await PresentedVideoFrame();
   if(request!=videoRequest||closing)return false;
   if(play){if(direct||VideoDrawingVisible){video.Play();if(!await video.ConfirmStarted())throw new IOException("视频解码器未能启动播放时钟，请重试或检查系统解码组件。");if(request!=videoRequest||closing)return false;}else hiddenVideoResume=true;}
   UpdateClock();UpdateTimer();QueueSave();return true;
  }
  catch(OperationCanceledException){return false;}
  catch(Exception ex){if(request==videoRequest){video.Close();directVideo=false;if(direct){currentTrack=null;ClearSubtitles();}Status.Text="无法播放视频，可能缺少系统解码器："+ex.Message;UpdateVideoSurface();UpdateTimer();}return false;}
 }
 async Task PresentedVideoFrame()
 {
  // A VideoDrawing hidden by the page's entry fade may not attach its native
  // presentation target on the first WPF render callback. Let that fade finish
  // before starting the graph; otherwise some system codecs stall at frame zero.
  UpdateLayout();
  for(int i=0;i<15&&!closing&&fullscreen==null&&VideoDrawingVisible&&(LyricsPage.Opacity<.995||VideoImage.ActualWidth<1||VideoImage.ActualHeight<1);i++)await Task.Delay(20);
  var completion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);EventHandler rendered=(_,_)=>completion.TrySetResult(true);
  CompositionTarget.Rendering+=rendered;try{await Task.WhenAny(completion.Task,Task.Delay(250));}finally{CompositionTarget.Rendering-=rendered;}
 }
 internal void CloseVideoSession(bool returnToImages=true)
 {
  Save();if(directVideo)CancelFade();++videoRequest;ExitVideoFullscreen();video.Close();hiddenVideoResume=false;companionSync=false;VideoSync.IsChecked=false;
  if(directVideo){directVideo=false;currentTrack=null;ClearSubtitles();NowPlaying.Text="已关闭视频";AudioInfo.Text="原声播放 · 本地收藏";Progress.Value=0;Elapsed.Text=Total.Text="00:00";}
  UpdateVideoSurface();if(returnToImages)Navigate(currentTrack==null?"home":"lyrics");UpdateTimer();
 }
 void CloseVideo(object sender,RoutedEventArgs e)=>CloseVideoSession();
 void ShowVideo(object sender,RoutedEventArgs e){if(video.Path==null)ChooseVideo(sender,e);else{Navigate("lyrics");UpdateVideoSurface();}}
 void PlayMaster(){CancelFade();if(currentTrack!=null)LoadSubtitleChoices();RawPlay();}
 void PauseMaster(){CancelFade();RawPause();}
 void SeekMaster(double seconds){var play=fadeDesired??PlaybackPlaying;CancelFade();seconds=Math.Clamp(seconds,0,PlaybackDuration);if(directVideo){video.Seek(seconds);if(play)video.Play(false);else video.Pause();}else engine.Seek(seconds,play);SynchronizeCompanion(true);}
 void VideoPlayPause(object sender,RoutedEventArgs e){hiddenVideoResume=false;if(directVideo||companionSync)TogglePlay(sender,e);else if(video.Playing)video.Pause();else video.Play();UpdateTimer();UpdateVideoControls();}
 void SyncVideo(object sender,RoutedEventArgs e){companionSync=VideoSync.IsChecked==true;VideoToggle.IsEnabled=VideoSeek.IsEnabled=true;if(companionSync)SynchronizeCompanion(true);}
 void SynchronizeCompanion(bool force=false)
 {
  if(!companionSync||directVideo||video.Path==null)return;
  if(video.Duration>0&&engine.Position>=video.Duration-.02){if(video.Position<video.Duration-.05)video.Seek(video.Duration);if(video.Playing)video.Pause();return;}
  if(force||Math.Abs(video.Position-engine.Position)>.25)video.Seek(engine.Position);
  if(engine.Playing&&!video.Playing&&!hiddenVideoResume)video.Play();else if(!engine.Playing&&video.Playing)video.Pause();
 }
 void VideoSeekKey(object sender,KeyEventArgs e){if(e.Key is Key.Left or Key.Right){if(directVideo||companionSync)Seek(PlaybackPosition+(e.Key==Key.Right?5:-3));else video.Seek(video.Position+(e.Key==Key.Right?5:-3));UpdateClock();e.Handled=true;}}
 void UpdateVideoControls()
 {
  if(video.Path==null)return;VideoPlayIcon.Kind=video.Playing?"Pause":"Play";VideoToggle.ToolTip=video.Playing?"暂停配套视频":"播放配套视频";System.Windows.Automation.AutomationProperties.SetName(VideoToggle,(string)VideoToggle.ToolTip);
  var master=directVideo||companionSync;var position=master?PlaybackPosition:video.Position;var duration=master?PlaybackDuration:video.Duration;
  VideoSeek.Maximum=Math.Max(1,duration);if(!videoSeeking)VideoSeek.Value=position;VideoTime.Text=Format(position)+" / "+Format(duration);
  if(fullscreenSeek!=null){fullscreenSeek.Maximum=Math.Max(1,duration);if(pointerSlider!=fullscreenSeek)fullscreenSeek.Value=position;if(fullscreenTime!=null)fullscreenTime.Text=Format(position)+" / "+Format(duration);if(fullscreenPlayIcon!=null)fullscreenPlayIcon.Kind=(master?PlaybackPlaying:video.Playing)?"Pause":"Play";}UpdateVideoSubtitle();
 }
 void UpdateVideoSurface()
 {
  if(VideoPane==null)return;ArrangeMedia();var has=video.Image!=null;
  var visible=has&&settings.ShowVideo&&IsVisible&&WindowState!=WindowState.Minimized&&CurrentPage=="lyrics"&&fullscreen==null;
  var fullVisible=fullscreen?.IsVisible==true&&fullscreen.WindowState!=WindowState.Minimized&&WindowState!=WindowState.Minimized;
  VideoImage.Source=visible?video.Image:null;if(fullscreenImage!=null)fullscreenImage.Source=has&&fullVisible?video.Image:null;
  var showing=visible||fullVisible;
  if(!directVideo&&has){if(!showing&&video.Playing){hiddenVideoResume=true;video.Pause();}else if(showing&&hiddenVideoResume){hiddenVideoResume=false;if(companionSync)SynchronizeCompanion(true);else video.Play();}}
  UpdateVideoControls();UpdateVideoSubtitle();
 }
 void VideoWidthChanged(object sender,System.Windows.Controls.Primitives.DragCompletedEventArgs e){settings.VideoWidth=Math.Clamp(VideoColumn.ActualWidth/Math.Max(1,LyricsColumn.ActualWidth),.6,3);QueueSave();}
 void FullscreenVideo(object sender,RoutedEventArgs e)=>EnterVideoFullscreen();
 internal void EnterVideoFullscreen()
 {
  if(video.Image==null||fullscreen!=null)return;
  fullscreen=CreateFullscreenView();
  fullscreen.PreviewKeyDown+=FullscreenKey;
  fullscreen.PreviewKeyUp+=PlayerKeyUp;fullscreen.Deactivated+=(_,_)=>spaceHeld=false;
  fullscreen.PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){ExitVideoFullscreen();e.Handled=true;}};
  fullscreen.Closed+=(_,_)=>{fullscreenIdle.Stop();fullscreenImage!.Source=null;fullscreenImage=null;fullscreenCaption=null;fullscreenSeek=null;fullscreenTime=null;fullscreenControls=null;fullscreenRoot=null;fullscreen=null;UpdateVisuals();UpdateTimer();UpdateClock();};
  VideoImage.Source=null;fullscreen.Show();fullscreen.WindowState=WindowState.Maximized;fullscreen.Activate();fullscreenSeek!.Focus();UpdateVisuals();UpdateTimer();ShowFullscreenControls();UpdateClock();
 }
 internal void ExitVideoFullscreen(){var w=fullscreen;if(w!=null)w.Close();}
 void ClearSubtitles(string hint="未找到匹配字幕"){document=new();lastIndices.Clear();lastSubtitle="";subtitleHint=hint;SubtitleLines.ItemsSource=null;NoLyrics.Visibility=Visibility.Visible;overlay.SetText("");UpdateFooterSubtitle();UpdateVideoSubtitle();}
}
