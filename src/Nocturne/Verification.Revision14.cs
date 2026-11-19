using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Nocturne;

public static partial class Verification
{
 static async Task VerifyRevision14(MainWindow w)
 {
  const string first="第一句",second="第二句",third="第三句";
  const string subtitles="WEBVTT\n\n00:01.000 --> 00:01.400\n第一句\n\n00:03.500 --> 00:04.200\n第二句\n\n00:07.000 --> 00:07.800\n第三句\n";
  string audio=Path.Combine(State.Home,"空档.wav"),next=Path.Combine(State.Home,"下一轨.wav");
  WriteWave(audio,10);WriteWave(next,10);
  File.WriteAllText(Path.ChangeExtension(audio,".vtt"),subtitles);
  File.WriteAllText(Path.ChangeExtension(next,".vtt"),"WEBVTT\n\n00:02.000 --> 00:03.000\n新音轨字幕\n");
  w.Left=30;w.Top=30;w.Width=920;w.Height=750;w.settings.PauseFade=false;w.settings.ReduceMotion=true;w.Volume.Value=.03;
  w.overlay.Left=135;w.overlay.Top=115;w.overlay.Width=475;w.overlay.Height=255;
  Check("打开测试音轨",w.OpenTrack(audio,false,0));await w.SubtitleReady;
  await ClickElement(w.OpenLyricsButton);
  var open=PageBounds(w.OpenLyricsButton,w.PlayerSurface);var toggle=PageBounds(w.DesktopLyricsButton,w.PlayerSurface);
  Check("底栏按钮同排且打开字幕在左",open.Right<=toggle.Left&&Math.Abs(open.Top-toggle.Top)<3&&toggle.Right<=w.PlayerSurface.ActualWidth);
  await ClickElement(w.DesktopLyricsButton);
  Check("底栏开关选中并进入可拖动编辑",w.DesktopLyricsButton.IsChecked==true&&w.overlay.IsVisible&&w.overlay.Editing&&!w.overlay.NativeClickThrough);
  await ClickElement(w.DesktopLyricsButton);
  Check("底栏再次点击关闭并清除选中",w.DesktopLyricsButton.IsChecked==false&&!w.overlay.IsVisible);
  await ClickElement(w.DesktopLyricsButton);
  bool Matches(string value)=>w.CurrentSubtitle==value&&w.FooterSubtitle.Text==value&&w.overlay.DisplayedText==value;
  string[] Highlighted()=>Visuals<ListBoxItem>(w.SubtitleLines).Where(row=>Equals(row.Tag,"active")).Select(row=>((Cue)row.DataContext).Text).ToArray();
  Check("首句前底栏和桌面留空且无高亮",Matches("")&&Highlighted().Length==0);
  w.Seek(1.1);await Delay(120);
  var plate=(Border)w.overlay.Content;
  Check("编辑底板为8 DIP半透明黑色",plate.CornerRadius==new CornerRadius(8)&&plate.Background is SolidColorBrush {Color:{A:170,R:0,G:0,B:0}});
  var glyph=w.overlay.GlyphBounds;
  await DragPlate(w.overlay,w.overlay.PointToScreen(new Point(glyph.Left+glyph.Width/2,glyph.Top+glyph.Height/2)),"台词区");
  await DragPlate(w.overlay,plate.PointToScreen(new Point(15,plate.ActualHeight/2)),"黑底侧边空白");
  await DragPlate(w.overlay,plate.PointToScreen(new Point(30,plate.ActualHeight-9)),"工具栏旁黑底空白");
  Snapshot(w,Path.Combine(State.Home,"footer.png"));
  NativeOverlayShot13(w.overlay,"desktop-edit.png",140);

  var settingsButton=Visuals<Button>(w.overlay).Single(button=>(string?)button.ToolTip=="更多字体与样式设置");
  var settingsOpened=false;
  var closeSettings=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(80)};
  closeSettings.Tick+=(_,_)=>{var dialog=Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();if(dialog==null)return;settingsOpened=true;dialog.Close();closeSettings.Stop();};
  closeSettings.Start();try{await ClickElement(settingsButton);}finally{closeSettings.Stop();}
  Check("工具栏设置按钮可打开设置",settingsOpened);
  await ClickElement(w.overlay.PlayTool);
  Check("工具栏播放按钮响应点击",w.PlaybackPlaying);
  await WaitForPosition(w,2.0);
  Check("自然播放跨过结束点保留三处台词与高亮",w.PlaybackPlaying&&w.PlaybackPosition<3.5&&Matches(first)&&Highlighted().SequenceEqual(new[]{first}));
  await WaitForPosition(w,3.6);await Delay(160);
  Check("播放到下一句才同步更新并滚动居中",Matches(second)&&Highlighted().SequenceEqual(new[]{second})&&CenterError(w)<5);
  w.PausePlayback();
  // Keep the always-on-top editor clear of the main rail under test.
  w.overlay.Left=w.Left+30;w.overlay.Top=w.Top+55;
  await VideoRail(w.Progress,2.2/w.PlaybackDuration,drag:true);
  Check("原生拖动进度条到较早空档按目标时间取句",Math.Abs(w.PlaybackPosition-2.2)<.2&&Matches(first)&&Highlighted().SequenceEqual(new[]{first})&&CenterError(w)<5);
  w.Seek(5.3);await Delay(120);
  Check("向前跳入空档取最近一句并居中",Matches(second)&&Highlighted().SequenceEqual(new[]{second})&&CenterError(w)<5);
  w.Seek(.5);await Delay(120);
  Check("回跳首句前清空且无高亮",Matches("")&&Highlighted().Length==0);
  w.Seek(9);await Delay(120);
  Check("最后一句结束后仍保留",Matches(third)&&Highlighted().SequenceEqual(new[]{third}));
  w.OpenTrack(next,false,0);
  Check("切轨即时清除上一轨",w.CurrentSubtitle==""&&w.overlay.DisplayedText==""&&w.FooterSubtitle.Text!=third&&Highlighted().Length==0);
  await w.SubtitleReady;Check("新音轨首句前留空",Matches(""));
  w.Seek(2.2);await Delay(120);
  var lockButton=Visuals<Button>(w.overlay).Single(button=>button.ToolTip?.ToString()?.StartsWith("锁定并")==true);
  await ClickElement(lockButton);
  Check("锁定移除黑底并恢复穿透且底栏仍选中",w.overlay.TransparentSurface&&w.overlay.NativeClickThrough&&w.DesktopLyricsButton.IsChecked==true);
  glyph=w.overlay.GlyphBounds;var hover=w.overlay.PointToScreen(new Point(glyph.Left+glyph.Width/2,glyph.Top+glyph.Height/2));
  NativeProbe.SetCursorPos((int)hover.X,(int)hover.Y);await Delay(270);
  var unlock=w.overlay.UnlockButton;var unlockWindow=Window.GetWindow(unlock);
  var anchor=unlockWindow==null?new Point():w.overlay.PointFromScreen(unlockWindow.PointToScreen(new Point()));
  Check("悬停小锁位于字形下方10 DIP",w.overlay.UnlockVisible&&Math.Abs(anchor.Y-glyph.Bottom-10)<2);
  var unlockPoint=unlock.PointToScreen(new Point(unlock.ActualWidth/2,unlock.ActualHeight/2));
  NativeProbe.ClickAt((int)unlockPoint.X,(int)unlockPoint.Y);await Delay(120);
  Check("原生点击小锁回到编辑",w.overlay.Editing&&!w.overlay.NativeClickThrough);
  var closeButton=Visuals<Button>(w.overlay).Single(button=>Equals(button.ToolTip,"关闭桌面字幕"));
  await ClickElement(closeButton);
  Check("工具栏关闭同步取消底栏选中",!w.overlay.IsVisible&&w.DesktopLyricsButton.IsChecked==false);

  var video=Path.Combine(State.Home,"视频空档.mp4");File.Copy(Path.Combine(Environment.CurrentDirectory,"test-fixtures","generated.mp4"),video);
  File.WriteAllText(Path.ChangeExtension(video,".vtt"),subtitles);
  Check("打开测试视频",await w.OpenVideo(video,true,false,1.1));await w.SubtitleReady;
  w.Seek(1.1);Check("视频时间范围内显示字幕",w.CurrentSubtitle==first&&w.VideoCaptionText==first);
  w.Seek(2.2);Check("视频空档不保留上一句",Matches("")&&w.VideoCaptionText==""&&Highlighted().Length==0);
 }

 static async Task WaitForPosition(MainWindow w,double seconds)
 {
  for(int i=0;i<180&&w.PlaybackPlaying&&w.PlaybackPosition<seconds;i++)await Delay(25);
 }

 static async Task DragPlate(OverlayWindow overlay,Point start,string area)
 {
  var handle=new WindowInteropHelper(overlay).Handle;
  if(!NativeProbe.TargetTestWindow(handle)||!NativeProbe.PointTargets(handle,(int)start.X,(int)start.Y))
   throw new InvalidOperationException("无法将原生拖动指向桌面字幕窗口");
  var destination=overlay.PointFromScreen(new Point(start.X+36,start.Y+17));var origin=overlay.PointFromScreen(start);
  var left=overlay.Left;var top=overlay.Top;
  await Task.Run(async()=>
  {
   NativeProbe.SetCursorPos((int)start.X,(int)start.Y);NativeProbe.Mouse(2);
   try{await Delay(90);NativeProbe.SetCursorPos((int)start.X+36,(int)start.Y+17);await Delay(100);}
   finally{NativeProbe.Mouse(4);}
  });
  await Delay(130);
  Check(area+"可原生拖动",Math.Abs(overlay.Left-left-(destination.X-origin.X))<4&&Math.Abs(overlay.Top-top-(destination.Y-origin.Y))<4,$"moved {overlay.Left-left:F1}, {overlay.Top-top:F1} DIP");
 }
}
