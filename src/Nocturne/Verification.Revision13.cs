using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NAudio.Wave;
namespace Nocturne;
public static partial class Verification
{
 static async Task VerifyRevision13(MainWindow w)
 {
  await VerifyRevision12(w);w.Activate();w.Topmost=true;
  var audio=Path.Combine(State.Home,"实际声音渐变.wav");WriteWave(audio,30);w.settings.PauseFade=true;w.Volume.Value=.65;w.OpenTrack(audio,true,0);await w.SubtitleReady;await Delay(450);
  var volume=w.Volume.Value;var watch=Stopwatch.StartNew();var clock=w.PlaybackPosition;w.RequestPlayback(false);await Delay(260);Check("淡出中真实时钟与字幕仍推进",w.PlaybackPlaying&&w.PlaybackPosition>clock+.15&&w.engine.TemporaryGain is >.05f and <.8f);await w.FadeReady;Check("约半秒淡出排空缓冲后暂停",!w.PlaybackPlaying&&watch.Elapsed.TotalMilliseconds>=500&&watch.Elapsed.TotalMilliseconds<950&&w.Volume.Value==volume,$"elapsed={watch.Elapsed.TotalMilliseconds}");
  w.RequestPlayback(true);await Delay(250);Check("恢复增益渐入且音量滑块稳定",w.PlaybackPlaying&&w.engine.TemporaryGain is >.1f and <.95f&&w.Volume.Value==volume);await w.FadeReady;
  await Loopback13(w);
  for(int i=0;i<8;i++){w.RequestPlayback(i%2!=0);await Delay(65);}await w.FadeReady;Check("快速反转只保留最新播放状态",w.PlaybackPlaying&&w.engine.TemporaryGain>.99&&Math.Abs(w.Volume.Value-volume)<.0001,$"playing={w.PlaybackPlaying}, gain={w.engine.TemporaryGain}, volume={w.Volume.Value}, expected={volume}");
  w.Volume.Value=0;w.RequestPlayback(false);await w.FadeReady;w.RequestPlayback(true);await w.FadeReady;Check("静音恢复仍为静音",w.engine.Volume==0&&w.Volume.Value==0);w.Volume.Value=volume;
  w.RequestPlayback(false);await Delay(80);w.OpenTrack(audio,true,2);await Delay(750);Check("切轨取消过时淡出任务",w.PlaybackPlaying&&w.PlaybackPosition>2.5&&w.engine.TemporaryGain>.99);
  w.WindowState=WindowState.Minimized;w.RequestPlayback(false);await w.FadeReady;w.RequestPlayback(true);await w.FadeReady;Check("最小化声音过渡有效且无星空绘制",w.PlaybackPlaying&&!w.Stars.Running&&!w.Wave.Running);w.WindowState=WindowState.Normal;w.PausePlayback();
  w.PlayForTest();await Delay(120);await ClickElement(w.PlayButton);await w.FadeReady;Check("鼠标播放按钮共用淡出逻辑",!w.PlaybackPlaying&&w.engine.TemporaryGain<.01);NativeProbe.Key(0x20,false);await Delay(45);NativeProbe.Key(0x20,true);await w.FadeReady;Check("空格共用淡入逻辑",w.PlaybackPlaying&&w.engine.TemporaryGain>.99);
  var movie=Path.Combine(Environment.CurrentDirectory,"test-fixtures","generated.mp4");var videoOpened=await w.OpenVideo(movie,true,false,3);Check("实际视频使用共同过渡入口",videoOpened);if(videoOpened){w.RequestPlayback(true);await w.FadeReady;var time=w.PlaybackPosition;var videoWatch=Stopwatch.StartNew();w.RequestPlayback(false);await w.FadeReady;Check("视频约500ms淡出后暂停且时间继续到暂停",!w.PlaybackPlaying&&w.PlaybackPosition>time+.3&&videoWatch.Elapsed.TotalMilliseconds>=480&&videoWatch.Elapsed.TotalMilliseconds<900);w.CloseVideoSession();}
  var sample=w.works.SelectMany(x=>x.Groups).SelectMany(g=>g.Audio).First(x=>Path.GetExtension(x).Equals(".wav",StringComparison.OrdinalIgnoreCase));w.OpenTrack(sample,false);await w.SubtitleReady;w.OpenLyricsForTest();
  w.overlay.RestoreEditing();w.overlay.Width=680;w.overlay.SetText("星光落在耳边\nおやすみなさい");await Delay(150);w.overlay.EndEditing();await Delay(80);
  var rect=w.overlay.GlyphBounds;var p=w.overlay.PointToScreen(new Point(rect.Left+rect.Width/2,rect.Top+rect.Height/2));NativeProbe.SetCursorPos((int)p.X,(int)p.Y);await Delay(200);var icon=w.overlay.UnlockButton;var ip=icon.TranslatePoint(new Point(),Window.GetWindow(icon));var screen=Window.GetWindow(icon)!.PointToScreen(new Point());var pos=w.overlay.PointFromScreen(screen);
  Check("解锁图标距实际文字下缘约10DIP",w.overlay.UnlockVisible&&Math.Abs(pos.Y-rect.Bottom-10)<2&&Math.Abs(pos.X+18-rect.Left-rect.Width/2)<2,$"gap={pos.Y-rect.Bottom}");
  var click=icon.PointToScreen(new Point(icon.ActualWidth/2,icon.ActualHeight/2));NativeProbe.SetCursorPos((int)click.X,(int)click.Y);await Delay(250);Check("文字到下方图标不中途消失",w.overlay.UnlockVisible);NativeProbe.ClickAt((int)click.X,(int)click.Y);await Delay(100);Check("下方小锁原生点击恢复编辑",w.overlay.Editing&&!w.overlay.NativeClickThrough);await Delay(300);Snapshot(w.overlay,Path.Combine(State.Home,"desktop-bottom-tools.png"));NativeOverlayShot13(w.overlay,"desktop-edit-native.png",150);
  w.overlay.EndEditing();w.overlay.Width=480;w.settings.OverlayFont=40;w.overlay.Apply();w.overlay.SetText("中文 日本語\n字体变化仍贴近字幕");await Delay(100);rect=w.overlay.GlyphBounds;p=w.overlay.PointToScreen(new Point(rect.Left+rect.Width/2,rect.Top+rect.Height/2));NativeProbe.SetCursorPos((int)p.X,(int)p.Y);await Delay(180);screen=Window.GetWindow(icon)!.PointToScreen(new Point());pos=w.overlay.PointFromScreen(screen);Check("字号宽度换行后重算解锁锚点",Math.Abs(pos.Y-rect.Bottom-10)<2&&w.overlay.NativeClickThrough);Snapshot(w.overlay,Path.Combine(State.Home,"desktop-locked.png"));NativeOverlayShot13(w.overlay,"desktop-unlock-native.png",52);w.overlay.Hide();
  w.Navigate("landing");await Delay(450);Check("主页入口正文实际使用明亮文字",new[]{w.HomeAdd,w.HomeListen,w.HomeVideo}.All(b=>b.Foreground is SolidColorBrush ink&&ink.Color.R>200));Snapshot(w,Path.Combine(State.Home,"home.png"));await w.SetBrowseMode(false);await Delay(450);Snapshot(w,Path.Combine(State.Home,"categories.png"));w.OpenLyricsForTest();await Delay(450);Snapshot(w,Path.Combine(State.Home,"subtitles.png"));
  var settings=new SettingsWindow(w.settings,w.overlay,w.ApplyLiveSettings){Owner=w};settings.Show();await Delay(160);Snapshot(settings,Path.Combine(State.Home,"settings.png"));Check("樱花开关与强度在设置中真实存在",Visuals<CheckBox>(settings).Any(c=>c.Content?.ToString()=="樱花点缀")&&Visuals<TextBlock>(settings).Any(t=>t.Text.StartsWith("樱花强度")));settings.Close();
  for(int i=0;i<12;i++){w.Navigate(i%2==0?"landing":"lyrics");await Delay(25);}await Delay(500);Check("快速连续切页最终页面可见且无旧层命中",w.CurrentPage=="lyrics"&&w.LyricsPage.Opacity==1&&w.LyricsPage.IsHitTestVisible&&!w.LandingPage.IsHitTestVisible);
  foreach(var scale in new[]{1d,1.25,1.5}){w.Width=820*scale;w.Height=740*scale;w.MainLayout.LayoutTransform=new ScaleTransform(scale,scale);w.Navigate("landing");await Delay(450);Check("主页三入口等效DPI边界 "+scale,PageBounds(w.HomeVideo,w).Right<=w.ActualWidth&&w.HomeAdd.ActualWidth>=195&&w.HomeListen.ActualHeight==150&&Math.Abs(PageBounds(w.HomeAdd,w).Top-PageBounds(w.HomeVideo,w).Top)<2&&PageBounds(w.WelcomeTitle,w).Top>=PageBounds(w.LandingPage,w).Top);Snapshot(w,Path.Combine(State.Home,"home-dpi-"+scale+".png"));}w.MainLayout.LayoutTransform=Transform.Identity;w.Save();
 }
 static void NativeOverlayShot13(OverlayWindow overlay,string name,int extra)
 {
  var r=overlay.GlyphBounds;var a=overlay.PointToScreen(new Point(0,r.Top-12));var b=overlay.PointToScreen(new Point(overlay.ActualWidth,r.Bottom+extra));using var bitmap=new System.Drawing.Bitmap(Math.Max(1,(int)(b.X-a.X)),Math.Max(1,(int)(b.Y-a.Y)));using(var g=System.Drawing.Graphics.FromImage(bitmap))g.CopyFromScreen((int)a.X,(int)a.Y,0,0,bitmap.Size);bitmap.Save(Path.Combine(State.Home,name),System.Drawing.Imaging.ImageFormat.Png);
 }
 static async Task Loopback13(MainWindow w)
 {
  var samples=new List<(double T,double Rms)>();var timer=Stopwatch.StartNew();
  try
  {
   using var capture=new WasapiLoopbackCapture();capture.DataAvailable+=(_,e)=>{double sum=0;int n=0;var format=capture.WaveFormat;if(format.BitsPerSample==32){for(int i=0;i+4<=e.BytesRecorded;i+=4){var value=BitConverter.ToSingle(e.Buffer,i);sum+=value*value;n++;}}else if(format.BitsPerSample==16){for(int i=0;i+2<=e.BytesRecorded;i+=2){var value=BitConverter.ToInt16(e.Buffer,i)/32768d;sum+=value*value;n++;}}if(n>0)lock(samples)samples.Add((timer.Elapsed.TotalMilliseconds,Math.Sqrt(sum/n)));};
   capture.StartRecording();await Delay(500);var down=timer.Elapsed.TotalMilliseconds;w.RequestPlayback(false);await w.FadeReady;await Delay(200);var up=timer.Elapsed.TotalMilliseconds;w.RequestPlayback(true);await w.FadeReady;await Delay(350);capture.StopRecording();await Delay(100);
   (double T,double Rms)[] raw;lock(samples)raw=samples.ToArray();var reference=raw.Where(x=>x.T>up+650).Select(x=>x.Rms).Order().ToArray();var steady=reference.Length==0?0:reference[reference.Length/2];var falling=raw.Where(x=>x.T>=down&&x.T<up).ToArray();var rising=raw.Where(x=>x.T>=up).ToArray();
   double Crossing((double T,double Rms)[] data,Func<double,bool> test)=>data.FirstOrDefault(x=>test(x.Rms)).T;
   var from=Crossing(falling,x=>x<steady*.9);var to=Crossing(falling,x=>x<steady*.1);var riseFrom=Crossing(rising,x=>x>steady*.1);var riseTo=Crossing(rising,x=>x>steady*.9);
   var downMs=(to-from)/.8;var upMs=(riseTo-riseFrom)/.8;State.Write("loopback-fade.json",new{method="Windows WASAPI output loopback RMS; generated stereo tone; 10%-90% crossing extrapolated for linear gain envelope, not loudness dB",steady,downMs,upMs,down,up,samples=raw.Select(x=>new{ms=x.T,rms=x.Rms})});
   Check("实际设备回环测到约500ms淡出",steady>.00001&&to>from&&downMs is >300 and <750,$"linear-envelope estimate={downMs:F1}ms, RMS={steady}");Check("实际设备回环测到约500ms淡入",riseTo>riseFrom&&upMs is >300 and <750,$"linear-envelope estimate={upMs:F1}ms");
  }catch(Exception ex){Check("实际输出回环验证",false,ex.Message);State.Write("loopback-error.json",new{error=ex.ToString()});}
 }
}
