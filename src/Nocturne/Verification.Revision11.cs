using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SkiaSharp;
namespace Nocturne;
public static partial class Verification
{
 static async Task Reproduce11(MainWindow w)
 {
  var root=Path.Combine(State.Home,"复现","社团");var work=Path.Combine(root,"A 社团","系列","作品");var other=Path.Combine(root,"B 社团","作品");Directory.CreateDirectory(work);Directory.CreateDirectory(other);var audio=Path.Combine(work,"01 中文 日本語.wav");WriteWave(audio,30);WriteWave(Path.Combine(other,"01.wav"),2);
  MetadataStore.SetCircle(w.settings,new[]{work},"A 社团");MetadataStore.SetCircle(w.settings,new[]{other},"B 社团");var images=Path.Combine(work,"任意名字","第二层");Directory.CreateDirectory(images);using(var bitmap=new SKBitmap(160,100)){using var canvas=new SKCanvas(bitmap);canvas.Clear(SKColors.Pink);using var image=SKImage.FromBitmap(bitmap);using var data=image.Encode(SKEncodedImageFormat.Png,90);File.WriteAllBytes(Path.Combine(images,"2 插图.PNG"),data.ToArray());}
  File.WriteAllText(Path.ChangeExtension(audio,"lrc"),"[00:00.00]<font size=\"44\">第一行</font>\n[00:03.00]{\\fs12}第二行\n[00:08.00]第三行");w.settings.Roots.Add(root);await w.Scan(true);await w.SetBrowseMode(false);await Delay(250);var source=w.CircleChoices.ItemsSource;var changing=w.EnterFolder(Organization.CircleId("A 社团"));var opacity=w.HomePage.Opacity;await changing;
  var imageCount=ImageDiscovery.Find(audio,w.works.Single(x=>x.Groups.Any(g=>g.Audio.Contains(audio)))).Files.Count;w.overlay.ShowPassive();var opensLocked=!w.overlay.Editing;var unlockCount=Visuals<Button>(w.overlay).Count(b=>b.ToolTip?.ToString()?.Contains("解锁")==true);w.overlay.Hide();
  w.OpenTrack(audio,true,0);await w.SubtitleReady;await Delay(250);w.settings.Stars=2;w.UpdateVisuals();var stars=w.Stars.Frames;var wave=w.Wave.Frames;await Delay(1050);var starFps=(w.Stars.Frames-stars)/1.05;var waveFps=(w.Wave.Frames-wave)/1.05;w.PausePlayback();var fonts=Visuals<TextBlock>(w.SubtitleLines).Where(t=>t.Text.Length>0).Select(t=>t.FontSize).Distinct().ToArray();var dirty=w.CurrentSubtitle.Contains("<font");
  var movie=Path.Combine(work,"02 视频.mp4");File.Copy(Path.Combine(Environment.CurrentDirectory,"test-fixtures","generated.mp4"),movie,true);var opened=await w.OpenVideo(movie,true,false,1);await Delay(200);var disabled=!w.VideoSeek.IsEnabled;w.EnterVideoFullscreen();await Delay(200);var full=Application.Current.Windows.Cast<Window>().FirstOrDefault(x=>x!=w&&x.Title=="月咲 Shion · 视频全屏");var fullSliders=full==null?-1:Visuals<Slider>(full).Count();w.ExitVideoFullscreen();w.CloseVideoSession();
  State.Write("reproduction.json",new{circleSelectionReplacesItemsSource=!ReferenceEquals(source,w.CircleChoices.ItemsSource),circleSelectionPageOpacity=opacity,arbitraryChildImagesFound=imageCount,desktopOpensLocked=opensLocked,lockedUnlockButtons=unlockCount,starFps,waveFps,visibleLyricFontSizes=fonts,lrcFormatMarkupLeaked=dirty,videoOpened=opened,directVideoProgressDisabled=disabled,fullscreenProgressControls=fullSliders});
 }
}
