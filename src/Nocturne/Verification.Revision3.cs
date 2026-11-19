using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;
using NAudio.Wave;
using SkiaSharp;

namespace Nocturne;
public static partial class Verification
{
 static IEnumerable<T> Visuals<T>(DependencyObject p) where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++){var c=VisualTreeHelper.GetChild(p,i);if(c is T typed)yield return typed;foreach(var inner in Visuals<T>(c))yield return inner;}}
 static IEnumerable<GlyphRun> Glyphs(Drawing? drawing){if(drawing is GlyphRunDrawing glyph)yield return glyph.GlyphRun;if(drawing is DrawingGroup group)foreach(var child in group.Children)foreach(var g in Glyphs(child))yield return g;}
 static readonly List<object> clickPoints=new();
 static async Task ClickElement(FrameworkElement e)
 {
  var window=Window.GetWindow(e)!;var handle=new WindowInteropHelper(window).Handle;
  if(!NativeProbe.TargetTestWindow(handle))throw new InvalidOperationException("原生点击的测试窗口未获得焦点。");
  await Delay(40);var p=e.PointToScreen(new Point(e.ActualWidth/2,e.ActualHeight/2));
  NativeProbe.SetCursorPos((int)p.X,(int)p.Y);await Delay(40);
  if(!NativeProbe.PointTargets(handle,(int)p.X,(int)p.Y))throw new InvalidOperationException("测试窗口被遮挡，原生点击无效。");
  clickPoints.Add(new{pointX=p.X,pointY=p.Y,height=e.ActualHeight,type=e.GetType().Name});NativeProbe.Mouse(2);try{await Delay(35);}finally{NativeProbe.Mouse(4);}await Delay(100);
 }
 static async Task VerifyRevision3(MainWindow w)
 {
  var root=Path.Combine(State.Home,"v13 中文 日本語 & 多.句点");var work=Path.Combine(root,"作品夜空");Directory.CreateDirectory(work);
  var a=Path.Combine(work,"01 晚安.日本語.part.1.WAV");var b=Path.Combine(work,"02 夜風.part.2.wav");var none=Path.Combine(work,"03 无字幕.wav");WriteWave(a,30);WriteWave(b,30);WriteWave(none,30);
  var alrc=Path.ChangeExtension(a,"LRC");File.WriteAllText(alrc,"[00:00.00]星光落在耳边。\n[00:02.00]中文、日本語、かなカナ。\n[00:10.00]おやすみなさい。",new UTF8Encoding(true));
  w.settings.Roots.Add(root);await w.Scan(true);Check("扫描建立完整主文件名字幕关联",w.library.Folders[work].SubtitleLinks.TryGetValue(Path.GetFileNameWithoutExtension(a),out var links)&&links.Contains(alrc));
  w.SelectFolder(work);w.Navigate("detail");w.overlay.ShowPassive();NativeProbe.SetForegroundWindow(new WindowInteropHelper(w).Handle);await Delay(200);
  if(w.Tracks.ItemContainerGenerator.ContainerFromIndex(0) is ListBoxItem item)await ClickElement(item);await w.SubtitleReady;
  Check("真实单击音轨播放并自动使用同名大写 LRC",w.currentTrack==a&&w.PlaybackPlaying&&w.document.Cues.Count==3&&w.CurrentSubtitle.Contains("星光"));Check("自动字幕直接进入已打开桌面字幕无需重开",w.overlay.DisplayedText==w.CurrentSubtitle&&w.overlay.IsVisible);w.PausePlayback();
  w.library.Folders[work].SubtitleLinks.Clear();w.library.Folders[work].Subtitles.Clear();w.settings.Tracks[a].Subtitle="不加载字幕";w.OpenTrack(a,false,2.5);await w.SubtitleReady;Check("旧索引及失效状态通过播放前复查修复",w.CurrentSubtitle=="中文、日本語、かなカナ。");
  w.OpenTrack(b,false,0);await w.SubtitleReady;Check("无字幕切歌清空前首",w.CurrentSubtitle==""&&w.document.Cues.Count==0);
  var bvtt=Path.ChangeExtension(b,"VtT");File.WriteAllText(bvtt,"WEBVTT\n\n00:00.000 --> 00:09.000\n新增字幕自动使用\n日本語の夜\n\n00:12.000 --> 00:20.000\n恢复后的字幕\n");w.PlayForTest();await w.SubtitleReady;Check("扫描后新增字幕恢复播放无需重扫",w.CurrentSubtitle=="新增字幕自动使用\n日本語の夜");w.PausePlayback();
  w.OpenTrack(a,false,0);var stale=w.SubtitleReady;w.OpenTrack(b,false,0);w.OpenTrack(none,false,0);await Task.WhenAll(stale,w.SubtitleReady);await Delay(150);Check("快速切歌旧异步结果不覆盖新音轨",w.currentTrack==none&&w.CurrentSubtitle==""&&w.document.Cues.Count==0&&w.overlay.DisplayedText=="");w.overlay.Hide();
  w.OpenTrack(b,false,12.6);await w.SubtitleReady;w.SetOffset(.3);w.Save();using(var child=StartProbe("--startup-probe",State.Home,"--resume-probe",b)){await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));using var p=JsonDocument.Parse(File.ReadAllText(Path.Combine(State.Home,"startup-probe.json")));Check("真实重启恢复同名字幕位置及偏移",p.RootElement.GetProperty("subtitle").GetString()=="恢复后的字幕"&&Math.Abs(p.RootElement.GetProperty("position").GetDouble()-12.6)<.1);}
  var flac=Path.Combine(work,"04 夕暮れ.part.FLAC");File.Copy(Path.Combine(Environment.GetEnvironmentVariable("NOCTURNE_CODEC_FIXTURES")??Path.GetFullPath("test-fixtures"),"generated.flac"),flac,true);File.WriteAllText(Path.ChangeExtension(flac,"VTT"),"WEBVTT\n\n00:00.000 --> 00:03.000\nFLAC 同名字幕・日本語\n");w.OpenTrack(flac,true,0);await w.SubtitleReady;Check("实际 FLAC 多句点文件名同名字幕自动使用",w.PlaybackPlaying&&w.CurrentSubtitle=="FLAC 同名字幕・日本語");w.PausePlayback();
  w.SetOffset(0);w.OpenTrack(a,false,2.6);await w.SubtitleReady;w.OpenLyricsForTest();w.ApplyFontForTest(true);await Delay(250);w.UpdateLayout();
  var found=new Typeface(FontBook.Art,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal).TryGetGlyphTypeface(out var embedded);Check("内置字体实际解析并覆盖中文日文假名",found&&embedded!=null&&"星光中文日本語かなカナ。".All(c=>embedded.CharacterToGlyphMap.TryGetValue(c,out var g)&&g!=0),embedded?.FontUri.ToString()??"unresolved");
  var text=Visuals<TextBlock>(w.SubtitleLines).FirstOrDefault(t=>t.Text.Contains("中文"));var runs=text==null?Array.Empty<GlyphRun>():Glyphs(VisualTreeHelper.GetDrawing(text)).ToArray();Check("歌词正文真实 GlyphRun 使用包内字体",runs.Any(g=>g.GlyphTypeface.FontUri.ToString().Contains("LXGWWenKai",StringComparison.OrdinalIgnoreCase)),string.Join("; ",runs.Select(g=>g.GlyphTypeface.FontUri.ToString()).Distinct()));Snapshot(w,Path.Combine(State.Home,"lyrics-art.png"));
  w.ApplyFontForTest(false);await Delay(150);Snapshot(w,Path.Combine(State.Home,"lyrics-clear.png"));w.Save();Check("清晰默认字体模式可持久切换",!State.Read<Settings>("settings.json").ArtLyrics);w.ApplyFontForTest(true);
  var large=Path.Combine(root,"大库");Directory.CreateDirectory(large);var seed=Path.Combine(State.Home,"tiny.wav");using(var wave=new WaveFileWriter(seed,new WaveFormat(48000,16,2)))wave.Write(new byte[4800],0,4800);
  var cover=Path.Combine(State.Home,"synthetic-cover.png");using(var bmp=new SKBitmap(900,600)){using var canvas=new SKCanvas(bmp);canvas.Clear(new SKColor(25,35,58));using var paint=new SKPaint{Color=new SKColor(141,143,187),IsAntialias=true};canvas.DrawCircle(600,200,95,paint);using var image=SKImage.FromBitmap(bmp);using var data=image.Encode(SKEncodedImageFormat.Png,90);File.WriteAllBytes(cover,data.ToArray());}
  await Task.Run(()=>{for(int i=0;i<2400;i++){var path=Path.Combine(large,$"作品 {i:0000}");Directory.CreateDirectory(path);File.Copy(seed,Path.Combine(path,"01.wav"),true);File.Copy(cover,Path.Combine(path,"cover.png"),true);if(i%3==0)File.WriteAllText(Path.Combine(path,"metadata.json"),"{\"circle\":{\"name\":\"星夜社团\"}}");}});
  foreach(var name in new[]{"作品甲","作品乙"}){var path=Path.Combine(root,"社团","风铃社",name);Directory.CreateDirectory(path);File.Copy(seed,Path.Combine(path,"01.wav"),true);}
  int ticks=0;var beat=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(10)};beat.Tick+=(_,_)=>ticks++;beat.Start();await w.Scan(true);beat.Stop();var catalog=w.works.Where(x=>x.Path.StartsWith(large+"\\",StringComparison.OrdinalIgnoreCase)).ToArray();
  Check("2400作品后台构建不占用界面线程",catalog.Length==2400&&ticks>2&&w.HomeWorkerThread!=Environment.CurrentManagedThreadId,$"works={w.works.Count}; UI ticks={ticks}; worker={w.HomeWorkerThread}; UI={Environment.CurrentManagedThreadId}");Check("社团仅读取明确元数据其余未设置",catalog.Count(x=>x.Circle=="星夜社团")==800&&catalog.Count(x=>x.Circle==MetadataStore.Unset)==1600);
  Check("明确社团目录保留作品并识别社团",w.works.Count(x=>x.Circle=="风铃社"&&x.CircleSource=="明确社团目录")==2);
  var targets=catalog.Take(12).Select(x=>x.Path).ToArray();var total=w.works.Count;
  var editor=new MetadataWindow(w.works,w.settings){Owner=w};editor.Show();await Delay(150);var editList=Visuals<ListBox>(editor).Single();foreach(var path in targets)editList.SelectedItems.Add(w.works.Single(x=>x.Path==path));
  void Press(string title)=>Visuals<Button>(editor).Single(x=>Equals(x.Content,title)).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
  Visuals<TextBox>(editor).Single(x=>x.MaxLength==120).Text="手动社团";Press("应用社团到所选作品");var tagName=Visuals<TextBox>(editor).Single(x=>x.MaxLength==80);tagName.Text="睡前";Press("创建标签");Press("给所选作品添加标签");editList.SelectedItems.Clear();foreach(var path in targets.Take(6))editList.SelectedItems.Add(w.works.Single(x=>x.Path==path));tagName.Text="日语";Press("创建标签");Press("给所选作品添加标签");editor.Close();w.Save();await w.BuildTree();
  w.CircleFilter.SelectedItem="手动社团";w.TagFilter.SelectedItem="日语";await w.HomeReady;Check("批量管理真实控件及组合筛选不复制作品",w.ShownWorks==6&&w.works.Count==total);
  MetadataStore.RenameTag(w.settings,"日语","日本語");MetadataStore.RemoveTag(w.settings,targets.Take(2),"日本語");w.Save();await w.BuildTree();await w.FilterForTest("手动社团","日本語");Check("标签重命名和批量移除关联",w.ShownWorks==4);
  await w.Scan(true);await w.FilterForTest("手动社团","日本語");var saved=State.Read<Settings>("settings.json");saved.Migrate();Check("重扫重读设置保留社团及标签",w.ShownWorks==4&&saved.WorkDetails[targets[0]].Circle=="手动社团"&&saved.CustomTags.Contains("日本語"));MetadataStore.DeleteTag(w.settings,"日本語");w.Save();Check("删除标签保留作品及音轨",w.settings.WorkDetails.Values.All(d=>!d.Tags.Contains("日本語"))&&File.Exists(Path.Combine(targets[0],"01.wav")));
  await w.BuildTree();await w.FilterForTest(null,null);w.Navigate("home");w.Width=1180;w.Height=820;w.settings.Stars=2;w.UpdateVisuals();await w.HomeReady;await Delay(700);w.UpdateLayout();
  var scroll=FindScroll(w.WorksList)!;var visibleRows=Visuals<ListBoxItem>(w.WorksList).Count();Check("大库只渲染可见与有限预取行",w.works.Count>=2400&&w.WorksList.Items.Count==(int)Math.Ceiling(w.ShownWorks/(double)w.Columns())&&visibleRows<20,$"rows={w.WorksList.Items.Count}; realized={visibleRows}; covers={Visuals<CoverView>(w.WorksList).Count()}");
  var trace=new List<object>();SmoothScroll.Trace=(delta,offset,target,phase)=>trace.Add(new{delta,offset,target,phase,time=DateTimeOffset.Now});
  var rebuilds=w.HomeRebuilds;var frames=new List<double>();var frameClock=Stopwatch.StartNew();double last=0;EventHandler rendered=(_,_)=>{var now=frameClock.Elapsed.TotalMilliseconds;if(last>0&&now-last>1)frames.Add(now-last);last=now;};CompositionTarget.Rendering+=rendered;
  var gaps=new List<double>();var pulse=Stopwatch.StartNew();var uiTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(10)};uiTimer.Tick+=(_,_)=>{gaps.Add(pulse.Elapsed.TotalMilliseconds);pulse.Restart();};uiTimer.Start();
  w.Topmost=true;NativeProbe.SetForegroundWindow(new WindowInteropHelper(w).Handle);var point=scroll.PointToScreen(new Point(80,80));NativeProbe.GetCursorPos(out var cursor);NativeProbe.SetCursorPos((int)point.X,(int)point.Y);var before=scroll.VerticalOffset;
  void Wheel(uint delta){var handle=new WindowInteropHelper(w).Handle;if(!NativeProbe.TargetTestWindow(handle))throw new InvalidOperationException("原生滚轮的测试窗口未获得焦点。");NativeProbe.MouseAt((int)point.X,(int)point.Y,0x800,delta);}
  for(int i=0;i<28;i++){Wheel(unchecked((uint)-120));await Delay(18);}await Delay(200);var down=scroll.VerticalOffset;for(int i=0;i<16;i++){Wheel(unchecked((uint)-15));await Delay(10);}await Delay(120);var precision=scroll.VerticalOffset;for(int i=0;i<8;i++){Wheel(120);await Delay(18);}await Delay(200);var reverse=scroll.VerticalOffset;NativeProbe.SetCursorPos(cursor.X,cursor.Y);w.Topmost=false;uiTimer.Stop();CompositionTarget.Rendering-=rendered;
  SmoothScroll.Trace=null;State.Write("scroll-input-trace.json",trace);double P(List<double> values,double p)=>values.Count==0?0:values.Order().ElementAt(Math.Min(values.Count-1,(int)(values.Count*p)));
  State.Write("scroll-performance.json",new{works=w.works.Count,visibleRows,before,down,precision,reverse,frameCount=frames.Count,frameMedianMs=P(frames,.5),frameP95Ms=P(frames,.95),frameMaxMs=frames.DefaultIfEmpty().Max(),uiP95Ms=P(gaps,.95),uiMaxMs=gaps.DefaultIfEmpty().Max(),coverCache=CoverView.Cache.Count,coverDecodes=CoverView.Cache.Decodes,coverDecodeThread=CoverView.Cache.DecodeThread,uiThread=Environment.CurrentManagedThreadId});
  Check("系统滚轮精细增量反向滚动无动画积压",down>before+300&&precision>down&&reverse<precision&&!SmoothScroll.Running(w.WorksList),$"offsets={before:F1}/{down:F1}/{precision:F1}/{reverse:F1}");Check("滚动不重建列表封面后台解码有界",w.HomeRebuilds==rebuilds&&CoverView.Cache.Count<=24&&CoverView.Cache.DecodeThread!=Environment.CurrentManagedThreadId,$"rebuild delta={w.HomeRebuilds-rebuilds}; cache={CoverView.Cache.Count}; UI p95={P(gaps,.95):F1}ms; frame p95={P(frames,.95):F1}ms");
  var keep=scroll.VerticalOffset;w.SelectWork(w.works[0]);await w.ReturnBrowse();await Delay(480);Check("进分类返回不重置滚动位置",Math.Abs(scroll.VerticalOffset-keep)<2,$"offset={keep:F1}→{scroll.VerticalOffset:F1}");var paints=CoverView.Paints;var decodes=CoverView.Cache.Decodes;await Delay(600);Check("星空不重复绘制或解码分类封面",CoverView.Paints==paints&&CoverView.Cache.Decodes==decodes,$"paint delta={CoverView.Paints-paints}; decode delta={CoverView.Cache.Decodes-decodes}");
  var timer=Stopwatch.StartNew();await w.FilterForTest(null,"睡前","作品");await Delay(100);Check("大库搜索及标签筛选在后台完成并从顶部显示",w.ShownWorks==12&&w.HomeWorkerThread!=Environment.CurrentManagedThreadId&&scroll.VerticalOffset<2,$"elapsed ms={timer.ElapsedMilliseconds}; offset={scroll.VerticalOffset}");await w.FilterForTest(null,null);
  w.CircleChoices.SelectedItem=w.CircleChoices.Items.Cast<MainWindow.CircleOption>().Single(c=>c.Name=="风铃社");await w.HomeReady;Check("固定社团入口直接渲染社团作品",w.CircleChoices.Visibility==Visibility.Visible&&w.ShownWorks==2);
  w.TagFilterButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Check("标签入口显示持久标签筛选",w.TagFiltersPanel.Visibility==Visibility.Visible&&w.TagFilter.Items.Cast<string>().Contains("睡前"));await w.FilterForTest(null,null);await Delay(400);Snapshot(w,Path.Combine(State.Home,"home-circles-tags.png"));
  var home=State.Home;try{State.Home=Path.Combine(home,"legacy-tags");Directory.CreateDirectory(State.Home);var old=new Settings{SchemaVersion=2,LegacyData=new()};using var tags=JsonDocument.Parse("{\"E:\\\\old-work\":[\"旧标签\"]}");using var names=JsonDocument.Parse("[\"未使用标签\"]");old.LegacyData["workTags"]=tags.RootElement.Clone();old.LegacyData["tags"]=names.RootElement.Clone();old.LegacyData["futureData"]=names.RootElement.Clone();State.Write("settings.json",old);old=State.Read<Settings>("settings.json");old.Migrate();State.Write("settings.json",old);var again=State.Read<Settings>("settings.json");Check("旧标签迁移保留关联未知字段及备份",again.WorkDetails.Values.Any(d=>d.Tags.Contains("旧标签"))&&again.CustomTags.Contains("未使用标签")&&again.LegacyData!.ContainsKey("futureData")&&File.Exists(Path.Combine(State.Home,"settings.json.v1.2.bak")));}finally{State.Home=home;}
  w.Save();
 }
}
