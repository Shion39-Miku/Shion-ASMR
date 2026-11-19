using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SkiaSharp;

namespace Nocturne;
public static partial class Verification
{
 static IEnumerable<BrowserTile> Tiles(MainWindow w)=>w.WorksList.Items.Cast<HomeRow>().SelectMany(r=>r.Entries);
 static readonly List<object> pointerInputs=new();
 static async Task MoveToRail(MainWindow w,Slider slider,double fraction,double? y=null)
 {
  w.UpdateLayout();var p=RailPoint(slider,fraction,y);var handle=new WindowInteropHelper(w).Handle;
  if(!NativeProbe.TargetTestWindow(handle))throw new Exception("原生进度验证无法获得前台");
  await Delay(70);NativeProbe.SetCursorPos((int)p.X,(int)p.Y);await Delay(40);NativeProbe.GetCursorPos(out var actual);
  pointerInputs.Add(new{slider=slider.Name,fraction,target=p,actualX=actual.X,actualY=actual.Y,hit=NativeProbe.PointTargets(handle,actual.X,actual.Y),foreground=NativeProbe.ForegroundDescription()});
  if(!NativeProbe.PointTargets(handle,actual.X,actual.Y)||Math.Abs(actual.X-p.X)>2||Math.Abs(actual.Y-p.Y)>2)throw new Exception("系统指针或遮挡使进度输入检查无效");
 }
 static Point RailPoint(Slider slider,double fraction,double? y=null)
 {
  var track=(Track)slider.Template.FindName("PART_Track",slider);var thumb=track.Thumb.ActualWidth;
  var point=track.PointToScreen(new Point(thumb/2+fraction*(track.ActualWidth-thumb),track.ActualHeight/2));
  if(y!=null)point.Y=slider.PointToScreen(new Point(0,y.Value)).Y;return point;
 }
 static async Task RailClick(MainWindow w,Slider slider,double fraction,double? y=null)
 {
  await MoveToRail(w,slider,fraction,y);NativeProbe.Mouse(2);await Delay(80);NativeProbe.Mouse(4);await Delay(110);w.UpdateClock();
 }
 static async Task RailDrag(MainWindow w,Slider slider,Action<bool> midway)
 {
  await MoveToRail(w,slider,.2);NativeProbe.Mouse(2);await Delay(100);
  try{await MoveToRail(w,slider,.6);await Delay(150);midway(false);await MoveToRail(w,slider,.4);await Delay(150);midway(true);}
  finally{NativeProbe.Mouse(4);await Delay(120);}
 }
 static bool FullCoverPixels(CoverView view)
 {
  var bitmap=new RenderTargetBitmap((int)view.ActualWidth,(int)view.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(view);var bytes=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(bytes,bitmap.PixelWidth*4,0);var colors=new int[4];
  for(int i=0;i<bytes.Length;i+=4){var b=bytes[i];var g=bytes[i+1];var r=bytes[i+2];if(r>200&&g<70&&b<70)colors[0]++;if(g>200&&r<70&&b<70)colors[1]++;if(r>200&&g>200&&b<70)colors[2]++;if(r>200&&b>200&&g<70)colors[3]++;}
  return colors.All(c=>c>3)&&Visuals<Image>(view).Single().Stretch==Stretch.Uniform;
 }
 static void WriteDynamics(string path)
 {
  using var writer=new NAudio.Wave.WaveFileWriter(path,new NAudio.Wave.WaveFormat(48000,16,2));var block=new byte[48000*4];
  for(int second=0;second<6;second++){var level=second<2?.0001:second<4?.004:.03;for(int i=0;i<48000;i++)for(int ch=0;ch<2;ch++){var sample=(short)(32767*level*Math.Sin(i*2*Math.PI*(ch==0?220:440)/48000));block[i*4+ch*2]=(byte)sample;block[i*4+ch*2+1]=(byte)(sample>>8);}writer.Write(block,0,block.Length);}
 }
 static async Task VerifyRevision6(MainWindow w)
 {
  w.library.Watch(Array.Empty<string>());w.settings.ReduceMotion=true;w.WindowState=WindowState.Normal;w.Width=1180;w.Height=850;w.Left=50;w.Top=40;w.Topmost=true;w.overlay.Hide();
  var root=Path.Combine(State.Home,"v16 层级・grid");var circlePath=Path.Combine(root,"社团","星音社");var paths=new[]{Path.Combine(circlePath,"系列甲","子系列","作品 一"),Path.Combine(circlePath,"系列甲","作品 二"),Path.Combine(circlePath,"单独作品 中文・日本語 mixed title 很长的名称用于检查两行标题换行和完整名称提示"),Path.Combine(root,"社团","別サークル","作品 三")};
  foreach(var dir in paths){Directory.CreateDirectory(dir);WriteWave(Path.Combine(dir,"01 晚安.part.wav"),40);}
  var audio=Path.Combine(paths[0],"01 晚安.part.wav");File.WriteAllText(Path.ChangeExtension(audio,"vtt"),"WEBVTT\n\n00:00.000 --> 00:10.000\n第一句・かな\n\n00:10.000 --> 00:20.000\n第二句\n\n00:20.000 --> 00:39.500\n第三句・星光\n");
  for(int i=0;i<3;i++)
  {
   using var bitmap=new SKBitmap(i==0?800:i==1?400:600,i==0?400:i==1?800:600);using var canvas=new SKCanvas(bitmap);canvas.Clear(new SKColor(35,48,78));using var paint=new SKPaint();
   var colors=new[]{SKColors.Red,SKColors.Lime,SKColors.Yellow,SKColors.Magenta};var xs=new[]{0,bitmap.Width-48,0,bitmap.Width-48};var ys=new[]{0,0,bitmap.Height-48,bitmap.Height-48};for(int j=0;j<4;j++){paint.Color=colors[j];canvas.DrawRect(xs[j],ys[j],48,48,paint);}using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(SKEncodedImageFormat.Png,90);File.WriteAllBytes(Path.Combine(paths[i],"cover.png"),data.ToArray());
  }
  w.settings.Roots.Add(root);await w.Scan(true);await w.FilterForTest(null,null);w.Navigate("home");await Delay(150);
  var circle=Organization.CircleId("星音社");var series=w.organization.ChildFolders(circle).Single(f=>f.Name=="系列甲");var sub=w.organization.ChildFolders(series.Id).Single();
  w.CircleChoices.SelectedItem=w.CircleChoices.Items.Cast<MainWindow.CircleOption>().Single(c=>c.FolderId==circle);await w.HomeReady;await Delay(200);
  Check("选择社团实际显示系列文件夹与直属作品",Tiles(w).Any(t=>t.Folder?.Id==series.Id)&&Tiles(w).Count(t=>t.Work!=null)==1&&w.browserFolder==circle&&!Tiles(w).Any(t=>t.Work?.Path==paths[0]));
  var button=Visuals<Button>(w.WorksList).Single(b=>b.Tag is BrowserTile t&&t.Folder?.Id==series.Id);Snapshot(w,Path.Combine(State.Home,"v16-before-series.png"));var clickPoint=button.PointToScreen(new Point(30,button.ActualHeight/2));var clicks=0;button.Click+=(_,_)=>clicks++;await ClickElement(button);await w.HomeReady;await Delay(150);Snapshot(w,Path.Combine(State.Home,"v16-after-series.png"));
  Check("原生点击系列进入作品和子系列",w.browserFolder==series.Id&&Tiles(w).Any(t=>t.Folder?.Id==sub.Id)&&Tiles(w).Any(t=>t.Work?.Path==paths[1]),$"clicks={clicks}; folder={w.browserFolder}; tiles={string.Join(',',Tiles(w).Select(t=>t.Name))}; size={button.ActualWidth}×{button.ActualHeight}; screen={clickPoint}; foreground={NativeProbe.ForegroundDescription()}");
  Check("面包屑显示社团和系列完整路径",w.Breadcrumb.Items.OfType<Button>().Select(b=>b.ToolTip as string).SequenceEqual(new[]{"社团","星音社","系列甲"})&&w.FolderBreadcrumbBar.Visibility==Visibility.Visible);
  button=Visuals<Button>(w.WorksList).Single(b=>b.Tag is BrowserTile t&&t.Folder?.Id==sub.Id);await ClickElement(button);await w.HomeReady;Check("任意子系列层级保持作品关联",w.browserFolder==sub.Id&&Tiles(w).Any(t=>t.Work?.Path==paths[0]));
  var back=Visuals<Button>(w.HomePage).Single(b=>Equals(b.ToolTip,"返回上一级"));await ClickElement(back);await w.HomeReady;Check("返回上一级实际恢复系列",w.browserFolder==series.Id&&Tiles(w).Any(t=>t.Work?.Path==paths[1]),$"folder={w.browserFolder}; tiles={string.Join(',',Tiles(w).Select(t=>t.Name))}");
  MetadataStore.AddTag(w.settings,new[]{paths[0]},"Shion 标签");var manual="manual:shion-verification";w.settings.Organization[manual]=new(){Id=manual,Parent=circle,Name="手动系列"};w.settings.WorkLocations[paths[0]]=manual;w.settings.WorkCovers[paths[0]]=Path.Combine(paths[0],"cover.png");w.settings.Tracks[audio]=new(){Position=12.5,Subtitle=Path.ChangeExtension(audio,"vtt"),Offset=.25};w.Save();await w.Scan(true);
  Check("重扫保留旧手动系列及逐轨字幕封面标签",w.organization.Locations[paths[0]]==manual&&w.settings.Tracks[audio].Position==12.5&&w.settings.WorkCovers[paths[0]].EndsWith("cover.png")&&w.works.Single(x=>x.Path==paths[0]).Tags.Contains("Shion 标签"));
  await w.EnterFolder(circle);Check("社团下显示手动系列不再平铺其作品",Tiles(w).Any(t=>t.Folder?.Id==manual)&&!Tiles(w).Any(t=>t.Work?.Path==paths[0]));
  await w.FilterForTest(null,"Shion 标签");Check("标签快捷筛选跨系列且不复制作品",Tiles(w).Single().Work?.Path==paths[0]);
  var savedHome=State.Home;try
  {
   State.Home=Path.Combine(savedHome,"v15-compatibility");var old=new Settings{SchemaVersion=5,WaveHeight=100};old.Roots.Add(root);old.CustomTags.Add("旧标签");old.Organization[manual]=new(){Id=manual,Parent=circle,Name="保留的系列"};old.Organization[circle]=new(){Id=circle,Name="星音社"};old.WorkLocations[paths[0]]=manual;old.WorkCovers[paths[0]]=Path.Combine(paths[0],"cover.png");old.Tracks[audio]=new(){Position=17.5,Subtitle=Path.ChangeExtension(audio,"vtt"),Offset=.7};State.Write("settings.json",old);old=State.Read<Settings>("settings.json");old.Migrate();State.Write("settings.json",old);var restored=State.Read<Settings>("settings.json");Check("1.5旧数据原键迁移保留系列封面字幕与进度",restored.WorkLocations[paths[0]]==manual&&restored.Organization[manual].Parent==circle&&restored.Tracks[audio].Position==17.5&&restored.Tracks[audio].Offset==.7&&restored.Tracks[audio].Subtitle==Path.ChangeExtension(audio,"vtt")&&restored.CustomTags.Contains("旧标签")&&restored.WorkCovers[paths[0]].EndsWith("cover.png")&&restored.WaveHeight==32&&File.Exists(Path.Combine(State.Home,"settings.json.v1.5.bak")));
  }finally{State.Home=savedHome;}
  await w.FilterForTest(null,null,"单独作品");await Delay(350);w.UpdateLayout();var layouts=new List<object>();
  foreach(var width in new[]{820d,1180d,1600d,2100d})
  {
   w.Width=width;await Delay(250);await w.HomeReady;w.UpdateLayout();var cols=w.Columns();var viewport=w.WorksList.ActualWidth;
   Check($"网格按实际视口宽度排布 {width}",cols==Math.Clamp((int)Math.Floor((viewport-18)/184),1,6)&&cols>=3&&cols<=6&&w.WorksList.Items.Cast<HomeRow>().All(r=>r.Columns==cols),$"viewport={viewport:F1}; columns={cols}");
   var title=Visuals<TextBlock>(w.WorksList).Single(t=>t.Text.StartsWith("单独作品"));Check($"长标题换行和完整提示 {width}",title.TextWrapping==TextWrapping.Wrap&&title.ActualHeight<=46.1&&title.ToolTip as string==title.Text&&PageBounds(title,w).Right<w.ActualWidth);layouts.Add(new{width,viewport,columns=cols});Snapshot(w,Path.Combine(State.Home,$"v16-grid-{width}.png"));
  }
  await w.FilterForTest(null,null,"v16"); // The scoped query need not depend on another revision's library size.
  await w.EnterFolder(circle);w.Width=1180;w.Height=850;await w.EnterFolder(manual);await Delay(350);w.UpdateLayout();
  foreach(var path in paths.Take(3))
  {
   await w.FilterForTest(null,null,Path.GetFileName(path));await Delay(350);w.UpdateLayout();var cover=Visuals<CoverView>(w.WorksList).First(v=>v.Path==Path.Combine(path,"cover.png"));
   Check("横竖方封面四角完整保留 "+Array.IndexOf(paths,path),FullCoverPixels(cover));
  }
  await w.FilterForTest(null,null);w.Width=1180;w.Height=850;w.OpenTrack(audio,false,10);await w.SubtitleReady;w.OpenLyricsForTest();await w.ImageReady;await Delay(200);w.overlay.ShowPassive();
  foreach(var scale in new[]{1d,1.25,1.5})
  {
   w.Width=1180*scale;w.Height=800*scale;w.MainLayout.LayoutTransform=new ScaleTransform(scale,scale);await Delay(200);w.UpdateLayout();
   await RailClick(w,w.Progress,.75,2);Check($"原生进度任意高度点击真实定位且暂停保持 {scale*100}%",Math.Abs(w.PlaybackPosition-30)<.1&&!w.PlaybackPlaying&&w.CurrentSubtitle.Contains("第三句")&&w.overlay.DisplayedText==w.CurrentSubtitle,$"actual={w.PlaybackPosition:F3}");
   await RailDrag(w,w.Progress,reverse=>Check($"拖动未松手已真实连续定位 {scale*100}% "+reverse,Math.Abs(w.PlaybackPosition-(reverse?16:24))<.12&&!w.PlaybackPlaying,$"position={w.PlaybackPosition}; captured={System.Windows.Input.Mouse.Captured}; seeks={w.PointerSeeks}"));
   Check($"拖动提交与时间显示同步 {scale*100}%",Math.Abs(w.PlaybackPosition-16)<.12&&Math.Abs(w.Progress.Value-w.PlaybackPosition)<.03&&w.Elapsed.Text==MainWindow.Format(w.PlaybackPosition));
   layouts.Add(new{simulatedScale=scale,progressWidth=w.Progress.ActualWidth,position=w.PlaybackPosition});Snapshot(w,Path.Combine(State.Home,$"v16-scale-{scale}.png"));
  }
  w.MainLayout.LayoutTransform=Transform.Identity;w.Width=1180;w.Height=850;await Delay(180);w.PlayForTest();await RailClick(w,w.Progress,.5);Check("播放时进度点击仍继续实际播放",w.PlaybackPlaying&&w.PlaybackPosition is >19.8 and <20.5);w.PausePlayback();
  w.Seek(10);await KeyPress(0x25);Check("新版左键实际后退3秒",Math.Abs(w.PlaybackPosition-7)<.04);await KeyPress(0x27);Check("新版右键实际前进5秒",Math.Abs(w.PlaybackPosition-12)<.04);
  var movie=Path.Combine(root,"直接视频.mp4");File.Copy(Path.Combine(Environment.GetEnvironmentVariable("NOCTURNE_CODEC_FIXTURES")??Path.GetFullPath("test-fixtures"),"generated.mp4"),movie,true);
  await w.OpenVideo(movie,false,true);w.VideoSync.IsChecked=true;w.Seek(10);await RailClick(w,w.Progress,.5);Check("主进度点击同步静音配套视频",!w.directVideo&&w.video.Muted&&Math.Abs(w.video.Position-w.PlaybackPosition)<.2&&!w.PlaybackPlaying);
  w.VideoSync.IsChecked=false;await RailClick(w,w.VideoSeek,.3);Check("独立视频进度点击不改变主音频",Math.Abs(w.video.Position-w.video.Duration*.3)<.15&&Math.Abs(w.PlaybackPosition-20)<.15);w.CloseVideoSession(false);
  await w.OpenVideo(movie,true,false,2);await RailClick(w,w.Progress,.6);Check("直接视频进度点击使用真实视频时钟且保留暂停",w.directVideo&&!w.PlaybackPlaying&&Math.Abs(w.video.Position-w.video.Duration*.6)<.15&&Math.Abs(w.Progress.Value-w.video.Position)<.03);
  await RailDrag(w,w.Progress,reverse=>Check("直接视频拖动中真实定位 "+reverse,Math.Abs(w.video.Position-w.video.Duration*(reverse?.4:.6))<.15&&!w.PlaybackPlaying,$"position={w.video.Position}; captured={System.Windows.Input.Mouse.Captured}; seeks={w.PointerSeeks}"));
  w.PlayForTest();await RailClick(w,w.Progress,.2);Check("直接视频播放中定位继续播放且无第二路音频",w.PlaybackPlaying&&w.engine.FilePath==null&&Math.Abs(w.video.Position-w.video.Duration*.2)<.5);w.CloseVideoSession(false);
  w.OpenTrack(audio,true,0);await w.SubtitleReady;w.settings.WaveHeight=32;w.settings.Visualizer=true;w.ApplyLiveSettings();await Delay(300);
  Check("轻量波形高度与轻声映射不夸大底噪",w.Wave.ActualHeight==32&&w.WavePanel.ActualHeight<45&&AudioWave.Map(.0001f)==0&&AudioWave.Map(.001f) is >.1f and <.4f&&AudioWave.Map(.005f)>AudioWave.Map(.001f)&&w.Wave.Peak>0);
  w.PausePlayback();await Delay(600);Check("波形暂停后平滑停止绘制",!w.Wave.Running&&w.Wave.Peak==0);
  var dynamics=Path.Combine(paths[0],"02 自生成轻声起伏.wav");WriteDynamics(dynamics);var volume=w.engine.Volume;w.OpenTrack(dynamics,true,0);await Delay(600);var noise=w.Wave.Peak;w.Seek(2.6);await Delay(450);var soft=w.Wave.Peak;w.Seek(4.6);await Delay(450);var strong=w.Wave.Peak;
  Check("真实轻声与强音驱动细线波形且底噪不常驻满幅",noise<.04&&soft is >.1f and <.6f&&strong>soft+.1&&strong<.95,$"noise={noise}; soft={soft}; strong={strong}");Check("波形视觉映射未改变实际用户音量",w.engine.Volume==volume&&Math.Abs(w.Volume.Value-volume)<.0001);w.PausePlayback();
  var info=FileVersionInfo.GetVersionInfo(Environment.ProcessPath!);var controls=new SettingsWindow(w.settings,w.overlay){Owner=w};controls.Show();await Delay(100);Check("Shion窗口托盘关于及版本信息一致",w.Title=="Shion"&&w.TrayTitle=="Shion"&&controls.Title=="Shion · 设置"&&Visuals<TextBlock>(controls).Any(t=>t.Text=="Shion 1.6.0 · 本地音声播放器")&&info.ProductName=="Shion"&&info.FileDescription=="Shion");controls.Close();Check("更名保留程序集与旧数据目录标识",typeof(MainWindow).Assembly.GetName().Name=="Nocturne"&&Path.GetFileName(State.DefaultHome)=="Nocturne");
  w.Save();using(var probe=StartProbe("--startup-probe",State.Home,"--resume-probe",audio)){await probe.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(25));using var result=JsonDocument.Parse(File.ReadAllText(Path.Combine(State.Home,"startup-probe.json")));var restored=State.Read<Settings>("settings.json");Check("Shion真实重启保留手动系列数据且默认主页不播放",result.RootElement.GetProperty("initialPage").GetString()=="home"&&result.RootElement.GetProperty("initiallyStopped").GetBoolean()&&restored.WorkLocations[paths[0]]==manual&&restored.Tracks[audio].Subtitle==Path.ChangeExtension(audio,"vtt"));}
  State.Write("shion-layout-validation.json",layouts);State.Write("pointer-seek-trace.json",w.PointerTrace);State.Write("pointer-input-trace.json",pointerInputs);w.overlay.Hide();
 }
}
