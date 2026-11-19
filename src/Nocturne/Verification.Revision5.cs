using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using SkiaSharp;

namespace Nocturne;
public static partial class Verification
{
 static Rect PageBounds(FrameworkElement element,Visual relative)=>element.TransformToAncestor(relative).TransformBounds(new Rect(0,0,element.ActualWidth,element.ActualHeight));
 static async Task VerifyRevision5(MainWindow w)
 {
  w.library.Watch(Array.Empty<string>());w.TagFiltersPanel.Visibility=Visibility.Collapsed;w.settings.ReduceMotion=false;w.settings.Visualizer=true;w.settings.ShowPictures=w.settings.ShowVideo=true;w.settings.PictureOnRight=false;w.settings.VideoOnRight=true;
  var root=Path.Combine(State.Home,"v15 自生成媒体");var cases=new Dictionary<string,string>();
  foreach(var kind in new[]{"无媒体","仅图片","仅视频","图片视频"})
  {
   var dir=Path.Combine(root,"社团","星光社","系列甲",kind);Directory.CreateDirectory(dir);var audio=Path.Combine(dir,"01 晚安.日本語.wav");WriteWave(audio,50);cases[kind]=audio;
   File.WriteAllText(Path.ChangeExtension(audio,"vtt"),"WEBVTT\n\n00:00.000 --> 00:05.000\n星光落在耳边\nおやすみなさい・かなカナ\n\n00:05.000 --> 00:20.000\n第二段的温柔\n\n00:21.000 --> 00:45.000\n晚安・日本語\n");
   if(kind.Contains("图片"))for(int i=0;i<2;i++){using var bitmap=new SKBitmap(i==0?640:360,i==0?360:640);using var canvas=new SKCanvas(bitmap);canvas.Clear(i==0?new SKColor(55,68,101):new SKColor(75,52,86));using var paint=new SKPaint{Color=new SKColor(201,175,218),IsAntialias=true};canvas.DrawCircle(bitmap.Width*.65f,bitmap.Height*.3f,65,paint);using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(SKEncodedImageFormat.Png,90);File.WriteAllBytes(Path.Combine(dir,i==0?"2 插画.png":"10 插画.png"),data.ToArray());}
   if(kind.Contains("视频"))File.Copy(Path.Combine(Environment.GetEnvironmentVariable("NOCTURNE_CODEC_FIXTURES")??Path.GetFullPath("test-fixtures"),"generated.mp4"),Path.ChangeExtension(audio,"mp4"),true);
  }
  w.settings.Roots.Add(root);await w.Scan(true);w.Width=1180;w.Height=850;
  var layouts=new List<object>();
  foreach(var pair in cases)
  {
   w.SelectFolder(Path.GetDirectoryName(pair.Value)!);w.Navigate("detail");var file=w.Tracks.Items.Cast<FileItem>().Single(x=>x.Path==pair.Value);w.Tracks.ScrollIntoView(file);await Delay(230);w.UpdateLayout();var item=w.Tracks.ItemContainerGenerator.ContainerFromItem(file) as ListBoxItem;if(item!=null)await ClickElement(item);await w.SubtitleReady;await w.AutoVideoReady;await w.ImageReady;await Delay(350);w.UpdateClock();
   Check(pair.Key+"原生单击音频自动进入歌词并加载多行字幕",w.currentTrack==pair.Value&&w.CurrentPage=="lyrics"&&w.CurrentSubtitle.Contains("おやすみ"),$"track={w.currentTrack}; page={w.CurrentPage}; subtitle={w.CurrentSubtitle}; foreground={NativeProbe.ForegroundDescription()}");
   bool image=pair.Key.Contains("图片"),video=pair.Key.Contains("视频");
   Check(pair.Key+"真实媒体区域按存在状态展开收起",(w.LyricsArtwork.Visibility==Visibility.Visible)==image&&(w.VideoPane.Visibility==Visibility.Visible)==video&&(image?w.LyricsPicture.Source!=null:true));
   if(video)Check(pair.Key+"同主文件名配套视频自动静音且只有音频主源",w.video.Muted&&!w.directVideo&&w.engine.Playing&&w.video.Path==Path.ChangeExtension(pair.Value,"mp4"));
   foreach(var width in new[]{820d,1180d,1600d})
   {
    w.Width=width;await Delay(220);w.UpdateLayout();var subtitle=PageBounds(w.SubtitlePanel,w.LyricsWorkspace);bool good=subtitle.Width>=270&&subtitle.Height>=100;
    foreach(var pane in new[]{w.LyricsArtwork,w.VideoPane}.Where(p=>p.Visibility==Visibility.Visible)){var rect=PageBounds(pane,w.LyricsWorkspace);var overlap=Rect.Intersect(rect,subtitle);good&=overlap.IsEmpty||overlap.Width*overlap.Height<1;good&=rect.Width>100&&rect.Height>45;}
    Check($"{pair.Key} {width}宽自适应媒体不重叠且歌词可读",good,$"lyric={subtitle.Width:0}×{subtitle.Height:0}; compact={w.CompactMedia}");layouts.Add(new{kind=pair.Key,width,good,subtitle,compact=w.CompactMedia});Snapshot(w,Path.Combine(State.Home,$"v15-{pair.Key}-{width}.png"));
   }
   w.PausePlayback();
  }
  w.Width=1500;w.Height=900;w.OpenTrack(cases["图片视频"],true,0);await w.SubtitleReady;await w.AutoVideoReady;await w.ImageReady;await Delay(250);
  w.Width=820;w.Height=680;await Delay(300);w.UpdateLayout();var small=PageBounds(w.SubtitlePanel,w.LyricsWorkspace);Check("820×680三媒体歌词保留独立可读区域",small.Width>500&&small.Height>=100&&w.VideoImage.ActualHeight>10,$"lyric={small}; video height={w.VideoImage.ActualHeight}");Snapshot(w,Path.Combine(State.Home,"v15-compact-minimum.png"));w.Width=1500;w.Height=900;await Delay(150);
  var controls=new SettingsWindow(w.settings,w.overlay,w.ApplyLiveSettings){Owner=w};controls.Show();await Delay(100);
  var show=Visuals<CheckBox>(controls).Single(c=>Equals(c.Content,"显示图片"));show.IsChecked=false;show.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));Check("设置显示隐藏图片立即作用于实际面板",w.LyricsArtwork.Visibility==Visibility.Collapsed);
  show.IsChecked=true;show.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
  var side=Visuals<ComboBox>(controls).Single(c=>c.ItemsSource is string[] options&&options[0]=="图片在左侧");side.SelectedIndex=1;await Delay(120);w.UpdateLayout();Check("设置图片视频同侧分区不覆盖歌词",VisualTreeHelper.GetParent(w.LyricsArtwork)==VisualTreeHelper.GetParent(w.VideoPane)&&Rect.Intersect(PageBounds(w.LyricsArtwork,w),PageBounds(w.VideoPane,w)).IsEmpty);
  var videoVisible=Visuals<CheckBox>(controls).Single(c=>Equals(c.Content,"显示视频"));videoVisible.IsChecked=false;videoVisible.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));await Delay(100);Check("隐藏配套视频暂停隐藏解码和绘制仍保留音频",!w.video.Playing&&w.VideoImage.Source==null&&w.engine.Playing);
  videoVisible.IsChecked=true;videoVisible.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));side.SelectedIndex=0;controls.Close();w.Save();var saved=State.Read<Settings>("settings.json");Check("媒体布局即时设置可以持久保存",saved.ShowPictures&&saved.ShowVideo&&!saved.PictureOnRight);
  w.BrowsePicture(1);await w.ImageReady;await Delay(200);Check("歌词图片按自然顺序切换并更新页码且不改变封面",w.LyricsImageCount.Text=="2 / 2"&&w.PicturePaths[0].EndsWith("2 插画.png")&&w.currentWork?.Cover?.EndsWith("2 插画.png")==true&&ReferenceEquals(w.Picture.Source,w.LyricsPicture.Source));w.BrowsePicture(1);await w.ImageReady;Check("歌词图片顺序循环且默认完整显示",w.LyricsImageCount.Text=="1 / 2"&&w.LyricsPicture.Stretch==Stretch.Uniform);
  w.PausePlayback();w.Seek(1);w.OpenLyricsForTest();await Delay(180);w.UpdateLayout();var active=w.SubtitleLines.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;var body=active==null?null:Visuals<TextBlock>(active).FirstOrDefault(t=>t.Text.Contains("おやすみ"));var backing=active==null?null:Visuals<Border>(active).FirstOrDefault(b=>b.CornerRadius.TopLeft==17);
  Check("当前多行歌词高亮包住正文且实际加粗提亮",active?.Tag as string=="active"&&active.FontWeight==FontWeights.Bold&&body!=null&&backing!=null&&backing.ActualHeight>=body.ActualHeight&&((SolidColorBrush)backing.Background).Color.A>100,$"weight={active?.FontWeight}; text={body?.ActualHeight}; background={backing?.ActualHeight}");
  Check("视觉动态范围对轻声可见且静音无随机动画",AudioWave.Map(.001f)>.2&&AudioWave.Map(.005f)>AudioWave.Map(.001f)&&AudioWave.Map(0)==0);
  var volume=w.engine.Volume;w.settings.WaveHeight=100;w.ApplyLiveSettings();w.PlayForTest();await Delay(400);Check("纤细波形保持独立区域且声音音量未改变",w.Wave.ActualWidth>400&&w.Wave.ActualHeight is >=16 and <=48&&w.Wave.Peak>.2&&w.engine.Volume==volume,$"size={w.Wave.ActualWidth}×{w.Wave.ActualHeight}; visualPeak={w.Wave.Peak}");
  w.settings.Stars=2;w.settings.StarIntensity=1.2;w.UpdateVisuals();var sample=w.Stars.Sample(0);var second=w.Stars.Sample(1);await Delay(1300);var after=w.Stars.Sample(0);var afterSecond=w.Stars.Sample(1);Check("真实星点位移可见且亮度错开闪烁",Math.Abs(after.x-sample.x)>.001&&Math.Abs((after.light-sample.light)-(afterSecond.light-second.light))>.0001,$"dx={(after.x-sample.x)*w.ActualWidth:0.0}px; brightness delta={after.light-sample.light:0.000}");
  await VerifyTransparentOverlay(w);
  // Circle entries are fixed; choosing one includes all series without entering folders.
  w.Navigate("home");await w.FilterForTest(null,null);var circle=w.CircleChoices.Items.Cast<MainWindow.CircleOption>().Single(c=>c.Name=="星光社");w.CircleChoices.SelectedItem=circle;await w.HomeReady;await Delay(100);await w.HomeReady;Check("固定社团入口恢复显示系列文件夹",w.ShownWorks==4&&w.WorksList.Items.Cast<HomeRow>().SelectMany(r=>r.Entries).Any(t=>t.Folder?.Name=="系列甲")&&(w.CircleChoices.SelectedItem as MainWindow.CircleOption)?.Name==circle.Name,$"shown={w.ShownWorks}; folder={w.browserFolder}; filter={w.circleFilter}; selected={(w.CircleChoices.SelectedItem as MainWindow.CircleOption)?.Name}; tiles={string.Join(',',w.WorksList.Items.Cast<HomeRow>().SelectMany(r=>r.Entries).Select(t=>t.Name))}");
  Check("系列文件夹可见且标签入口收纳",w.WorksList.Items.Cast<HomeRow>().SelectMany(r=>r.Entries).Any(t=>t.Folder!=null)&&w.TagFiltersPanel.Visibility==Visibility.Collapsed);
  var kept=w.WorksList.ItemsSource;var chosen=w.works.Single(x=>x.Groups.Any(g=>g.Audio.Contains(cases["图片视频"])));w.SelectWork(chosen);w.Navigate("home");await w.RenderHome();Check("返回保留社团筛选和作品列表位置数据源",ReferenceEquals(kept,w.WorksList.ItemsSource)&&w.circleFilter=="星光社");
  w.CircleChoices.SelectedIndex=0;await w.HomeReady;Check("全部作品跨社团浏览且手动组织关系保留",w.ShownWorks==w.works.Count&&w.CircleChoices.SelectedIndex==0&&w.organization.Locations.ContainsKey(chosen.Path));
  w.settings.ShowPictures=w.settings.ShowVideo=true;w.settings.PictureOnRight=false;w.settings.VideoOnRight=true;w.settings.WaveHeight=64;w.settings.Stars=2;w.Width=1500;w.Height=900;w.OpenTrack(cases["图片视频"],true,0);await w.SubtitleReady;await w.AutoVideoReady;w.ApplyLiveSettings();await Delay(500);await Measure("组合_三栏歌词图片视频波形星空",w);
  w.overlay.ShowPassive();w.WindowState=WindowState.Minimized;var starFrames=w.Stars.Frames;var waveFrames=w.Wave.Frames;await Measure("最小化_新版透明桌面字幕",w);Check("新版最小化完全停止不可见绘制并保持主音频",w.Stars.Frames==starFrames&&w.Wave.Frames==waveFrames&&!w.video.Playing&&w.engine.Playing);w.WindowState=WindowState.Normal;w.overlay.Hide();w.PausePlayback();
  State.Write("media-layout-validation.json",layouts);w.Save();
 }
 static async Task VerifyTransparentOverlay(MainWindow w)
 {
  w.Hide();var host=new Window{Width=1000,Height=330,Left=80,Top=160,Background=new SolidColorBrush(Color.FromRgb(52,71,94)),WindowStyle=WindowStyle.None,ShowInTaskbar=false};host.Show();host.Activate();await Delay(100);
  w.overlay.Left=120;w.overlay.Top=190;w.overlay.Width=850;w.overlay.Height=220;w.settings.OverlayArt=true;w.settings.OverlayColor="#FFB2D9";w.settings.OverlayBackground=true;w.overlay.EndEditing();w.overlay.SetText("透明底的粉色星光\n日本語・かなカナ");
  System.Drawing.Color Pixel(Point p){using var bitmap=new System.Drawing.Bitmap(1,1);using var graphics=System.Drawing.Graphics.FromImage(bitmap);graphics.CopyFromScreen((int)p.X,(int)p.Y,0,0,new System.Drawing.Size(1,1));return bitmap.GetPixel(0,0);}
  var corner=host.PointToScreen(new Point(45,35));var baseline=Pixel(corner);var focus=NativeProbe.GetForegroundWindow();w.overlay.ShowPassive();await Delay(180);var composited=Pixel(corner);
  var bmp=new RenderTargetBitmap((int)w.overlay.ActualWidth,(int)w.overlay.ActualHeight,96,96,PixelFormats.Pbgra32);bmp.Render(w.overlay);var rgba=new byte[bmp.PixelWidth*bmp.PixelHeight*4];bmp.CopyPixels(rgba,bmp.PixelWidth*4,0);bool pink=Enumerable.Range(0,rgba.Length/4).Any(i=>rgba[i*4+2]>180&&rgba[i*4+1]<rgba[i*4+2]-20&&rgba[i*4+3]>100);
  Check("桌面窗口真实透明合成无黑底且不抢焦点",w.overlay.TransparentSurface&&rgba[3]==0&&baseline.ToArgb()==composited.ToArgb()&&NativeProbe.GetForegroundWindow()==focus&&w.overlay.NativeClickThrough&&w.overlay.NativeNoActivate,$"native corner before={baseline}; after={composited}");
  Check("桌面字幕实际使用包内艺术字与粉色像素",pink&&w.overlay.DisplayFont.Source.Contains("LXGW",StringComparison.OrdinalIgnoreCase));Snapshot(w.overlay,Path.Combine(State.Home,"desktop-transparent-pink.png"));w.overlay.Hide();host.Close();w.Show();w.Activate();
 }
}
