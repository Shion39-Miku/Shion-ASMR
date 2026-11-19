using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Microsoft.Win32;
namespace Nocturne;
public partial class MainWindow
{
 Slider? fullscreenSeek;TextBlock? fullscreenCaption,fullscreenTime;Symbol? fullscreenPlayIcon;Border? fullscreenControls;Grid? fullscreenRoot;
 readonly DispatcherTimer fullscreenIdle=new(){Interval=TimeSpan.FromSeconds(2.5)};bool fullscreenTimerWired;
 SubtitleDocument companionCaptions=new();static readonly FontFamily videoFont=new("Microsoft YaHei UI, Yu Gothic UI, Microsoft JhengHei UI, Segoe UI");
 internal string VideoCaptionText=>VideoCaption.Text;internal string FullscreenCaptionText=>fullscreenCaption?.Text??"";internal Slider? FullscreenProgress=>fullscreenSeek;
 internal bool FullscreenControlsVisible=>fullscreenControls?.IsHitTestVisible==true;
 TrackPreference VideoPreference(){if(video.Path==null)return new();if(!settings.Tracks.TryGetValue(video.Path,out var p))settings.Tracks[video.Path]=p=new();return p;}
 async Task LoadCompanionCaptions(string path,int request)
 {
  companionCaptions=new();var saved=VideoPreference();var catalog=works.ToArray();var owner=catalog.FirstOrDefault(w=>w.Groups.Any(g=>g.Audio.Contains(path)));
  var loaded=await Task.Run(()=>{var choice=SubtitleMatcher.Candidates(path,owner,library,saved.Subtitle,catalog).FirstOrDefault();return(choice,doc:choice==null?new SubtitleDocument():SubtitleDocument.Load(choice,saved.Encoding));});
  if(closing||request!=videoRequest||video.Path!=path)return;companionCaptions=loaded.doc;saved.Subtitle=loaded.choice;QueueSave();UpdateVideoSubtitle();
 }
 void VideoViewportChanged(object sender,SizeChangedEventArgs e)=>UpdateVideoSubtitle();
 void UpdateVideoSubtitle()
 {
  if(VideoCaption==null)return;string value="";
  if(settings.VideoSubtitles&&video.Path!=null){var doc=directVideo?document:companionCaptions;var p=VideoPreference();value=string.Join("\n",doc.Active(video.Position-p.Offset).Select(i=>doc.Cues[i].Text));}
  void Apply(TextBlock caption,FrameworkElement host,bool full)
  {
   if(caption.Text!=value)caption.Text=value;caption.FontFamily=videoFont;var ratio=video.Image==null?16d/9:video.Image.Width/Math.Max(1,video.Image.Height);var width=Math.Max(1,Math.Min(host.ActualWidth,host.ActualHeight*ratio));var height=width/ratio;
   caption.MaxWidth=Math.Max(1,width*.9);caption.FontSize=Math.Clamp(Math.Clamp(height/30,13,36)*settings.VideoSubtitleScale,11,48);caption.LineHeight=caption.FontSize*1.45;caption.MaxHeight=Math.Max(caption.LineHeight*2,height*.35);caption.TextTrimming=TextTrimming.CharacterEllipsis;var bottom=Math.Max(0,(host.ActualHeight-height)/2)+Math.Max(10,height*.05)+(full?82:0);caption.Margin=new Thickness(8,0,8,bottom);
  }
  Apply(VideoCaption,VideoViewport,false);if(fullscreenCaption!=null&&fullscreenRoot!=null)Apply(fullscreenCaption,fullscreenRoot,true);
 }
 void VideoCaptionOptions(object sender,RoutedEventArgs e)
 {
  if(sender is not Button button)return;var menu=new ContextMenu();var on=new MenuItem{Header=(settings.VideoSubtitles?"✓ ":"")+"显示视频字幕",IsCheckable=true,IsChecked=settings.VideoSubtitles};on.Click+=(_,_)=>{settings.VideoSubtitles=on.IsChecked;on.Header=(settings.VideoSubtitles?"✓ ":"")+"显示视频字幕";UpdateVideoSubtitle();QueueSave();};menu.Items.Add(on);
  void Item(string title,Action action){var item=new MenuItem{Header=title};item.Click+=(_,_)=>action();menu.Items.Add(item);}
  Item("字幕字号 +10%",()=>{settings.VideoSubtitleScale=Math.Clamp(settings.VideoSubtitleScale+.1,.6,1.8);UpdateVideoSubtitle();QueueSave();});Item("字幕字号 −10%",()=>{settings.VideoSubtitleScale=Math.Clamp(settings.VideoSubtitleScale-.1,.6,1.8);UpdateVideoSubtitle();QueueSave();});Item("字幕提前 0.1 秒",()=>AdjustVideoCaptionOffset(-.1));Item("字幕延后 0.1 秒",()=>AdjustVideoCaptionOffset(.1));Item("手动选择视频字幕…",PickVideoSubtitle);
  button.ContextMenu=menu;menu.PlacementTarget=button;menu.IsOpen=true;
 }
 internal void AdjustVideoCaptionOffset(double delta){if(directVideo)SetOffset(Preference().Offset+delta);else{var p=VideoPreference();p.Offset=Math.Round(Math.Clamp(p.Offset+delta,-600,600),2);UpdateVideoSubtitle();QueueSave();}}
 async void PickVideoSubtitle(){if(video.Path==null)return;var picker=new OpenFileDialog{Filter="字幕文件|*.lrc;*.vtt"};if(picker.ShowDialog(fullscreen??this)==true){VideoPreference().Subtitle=picker.FileName;if(directVideo){LoadSubtitleChoices();await SubtitleReady;}else await LoadCompanionCaptions(video.Path,videoRequest);}}
 Window CreateFullscreenView()
 {
  fullscreenImage=new Image{Source=video.Image,Stretch=Stretch.Uniform,IsHitTestVisible=false};fullscreenRoot=new Grid{Background=Brushes.Black};fullscreenRoot.Children.Add(fullscreenImage);
  fullscreenCaption=new TextBlock{Foreground=Brushes.White,FontWeight=FontWeights.SemiBold,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false,Effect=new DropShadowEffect{Color=Colors.Black,BlurRadius=3,ShadowDepth=1,Opacity=.9}};fullscreenRoot.Children.Add(fullscreenCaption);
  var controls=new Grid();controls.ColumnDefinitions.Add(new(){Width=GridLength.Auto});controls.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});controls.ColumnDefinitions.Add(new(){Width=GridLength.Auto});controls.ColumnDefinitions.Add(new(){Width=GridLength.Auto});controls.ColumnDefinitions.Add(new(){Width=GridLength.Auto});
  fullscreenPlayIcon=new Symbol{Kind=video.Playing?"Pause":"Play"};var play=new Button{Content=fullscreenPlayIcon,Width=42,Padding=new Thickness(10),ToolTip="视频播放 / 暂停"};play.Click+=VideoPlayPause;controls.Children.Add(play);
  fullscreenSeek=new Slider{Name="FullscreenSeek",Minimum=0,Maximum=Math.Max(1,video.Duration),IsMoveToPointEnabled=false,ToolTip="点击或拖动视频进度"};System.Windows.Automation.AutomationProperties.SetName(fullscreenSeek,"全屏视频进度");Grid.SetColumn(fullscreenSeek,1);controls.Children.Add(fullscreenSeek);var slider=fullscreenSeek;
  slider.PreviewMouseLeftButtonDown+=(_,e)=>BeginPointerSeek(slider,e);slider.PreviewMouseMove+=(_,e)=>{if(e.LeftButton==MouseButtonState.Pressed)MovePointerSeek(slider,e);};slider.PreviewMouseLeftButtonUp+=(_,e)=>EndPointerSeek(slider,e);slider.LostMouseCapture+=(_,_)=>EndPointerSeek(slider);
  fullscreenTime=new TextBlock{MinWidth=110,Margin=new Thickness(12,0,8,0),VerticalAlignment=VerticalAlignment.Center,FontSize=12};Grid.SetColumn(fullscreenTime,2);controls.Children.Add(fullscreenTime);
  var captions=new Button{Content="CC",ToolTip="视频字幕、字号与偏移"};captions.Click+=VideoCaptionOptions;Grid.SetColumn(captions,3);controls.Children.Add(captions);var exit=new Button{Content="退出全屏",ToolTip="Esc 退出全屏"};exit.Click+=(_,_)=>ExitVideoFullscreen();Grid.SetColumn(exit,4);controls.Children.Add(exit);
  fullscreenControls=new Border{Child=controls,Background=new SolidColorBrush(Color.FromArgb(220,20,24,38)),CornerRadius=new CornerRadius(18),Padding=new Thickness(10,8,10,8),Margin=new Thickness(28,0,28,22),VerticalAlignment=VerticalAlignment.Bottom};fullscreenRoot.Children.Add(fullscreenControls);fullscreenRoot.MouseMove+=(_,_)=>ShowFullscreenControls();fullscreenRoot.SizeChanged+=VideoViewportChanged;
  if(!fullscreenTimerWired){fullscreenTimerWired=true;fullscreenIdle.Tick+=(_,_)=>{fullscreenIdle.Stop();if(fullscreenControls==null)return;if(fullscreenControls.IsMouseOver||pointerSlider==fullscreenSeek){fullscreenIdle.Start();return;}fullscreenControls.IsHitTestVisible=false;fullscreenControls.BeginAnimation(OpacityProperty,new DoubleAnimation(0,TimeSpan.FromMilliseconds(160)));UpdateVideoSubtitle();};}
  var window=new Window{Title="月咲 Shion · 视频全屏",Owner=this,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,Background=Brushes.Black,Content=fullscreenRoot,WindowStartupLocation=WindowStartupLocation.Manual,Left=Left,Top=Top,Width=ActualWidth,Height=ActualHeight};
  void VisibilityChanged(){UpdateVisuals();UpdateTimer();if(window.IsVisible&&window.WindowState!=WindowState.Minimized&&WindowState!=WindowState.Minimized)ShowFullscreenControls();else fullscreenIdle.Stop();}
  window.StateChanged+=(_,_)=>VisibilityChanged();window.IsVisibleChanged+=(_,_)=>VisibilityChanged();window.Activated+=(_,_)=>UpdateTimer();window.Deactivated+=(_,_)=>UpdateTimer();return window;
 }
 internal void ShowFullscreenControls(){if(fullscreenControls==null)return;fullscreenControls.BeginAnimation(OpacityProperty,null);fullscreenControls.Opacity=1;fullscreenControls.IsHitTestVisible=true;fullscreenIdle.Stop();fullscreenIdle.Start();UpdateVideoSubtitle();}
 void FullscreenKey(object sender,KeyEventArgs e)
 {
  ShowFullscreenControls();if(!directVideo&&!companionSync&&e.Key is Key.Left or Key.Right or Key.Space){e.Handled=true;if(e.Key==Key.Space){if(!e.IsRepeat)VideoPlayPause(sender,new());}else video.Seek(video.Position+(e.Key==Key.Left?-3:5));UpdateClock();return;}PlayerKey(sender,e);
 }
}
