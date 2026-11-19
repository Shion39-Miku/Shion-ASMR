using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Nocturne;
public static partial class Verification
{
 static async Task VerifyRevision12(MainWindow w,bool baseline=false)
 {
  w.Width=1000;w.Height=800;w.Left=20;w.Top=20;w.Topmost=true;w.settings.PauseFade=false;
  var root=Path.Combine(State.Home,"真实素材 中文 日本語");var work=Path.Combine(root,"社团A","系列甲","作品 长标题");Directory.CreateDirectory(work);
  var a=Path.Combine(work,"01 晚安.part.WAV");var b=Path.Combine(work,"02 おやすみ.wav");WriteLargeWave(a,420);WriteLargeWave(b,420);
  string Stamp(int n)=>TimeSpan.FromSeconds(n).ToString(@"mm\:ss\.fff");
  foreach(var path in new[]{a,b})File.WriteAllText(Path.ChangeExtension(path,"vtt"),"WEBVTT\n\n"+string.Join("\n\n",Enumerable.Range(0,200).Select(i=>$"{Stamp(i*2)} --> {Stamp(i*2+2)}\n第{i+1}句・中文 日本語 かな"+(i%3==0?"\n多行台词，安静地听见星光落下。":""))));
  const string circle="circle12",series="series12";w.settings.Organization[circle]=new(){Id=circle,Name="社团A"};w.settings.Organization[series]=new(){Id=series,Parent=circle,Name="系列甲"};w.settings.WorkLocations[work]=series;MetadataStore.SetCircle(w.settings,new[]{work},"社团A");MetadataStore.AddTag(w.settings,new[]{work},"收藏");w.settings.Roots.Add(root);await w.Scan(true);await w.SetBrowseMode(false);await w.EnterFolder(circle);await w.EnterFolder(series);
  var owner=w.works.Single(x=>x.Groups.Any(g=>g.Audio.Contains(a)));w.settings.WorkLocations[owner.Path]=series;MetadataStore.AddTag(w.settings,new[]{owner.Path},"收藏");await w.BuildTree();await w.EnterFolder(series);w.SelectWork(owner);var back=Visuals<Button>(w.DetailPage).First(x=>x.ToolTip?.ToString()=="返回分类");back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await w.HomeReady;await Delay(200);Check("作品返回实际系列而非功能主页",w.CurrentPage=="home"&&w.browserFolder==series);
  foreach(var paused in new[]{true,false})
  {
   w.settings.Tracks[a]=new(){Position=280.4,Offset=.15,FollowLyrics=false};w.Navigate("home");w.OpenTrack(a,!paused);await w.SubtitleReady;w.Navigate("home");w.OpenLyricsForTest();await Delay(250);
   Check("历史恢复首句立即居中 paused="+paused,CenterError(w)<5&&w.CurrentSubtitle.Contains("第141句"),"error="+CenterError(w)+" position="+w.PlaybackPosition);Check("定位保留播放状态 "+paused,w.PlaybackPlaying==!paused&&w.PlaybackPosition>=280.3&&w.PlaybackPosition<282);w.PausePlayback();
  }
  if(baseline){State.Write("baseline12.json",new{checks});return;}
  for(int i=0;i<8;i++){var path=i%2==0?a:b;w.settings.Tracks[path]=new(){Position=i%2==0?80.4:310.4};w.OpenTrack(path,false);w.OpenLyricsForTest();}await w.SubtitleReady;await Delay(250);Check("快速切换历史音轨只定位最终字幕",w.currentTrack==b&&w.CurrentSubtitle.Contains("第156句")&&CenterError(w)<5);
  w.Navigate("home");w.OpenTrack(a,false,200.4);w.OpenLyricsForTest();w.Seek(300.4);await w.SubtitleReady;await Delay(200);Check("字幕晚加载采用最新实际时钟",w.CurrentSubtitle.Contains("第151句")&&CenterError(w)<5&&!w.PlaybackPlaying);
  Check("底部主台词次标题且不重复",w.FooterSubtitle.FontSize>=18&&w.FooterSubtitle.Text.Contains("第151句")&&w.NowPlaying.Text==Path.GetFileNameWithoutExtension(a)&&w.FooterSubtitle.TranslatePoint(new Point(),w.PlayerSurface).Y<w.NowPlaying.TranslatePoint(new Point(),w.PlayerSurface).Y);
  w.SetLyricsFollow(false);FindScroll(w.SubtitleLines)!.ScrollToTop();w.Navigate("home");w.OpenLyricsForTest();await Delay(200);Check("重新进入清除手动浏览并居中",CenterError(w)<5&&w.settings.Tracks[a].FollowLyrics);
  w.Navigate("home");await w.FilterForTest(null,"收藏","作品");w.SelectWork(owner);back.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await w.HomeReady;await Delay(200);Check("返回保留搜索标签社团浏览上下文",w.CurrentPage=="home"&&w.Search.Text=="作品"&&w.CurrentFilters.Text.Contains("收藏"));
  w.overlay.RestoreEditing();w.overlay.Left=120;w.overlay.Top=100;w.overlay.Width=430;w.settings.OverlayFont=48;w.overlay.Apply();w.overlay.SetText("中文 日本語 かな\n长台词换行时工具栏仍在下方\n关闭和锁定按钮不被挤出");await Delay(180);
  var buttons=Visuals<Button>(w.overlay).Where(x=>x.IsVisible).ToArray();var text=Visuals<TextBlock>(w.overlay).First(x=>x.Text.StartsWith("中文 日本語"));var textY=text.TranslatePoint(new Point(),w.overlay).Y;
  Check("桌面工具在台词下方且全部位于窗口",buttons.Length>=8&&buttons.All(x=>{var p=x.TranslatePoint(new Point(),w.overlay);return p.Y>=w.overlay.GlyphBounds.Bottom&&p.X>=0&&p.X+x.ActualWidth<=w.overlay.ActualWidth;}));
  Snapshot(w.overlay,Path.Combine(State.Home,"desktop-multiline.png"));
  foreach(var width in new[]{380d,550d}){w.overlay.Width=width;w.settings.OverlayFont=64;w.overlay.Apply();await Delay(120);Check("桌面64号字工具完整且互不遮挡 "+width,Visuals<Button>(w.overlay).Where(x=>x.IsVisible).All(x=>{var p=x.TranslatePoint(new Point(),w.overlay);return p.X>=0&&p.X+x.ActualWidth<=w.overlay.ActualWidth&&p.Y>=w.overlay.GlyphBounds.Bottom;}));}w.overlay.Width=430;w.settings.OverlayFont=48;w.overlay.Apply();await Delay(80);var top=w.overlay.Top;var left=w.overlay.Left;w.overlay.SetText("短台词");await Delay(100);Check("台词变化不移动窗口或工具栏",w.overlay.Top==top&&w.overlay.Left==left&&buttons.All(x=>x.TranslatePoint(new Point(),w.overlay).Y>=w.overlay.GlyphBounds.Bottom));
  Snapshot(w.overlay,Path.Combine(State.Home,"desktop-editor.png"));w.overlay.EndEditing();Check("锁定保持透明穿透",w.overlay.TransparentSurface&&w.overlay.NativeClickThrough);w.overlay.Hide();
  foreach(var scale in new[]{1d,1.25,1.5}){w.Width=820*scale;w.Height=740*scale;w.MainLayout.LayoutTransform=new ScaleTransform(scale,scale);w.OpenLyricsForTest();w.settings.FontSize=36;w.ApplyFontForTest(true);await Delay(200);Check("等效缩放底栏控件不重叠 "+scale,PageBounds(w.FooterSubtitle,w.PlayerSurface).Right<=PageBounds(w.PreviousButton,w.PlayerSurface).Left&&PageBounds(w.OpenLyricsButton,w.PlayerSurface).Right<=w.PlayerSurface.ActualWidth&&CenterError(w)<5);Snapshot(w,Path.Combine(State.Home,"layout-"+scale+".png"));}
  w.MainLayout.LayoutTransform=Transform.Identity;w.Width=1180;w.Height=850;w.settings.FontSize=26;w.ApplyFontForTest(true);w.OpenTrack(a,false,280.4);await w.SubtitleReady;await Delay(200);Check("回收及字号变更后台词实际字号统一",Visuals<TextBlock>(w.SubtitleLines).Where(t=>t.DataContext is Cue c&&!c.Spacer).All(t=>Math.Abs(t.FontSize-w.settings.FontSize)<.01&&t.FontFamily.Source==w.SubtitleLines.FontFamily.Source));Snapshot(w,Path.Combine(State.Home,"player-footer.png"));w.Save();
  using(var probe=StartProbe("--startup-probe",State.Home,"--resume-probe",a)){await probe.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(25));using var data=System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(State.Home,"startup-probe.json")));Check("真实重启恢复逐轨进度及字幕",data.RootElement.GetProperty("position").GetDouble()>280.3&&data.RootElement.GetProperty("subtitle").GetString()!.Contains("第141句"));}
 }
}
