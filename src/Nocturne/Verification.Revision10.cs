using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Nocturne;
public static partial class Verification
{
 static double CenterError(MainWindow w)
 {
  w.UpdateLayout();var active=w.document.Active(w.PlaybackPosition-w.settings.Tracks[w.currentTrack!].Offset,holdPrevious:!w.directVideo);
  if(active.Count==0)return double.PositiveInfinity;
  var row=w.SubtitleLines.ItemContainerGenerator.ContainerFromItem(w.document.Cues[active[0]]) as ListBoxItem;var viewport=FindScroll(w.SubtitleLines);
  if(row==null||viewport==null)return double.PositiveInfinity;var box=PageBounds(row,viewport);
  return Math.Abs(box.Top+row.ActualHeight/2-viewport.ViewportHeight/2);
 }
 static async Task VerifyRevision10(MainWindow w)
 {
  w.Width=1180;w.Height=850;w.Left=40;w.Top=30;w.Topmost=true;w.settings.PauseFade=false;w.overlay.Hide();
  var root=Path.Combine(State.Home,"导入与字幕 中文 日本語 & [样例]","社团");var work=Path.Combine(root,"星社","系列甲","作品 星夜");Directory.CreateDirectory(work);
  var a=Path.Combine(work,"01 晚安.part.1.WAV");var b=Path.Combine(work,"02 切歌.wav");var none=Path.Combine(work,"03 无字幕.wav");WriteWave(a,125);WriteWave(b,12);WriteWave(none,12);
  string Stamp(double seconds)=>TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss\.fff");
  var longCue=string.Concat(Enumerable.Repeat("轻声细语・おやすみ・かなカナ。",14));
  File.WriteAllText(Path.ChangeExtension(a,"VTT"),"WEBVTT\n\n"+string.Join("\n\n",Enumerable.Range(0,80).Select(i=>$"{Stamp(i*1.5)} --> {Stamp(i*1.5+1.2)}\n"+(i==26?longCue:$"第{i+1}句 星光轻轻落下・かなカナ"+(i%3==0?"\n多行台词的第二行":"")))),new UTF8Encoding(true));
  File.WriteAllText(Path.ChangeExtension(b,"lrc"),"[00:00.00]另一首的台词\n[00:04.00]另一首第二句");
  MetadataStore.SetCircle(w.settings,new[]{work},"星社");
  for(int i=0;i<32;i++){var dir=Path.Combine(root,$"社团样本{i:00}","作品");Directory.CreateDirectory(dir);WriteWave(Path.Combine(dir,"01.wav"),1);MetadataStore.SetCircle(w.settings,new[]{dir},$"社团样本{i:00} 名称长一些仍然清晰");}
  w.Navigate("landing");
  if(Environment.GetCommandLineArgs().Contains("--native-import"))
  {
   State.Write("native-import-target.json",new{root});await ClickElement(w.AddFolderButton);
   for(int wait=0;wait<1800&&!w.ImportResult.Text.StartsWith("导入完成");wait++)await Delay(100);
   if(!w.ImportResult.Text.StartsWith("导入完成"))throw new TimeoutException("目录选择导入未在验证时间内完成。");
  }
  else await w.ImportFolders(new[]{root});w.library.Watch(Array.Empty<string>());
  Check("导入使用真实后台扫描并显示完成结果",w.library.Folders.ContainsKey(work)&&w.ImportResult.Text.StartsWith("导入完成")&&w.StatusSurface.Visibility==Visibility.Visible&&w.Status.Text.StartsWith("导入完成"));
  Check("两个固定入口始终显示图标与文字",new FrameworkElement[]{w.DesktopLyricsButton,w.AddFolderButton}.All(button=>Visuals<Symbol>(button).Any()&&Visuals<TextBlock>(button).Any(t=>t.Text is "桌面字幕" or "添加文件夹")));
  await ClickElement(w.DesktopLyricsButton);Check("桌面歌词真实点击开启且选中反馈可见",w.overlay.IsVisible&&System.Windows.Automation.AutomationProperties.GetItemStatus(w.DesktopLyricsButton)=="已开启"&&w.Status.Text.Contains("已开启"));
  await ClickElement(w.DesktopLyricsButton);Check("桌面歌词再次点击关闭并取消选中",!w.overlay.IsVisible&&System.Windows.Automation.AutomationProperties.GetItemStatus(w.DesktopLyricsButton)=="已关闭"&&w.Status.Text.Contains("已关闭"));
  await w.SetBrowseMode(false);await Delay(180);Check("社团选择栏加宽且保持大量选项可滚动",w.BrowserSidebar.ActualWidth>=250&&w.CircleChoices.Items.Count>=33&&FindScroll(w.CircleChoices)?.ScrollableHeight>0);
  w.CircleSearch.Text="样本23";await Delay(100);Check("社团搜索只筛选导航且不改变层级",w.CircleChoices.Items.Count==2&&w.CircleChoices.Items.Cast<MainWindow.CircleOption>().Any(c=>c.Label.Contains("样本23")));
  w.CircleSearch.Text="";await w.EnterFolder(Organization.CircleId("星社"));await Delay(180);
  var series=Tiles(w).Single(t=>t.Folder?.Name=="系列甲");var selected=w.CircleChoices.ItemContainerGenerator.ContainerFromItem(w.CircleChoices.SelectedItem) as ListBoxItem;
  Check("社团保持选中大行且系列文件夹仍在",series.Folder!=null&&selected?.ActualHeight>=62&&Visuals<Border>(selected).Any(border=>border.BorderBrush is SolidColorBrush color&&color.Color.A>0));
  var circleViewport=FindScroll(w.CircleChoices)!;var selectedBounds=selected==null?Rect.Empty:PageBounds(selected,circleViewport);Check("大量社团时当前选中项完整可见",!selectedBounds.IsEmpty&&selectedBounds.Top>=-1&&selectedBounds.Bottom<=circleViewport.ViewportHeight+1,$"row={selectedBounds}; viewport={circleViewport.ViewportHeight}");
  await w.EnterFolder(series.Folder!.Id);Check("系列进入及面包屑保留作品归属",Tiles(w).Any(t=>t.Work?.Path==work)&&w.Breadcrumb.Items.Count>=3);
  foreach(var width in new[]{820d,1180d,1600d})
  {
   w.Width=width;w.Height=800;await Delay(160);w.UpdateLayout();
   bool buttons=new FrameworkElement[]{w.AddFolderButton,w.DesktopLyricsButton}.All(button=>PageBounds(button,w).Right<w.ActualWidth&&Visuals<TextBlock>(button).All(t=>t.ActualWidth>45));
   Check("固定文字按钮与社团栏适配宽度 "+width,buttons&&w.BrowserSidebar.ActualWidth>=230&&w.WorksList.ActualWidth>300);
   Snapshot(w,Path.Combine(State.Home,$"browser-{width}.png"));
  }
  foreach(var scale in new[]{1.25,1.5}){w.Width=1180*scale;w.Height=800*scale;w.MainLayout.LayoutTransform=new ScaleTransform(scale,scale);await Delay(180);Check("按钮与社团栏等效缩放 "+scale,w.AddFolderButton.ActualWidth>=110&&w.BrowserSidebar.ActualWidth>=230&&PageBounds(w.DesktopLyricsButton,w).Right<w.ActualWidth);Snapshot(w,Path.Combine(State.Home,$"scale-{scale}.png"));}w.MainLayout.LayoutTransform=Transform.Identity;w.Width=1180;w.Height=850;
  w.OpenTrack(a,false,30.3);await w.SubtitleReady;w.Navigate("home");w.overlay.ShowPassive();w.UpdateClock();
  Check("离开歌词页底栏与桌面共用实时字幕",w.FooterSubtitle.Text.Contains("第21句")&&w.FooterSubtitle.ToolTip as string==w.CurrentSubtitle&&w.overlay.DisplayedText==w.CurrentSubtitle&&w.CurrentPage=="home");
  var paused=w.PlaybackPosition;w.Seek(31.8);Check("暂停定位立即更新底部台词且不播放",!w.PlaybackPlaying&&w.FooterSubtitle.Text.Contains("第22句")&&w.overlay.DisplayedText==w.CurrentSubtitle);
  w.Seek(31.3);Check("音频VTT空档保留上一句",w.FooterSubtitle.Text.Contains("第21句")&&w.CurrentSubtitle.Contains("第21句")&&w.overlay.DisplayedText==w.CurrentSubtitle);
  w.Seek(10.2);var old=w.FooterSubtitle.Text;w.PlayForTest();await Delay(650);Check("分类页播放时底栏随实际时钟切换",w.PlaybackPosition>10.7&&w.FooterSubtitle.Text!=old&&w.CurrentPage=="home"&&w.FooterSubtitle.ToolTip as string==w.CurrentSubtitle);w.PausePlayback();
  w.Seek(31.8);w.PlayButton.Focus();await KeyPress(0x27);Check("快捷键前进后底栏立即更新",w.FooterSubtitle.Text.Contains("第25句")&&!w.PlaybackPlaying);await KeyPress(0x25);Check("快捷键后退后底栏立即更新",w.FooterSubtitle.Text.Contains("第23句")&&!w.PlaybackPlaying);
  w.OpenTrack(b,false,1);await w.SubtitleReady;Check("切换音轨自动字幕立即替换底栏",w.FooterSubtitle.Text=="另一首的台词"&&w.overlay.DisplayedText=="另一首的台词");
  w.OpenTrack(none,false,1);Check("无字幕音轨切换立即清除上一首",!w.FooterSubtitle.Text.Contains("另一首")&&w.overlay.DisplayedText=="");await w.SubtitleReady;Check("无匹配字幕显示简洁提示",w.FooterSubtitle.Text=="未找到匹配字幕"&&w.CurrentSubtitle=="");
  w.OpenTrack(a,false,39.3);await w.SubtitleReady;w.Navigate("home");w.Width=820;await Delay(240);Check("长台词底栏单行省略且悬停保留完整内容",w.FooterSubtitle.Text==longCue&&w.FooterSubtitle.ToolTip as string==longCue&&w.FooterSubtitle.TextTrimming==TextTrimming.CharacterEllipsis&&w.FooterSubtitle.TextWrapping==TextWrapping.NoWrap&&w.FooterSubtitle.ActualHeight<24);Snapshot(w,Path.Combine(State.Home,"long-footer-820.png"));
  ToolTipService.SetInitialShowDelay(w.FooterSubtitle,100);var hover=w.FooterSubtitle.PointToScreen(new Point(20,10));NativeProbe.SetCursorPos((int)hover.X,(int)hover.Y);await Delay(650);
  var tip=PresentationSource.CurrentSources.Cast<PresentationSource>().Select(s=>s.RootVisual).Where(v=>v!=null).SelectMany(v=>Visuals<ToolTip>(v!)).FirstOrDefault(t=>t.IsOpen&&t.Content as string==longCue);
  Check("真实悬停完整长台词且提示框换行",tip!=null&&tip.ActualWidth<=620&&Visuals<TextBlock>(tip).Any(t=>t.Text==longCue&&t.ActualHeight>30),$"tooltip={tip?.ActualWidth}x{tip?.ActualHeight}");NativeProbe.SetCursorPos(55,55);ToolTipService.SetInitialShowDelay(w.FooterSubtitle,500);w.Width=1180;
  w.OpenTrack(a,false,60.3);await w.SubtitleReady;w.settings.ReduceMotion=false;w.OpenLyricsForTest();await Delay(200);
  w.SetLyricsFollow(false);FindScroll(w.SubtitleLines)!.ScrollToTop();w.Navigate("home");var position=w.PlaybackPosition;
  await ClickElement(w.OpenLyricsButton);await Delay(120);var error=CenterError(w);
  Check("中途打开歌词清除旧浏览位置并直接居中",error<5&&!SmoothScroll.Running(w.SubtitleLines)&&w.FollowButton.Content as string=="正在跟随字幕",$"center error={error}; scrolling={SmoothScroll.Running(w.SubtitleLines)}");
  Check("打开歌词不重播不改变暂停或进度",!w.PlaybackPlaying&&Math.Abs(w.PlaybackPosition-position)<.02);
  Snapshot(w,Path.Combine(State.Home,"lyrics-current-centered.png"));
  w.SetLyricsFollow(false);FindScroll(w.SubtitleLines)!.ScrollToTop();w.Seek(90.3);await Delay(100);Check("进入后仍可手动浏览并显示恢复入口",w.FollowButton.Content as string=="回到当前台词"&&FindScroll(w.SubtitleLines)!.VerticalOffset<1);await ClickElement(w.FollowButton);await Delay(250);Check("恢复跟随重新居中",CenterError(w)<5);
  foreach(var index in new[]{0,40,79}){w.Navigate("home");w.Seek(index*1.5+.3);w.OpenLyricsForTest();await Delay(150);Check("首次定位首尾及多行台词 "+index,CenterError(w)<5,$"center error={CenterError(w)}");}
  w.Navigate("home");w.OpenTrack(a,false,3);var loading=w.LoadSubtitleChoicesForTest();w.OpenLyricsForTest();w.Seek(75.3);await loading;await Delay(180);Check("字幕异步完成按最新时钟定位",w.FooterSubtitle.Text.Contains("第51句")&&CenterError(w)<5,$"position={w.PlaybackPosition}; center error={CenterError(w)}");
  foreach(var width in new[]{820d,1180d}){w.Width=width;w.Height=680;w.settings.FontSize=34;w.ApplyFontForTest(true);w.Navigate("home");w.OpenLyricsForTest();await Delay(180);Check("窄窗口及字号变化后居中 "+width,CenterError(w)<5&&w.FooterSubtitle.ActualHeight>=17,$"center error={CenterError(w)}");}
  w.Width=1180;w.Height=850;w.settings.FontSize=28;w.ApplyFontForTest(true);w.Navigate("home");w.Seek(60.8);w.PlayForTest();position=w.PlaybackPosition;await ClickElement(w.OpenLyricsButton);await Delay(180);Check("播放中打开歌词继续播放且保持当前时间",w.PlaybackPlaying&&w.PlaybackPosition>position&&w.PlaybackPosition-position<1&&CenterError(w)<5,$"position delta={w.PlaybackPosition-position}; center error={CenterError(w)}");
  for(int i=0;i<9;i++){w.Navigate("home");w.Seek(i*7.5+.3);w.OpenLyricsForTest();}await Delay(180);Check("连续进出不被旧定位任务覆盖",w.CurrentPage=="lyrics"&&CenterError(w)<5);w.PausePlayback();
  await Delay(4200);Check("操作反馈到时消失且不覆盖底栏台词",w.StatusSurface.Visibility==Visibility.Collapsed&&w.FooterSubtitle.ToolTip as string==w.CurrentSubtitle);
  w.overlay.Hide();w.Navigate("home");await Delay(240);Snapshot(w,Path.Combine(State.Home,"footer-current-line.png"));w.Save();
  using(var probe=StartProbe("--startup-probe",State.Home,"--resume-probe",a)){await probe.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(25));using var json=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(State.Home,"startup-probe.json")));Check("重启默认功能主页不播放且保留逐轨记录",json.RootElement.GetProperty("initialPage").GetString()=="landing"&&json.RootElement.GetProperty("initiallyStopped").GetBoolean()&&json.RootElement.GetProperty("subtitle").GetString()==w.CurrentSubtitle);}
 }
}
