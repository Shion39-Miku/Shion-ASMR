using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using NAudio.Wave;
using SkiaSharp;

namespace Nocturne;
public static partial class Verification
{
 sealed class ConstantSamples : ISampleProvider
 {
  public WaveFormat WaveFormat=>WaveFormat.CreateIeeeFloatWaveFormat(1000,2);
  public int Read(float[] buffer,int offset,int count){Array.Fill(buffer,1f,offset,count);return count;}
 }
 static IInputElement? keyTarget;
 static async Task InputTarget()
 {
  var window=(MainWindow)Application.Current.MainWindow;var handle=new WindowInteropHelper(window).Handle;
  if(!NativeProbe.TargetTestWindow(handle))throw new InvalidOperationException("无法将原生键盘验证指向测试窗口，此次输入验证无效。");
  Keyboard.Focus(keyTarget??window.PlayButton);
  await Task.CompletedTask;
 }
 static async Task KeyPress(ushort key){await InputTarget();NativeProbe.KeyStroke(key);await Delay(100);}
 static async Task DoubleClickElement(FrameworkElement element)
 {
  var window=Window.GetWindow(element)!;if(!NativeProbe.TargetTestWindow(new WindowInteropHelper(window).Handle))throw new InvalidOperationException("双击输入验证未获得前台");
  var point=element.PointToScreen(new Point(Math.Min(30,element.ActualWidth/2),element.ActualHeight/2));if(!NativeProbe.PointTargets(new WindowInteropHelper(window).Handle,(int)point.X,(int)point.Y))throw new InvalidOperationException("双击测试窗口被遮挡");
  NativeProbe.ClickAt((int)point.X,(int)point.Y);await Delay(80);NativeProbe.ClickAt((int)point.X,(int)point.Y);await Delay(120);
 }
 static async Task VerifyRevision4(MainWindow w)
 {
  w.library.Watch(Array.Empty<string>());var root=Path.Combine(State.Home,"v14 层级 日本語");
  var paths=new[]{Path.Combine(root,"社团","社团 A","系列甲","子系列","作品 1","CD1"),Path.Combine(root,"社团","社团 A","系列甲","作品 2"),Path.Combine(root,"社团","社团 A","不属于系列的作品 4")};
  foreach(var dir in paths){Directory.CreateDirectory(dir);WriteWave(Path.Combine(dir,"01 星の.晚安.wav"),40);}
  using(var bitmap=new SKBitmap(600,600)){using var canvas=new SKCanvas(bitmap);canvas.Clear(new SKColor(45,53,81));using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(SKEncodedImageFormat.Png,90);File.WriteAllBytes(Path.Combine(paths[0],"01 封面.png"),data.ToArray());}
  var track=Path.Combine(paths[0],"01 星の.晚安.wav");File.WriteAllText(Path.ChangeExtension(track,"vtt"),"WEBVTT\n\n00:00.000 --> 00:03.000\n星光・おやすみ\n\n00:04.000 --> 00:09.000\n第二句・かなカナ\n\n00:14.000 --> 00:28.000\n第三句・晚安\n");
  var oldSeries=Path.Combine(root,"社团","社团 A","系列甲");MetadataStore.AddTag(w.settings,new[]{oldSeries},"旧版系列标签");
  w.settings.Roots.Add(root);await w.Scan(true);var owner=w.works.Single(x=>x.Groups.Any(g=>g.Path==paths[0]));var circle=Organization.CircleId("社团 A");
  Check("旧版合并分类拆分继承标签不丢数据",w.settings.WorkDetails.ContainsKey(owner.Path)&&w.settings.WorkDetails[owner.Path].Tags.Contains("旧版系列标签"));
  var series=w.organization.ChildFolders(circle).Single(x=>x.Name=="系列甲");var sub=w.organization.ChildFolders(series.Id).Single(x=>x.Name=="子系列");
  Check("明确目录建立社团系列子系列作品且可选系列",w.organization.ChildWorks(sub.Id).Single().Path==owner.Path&&w.organization.ChildWorks(circle).Any(x=>x.Name=="不属于系列的作品 4")&&w.works.Count(x=>x.Circle=="社团 A")==3);
  w.settings.ExpandedFolders.Clear();w.ShowFolderNavigation();await w.EnterFolder("");var before=w.FolderTree.Items.Count;w.FolderTree.ScrollIntoView(w.FolderTree.Items.Cast<TreeEntry>().First(t=>t.Id==circle));await Delay(150);
  var expand=Visuals<Button>(w.FolderTree).FirstOrDefault(b=>b.Tag is TreeEntry t&&t.Id==circle&&Equals(b.Content,"›"));await Delay(180);expand=Visuals<Button>(w.FolderTree).FirstOrDefault(b=>b.Tag is TreeEntry t&&t.Id==circle&&Equals(b.Content,"›"));expand?.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));await Delay(150);
  Check("实际展开按钮按需生成下级并支持折叠",w.settings.ExpandedFolders.Contains(circle)&&w.FolderTree.Items.Count>before);
  var collapse=Visuals<Button>(w.FolderTree).Single(b=>b.Tag is TreeEntry t&&t.Id==circle&&Equals(b.Content,"⌄"));collapse.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));await Delay(150);Check("折叠不实例化下级作品",!w.settings.ExpandedFolders.Contains(circle)&&w.FolderTree.Items.Count==before);
  await w.EnterFolder(series.Id);Check("进入下级及路径面包屑实际渲染",w.browserFolder==series.Id&&w.Breadcrumb.Items.Count==3&&w.WorksList.Items.Cast<HomeRow>().SelectMany(r=>r.Entries).Any(t=>t.Folder?.Id==sub.Id));
  var parent=Visuals<Button>(w.HomePage).Single(b=>Equals(b.ToolTip,"返回上一级"));parent.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));await Delay(180);await w.HomeReady;Check("返回上一级保留社团中的作品",w.browserFolder==circle&&w.WorksList.Items.Cast<HomeRow>().SelectMany(r=>r.Entries).Any(t=>t.Work?.Name=="不属于系列的作品 4"));
  var folderEditor=new OrganizationWindow(w.organization,w.works,w.settings,series.Id){Owner=w};folderEditor.Show();await Delay(150);
  var name=Visuals<TextBox>(folderEditor).Single(t=>t.MaxLength==160);var folderList=Visuals<ComboBox>(folderEditor).First();var workList=Visuals<ListBox>(folderEditor).Single();
  void Press(string title)=>Visuals<Button>(folderEditor).Single(b=>Equals(b.Content,title)).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
  name.Text="手动系列";Press("在所选位置创建文件夹");var manual=(string)folderList.SelectedValue;name.Text="手动子系列 日本語";Press("重命名所选文件夹");workList.SelectedItems.Add(owner);Press("将所选作品移入此文件夹");folderEditor.Close();
  w.settings.WorkCovers[owner.Path]=owner.Cover??"";MetadataStore.AddTag(w.settings,new[]{owner.Path},"跨目录测试");w.OpenTrack(track,false,7.5);await w.SubtitleReady;w.SetOffset(.25);w.Save();await w.Scan(true);
  Check("真实整理控件创建重命名移动并在重扫后保留",w.organization.Folders[manual].Name=="手动子系列 日本語"&&w.organization.Locations[owner.Path]==manual&&w.settings.Organization[manual].Parent==series.Id&&File.Exists(track));
  await w.EnterFolder(manual);await Delay(200);var coverBefore=w.WorksList.ItemsSource;w.SelectWork(w.works.Single(x=>x.Path==owner.Path));w.ChooseCover(Path.Combine(paths[0],"01 封面.png"));w.Navigate("home");await w.HomeReady;await Delay(200);Check("文件夹作品封面绑定仍更新且不重建列表",ReferenceEquals(coverBefore,w.WorksList.ItemsSource)&&Visuals<CoverView>(w.WorksList).Any(c=>c.Path==Path.Combine(paths[0],"01 封面.png")));
  var wid="work:"+owner.Path;w.settings.ExpandedFolders.Add(wid);await w.RefreshOrganization();var trackRow=w.FolderTree.Items.Cast<TreeEntry>().First(t=>t.Track==track);w.FolderTree.ScrollIntoView(trackRow);await Delay(400);w.FolderTree.UpdateLayout();var container=w.FolderTree.ItemContainerGenerator.ContainerFromItem(trackRow);var treeTrack=container==null?null:Visuals<Button>(container).FirstOrDefault(b=>b.Tag is TreeEntry t&&t.Track==track&&b.Content is Grid);treeTrack?.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));await w.SubtitleReady;Check("作品可展开为音轨并从树中直接播放",treeTrack!=null&&w.currentTrack==track&&w.PlaybackPlaying,$"button={treeTrack!=null}; container={container!=null}; rows={w.FolderTree.Items.Count}; height={w.FolderTree.ActualHeight}; playing={w.PlaybackPlaying}; current={w.currentTrack}");w.PausePlayback();w.Seek(7.5);w.Save();
  var moving=new OrganizationWindow(w.organization,w.works,w.settings,manual){Owner=w};moving.Show();await Delay(150);var combos=Visuals<ComboBox>(moving).ToArray();combos[1].SelectedValue=manual;Visuals<Button>(moving).Single(b=>Equals(b.Content,"将所选文件夹移至目标位置")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Check("循环移动文件夹被拒绝",w.settings.Organization[manual].Parent==series.Id);combos[1].SelectedValue=circle;Visuals<Button>(moving).Single(b=>Equals(b.Content,"将所选文件夹移至目标位置")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));moving.Close();await w.BuildTree();Check("移动整个文件夹保留作品及子树关系",w.organization.Folders[manual].Parent==circle&&w.organization.Locations[owner.Path]==manual);w.Save();
  var restored=State.Read<Settings>("settings.json");restored.Migrate();var rebuilt=Organization.Build(w.works,restored);Check("重新读取层级保留原播放字幕标签及封面字典",rebuilt.Locations[owner.Path]==manual&&restored.Tracks[track].Position>7.4&&restored.Tracks[track].Offset==.25&&restored.WorkDetails[owner.Path].Tags.Contains("跨目录测试")&&restored.WorkCovers.ContainsKey(owner.Path));
  await w.FilterForTest(null,"跨目录测试");Check("标签跨文件夹筛选不复制作品",w.ShownWorks==1&&w.WorksList.Items.Cast<HomeRow>().SelectMany(r=>r.Entries).Single().Work?.Path==owner.Path);
  w.OpenTrack(track,false,1.2);await w.SubtitleReady;w.OpenLyricsForTest();w.overlay.ShowPassive();w.settings.ReduceMotion=true;w.Topmost=true;w.Activate();NativeProbe.SetForegroundWindow(new WindowInteropHelper(w).Handle);await Delay(300);
  var cue=w.document.Cues[2];w.SubtitleLines.ScrollIntoView(cue);await Delay(150);var item=(ListBoxItem)w.SubtitleLines.ItemContainerGenerator.ContainerFromItem(cue);NativeProbe.GetCursorPos(out var cursor);Snapshot(w,Path.Combine(State.Home,"input-before.png"));var origin=w.PointToScreen(new Point(0,0));var dpiInput=VisualTreeHelper.GetDpi(w);using(var nativeBitmap=new System.Drawing.Bitmap((int)(w.ActualWidth*dpiInput.DpiScaleX),(int)(w.ActualHeight*dpiInput.DpiScaleY))){using var graphics=System.Drawing.Graphics.FromImage(nativeBitmap);graphics.CopyFromScreen((int)origin.X,(int)origin.Y,0,0,nativeBitmap.Size);nativeBitmap.Save(Path.Combine(State.Home,"input-native-before.png"));}State.Write("input-layout.json",new{origin,dpi=dpiInput.DpiScaleX,item=PageBounds(item,w),list=PageBounds(w.SubtitleLines,w),cue=cue.Text,itemCue=(item.DataContext as Cue)?.Text});
  try
  {
   await ClickElement(item);var selected=w.PlaybackPosition;Check("原生单击歌词只选中不跳转",Math.Abs(selected-1.2)<.03&&!w.PlaybackPlaying&&w.SubtitleLines.SelectedItems.Contains(cue),$"position={selected}; seeks={w.LyricSeeks}; selected={w.SubtitleLines.SelectedIndex}; active={w.IsActive}; foreground={NativeProbe.ForegroundDescription()}");await Delay(System.Windows.Forms.SystemInformation.DoubleClickTime+150);
   var count=w.LyricSeeks;await DoubleClickElement(item);await Delay(100);w.UpdateClock();Check("原生双击只跳转一次保留暂停并同步三处",w.LyricSeeks==count+1&&Math.Abs(w.PlaybackPosition-14.25)<.04&&!w.PlaybackPlaying&&Math.Abs(w.Progress.Value-w.PlaybackPosition)<.03&&w.CurrentSubtitle.Contains("第三句")&&w.overlay.DisplayedText==w.CurrentSubtitle,$"seek calls={w.LyricSeeks-count}; position={w.PlaybackPosition:F3}");
   await Delay(System.Windows.Forms.SystemInformation.DoubleClickTime+150);var point=item.PointToScreen(new Point(30,15));NativeProbe.SetCursorPos((int)point.X,(int)point.Y);NativeProbe.Mouse(2);NativeProbe.SetCursorPos((int)point.X+60,(int)point.Y+10);NativeProbe.Mouse(4);NativeProbe.Mouse(0x800,120);await Delay(180);Check("拖动选择与滚轮浏览不跳转",w.LyricSeeks==count+1&&Math.Abs(w.PlaybackPosition-14.25)<.04);
   w.PlayForTest();await Delay(200);var second=w.document.Cues[1];w.SubtitleLines.ScrollIntoView(second);await Delay(150);item=(ListBoxItem)w.SubtitleLines.ItemContainerGenerator.ContainerFromItem(second);await Delay(System.Windows.Forms.SystemInformation.DoubleClickTime+150);await DoubleClickElement(item);await Delay(120);Check("播放中双击跳转保留播放及偏移",w.PlaybackPlaying&&w.PlaybackPosition>=4.25&&w.PlaybackPosition<5.1);w.PausePlayback();
   w.Seek(10);w.PlayButton.Focus();await KeyPress(0x25);Check("左方向键后退3秒并更新进度",Math.Abs(w.PlaybackPosition-7)<.04&&Math.Abs(w.Progress.Value-7)<.04);await KeyPress(0x27);Check("右方向键前进5秒",Math.Abs(w.PlaybackPosition-12)<.04);
   w.Seek(1);await KeyPress(0x25);Check("快捷键后退下界为0",w.PlaybackPosition<.01);w.Seek(39);await KeyPress(0x27);Check("快捷键前进不超过媒体总时长",w.PlaybackPosition<=w.PlaybackDuration&&w.PlaybackPosition>39.9);
   w.Volume.Value=.98;await KeyPress(0x26);Check("音量快捷键上界100及显示",w.Volume.Value==1&&w.VolumeText.Text=="100%");w.Volume.Value=.02;await KeyPress(0x28);Check("音量快捷键下界0及显示",w.Volume.Value==0&&w.VolumeText.Text=="0%");w.Volume.Value=.5;await KeyPress(0x26);await KeyPress(0x28);Check("音量每次精确5个百分点",Math.Abs(w.Volume.Value-.5)<.0001);
   w.Seek(10);keyTarget=w.Search;w.Search.Focus();var original=w.PlaybackPosition;await KeyPress(0x25);await KeyPress(0x27);await KeyPress(0x20);Check("搜索输入焦点避让播放器快捷键",Math.Abs(w.PlaybackPosition-original)<.03&&!w.PlaybackPlaying&&w.Search.Text.Contains(' '));w.Search.Text="";keyTarget=null;
   w.PlayButton.Focus();w.Seek(2);await InputTarget();NativeProbe.Key(0x20,false);await Delay(40);NativeProbe.Key(0x20,false);await Delay(40);NativeProbe.Key(0x20,false);await Delay(40);NativeProbe.Key(0x20,true);await Delay(100);await w.FadeReady;Check("长按空格不会反复切换",w.PlaybackPlaying,$"position={w.PlaybackPosition}; focus={Keyboard.FocusedElement?.GetType().Name}; trace={JsonSerializer.Serialize(w.KeyboardTrace)}");await KeyPress(0x20);await w.FadeReady;Check("空格暂停淡出后真正暂停",!w.PlaybackPlaying&&Math.Abs(w.engine.Volume-.5)<.0001&&Math.Abs(w.Volume.Value-.5)<.0001&&w.engine.TemporaryGain<.01);
  }
  finally{w.Topmost=false;NativeProbe.SetCursorPos(cursor.X,cursor.Y);}
  var envelope=new FadeProvider(new ConstantSamples());envelope.Set(0,120);var buffer=new float[240];envelope.Read(buffer,0,120);var middle=envelope.Gain;envelope.Set(1,120);envelope.Read(buffer,120,120);Check("PCM独立增益按帧平滑且声道比例不变",Math.Abs(middle-.5)<.001&&buffer[120]>=buffer[118]&&Enumerable.Range(0,120).All(i=>buffer[i*2]==buffer[i*2+1])&&buffer.Take(120).Chunk(2).Select(x=>x[0]).Zip(buffer.Take(120).Chunk(2).Select(x=>x[0]).Skip(1)).All(p=>p.First>=p.Second));
  w.Volume.Value=.63;w.RequestPlayback(true);await w.FadeReady;var gainTrace=new List<object>();for(int i=0;i<10;i++){w.RequestPlayback(i%2!=0);await Delay(25);gainTrace.Add(new{playing=w.PlaybackPlaying,gain=w.engine.TemporaryGain,volume=w.engine.Volume});}await w.FadeReady;await Delay(300);Check("快速暂停恢复最后状态正确且用户音量不跳动",w.PlaybackPlaying&&Math.Abs(w.engine.TemporaryGain-1)<.001&&Math.Abs(w.engine.Volume-.63)<.0001&&Math.Abs(w.Volume.Value-.63)<.0001);
  w.RequestPlayback(false);await Delay(30);w.Seek(8);await Delay(350);Check("拖动取消旧淡出且维持暂停意图",!w.PlaybackPlaying&&Math.Abs(w.PlaybackPosition-8)<.04);
  w.PlayForTest();w.RequestPlayback(false);await Delay(30);w.OpenTrack(Path.Combine(paths[1],"01 星の.晚安.wav"),true,2);await w.SubtitleReady;await Delay(350);Check("切歌后旧淡出任务不暂停新音轨",w.PlaybackPlaying&&w.currentTrack!=track&&w.engine.TemporaryGain==1);
  w.Volume.Value=0;w.RequestPlayback(false);await w.FadeReady;w.RequestPlayback(true);await w.FadeReady;Check("静音恢复保持静音不修改保存音量",w.PlaybackPlaying&&w.engine.Volume==0&&w.Volume.Value==0);w.Volume.Value=.4;
  w.WindowState=WindowState.Minimized;w.RequestPlayback(false);await w.FadeReady;w.RequestPlayback(true);await w.FadeReady;await Delay(200);Check("最小化淡入淡出实际执行且无隐藏动画",w.PlaybackPlaying&&w.engine.TemporaryGain==1&&!w.Stars.Running&&!w.Wave.Running);w.WindowState=WindowState.Normal;
  w.Seek(39.999);w.RequestPlayback(true);await w.FadeReady;await Delay(200);Check("媒体末尾重新播放保留淡入且旧任务不串台",w.PlaybackPlaying&&w.PlaybackPosition<2&&w.engine.TemporaryGain==1);w.RequestPlayback(false);await w.FadeReady;
  w.settings.PauseFade=false;w.RequestPlayback(false);await w.FadeReady;Check("关闭过渡选项立即暂停",!w.PlaybackPlaying&&w.engine.TemporaryGain==1);w.settings.PauseFade=true;State.Write("fade-trace.json",gainTrace);State.Write("lyric-click-trace.json",w.LyricClickTrace);State.Write("native-click-points.json",clickPoints);State.Write("keyboard-trace.json",w.KeyboardTrace);
  await w.EnterFolder(manual);w.OpenTrack(track,false,14.5);await w.SubtitleReady;w.currentWork!.Name="很长的作品标题・日本語かな：星光落在耳边 / 排版、清晰度与完整名称验证";w.SelectWork(w.currentWork);w.NowPlaying.Text=w.LyricTrackName.Text=w.currentWork.Name;
  var movie=Path.Combine(Environment.GetEnvironmentVariable("NOCTURNE_CODEC_FIXTURES")??Path.GetFullPath("test-fixtures"),"generated.mp4");
  Check("直接视频可使用同一淡入淡出控制",await w.OpenVideo(movie,true,false,2));w.RequestPlayback(true);await w.FadeReady;await Delay(300);w.RequestPlayback(false);await w.FadeReady;Check("实际视频淡出后暂停不改变用户音量",!w.PlaybackPlaying&&Math.Abs(w.engine.Volume-.4)<.001&&Math.Abs(w.Volume.Value-.4)<.001);w.RequestPlayback(true);await Delay(30);w.RequestPlayback(false);await Delay(30);w.RequestPlayback(true);await w.FadeReady;Check("视频快速反转最终播放且进度继续",w.PlaybackPlaying&&w.video.Position>=2);w.CloseVideoSession(false);
  w.OpenTrack(track,false,14.5);await w.SubtitleReady;w.SelectWork(w.works.Single(x=>x.Path==owner.Path));w.NowPlaying.Text=w.LyricTrackName.Text="很长的作品标题・日本語かな：星光落在耳边 / 排版、清晰度与完整名称验证";
  var layouts=new List<object>();foreach(var size in new[]{(820d,680d),(960d,720d),(1180d,800d),(1600d,1000d)})
  {
   w.Width=size.Item1;w.Height=size.Item2;foreach(var page in new[]{"home","detail","lyrics"}){w.Navigate(page);await w.HomeReady;await Delay(200);w.UpdateLayout();
    Rect Bounds(FrameworkElement e)=>e.TransformToAncestor(w).TransformBounds(new Rect(0,0,e.ActualWidth,e.ActualHeight));
    var play=Bounds(w.PlayButton);var previous=Bounds(w.PreviousButton);var next=Bounds(w.NextButton);var loop=Bounds(w.LoopChoice);var vol=Bounds(w.Volume);var search=Bounds(w.Search);var content=Bounds(page=="home"?w.HomePage:page=="detail"?w.DetailPage:w.LyricsPage);
    bool valid=play.Right<=next.Left+1&&previous.Right<=play.Left+1&&next.Right<=loop.Left+1&&loop.Right<=vol.Left+1&&vol.Right<w.ActualWidth&&search.Right<w.ActualWidth&&content.Bottom<=Bounds(w.Progress).Top&&content.Height>100;
    layouts.Add(new{width=size.Item1,height=size.Item2,page,valid,play,previous,next,loop,vol,search,content,dpi=VisualTreeHelper.GetDpi(w).DpiScaleX});Check($"布局 {size.Item1}×{size.Item2} {page}",valid);Snapshot(w,Path.Combine(State.Home,$"layout-{size.Item1}-{page}.png"));
   }
  }
  // Simulated effective DIP constraints are separate from physical monitor DPI evidence.
  w.Navigate("lyrics");foreach(var scale in new[]{1.25,1.5,2d}){w.Width=1180*scale;w.Height=800*scale;w.MainLayout.LayoutTransform=new ScaleTransform(scale,scale);await Delay(160);w.UpdateLayout();var valid=w.MainLayout.ActualWidth>1000&&w.MainLayout.ActualHeight>650&&w.Progress.ActualWidth>200;Check($"等效布局缩放 {scale*100:0}%",valid);layouts.Add(new{simulatedScale=scale,valid,availableWidth=w.MainLayout.ActualWidth,availableHeight=w.MainLayout.ActualHeight});Snapshot(w,Path.Combine(State.Home,$"layout-scale-{scale}.png"));}w.MainLayout.LayoutTransform=Transform.Identity;
  State.Write("layout-validation.json",layouts);w.Width=1180;w.Height=800;w.settings.ReduceMotion=false;w.overlay.Hide();w.Save();await w.EnterFolder("");
  using(var probe=StartProbe("--startup-probe",State.Home,"--resume-probe",track)){await probe.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(25));using var result=JsonDocument.Parse(File.ReadAllText(Path.Combine(State.Home,"startup-probe.json")));Check("最终版本真实重启进入主页不播放且保留层级",result.RootElement.GetProperty("initialPage").GetString()=="landing"&&result.RootElement.GetProperty("initiallyStopped").GetBoolean()&&State.Read<Settings>("settings.json").WorkLocations[owner.Path]==manual);}
 }
}
