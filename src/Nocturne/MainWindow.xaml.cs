using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Automation;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Nocturne;
public sealed record FileItem(string Path,double Seconds=0)
{
 public string Name=>System.IO.Path.GetFileName(Path);public string Hint=>System.IO.Path.GetExtension(Path).TrimStart('.').ToUpperInvariant();
 public string ProgressText=>Seconds>0?TimeSpan.FromSeconds(Seconds).ToString(@"h\:mm\:ss"):"";public override string ToString()=>Name;
}
public sealed record HomeRow(List<BrowserTile> Entries,int Columns);
public sealed record GroupItem(Folder Folder,string Name);
public partial class MainWindow : Window
{
 internal readonly Settings settings;internal readonly Library library;internal readonly AudioEngine engine=new();internal readonly OverlayWindow overlay;
 internal readonly ImageCache imageCache=new();
 readonly FrameTicker sync=new();readonly DispatcherTimer searchTimer=new(){Interval=TimeSpan.FromMilliseconds(250)},saveTimer=new(){Interval=TimeSpan.FromSeconds(12)},saveDebounce=new(){Interval=TimeSpan.FromMilliseconds(700)};
 readonly CancellationTokenSource cancellation=new();TrayIcon? tray;
 bool ready,loadingSubtitle,seeking,highlighting,closing,dirty,selectingGroup;readonly bool testing;int navigationVersion,homeColumns;
 internal List<Work> works=new();internal Work? currentWork;internal string folder="";internal string? currentTrack;internal SubtitleDocument document=new();
 internal string CurrentPage {get;private set;}="landing";public Task ImageReady {get;private set;}=Task.CompletedTask;
 internal bool ShuttingDown=>closing;public event Action? StartupCompleted;
 internal string TrayTitle=>tray?.Text??"";
 public MainWindow(bool test=false)
 {
  testing=test;settings=State.BootSettings??State.Read<Settings>("settings.json");State.BootSettings=null;settings.Migrate();library=new Library();InitializeComponent();NativeTheme.Attach(this);Motion.Use(settings);
  Width=Math.Clamp(settings.Width,820,3000);Height=Math.Clamp(settings.Height,680,2000);Left=settings.Left;Top=settings.Top;
  overlay=new OverlayWindow(settings);overlay.Command+=OverlayCommand;overlay.ConfigurationChanged+=()=>{if(!closing){QueueSave();UpdateOverlayButtons();}};InitializePlaybackUI();Volume.Value=settings.Volume;engine.Volume=(float)settings.Volume;LoopChoice.SelectedIndex=Math.Clamp(settings.Loop,0,3);EncodingChoice.SelectedIndex=0;
  SubtitleLines.AddHandler(Mouse.PreviewMouseUpEvent,new MouseButtonEventHandler(LyricReleased),true);SubtitleLines.AddHandler(Mouse.PreviewMouseMoveEvent,new MouseEventHandler(LyricPointerMoved),true);PreviewMouseDown+=LyricMouseScope;PreviewKeyDown+=PlayerKey;PreviewKeyUp+=PlayerKeyUp;Deactivated+=(_,_)=>{spaceHeld=false;CancelLyricClick();if(ready){UpdateVisuals();UpdateTimer();}};Activated+=(_,_)=>{if(ready){UpdateVisuals();UpdateTimer();UpdateClock();UpdateSubtitle(true);}};VolumeText.Text=$"{Volume.Value*100:0}%";ApplyTypography();SubtitleLines.ItemContainerGenerator.StatusChanged+=(_,_)=>RefreshLyricHighlights(ActivePlaybackCues());SubtitleLines.AddHandler(ScrollViewer.ScrollChangedEvent,new ScrollChangedEventHandler((_,e)=>{if(e.VerticalChange!=0||e.ExtentHeightChange!=0)RefreshLyricHighlights(ActivePlaybackCues());}));SourceInitialized+=(_,_)=>InitializeTray();
  sync.Tick+=(_,_)=>UpdateClock();searchTimer.Tick+=async(_,_)=>{searchTimer.Stop();await RenderHome();PopulateTracks();};saveTimer.Tick+=(_,_)=>{if(PlaybackPlaying||dirty)Save();};saveDebounce.Tick+=(_,_)=>{saveDebounce.Stop();Save();};
  video.Ended+=error=>{if(directVideo)OnEnded(error);else UpdateTimer();};
  engine.Ended+=error=>{var generation=engine.Generation;Dispatcher.BeginInvoke(()=>{if(!directVideo&&engine.Generation==generation)OnEnded(error);});};library.Changed+=()=>Dispatcher.BeginInvoke(async()=>{if(!closing)await Scan(false);});
  Loaded+=async(_,_)=>{OverlayWindow.ClampToScreens(this);Navigate("landing");await BuildTree();StartupCompleted?.Invoke();if(!test)library.Watch(settings.Roots);UpdateVisuals();UpdateTimer();if(!test){saveTimer.Start();if(!File.Exists(Path.Combine(State.Home,"video-index-v1")))await Scan(false);}};
  StateChanged+=(_,_)=>{UpdateVisuals();UpdateTimer();if(WindowState!=WindowState.Minimized)UpdateSubtitle(true);else{SmoothScroll.Stop(SubtitleLines);SmoothScroll.Stop(WorksList);SmoothScroll.Stop(CircleChoices);}};IsVisibleChanged+=(_,_)=>{UpdateVisuals();UpdateTimer();};overlay.IsVisibleChanged+=(_,_)=>{UpdateOverlayButtons();UpdateTimer();};
  Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplayChanged;
  Closed+=(_,_)=>{closing=true;CancelFade();cancellation.Cancel();subtitleCancel?.Cancel();SmoothScroll.Stop(WorksList);overlay.Save();sync.Stop();saveTimer.Stop();searchTimer.Stop();saveDebounce.Stop();library.Dispose();engine.Analysis.Enabled=false;Wave.Configure(engine.Analysis,false,false,false);++videoRequest;ExitVideoFullscreen();video.Dispose();engine.Dispose();Stars.Configure(0,false);overlay.Finish();tray?.Dispose();Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplayChanged;};ready=true;
 }
 void DisplayChanged(object? sender,EventArgs e)=>Dispatcher.BeginInvoke(()=>{OverlayWindow.ClampToScreens(this);OverlayWindow.ClampToScreens(overlay);});
 void RestoreMain(){if(!IsVisible)Show();if(WindowState==WindowState.Minimized)WindowState=WindowState.Normal;Activate();UpdateClock();UpdateSubtitle(true);}
 void MinimizeWindow(object sender,RoutedEventArgs e)=>WindowState=WindowState.Minimized;
 void MaximizeWindow(object sender,RoutedEventArgs e)=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
 void CloseWindow(object sender,RoutedEventArgs e)=>Close();
 void QueueSave(){dirty=true;if(testing)return;saveDebounce.Stop();saveDebounce.Start();}
 internal void Save()
 {
  if(!ready)return;saveDebounce.Stop();SaveResume();var r=WindowState==WindowState.Normal?new Rect(Left,Top,ActualWidth,ActualHeight):RestoreBounds;
  settings.Left=r.Left;settings.Top=r.Top;settings.Width=r.Width;settings.Height=r.Height;settings.Volume=engine.Volume;overlay.Save();State.Write("settings.json",settings);dirty=false;
 }
 void SaveResume()
 {
  if(currentTrack==null||PlaybackPath!=currentTrack)return;var p=Preference();p.Position=PlaybackPosition;
  settings.Resume[Path.GetDirectoryName(currentTrack)!]=new ResumePoint{Track=currentTrack,Seconds=PlaybackPosition};settings.LastFolder=Path.GetDirectoryName(currentTrack)!;dirty=true;
 }
 internal void Navigate(string page)
 {
  if(page==CurrentPage){if(page=="lyrics"&&currentTrack!=null)BeginLyricsEntry();UpdateTimer();return;}
  if(page=="lyrics")lyricsOrigin=CaptureBrowse();var version=++navigationVersion;var next=page=="landing"?LandingPage:page=="home"?HomePage:page=="lyrics"?LyricsPage:DetailPage;
  foreach(var panel in new[]{LandingPage,HomePage,DetailPage,LyricsPage})
  {
   panel.BeginAnimation(OpacityProperty,null);
   if(panel==next){panel.Visibility=Visibility.Visible;Motion.Enter(panel,settings);Panel.SetZIndex(panel,1);}
   else if(panel.Visibility==Visibility.Visible&&Motion.Enabled(settings)&&WindowState!=WindowState.Minimized)
   {
    Panel.SetZIndex(panel,0);var old=panel;var fade=new DoubleAnimation(panel.Opacity,0,TimeSpan.FromMilliseconds(400));fade.Completed+=(_,_)=>{if(version==navigationVersion){old.Visibility=Visibility.Collapsed;old.BeginAnimation(OpacityProperty,null);old.Opacity=1;}};panel.BeginAnimation(OpacityProperty,fade);
   }else panel.Visibility=Visibility.Collapsed;
   panel.IsHitTestVisible=panel==next;
  }
  CurrentPage=page;NavigationBar.Visibility=page=="landing"?Visibility.Collapsed:Visibility.Visible;NavigationRow.Height=new GridLength(page=="landing"?0:62);if(page=="lyrics"&&currentTrack!=null){BeginLyricsEntry();UpdatePictures();}else CancelLyricsEntry();UpdateVideoSurface();UpdateTimer();
 }
 void GoHome(object sender,RoutedEventArgs e){Navigate("landing");}
 internal bool OpenTrack(string path,bool play,double? position=null,bool revealVideo=true)
 {
  if(!File.Exists(path)){Status.Text="未找到文件，请检查磁盘连接或刷新媒体文件夹。";return false;}
  if(!Library.AudioTypes.Contains(Path.GetExtension(path))&&!Library.VideoTypes.Contains(Path.GetExtension(path))){Status.Text="不支持此格式，请选择 WAV、MP3、FLAC、M4A、OGG 或常见视频文件。";return false;}
  if(Library.VideoTypes.Contains(Path.GetExtension(path))){VideoReady=OpenVideo(path,true,play,position,revealVideo);return true;}
  Save();CancelFade();++videoRequest;ExitVideoFullscreen();video.Close();directVideo=false;hiddenVideoResume=false;companionSync=false;VideoSync.IsChecked=false;UpdateVideoSurface();try
  {
   var page=CurrentPage;var target=Path.GetDirectoryName(path)!;if(!settings.Tracks.TryGetValue(path,out var preference))settings.Tracks[path]=preference=new();
   var seconds=position??preference.Position??(settings.Resume.TryGetValue(target,out var legacy)&&legacy.Track==path?legacy.Seconds:0);
   engine.Open(path,seconds);currentTrack=path;CaptureQueue(path);lastTrackCandidate=path;ContinueButton.Visibility=Visibility.Visible;preference.LastPlayed=DateTimeOffset.Now;
   if(CurrentPage=="home")RememberBrowse();SelectFolder(target);Navigate(play&&revealVideo?"lyrics":page);Preference().LyricsOpen=play&&revealVideo;LoadSubtitleChoices();NowPlaying.Text=Path.GetFileNameWithoutExtension(path);LyricTrackName.Text=NowPlaying.Text;
   AudioInfo.Text=$"{Path.GetExtension(path).TrimStart('.').ToUpperInvariant()} · {engine.Channels} 声道 · 原声";Status.Text="正在收听 · "+(currentWork?.Name??Path.GetFileName(target));
   Progress.Maximum=Math.Max(1,engine.Duration);Total.Text=Format(engine.Duration);if(play)engine.Play();
   UpdatePictures();UpdateClock();ApplyLiveSettings();AutoVideoReady=OpenMatchingVideo(path);QueueSave();return true;
  }
  catch(Exception ex){currentTrack=null;ClearSubtitles();NowPlaying.Text="无法播放";Status.Text="无法播放此文件，请检查文件或 Windows 解码组件："+ex.Message;UpdateTimer();return false;}
 }
 void TogglePlay(object sender,RoutedEventArgs e)
 {
  if(Tracks.SelectedItem is FileItem chosen&&chosen.Path!=currentTrack&&CurrentPage=="detail"){OpenTrack(chosen.Path,true);return;}
  if(PlaybackPath==null){if(Tracks.SelectedItem is FileItem file)OpenTrack(file.Path,true);else ContinueListening(sender,e);return;}
  RequestPlayback(!(fadeDesired??PlaybackPlaying));
 }
 internal void PausePlayback(){PauseMaster();Save();UpdateClock();UpdateTimer();}
 void PreviousTrack(object sender,RoutedEventArgs e)=>Step(-1,false);
 void NextTrack(object sender,RoutedEventArgs e)=>Step(1,false);
 void Step(int delta,bool automatic)
 {
  if(currentTrack==null||!library.Folders.TryGetValue(Path.GetDirectoryName(currentTrack)!,out var f)||f.Audio.Count==0){PauseMaster();Save();UpdateTimer();UpdateClock();return;}
  if(queueFolder!=Path.GetDirectoryName(currentTrack)||!playbackQueue.Contains(currentTrack))CaptureQueue(currentTrack);var queue=playbackQueue;int index=queue.FindIndex(x=>x.Equals(currentTrack,StringComparison.OrdinalIgnoreCase))+delta;
  if(index<0||index>=queue.Count){if(settings.Loop==1||!automatic)index=(index+queue.Count)%queue.Count;else{PauseMaster();Save();UpdateTimer();UpdateClock();return;}}
  var page=CurrentPage;OpenTrack(queue[index],true,automatic?0:null,!automatic);if(automatic)Navigate(page);
 }
 void OnEnded(Exception? ex){if(closing)return;if(ex!=null){PauseMaster();Status.Text="媒体设备或解码发生错误："+ex.Message;Save();UpdateTimer();return;}if(settings.Loop==2){SeekMaster(0);PlayMaster();UpdateTimer();}else Step(1,true);}
 internal void Seek(double value){SeekMaster(value);if(CurrentPage=="lyrics")BeginLyricsEntry();UpdateClock();UpdateTimer();QueueSave();}
 void SeekKey(object sender,KeyEventArgs e){if(e.Key is Key.Left or Key.Right){Seek(Math.Clamp(PlaybackPosition+(e.Key==Key.Left?-3:5),0,PlaybackDuration));e.Handled=true;}}
 void VolumeChanged(object sender,RoutedPropertyChangedEventArgs<double> e){if(ready){engine.Volume=(float)e.NewValue;video.Volume(e.NewValue*videoGain);VolumeText.Text=$"{e.NewValue*100:0}%";Volume.ToolTip=VolumeText.Text;QueueSave();}}
 void LoopChanged(object sender,SelectionChangedEventArgs e){if(ready){settings.Loop=LoopChoice.SelectedIndex;QueueSave();}}
 void UpdateTimer()
 {
  if(closing){sync.Stop();return;}
  var visible=IsVisible&&WindowState!=WindowState.Minimized&&fullscreen==null;
  var fullVisible=fullscreen?.IsVisible==true&&fullscreen.WindowState!=WindowState.Minimized&&WindowState!=WindowState.Minimized;sync.Interval=TimeSpan.FromMilliseconds((visible&&FrontInteraction||fullVisible&&fullscreen!.IsActive)&&!settings.ReduceMotion?1000d/60:100);
  if((PlaybackPlaying&&(visible||fullVisible||overlay.IsVisible))||(video.Playing&&(fullVisible||visible&&CurrentPage=="lyrics")))sync.Start();else sync.Stop();
  overlay.SetPlaying(fadeDesired??PlaybackPlaying);PlayerTimeline.Visibility=currentTrack==null?Visibility.Collapsed:Visibility.Visible;PlayIcon.Kind=(fadeDesired??PlaybackPlaying)?"Pause":"Play";var label=(fadeDesired??PlaybackPlaying)?"暂停":"播放";PlayButton.ToolTip=label;AutomationProperties.SetName(PlayButton,label);
  UpdateOverlayButtons();PreviousButton.IsEnabled=NextButton.IsEnabled=currentTrack!=null;WavePanel.Visibility=settings.Visualizer&&!directVideo&&currentTrack!=null?Visibility.Visible:Visibility.Collapsed;Wave.Configure(engine.Analysis,engine.Playing,visible&&FrontInteraction&&CurrentPage=="lyrics",settings.Visualizer&&!directVideo);
 }
 internal void UpdateClock(){if(IsVisible&&WindowState!=WindowState.Minimized&&fullscreen==null){var seconds=PlaybackPosition;if(!seeking)Progress.Value=seconds;Elapsed.Text=Format(seconds);}SynchronizeCompanion();if(video.Path!=null&&(fullscreen?.IsVisible==true||IsVisible&&WindowState!=WindowState.Minimized&&CurrentPage=="lyrics"))UpdateVideoControls();UpdateSubtitle();}
 internal static string Format(double seconds)=>TimeSpan.FromSeconds(Math.Max(0,seconds)).ToString(seconds>=3600?@"h\:mm\:ss":@"mm\:ss");
 bool FrontInteraction=>IsActive||OwnedWindows.Cast<Window>().Any(w=>w!=fullscreen&&w.IsActive);
 internal void UpdateVisuals(){if(closing){Stars.Configure(0,false);return;}Stars.Visibility=Visibility.Visible;Stars.ConfigurePetals(settings.Sakura&&!settings.ReduceMotion,settings.SakuraStrength);Stars.Configure(settings.ReduceMotion?0:settings.Stars,IsVisible&&FrontInteraction&&WindowState!=WindowState.Minimized&&fullscreen==null,settings.StarIntensity);UpdateVideoSurface();}
 void ToggleOverlay(object sender,RoutedEventArgs e)
 {
  if(overlay.IsVisible)CloseOverlay();
  else OpenOverlayEditor();
 }
 void OpenOverlayEditor()
 {
  overlay.RestoreEditing();
  OverlayWindow.ClampToScreens(overlay);
  UpdateSubtitle();
  Status.Text="桌面字幕已开启 · 拖动黑底移动";
  QueueSave();
 }
 void CloseOverlay()
 {
  overlay.EndEditing();
  overlay.Hide();
  Status.Text="桌面字幕已关闭";
  QueueSave();
 }
 void OverlayCommand(string command){switch(command){case "play":TogglePlay(this,new());break;case "previous":Step(-1,false);break;case "next":Step(1,false);break;case "earlier":SetOffset(Preference().Offset-.1);break;case "later":SetOffset(Preference().Offset+.1);break;case "settings":RestoreMain();OpenSettings(this,new());break;}}
 void OpenSettings(object sender,RoutedEventArgs e){new SettingsWindow(settings,overlay,ApplyLiveSettings){Owner=this}.ShowDialog();ApplyTypography();UpdateVisuals();UpdateTimer();Save();}
 internal void RefreshTimerForTest()=>UpdateTimer();
 internal void NormalBackgroundForTest(bool enabled){if(enabled){saveTimer.Start();library.Watch(settings.Roots);}else{saveTimer.Stop();library.Watch(Array.Empty<string>());}}
 internal void RestoreFromTrayForTest()=>tray?.ContextMenuStrip.Items[2].PerformClick();
 protected override void OnRenderSizeChanged(SizeChangedInfo info){base.OnRenderSizeChanged(info);if(!ready)return;if(imageFit)Dispatcher.BeginInvoke(ResizeImage,DispatcherPriority.Loaded);var cols=Columns();if(cols!=homeColumns)RenderHome();CompactChrome();ArrangeMedia();Wave.Height=ActualHeight<740?20:Math.Clamp(settings.WaveHeight,16,48);}
}
