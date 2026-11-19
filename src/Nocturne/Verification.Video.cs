using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace Nocturne;
public static partial class Verification
{
 static async Task VerifyVideo(MainWindow w)
 {
  var fixtures=Environment.GetEnvironmentVariable("NOCTURNE_CODEC_FIXTURES")??Path.GetFullPath("test-fixtures");
  var root=Path.Combine(State.Home,"视频 日本語 & 中文");var work=Path.Combine(root,"作品 月光");var tracks=Path.Combine(work,"CD1");var subs=Path.Combine(work,"字幕","日本語");Directory.CreateDirectory(tracks);Directory.CreateDirectory(subs);
  var movie=Path.Combine(tracks,"01 月光.mp4");File.Copy(Path.Combine(fixtures,"generated.mp4"),movie,true);var audio=Path.Combine(tracks,"02 耳语.wav");WriteWave(audio,40);var missing=Path.Combine(tracks,"03 无字幕.wav");WriteWave(missing,10);
  File.WriteAllText(Path.Combine(tracks,"01 月光.vtt"),"WEBVTT\n\n00:00.000 --> 00:02.000\n视频开场\n第二行\n\n00:04.000 --> 00:09.000\n视频中段\n\n00:12.000 --> 00:20.000\n视频后段\n");
  File.WriteAllText(Path.Combine(tracks,"01 月光.lrc"),"[00:00.00]LRC 备选\n[00:04.00]LRC 中段");
  File.WriteAllText(Path.Combine(subs,"02 耳语.vtt"),"WEBVTT\n\n00:00.000 --> 00:03.000\n作品子目录字幕\n\n00:05.000 --> 00:20.000\n恢复位置字幕\n");
  File.WriteAllText(Path.Combine(subs,"无关音轨.vtt"),"WEBVTT\n\n00:00.000 --> 00:20.000\n不可误加载\n");
  w.settings.Roots.Add(root);await w.Scan(true);
  Check("视频进入媒体库且与音轨同组",w.library.Folders[tracks].Audio.Contains(movie));
  w.Navigate("detail");w.OpenTrack(audio,true,0);await w.SubtitleReady;Check("自动匹配作品字幕子目录且进入歌词页",w.CurrentSubtitle=="作品子目录字幕"&&w.CurrentPage=="lyrics");
  var master=w.engine.Position;Check("打开真实 H264 AAC MP4 配套视频",await w.OpenVideo(movie,false,true));await Delay(900);
  Check("配套视频静音且音频继续为主源",w.video.Muted&&w.engine.Playing&&w.engine.Position>master+.5&&w.video.Position>.4&&!w.directVideo,$"audio={w.engine.Position:F3}; video={w.video.Position:F3}");
  w.video.Seek(14);await Delay(200);Check("默认独立视频进度不关联音频",Math.Abs(w.video.Position-w.engine.Position)>8);
  w.VideoSync.IsChecked=true;await Delay(450);Check("显式启用同步才跟随音频",Math.Abs(w.video.Position-w.engine.Position)<.35);
  w.PausePlayback();await Delay(200);Check("同步配套视频跟随暂停",!w.video.Playing&&!w.engine.Playing);
  w.Seek(6);await Delay(200);Check("同步定位与字幕",Math.Abs(w.video.Position-6)<.3&&w.CurrentSubtitle=="恢复位置字幕");
  w.Seek(30);await Delay(160);Check("同步超出视频长度时停在末尾避免反复循环",!w.video.Playing&&w.video.Position>24.8);w.Seek(6);
  w.VideoSync.IsChecked=false;w.video.Play();w.RefreshTimerForTest();w.WindowState=WindowState.Minimized;await Delay(200);Check("最小化暂停隐藏配套解码并卸下绘制",!w.video.Playing&&!w.VideoDrawingVisible&&!w.Stars.Running);
  w.WindowState=WindowState.Normal;await Delay(250);Check("恢复可见页面继续配套视频",w.video.Playing&&w.VideoDrawingVisible);
  w.CloseVideoSession();await Delay(450);Check("关闭配套视频保留音频并收起视频列",w.video.Path==null&&w.engine.FilePath==audio&&!w.VideoDrawingVisible&&w.VideoColumn.ActualWidth==0);
  Check("直接视频实际打开",await w.OpenVideo(movie,true,true,0));var decodeStart=Stopwatch.StartNew();for(int i=0;i<40&&w.PlaybackPosition<.35;i++)await Delay(100);
  Check("直接视频只有一路声音和视频时钟",w.directVideo&&!w.video.Muted&&w.engine.FilePath==null&&w.PlaybackPosition>.3&&w.PlaybackPlaying,$"position={w.PlaybackPosition:F3}; playing={w.PlaybackPlaying}; audio reader={w.engine.FilePath??"closed"}; observed start ms={decodeStart.ElapsedMilliseconds}");
  Check("首次匹配 VTT 优先并显示多行",w.settings.Tracks[movie].Subtitle!.EndsWith(".vtt")&&w.CurrentSubtitle=="视频开场\n第二行");
  w.PausePlayback();w.Seek(4.8);await Delay(180);w.UpdateClock();Check("视频暂停定位字幕立即刷新",w.CurrentSubtitle=="视频中段");
  w.SetOffset(2);Check("视频字幕偏移立即生效且空白区清空",w.CurrentSubtitle=="");w.SetOffset(0);
  w.Width=960;w.Height=680;await Delay(200);Check("小窗口左右布局保留字幕与播放控件",w.SubtitlePanel.ActualWidth>=220&&w.VideoPane.ActualWidth>=260&&w.PlayButton.IsVisible);Snapshot(w,Path.Combine(State.Home,"video-layout.png"));
  w.LyricsColumn.Width=new GridLength(1.4,GridUnitType.Star);w.VideoColumn.Width=new GridLength(1.4,GridUnitType.Star);w.settings.VideoWidth=1;w.settings.RightMediaShare=1;w.Navigate("home");w.Navigate("lyrics");w.UpdateLayout();Check("调整过的星号列在页面切换后保持保存的宽度比例",Math.Abs(w.VideoColumn.ActualWidth/w.LyricsColumn.ActualWidth-1)<.05);
  w.EnterVideoFullscreen();await Delay(300);Check("全屏复用同一视频会话",w.VideoFullscreen&&w.video.Path==movie&&w.VideoDrawingVisible);
  // Fullscreen is an explicit user action. Establish that input target before sending OS keys,
  // as previous game/child-process probes can leave another application in the foreground.
  NativeProbe.SetForegroundWindow(w.VideoFullscreenHandle);await Delay(100);var fullscreenFocus=NativeProbe.GetForegroundWindow()==w.VideoFullscreenHandle;
  NativeProbe.Key(0x1B,false);NativeProbe.Key(0x1B,true);await Delay(250);Check("实际 Esc 退出全屏返回左右播放页面",fullscreenFocus&&!w.VideoFullscreen&&w.CurrentPage=="lyrics"&&w.VideoImage.Source!=null,$"input focused on fullscreen={fullscreenFocus}; after={NativeProbe.ForegroundDescription()}");w.ExitVideoFullscreen();
  w.Seek(6.4);w.Save();var saved=w.PlaybackPosition;w.OpenTrack(audio,false,7);await w.SubtitleReady;Check("切回音频释放视频且恢复独立字幕",!w.directVideo&&w.video.Path==null&&w.CurrentSubtitle=="恢复位置字幕");
  Check("视频恢复独立历史位置",await w.OpenVideo(movie,true,false));await Delay(180);w.UpdateClock();Check("恢复视频时间与字幕重算一致",Math.Abs(w.PlaybackPosition-saved)<.3&&w.CurrentSubtitle=="视频中段");
  w.overlay.ShowPassive();w.video.Play();w.RefreshTimerForTest();w.WindowState=WindowState.Minimized;var minimizedPosition=w.PlaybackPosition;await Delay(450);Check("直接视频最小化保持声音时钟与桌面字幕且卸下画面",w.PlaybackPlaying&&w.PlaybackPosition>minimizedPosition+.25&&!w.VideoDrawingVisible&&!w.Stars.Running&&w.CurrentSubtitle=="视频中段");w.WindowState=WindowState.Normal;w.PausePlayback();w.overlay.Hide();w.Seek(saved);
  w.Save();using(var child=StartProbe("--startup-probe",State.Home,"--resume-probe",movie)){await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));var probe=JsonDocument.Parse(File.ReadAllText(Path.Combine(State.Home,"startup-probe.json"))).RootElement;Check("新进程启动仍为分类主页且视频历史和字幕可恢复",probe.GetProperty("initialPage").GetString()=="home"&&probe.GetProperty("initiallyStopped").GetBoolean()&&Math.Abs(probe.GetProperty("position").GetDouble()-saved)<.3&&probe.GetProperty("subtitle").GetString()=="视频中段");}
  w.settings.Tracks[movie].Subtitle=Path.Combine(tracks,"01 月光.lrc");await w.LoadSubtitleChoicesForTest();Check("保存的 LRC 选择优先 VTT",w.CurrentSubtitle=="LRC 中段");
  w.OpenTrack(missing,false,0);await w.SubtitleReady;Check("缺失字幕清空上一首与桌面字幕且页面不变",w.document.Cues.Count==0&&w.CurrentSubtitle==""&&w.NoLyrics.Text.Contains("未找到匹配字幕")&&w.CurrentPage=="lyrics");
  var addition=Path.Combine(tracks,"03 无字幕.vtt");File.WriteAllText(addition,"WEBVTT\n\n00:00.000 --> 00:09.000\n稍后添加的字幕\n");w.OpenTrack(missing,false,1);await w.SubtitleReady;Check("无字幕记录不会阻止以后自动匹配",w.CurrentSubtitle=="稍后添加的字幕");
  var manual=Path.Combine(State.Home,"手动字幕.vtt");File.WriteAllText(manual,"WEBVTT\n\n00:00.000 --> 00:20.000\n手动优先\n");w.settings.Tracks[audio].Subtitle=manual;w.OpenTrack(audio,false,6);await w.SubtitleReady;Check("有效手动字幕跨目录优先且偏移保存",w.CurrentSubtitle=="手动优先");
  w.OpenTrack(audio,true,0);await w.SubtitleReady;await w.OpenVideo(movie,false,true);w.settings.Stars=2;w.UpdateVisuals();await Delay(400);await Measure("音频_静音配套视频_星空字幕",w);
  w.CloseVideoSession();await Delay(700);bool released=false;try{using var exclusive=new FileStream(movie,FileMode.Open,FileAccess.ReadWrite,FileShare.None);released=true;}catch(IOException){}
  Check("关闭视频释放文件句柄",released);
  var wmv=Path.Combine(fixtures,"generated.wmv");if(File.Exists(wmv)){Check("实际 WMV 视频解码",await w.OpenVideo(wmv,true,true,0));for(int i=0;i<40&&w.PlaybackPosition<=.1;i++)await Delay(100);Check("WMV 实际播放时钟前进",w.PlaybackPlaying&&w.PlaybackPosition>.1);w.CloseVideoSession();}
  w.settings.Loop=2;await w.OpenVideo(movie,true,true,24.8);for(int i=0;i<40&&w.PlaybackPosition>2;i++)await Delay(100);Check("视频实际结束后单曲循环",w.directVideo&&w.PlaybackPlaying&&w.PlaybackPosition<2);
  w.settings.Loop=0;await w.OpenVideo(movie,true,true,24.8);for(int i=0;i<40&&w.directVideo;i++)await Delay(100);Check("视频结束自动切音频不重复声音",!w.directVideo&&w.engine.FilePath==audio&&w.engine.Playing&&w.video.Path==null);
  w.settings.Loop=1;w.Navigate("home");w.OpenTrack(missing,true,9.8,false);await w.SubtitleReady;for(int i=0;i<40&&w.currentTrack!=movie;i++)await Delay(100);await w.VideoReady;Check("自动切入视频保留当前主页且不强制打开歌词",w.currentTrack==movie&&w.directVideo&&w.CurrentPage=="home"&&!w.VideoDrawingVisible);w.settings.Loop=0;
  var bad=Path.Combine(tracks,"损坏.mp4");File.WriteAllText(bad,"invalid mp4");Check("损坏视频提示且不崩溃",!await w.OpenVideo(bad,true,true)&&w.video.Path==null);
  bool repeated=true;var startPositions=new System.Collections.Generic.List<double>();for(int cycle=0;cycle<4;cycle++){w.OpenTrack(audio,false,0);await w.SubtitleReady;repeated&=await w.OpenVideo(movie,true,true,0);for(int i=0;i<40&&w.PlaybackPosition<.2;i++)await Delay(100);startPositions.Add(w.PlaybackPosition);repeated&=w.PlaybackPlaying&&w.PlaybackPosition>.15;w.CloseVideoSession();}Check("连续音频视频切换的真实视频时钟均正常启动",repeated,"positions="+string.Join(", ",startPositions.Select(x=>x.ToString("F3"))));
  w.OpenTrack(audio,false,6);await w.SubtitleReady;w.Save();w.Navigate("home");await Delay(150);Check("测试结束返回分类主页数据仍在",w.settings.Tracks.ContainsKey(movie)&&w.settings.Tracks[audio].Position>=5.8&&w.CurrentPage=="home");
 }
}
