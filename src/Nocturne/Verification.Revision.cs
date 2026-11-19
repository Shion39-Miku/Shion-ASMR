using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using NAudio.Wave;

namespace Nocturne;
public static partial class Verification
{
 static async Task ProbeGames(MainWindow w){var root=Path.Combine(State.Home,"game-fixture");Directory.CreateDirectory(root);var track=Path.Combine(root,"generated.wav");WriteWave(track,30);w.settings.Roots.Add(root);await w.Scan(true);w.OpenTrack(track,true,0);await w.SubtitleReady;await GameInput(w,track,false);await GameInput(w,track,true);}
 public static async Task ProbeStartup(MainWindow w,string? resume)
 {
  try
  {
   await Delay(300);var initialPage=w.CurrentPage;var initiallyStopped=!w.engine.Playing&&w.currentTrack==null;Snapshot(w,Path.Combine(State.Home,"restart-home.png"));
   bool opened=resume!=null&&w.OpenTrack(resume,false);await w.SubtitleReady;if(resume!=null&&Library.VideoTypes.Contains(Path.GetExtension(resume)))opened=await w.VideoReady;await Delay(200);w.UpdateClock();
   State.Write("startup-probe.json",new{initialPage,initiallyStopped,opened,position=w.PlaybackPosition,subtitle=w.CurrentSubtitle,roots=w.settings.Roots.Count,tracks=w.settings.Tracks.Count,overlayFont=w.overlay.DisplayFont.Source,overlaySize=w.overlay.DisplayFontSize,overlayColor=w.settings.OverlayColor,overlayLeft=w.overlay.Left,overlayTop=w.overlay.Top,manualFolders=w.settings.Organization.Count});
  }
  finally{w.ExitApplication();Application.Current.Shutdown();}
 }
 static async Task VerifyRevision(MainWindow w,string root,string cd1,string cd2,string first)
 {
  var baseHome=State.Home;
  // Migration test runs synchronously in a separate fixture profile; real user data is never written.
  var migration=Path.Combine(baseHome,"migration-fixture");Directory.CreateDirectory(migration);
  try
  {
   State.Home=migration;var legacy=new Settings();legacy.Resume[cd1]=new ResumePoint{Track=first,Seconds=12.5};legacy.Tracks[first]=new TrackPreference{Subtitle=Path.Combine(cd1,"2 耳语.vtt"),Offset=0.8};legacy.Covers[cd1]=Path.Combine(root,"作品 星夜","cover.png");State.Write("settings.json",legacy);var bytes=File.ReadAllBytes(Path.Combine(migration,"settings.json"));
   var upgraded=State.Read<Settings>("settings.json");upgraded.Migrate();Check("旧版进度字幕封面迁移保留",upgraded.Tracks[first].Position==12.5&&upgraded.Tracks[first].Offset==0.8&&upgraded.Covers[cd1]==legacy.Covers[cd1]&&upgraded.Tracks[first].Subtitle==legacy.Tracks[first].Subtitle);
   Check("升级备份原设置且不改写旧文件",bytes.SequenceEqual(File.ReadAllBytes(Path.Combine(migration,"settings.json.v1.bak")))&&bytes.SequenceEqual(File.ReadAllBytes(Path.Combine(migration,"settings.json"))));
  }
  finally{State.Home=baseHome;}
  var owner=Path.Combine(root,"结构示例");var a=Path.Combine(owner,"作品A");var b=Path.Combine(owner,"作品B");var c=Path.Combine(owner,"作品C");
  foreach(var p in new[]{Path.Combine(a,"音轨1"),Path.Combine(a,"音轨2"),Path.Combine(b,"Tracks","Disc 1"),Path.Combine(b,"Tracks","Disc 2"),Path.Combine(c,"左耳"),Path.Combine(c,"右耳")}){Directory.CreateDirectory(p);File.Copy(Path.Combine(cd2,"01 日本語.wav"),Path.Combine(p,"001.wav"),true);}
  var cover=Path.Combine(root,"作品 星夜","cover.png");File.Copy(cover,Path.Combine(a,"01_default.png"),true);File.Copy(cover,Path.Combine(a,"10_second.png"),true);File.Copy(cover,Path.Combine(a,"音轨1","02_first.png"),true);File.Copy(cover,Path.Combine(a,"音轨1","10_last.png"),true);
  File.Copy(cover,Path.Combine(cd1,"01_picture.png"),true);File.Copy(cover,Path.Combine(cd1,"10_picture.png"),true);
  await w.Scan(true);var catalog=w.works;
  Check("音轨1和音轨2归为作品A",catalog.SingleOrDefault(x=>x.Path==a) is Work wa&&wa.Groups.Count==2&&wa.Name=="作品A");
  Check("Tracks与Disc中间目录保留同一作品",catalog.SingleOrDefault(x=>x.Path==b) is Work wb&&wb.Groups.Count==2&&wb.TrackCount==2);
  Check("非关键词子目录根据结构归组",catalog.SingleOrDefault(x=>x.Path==c) is Work wc&&wc.Groups.Count==2);
  Check("不合并媒体根下不同作品",catalog.Any(x=>x.Path==a)&&catalog.Any(x=>x.Path==b)&&catalog.Any(x=>x.Path==c)&&catalog.All(x=>x.Path!=root));
  var workA=catalog.Single(x=>x.Path==a);Check("分类封面默认自然排序第一张",workA.Cover==Path.Combine(a,"01_default.png"));
  w.SelectFolder(Path.Combine(a,"音轨1"));await w.ImageReady;Check("预览按当前文件夹自然排序",w.PicturePaths[0]==Path.Combine(a,"音轨1","02_first.png")&&w.PictureIndex==0);
  var prior=w.Picture.Source;w.BrowsePicture(1);Check("切图加载期间不先清空",ReferenceEquals(w.Picture.Source,prior));await w.ImageReady;Check("多图有位置提示和切换入口",w.PictureIndex==1&&w.ImageName.Text.StartsWith("2 / 2")&&w.NextImageButton.Visibility==Visibility.Visible);
  var local=Path.Combine(baseHome,"external-cover.png");File.Copy(cover,local,true);w.ChooseCover(local);w.BrowsePicture(-1);await w.ImageReady;Check("浏览图片不修改分类封面",w.currentWork?.Cover==local&&w.settings.WorkCovers[a]==local);
  w.SelectFolder(Path.Combine(a,"音轨2"));await w.ImageReady;Check("无当前目录图片回退作品目录",w.PicturePaths[0]==Path.Combine(a,"01_default.png"));
  await w.AssignWork(new[]{Path.Combine(a,"音轨2")},b,"重新命名作品B");var restored=State.Read<Settings>("settings.json");restored.Migrate();var rebuilt=WorkCatalog.Build(w.library,restored);Check("手动分类名称归属持久保存",rebuilt.Single(x=>x.Path==b).Name=="重新命名作品B"&&rebuilt.Single(x=>x.Path==b).Groups.Any(x=>x.Path==Path.Combine(a,"音轨2")));
  Check("本地指定封面持久保存",rebuilt.Single(x=>x.Path==a).Cover==local);
  await w.Scan(true);Check("重新扫描不覆盖手动分类修正",w.works.Single(x=>x.Path==b).Name=="重新命名作品B"&&w.works.Single(x=>x.Path==b).Groups.Any(x=>x.Path==Path.Combine(a,"音轨2")));
  var second=Path.Combine(cd1,"10 安眠.wav");var secondSubtitle=Path.Combine(baseHome,"second-track.vtt");File.WriteAllText(secondSubtitle,"WEBVTT\n\n00:01.000 --> 00:03.000\n独立音轨字幕\n");
  w.OpenTrack(first,false,2.4);await w.SubtitleReady;w.settings.Tracks[first].Subtitle=Path.Combine(cd1,"2 耳语.vtt");await w.LoadSubtitleChoicesForTest();w.SetOffset(0.3);w.SetLyricsFollow(false);w.PausePlayback();
  w.OpenTrack(second,false,1.25);await w.SubtitleReady;w.settings.Tracks[second].Subtitle=secondSubtitle;await w.LoadSubtitleChoicesForTest();w.SetOffset(-0.4);w.PausePlayback();
  w.OpenTrack(first,false);await w.SubtitleReady;Check("音频A恢复独立进度与字幕",Math.Abs(w.engine.Position-2.4)<0.03&&w.settings.Tracks[first].Offset==0.3&&w.CurrentSubtitle=="下一句",$"pos={w.engine.Position}; text={w.CurrentSubtitle}");
  w.OpenTrack(second,false);await w.SubtitleReady;Check("音频B恢复独立进度与字幕",Math.Abs(w.engine.Position-1.25)<0.03&&w.settings.Tracks[second].Offset==-0.4&&w.CurrentSubtitle=="独立音轨字幕",$"pos={w.engine.Position}; text={w.CurrentSubtitle}");
  w.OpenTrack(first,false);await w.SubtitleReady;w.OpenLyricsForTest();w.SetLyricsFollow(false);await Delay(200);var scroll=FindScroll(w.SubtitleLines);scroll?.ScrollToTop();await Delay(100);var scrollBefore=scroll?.VerticalOffset;for(int i=0;i<5;i++){w.UpdateClock();await Delay(30);}Check("暂停浏览歌词不强制跟随",!w.settings.Tracks[first].FollowLyrics&&scroll?.VerticalOffset==scrollBefore,$"before={scrollBefore}; after={scroll?.VerticalOffset}");w.SetLyricsFollow(true);Check("可以恢复歌词跟随",w.settings.Tracks[first].FollowLyrics);
  w.Save();using(var child=StartProbe("--startup-probe",baseHome,"--resume-probe",second)){await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));var probe=JsonDocument.Parse(File.ReadAllText(Path.Combine(baseHome,"startup-probe.json"))).RootElement;
   Check("真实新进程启动主页且不自动播放",probe.GetProperty("initialPage").GetString()=="home"&&probe.GetProperty("initiallyStopped").GetBoolean());Check("重新启动恢复逐音频时间与歌词",Math.Abs(probe.GetProperty("position").GetDouble()-1.25)<0.03&&probe.GetProperty("subtitle").GetString()=="独立音轨字幕");}
  // The tap must return every PCM sample unchanged, with energy derived from that stream.
  var analysis=new AudioAnalysis{Enabled=true};var samples=new float[4800];for(int i=0;i<samples.Length;i++)samples[i]=(float)(Math.Sin(i*0.2)*0.03);var expected=(float[])samples.Clone();analysis.Observe(samples,0,samples.Length,2);var values=new float[40];analysis.Copy(values);
  Check("波形来自音频且不修改采样",analysis.Blocks>0&&values.Any(x=>x>0)&&samples.SequenceEqual(expected));analysis.Clear();analysis.Observe(new float[4800],0,4800,2);analysis.Copy(values);Check("静音波形能量为零",values.All(x=>x==0));
  w.OpenTrack(first,true,0);await w.SubtitleReady;w.settings.Visualizer=true;w.RefreshTimerForTest();await Delay(500);Check("真实播放驱动轻量波形",w.engine.Analysis.Blocks>0&&w.Wave.Running);
  w.PausePlayback();await Delay(800);Check("暂停后波形平滑停止",!w.Wave.Running&&!w.engine.Analysis.Enabled,$"running={w.Wave.Running}; enabled={w.engine.Analysis.Enabled}");w.engine.Play();w.RefreshTimerForTest();await Delay(300);Check("恢复播放波形继续",w.Wave.Running&&w.engine.Analysis.Enabled);
  await GameInput(w,first,false);await GameInput(w,first,true);w.WindowState=WindowState.Normal;w.Activate();w.overlay.Hide();w.OpenTrack(first,true,0);await w.SubtitleReady;w.SetOffset(0);w.Navigate("detail");await w.ImageReady;
 }
 static Process StartProbe(params string[] args){var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,WindowStyle=ProcessWindowStyle.Normal};foreach(var a in args)info.ArgumentList.Add(a);return Process.Start(info)!;}
 static JsonElement ReadGame(string path){using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);using var document=JsonDocument.Parse(stream);return document.RootElement.Clone();}
 static ScrollViewer? FindScroll(System.Windows.DependencyObject element){for(int i=0;i<VisualTreeHelper.GetChildrenCount(element);i++){var child=VisualTreeHelper.GetChild(element,i);if(child is ScrollViewer sv)return sv;var nested=FindScroll(child);if(nested!=null)return nested;}return null;}
 static async Task GameInput(MainWindow w,string track,bool borderless)
 {
  string label=borderless?"无边框游戏":"窗口化游戏";var directory=Path.Combine(State.Home,borderless?"game-borderless":"game-windowed");Directory.CreateDirectory(directory);var file=Path.Combine(directory,"game.json");if(File.Exists(file))File.Delete(file);
  NativeProbe.GetCursorPos(out var cursor);w.overlay.Hide();w.Hide();using var game=borderless?StartProbe("--game-probe",directory,"--borderless"):StartProbe("--game-probe",directory);IntPtr handle=IntPtr.Zero;
  try
  {
   for(int i=0;i<80&&!File.Exists(file);i++)await Delay(50);var initial=ReadGame(file);handle=new IntPtr(initial.GetProperty("hwnd").GetInt64());if(!NativeProbe.TargetTestWindow(handle))throw new InvalidOperationException("游戏输入验证未能获得前台，结果无效。");await Delay(200);
   NativeProbe.SetCursorPos(initial.GetProperty("left").GetInt32()+100,initial.GetProperty("top").GetInt32()+130);NativeProbe.Mouse(0x2);NativeProbe.Mouse(0x4);await Delay(200);var baseline=ReadGame(file);
   var foreground=NativeProbe.GetForegroundWindow();Check(label+"测试进程获得焦点",foreground==handle);
   var dpi=VisualTreeHelper.GetDpi(w.overlay);w.overlay.Left=(initial.GetProperty("left").GetInt32()+120)/dpi.DpiScaleX;w.overlay.Top=(initial.GetProperty("top").GetInt32()+210)/dpi.DpiScaleY;w.overlay.Width=600;w.overlay.Height=140;w.overlay.EndEditing();w.overlay.ShowPassive();w.overlay.SetText("游戏输入透传验证 · 字幕只显示");await Delay(150);
   Check(label+"显示字幕不抢焦点",NativeProbe.GetForegroundWindow()==handle&&w.overlay.NativeNoActivate&&w.overlay.NativeClickThrough&&w.overlay.DisplayBorder==0);
   var point=w.overlay.PointToScreen(new System.Windows.Point(200,60));NativeProbe.SetCursorPos((int)point.X,(int)point.Y);await Delay(50);bool injected=NativeProbe.Mouse(0x2);await Delay(50);NativeProbe.SetCursorPos((int)point.X+25,(int)point.Y+4);await Delay(50);injected&=NativeProbe.Mouse(0x4);injected&=NativeProbe.Mouse(0x800,120);injected&=NativeProbe.Key(0x25,false);injected&=NativeProbe.Key(0x25,true);await Delay(200);
   var status=ReadGame(file);Check(label+"鼠标移动点击滚轮拖动键盘透传",injected&&new[]{"moves","downs","ups","wheels","drags","keys"}.All(n=>status.GetProperty(n).GetInt32()>baseline.GetProperty(n).GetInt32()),status.ToString());
   var before=NativeProbe.ForegroundDescription();w.overlay.SetText("切换音轨后仍不激活");var afterText=NativeProbe.ForegroundDescription();w.OpenTrack(track,true,0);await w.SubtitleReady;var afterTrack=NativeProbe.ForegroundDescription();w.overlay.ShowPassive();var afterShow=NativeProbe.ForegroundDescription();await Delay(150);var afterDelay=NativeProbe.ForegroundDescription();Check(label+"字幕更新和切歌不改变焦点",NativeProbe.GetForegroundWindow()==handle,$"game pid={game.Id}; player pid={Environment.ProcessId}; before: {before}; text: {afterText}; track: {afterTrack}; show: {afterShow}; settled: {afterDelay}");
  }
  finally{w.overlay.Hide();if(handle!=IntPtr.Zero)NativeProbe.PostMessage(handle,0x10,IntPtr.Zero,IntPtr.Zero);await game.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));NativeProbe.SetCursorPos(cursor.X,cursor.Y);w.Show();}
 }
}
