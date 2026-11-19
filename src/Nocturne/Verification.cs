using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NAudio.Wave;
using SkiaSharp;

namespace Nocturne;
public static partial class Verification
{
 static readonly List<object> checks=new();static readonly List<object> performance=new();static int failures;
 static void Check(string name,bool passed,string detail="",bool allowClosed=false){if(!allowClosed&&Application.Current.MainWindow is MainWindow w&&w.ShuttingDown)throw new OperationCanceledException("验证窗口提前关闭，后续检查无效："+name);checks.Add(new{name,passed,detail});if(!passed)failures++;State.Write("verification-progress.json",checks);}
 static async Task Delay(int ms)=>await Task.Delay(ms);
 public static async Task Run(MainWindow w)
 {
  try
  {
   await w.CatalogReady;
   if(Environment.GetCommandLineArgs().Contains("--branding-release"))
   {
    await VerifyBrandingRelease(w);
    State.Write("verification.json",new{completed=DateTimeOffset.Now,failures,checks});
    return;
   }
   var inputReady=false;for(int attempt=0;attempt<20&&!inputReady;attempt++){w.Activate();inputReady=NativeProbe.TargetTestWindow(new System.Windows.Interop.WindowInteropHelper(w).Handle);if(!inputReady)await Delay(50);}if(!inputReady)throw new InvalidOperationException("验证原生窗口无法取得前台："+NativeProbe.ForegroundDescription());
   if(Environment.GetCommandLineArgs().Contains("--revision15-only")){await VerifyOverlayResize(w);State.Write("verification.json",new{completed=DateTimeOffset.Now,failures,checks});return;}
   if(Environment.GetCommandLineArgs().Contains("--revision14-only")){await VerifyRevision14(w);State.Write("verification.json",new{completed=DateTimeOffset.Now,failures,checks});return;}
   if(Environment.GetCommandLineArgs().Contains("--revision13-only")){await VerifyRevision13(w);State.Write("verification.json",new{completed=DateTimeOffset.Now,failures,checks});return;}
   if(Environment.GetCommandLineArgs().Contains("--revision12-only")||Environment.GetCommandLineArgs().Contains("--repro12")){await VerifyRevision12(w,Environment.GetCommandLineArgs().Contains("--repro12"));State.Write("verification.json",new{completed=DateTimeOffset.Now,failures,checks});return;}
   if(Environment.GetCommandLineArgs().Contains("--repro11")){await Reproduce11(w);return;}
   if(Environment.GetCommandLineArgs().Contains("--revision11-only")){await VerifyRevision11(w);State.Write("verification.json",new{completed=DateTimeOffset.Now,failures,checks,performance});return;}
   if(Environment.GetCommandLineArgs().Contains("--frames11-only")){await FramesOnly11(w);State.Write("verification.json",new{completed=DateTimeOffset.Now,failures,checks,performance});return;}
   if(Environment.GetCommandLineArgs().Contains("--revision10-only")){await VerifyRevision10(w);State.Write("verification.json",new{completed=DateTimeOffset.Now,failures,checks,performance});return;}
   if(Environment.GetCommandLineArgs().Contains("--revision9-only")){await VerifyRevision9(w);State.Write("verification.json",new{completed=DateTimeOffset.Now,failures,checks,performance});return;}
   if(Environment.GetCommandLineArgs().Contains("--revision8-only")){await VerifyRevision8(w);State.Write("verification.json",new{completed=DateTimeOffset.Now,failures,checks,performance});return;}
   if(Environment.GetCommandLineArgs().Contains("--revision7-only")){await VerifyRevision7(w);State.Write("verification.json",new{completed=DateTimeOffset.Now,failures,checks,performance});return;}
   if(Environment.GetCommandLineArgs().Contains("--revision6-only")){await VerifyRevision6(w);State.Write("verification.json",new{completed=DateTimeOffset.Now,failures,checks,performance});return;}
   if(Environment.GetCommandLineArgs().Contains("--revision5-only")){await VerifyRevision5(w);State.Write("verification.json",new{completed=DateTimeOffset.Now,failures,checks,performance});return;}
   if(Environment.GetCommandLineArgs().Contains("--revision4-only")){await VerifyRevision4(w);State.Write("verification.json",new{completed=DateTimeOffset.Now,failures,checks,performance});return;}
   if(Environment.GetCommandLineArgs().Contains("--revision3-only")){await VerifyRevision3(w);State.Write("verification.json",new{completed=DateTimeOffset.Now,failures,checks,performance});return;}
   if(Environment.GetCommandLineArgs().Contains("--video-only")){await VerifyVideo(w);State.Write("verification.json",new{completed=DateTimeOffset.Now,failures,checks,performance});return;}
   if(Environment.GetCommandLineArgs().Contains("--game-only")){await ProbeGames(w);State.Write("verification.json",new{completed=DateTimeOffset.Now,failures,checks});return;}
   Check("每次启动进入分类主页且不自动播放",w.CurrentPage=="home"&&w.currentTrack==null&&!w.engine.Playing);
   var root=Path.Combine(State.Home,"素材 日本語 & 中文 [测试]");var cd1=Path.Combine(root,"作品 星夜","CD1");var cd2=Path.Combine(root,"作品 星夜","CD2");var none=Path.Combine(root,"无图片作品");Directory.CreateDirectory(cd1);Directory.CreateDirectory(cd2);Directory.CreateDirectory(none);
   WriteWave(Path.Combine(cd1,"2 耳语.wav"),45);WriteWave(Path.Combine(cd1,"10 安眠.wav"),4);WriteWave(Path.Combine(cd2,"01 日本語.wav"),4);WriteWave(Path.Combine(none,"01 无字幕.wav"),4);
   var huge=Path.Combine(cd2,"02 大音频.wav");WriteLargeWave(huge,1800);
   File.WriteAllText(Path.Combine(cd1,"2 耳语.lrc"),"[offset:100]\n[00:00.00]开始\n[00:02.00][00:04.00]多时间标签\n[00:06.00]夜晚",new UTF8Encoding(true));
   File.WriteAllText(Path.Combine(cd1,"2 耳语.vtt"),"WEBVTT\n\nintro\n00:00.000 --> 00:01.000\n<v 林><b>中文 &amp; 日本語</b>\n第二行\n\n00:02.000 --> 00:03.500 align:center\n下一句\n\nNOTE ignore\nignored\n",new UTF8Encoding(true));
   using(var bmp=new SKBitmap(900,900)){using var canvas=new SKCanvas(bmp);canvas.Clear(new SKColor(28,38,63));using var paint=new SKPaint{Color=new SKColor(153,147,189),IsAntialias=true};canvas.DrawCircle(570,300,130,paint);paint.Color=new SKColor(28,38,63);canvas.DrawCircle(620,265,130,paint);paint.Color=new SKColor(200,205,225);foreach(var p in new[]{(80,180),(170,460),(740,580),(400,110),(680,140)})canvas.DrawCircle(p.Item1,p.Item2,2,paint);using var img=SKImage.FromBitmap(bmp);using var data=img.Encode(SKEncodedImageFormat.Png,90);File.WriteAllBytes(Path.Combine(root,"作品 星夜","cover.png"),data.ToArray());using var webp=img.Encode(SKEncodedImageFormat.Webp,80);File.WriteAllBytes(Path.Combine(cd1,"02 插图.webp"),webp.ToArray());}
   // Large, nested fixture tests scanning responsiveness and parent grouping without duplicate tracks.
   for(int i=0;i<1200;i++){var d=Path.Combine(root,"目录压力",$"系列{i/100:00}",$"作品{i:0000}");Directory.CreateDirectory(d);File.WriteAllText(Path.Combine(d,"readme.txt"),"fixture");}
   w.settings.Roots.Clear();w.settings.Roots.Add(root);int heartbeats=0;var uiTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(10)};uiTimer.Tick+=(_,_)=>heartbeats++;uiTimer.Start();await w.Scan(true);uiTimer.Stop();
   Check("多级目录后台扫描",w.library.Folders.Count>=1200&&w.library.Folders.Values.Sum(x=>x.Audio.Count)==5&&w.library.ScanThreadId!=Environment.CurrentManagedThreadId,$"folders={w.library.Folders.Count}; worker thread={w.library.ScanThreadId}; UI thread={Environment.CurrentManagedThreadId}; UI ticks={heartbeats}");
   Check("自然排序",w.library.Folders[cd1].Audio[0].EndsWith("2 耳语.wav"));
   Check("父子分组无重复音轨",w.library.Folders[Path.Combine(root,"作品 星夜")].Audio.Count==0&&w.library.Folders[cd2].Audio.Count==2);
   await w.Scan(false);Check("缓存增量扫描",w.library.FilesEnumerated==0,"enumerated files="+w.library.FilesEnumerated+"; "+w.library.CacheDiagnostics);
   var lrc=SubtitleDocument.Load(Path.Combine(cd1,"2 耳语.lrc"),"自动");Check("LRC BOM / 多标签 / offset",lrc.Cues.Count==4&&lrc.Active(1.95).Single()==1&&Math.Abs(lrc.Cues[1].Start-1.9)<0.00001);
   var vtt=SubtitleDocument.Load(Path.Combine(cd1,"2 耳语.vtt"),"自动");Check("VTT 多行与标记",vtt.Cues[0].Text=="中文 & 日本語\n第二行");Check("VTT 空白区清空",vtt.Active(1.5).Count==0&&vtt.Active(3.5).Count==0);
   var sjis=Path.Combine(State.Home,"shift-jis.lrc");File.WriteAllText(sjis,"[00:01.00]おやすみ",Encoding.GetEncoding(932));Check("Shift-JIS 自动回退",SubtitleDocument.Load(sjis,"自动").Cues[0].Text=="おやすみ");
   var gb=Path.Combine(State.Home,"gb.lrc");File.WriteAllText(gb,"[00:01.00]晚安",Encoding.GetEncoding("GB18030"));Check("GB18030 手动编码",SubtitleDocument.Load(gb,"GB18030").Cues[0].Text=="晚安");
   w.SelectFolder(cd1);w.settings.Background=1;w.settings.Stars=0;w.UpdateBackground();await Delay(1200);
   await Measure("空闲_静态星空",w);
   var path=Path.Combine(cd1,"2 耳语.wav");Check("真实 WAV 音频设备播放",w.OpenTrack(path,true,0));await w.SubtitleReady;await Delay(650);Check("设备位置前进",w.engine.Position>0.25&&w.engine.Channels==2,$"position={w.engine.Position:F3}, channels={w.engine.Channels}");
   w.engine.Pause();var paused=w.engine.Position;await Delay(200);Check("暂停时钟冻结",Math.Abs(w.engine.Position-paused)<0.02);
   w.settings.Tracks[path].Subtitle=Path.Combine(cd1,"2 耳语.vtt");await w.LoadSubtitleChoicesForTest();w.Seek(0.4);Check("暂停定位后字幕立即同步",w.CurrentSubtitle.Contains("第二行"));w.SetOffset(1);Check("暂停调整偏移立即刷新",w.CurrentSubtitle=="");w.SetOffset(0);w.Seek(1.5);Check("暂停定位到音频 VTT 空档保留上一句",w.CurrentSubtitle.Contains("第二行"));w.Seek(2.3);Check("定位到下一条字幕",w.CurrentSubtitle=="下一句");
   w.engine.Play();w.RefreshTimerForTest();await Delay(500);Check("拖动后恢复播放",w.engine.Playing&&w.engine.Position>2.5);
   Check("流式大 WAV 打开定位",w.OpenTrack(huge,true,900)&&w.engine.Duration>1799&&w.engine.Position>=899.9,$"file bytes={new FileInfo(huge).Length}");await w.SubtitleReady;await Delay(300);Check("大音频实际播放时钟",w.engine.Position>900.1);
   using(var reader=new AudioFileReader(path)){var samples=new float[4000];reader.Read(samples,0,samples.Length);Check("立体声未折叠",reader.WaveFormat.Channels==2&&Enumerable.Range(0,1000).Any(i=>Math.Abs(samples[i*2]-samples[i*2+1])>0.001));}
   // Encode our generated sample using installed Windows encoders; no external media downloads.
   foreach(var format in new[]{"mp3","m4a"})try{var encoded=Path.Combine(State.Home,"generated."+format);using(var reader=new AudioFileReader(path)){if(format=="mp3")MediaFoundationEncoder.EncodeToMp3(reader,encoded);else MediaFoundationEncoder.EncodeToAac(reader,encoded);}Check("实际解码 "+format,w.OpenTrack(encoded,true,0));await w.SubtitleReady;await Delay(250);Check(format+" 时钟",w.engine.Position>0.05);}catch(Exception ex){Check("实际解码 "+format,false,ex.ToString());}
   var codecFixtures=Environment.GetEnvironmentVariable("NOCTURNE_CODEC_FIXTURES");
   foreach(var ext in new[]{"flac","ogg"}){var sample=Path.Combine(codecFixtures??"test-fixtures","generated."+ext);Check("实际解码 "+ext,File.Exists(sample)&&w.OpenTrack(sample,true,0));await w.SubtitleReady;await Delay(300);Check(ext+" 设备时钟",w.engine.Position>0.1);w.Seek(1.5);await Delay(200);Check(ext+" 定位播放",w.engine.Position>1.6);}
   w.OpenTrack(path,true,0);await w.SubtitleReady;var scanPosition=w.engine.Position;await w.Scan(true);await Delay(120);Check("后台扫描不停止播放",w.engine.Playing&&w.engine.Position>scanPosition+0.08);
   w.library.Watch(w.settings.Roots);var added=Path.Combine(cd2,"03 变更.wav");File.Copy(Path.Combine(cd2,"01 日本語.wav"),added,true);for(int i=0;i<40&&w.library.Folders[cd2].Audio.Count!=3;i++)await Delay(100);Check("监听合并后更新索引",w.library.Folders[cd2].Audio.Count==3);File.Delete(added);for(int i=0;i<40&&w.library.Folders[cd2].Audio.Count!=2;i++)await Delay(100);Check("监听删除后更新索引",w.library.Folders[cd2].Audio.Count==2);
   w.settings.Loop=2;w.OpenTrack(path,true,44.8);await w.SubtitleReady;await Delay(650);Check("真实播放结束后单曲循环",w.currentTrack==path&&w.engine.Playing&&w.engine.Position<2);
   w.settings.Loop=0;w.OpenTrack(path,true,44.8);await w.SubtitleReady;await Delay(650);Check("真实播放结束后顺序下一首",w.currentTrack==Path.Combine(cd1,"10 安眠.wav")&&w.engine.Playing);
   w.OpenTrack(Path.Combine(cd1,"10 安眠.wav"),true,3.8);await w.SubtitleReady;await Delay(650);Check("顺序列表末尾停止",!w.engine.Playing);
   w.settings.Loop=1;w.OpenTrack(Path.Combine(cd1,"10 安眠.wav"),true,3.8);await w.SubtitleReady;await Delay(650);Check("真实播放结束后列表循环",w.currentTrack==path&&w.engine.Playing);w.settings.Loop=0;
   w.OpenTrack(path,true,0);await w.SubtitleReady;var heapBefore=GC.GetTotalMemory(true);var handlesBefore=Process.GetCurrentProcess().HandleCount;for(int i=0;i<60;i++){w.OpenTrack(i%2==0?path:Path.Combine(cd1,"10 安眠.wav"),false,0);await w.SubtitleReady;await Delay(10);}var heapAfter=GC.GetTotalMemory(true);var handlesAfter=Process.GetCurrentProcess().HandleCount;Check("60 次切歌对象与句柄有界",heapAfter-heapBefore<16*1048576&&handlesAfter-handlesBefore<25,$"retained managed delta={heapAfter-heapBefore}; handles delta={handlesAfter-handlesBefore}; diagnostic GC only, outside performance samples");
   await VerifyRevision(w,root,cd1,cd2,path);
   w.OpenTrack(path,true,0);await w.SubtitleReady;
   w.Search.Text="作品 星夜";await w.BuildTree();Check("搜索父作品保留 CD 音轨",w.Tracks.Items.Count==2);w.Search.Text="";await w.BuildTree();
   w.settings.Background=1;w.settings.Stars=0;w.UpdateBackground();await Delay(800);await Measure("正常播放_静态星空",w);
   w.settings.Stars=2;w.settings.Visualizer=true;w.settings.ReduceMotion=false;w.Navigate("detail");w.UpdateVisuals();w.RefreshTimerForTest();await Delay(800);await Measure("播放_星空图片过渡页面切换波形",w);Check("星空在可见时运行",w.Stars.Running);
   w.overlay.EndEditing();w.overlay.ShowPassive();w.WindowState=WindowState.Minimized;await Delay(500);var analyzed=w.engine.Analysis.Blocks;var frames=w.Wave.Frames;Check("最小化停止星空与波形继续音频",!w.Stars.Running&&!w.Wave.Running&&!w.engine.Analysis.Enabled&&w.engine.Playing);await Measure("最小化_桌面字幕",w);Check("最小化无隐藏分析和波形绘制",w.engine.Analysis.Blocks==analyzed&&w.Wave.Frames==frames,$"blocks={analyzed}->{w.engine.Analysis.Blocks}; frames={frames}->{w.Wave.Frames}; state={w.WindowState}; wave={w.Wave.Running}; analysis={w.engine.Analysis.Enabled}");
   w.overlay.SetClickThrough(true);Check("原生鼠标穿透启用",w.overlay.NativeClickThrough);w.RestoreFromTrayForTest();Check("托盘菜单解除穿透",!w.overlay.NativeClickThrough&&!w.settings.OverlayLocked);
   w.overlay.ShowPassive();Check("显示字幕自动结束编辑恢复安全模式",!w.overlay.Editing&&w.overlay.NativeClickThrough&&w.overlay.NativeNoActivate&&w.settings.OverlayLocked);
   w.overlay.RestoreEditing();w.overlay.Close();Check("关闭编辑字幕恢复穿透并隐藏",!w.overlay.IsVisible&&!w.overlay.Editing&&w.overlay.NativeClickThrough);
   w.overlay.Left=-50000;w.overlay.Top=-50000;OverlayWindow.ClampToScreens(w.overlay);Check("屏幕外位置恢复",w.overlay.Left>-50000&&w.overlay.Top>-50000);
   w.WindowState=WindowState.Normal;w.overlay.Hide();w.OpenTrack(Path.Combine(none,"01 无字幕.wav"),false,0);await w.SubtitleReady;Check("无字幕无图片不残留",w.document.Cues.Count==0&&w.Picture.Source==null&&w.Backdrop.Source==null&&w.CurrentSubtitle=="");
   w.SelectFolder(cd2);await w.ImageReady;Check("向上找封面不越根",w.Picture.Source!=null&&w.PicturePaths.All(x=>x.StartsWith(root)));
   for(int i=0;i<9;i++){var p=Path.Combine(State.Home,$"cache{i}.png");File.Copy(Path.Combine(root,"作品 星夜","cover.png"),p,true);w.imageCache.Load(p);}Check("图片缓存容量限制",w.imageCache.Count<=4,"count="+w.imageCache.Count);
   var broken=Path.Combine(State.Home,"broken.mp3");File.WriteAllText(broken,"not audio");Check("损坏音频不崩溃",!w.OpenTrack(broken,true));await w.SubtitleReady;
   w.OpenTrack(path,false,2.2);await w.SubtitleReady;w.SetOffset(0.3);w.Save();var saved=State.Read<Settings>("settings.json");Check("作品进度与字幕偏移保存",saved.Resume[cd1].Seconds>=2.1&&saved.Tracks[path].Offset==0.3);
   var offline=Path.Combine(State.Home,"offline-root");w.settings.Roots.Add(offline);await w.BuildTree();w.RemoveMediaRoot(offline);Check("离线根目录可以移除",!w.settings.Roots.Contains(offline));w.SelectFolder(cd1);
   w.settings.Background=0;w.UpdateBackground();await w.BuildTree();await w.ImageReady;w.Navigate("detail");await Delay(500);Snapshot(w,Path.Combine(State.Home,"main-window.png"));w.Navigate("home");await Delay(500);Snapshot(w,Path.Combine(State.Home,"home.png"));w.OpenLyricsForTest();await Delay(300);Snapshot(w,Path.Combine(State.Home,"lyrics.png"));w.overlay.EndEditing();w.overlay.ShowPassive();w.overlay.SetText("中文 & 日本語\n多行桌面字幕预览");await Delay(200);Snapshot(w.overlay,Path.Combine(State.Home,"desktop-subtitles.png"));
   await VerifyVideo(w);
   await VerifyRevision3(w);
   await VerifyRevision4(w);
   await VerifyRevision5(w);
   await VerifyRevision6(w);
   State.Write("verification.json",new{completed=DateTimeOffset.Now,failures,checks,performance,environment=new{os=Environment.OSVersion.ToString(),cpu=Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER"),logicalProcessors=Environment.ProcessorCount,runtime=Environment.Version.ToString(),renderTier=RenderCapability.Tier>>16,is64Bit=Environment.Is64BitProcess},method="Process.TotalProcessorTime delta / elapsed / logical CPUs; WorkingSet64 and PrivateMemorySize64; 12 samples at 1 second. GPU sampled separately by Windows GPU Engine counters."});
  }
  catch(Exception ex){State.Write("fatal-test-error.json",new{error=ex.ToString(),checks,performance});failures++;}
  finally{w.ExitApplication();Application.Current.Shutdown(failures==0?0:1);}
 }
 static async Task Measure(string name,MainWindow w)
 {
  var p=Process.GetCurrentProcess();var before=p.TotalProcessorTime;var started=DateTimeOffset.Now;var sw=Stopwatch.StartNew();var working=new List<long>();var privateBytes=new List<long>();
  State.Write("performance-phase.json",new{phase=name,pid=p.Id,started});
  for(int i=0;i<12;i++){if(name.Contains("图片过渡")){w.Navigate(i%3==0?"lyrics":"detail");if(i%3!=0)w.BrowsePicture(1);}if(name.Contains("三栏"))w.BrowsePicture(1);await Delay(1000);p.Refresh();working.Add(p.WorkingSet64);privateBytes.Add(p.PrivateMemorySize64);}p.Refresh();
  performance.Add(new{name,pid=p.Id,started,ended=DateTimeOffset.Now,seconds=sw.Elapsed.TotalSeconds,cpuPercent=(p.TotalProcessorTime-before).TotalSeconds/sw.Elapsed.TotalSeconds/Environment.ProcessorCount*100,workingSetMiB=working.Average()/1048576,privateMiB=privateBytes.Average()/1048576,peakWorkingSetMiB=working.Max()/1048576,starsRunning=w.Stars.Running,playing=w.PlaybackPlaying,overlayVisible=w.overlay.IsVisible});State.Write("performance.json",performance);
 }
 static void Snapshot(Window w,string path){w.UpdateLayout();var bmp=new RenderTargetBitmap((int)w.ActualWidth,(int)w.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(w);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using var f=File.Create(path);encoder.Save(f);}
 static void WriteWave(string path,int seconds){using var writer=new WaveFileWriter(path,new WaveFormat(48000,16,2));var block=new byte[48000*4];for(int t=0;t<seconds;t++){for(int i=0;i<48000;i++){short l=(short)(Math.Sin(i*2*Math.PI*220/48000)*700),r=(short)(Math.Sin(i*2*Math.PI*440/48000)*700);block[i*4]=(byte)l;block[i*4+1]=(byte)(l>>8);block[i*4+2]=(byte)r;block[i*4+3]=(byte)(r>>8);}writer.Write(block,0,block.Length);}}
 static void WriteLargeWave(string path,int seconds){using var stream=File.Create(path);using var b=new BinaryWriter(stream);var length=seconds*48000*4;b.Write(Encoding.ASCII.GetBytes("RIFF"));b.Write(length+36);b.Write(Encoding.ASCII.GetBytes("WAVEfmt "));b.Write(16);b.Write((short)1);b.Write((short)2);b.Write(48000);b.Write(192000);b.Write((short)4);b.Write((short)16);b.Write(Encoding.ASCII.GetBytes("data"));b.Write(length);stream.SetLength(length+44L);}
}
