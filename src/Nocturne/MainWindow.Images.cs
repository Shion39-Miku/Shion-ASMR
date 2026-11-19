using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using System.Threading;

namespace Nocturne;
public partial class MainWindow
{
 int pictureSearchRequest;List<string> pictures=new();int imageIndex,imageRequest;bool imageFit=true;double imageZoom=1;
 CancellationTokenSource? imageDecode,pictureSearchCancel;
 internal IReadOnlyList<string> PicturePaths=>pictures;internal int PictureIndex=>imageIndex;
 internal void UpdatePictures()=>ImageReady=DiscoverPictures();
 async Task DiscoverPictures()
 {
  var request=++pictureSearchRequest;pictureSearchCancel?.Cancel();pictureSearchCancel?.Dispose();var cancel=pictureSearchCancel=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);var token=cancel.Token;
  var track=currentTrack;var path=folder;var catalog=works.ToArray();var work=CurrentPage=="lyrics"&&track!=null?catalog.FirstOrDefault(w=>w.Groups.Any(g=>g.Audio.Contains(track)))??currentWork:currentWork;var source=CurrentPage=="lyrics"&&track!=null?track:System.IO.Path.Combine(path,"_preview.wav");
  try{var result=await Task.Run(()=>ImageDiscovery.Find(source,work,catalog,token),token);var found=result.Files;if(request!=pictureSearchRequest||path!=folder||track!=currentTrack||closing)return;pictures=found;imageIndex=0;imageFit=true;await ShowPicture();if(result.Error!=null){Status.Text="图片目录读取失败："+result.Error;if(found.Count==0){LyricsImageCount.Text="图片检索失败";ImageName.Text=Status.Text;}}ArrangeMedia();UpdateBackground();}
  catch(OperationCanceledException){}
 }
 async Task ShowPicture()
 {
  int version=++imageRequest;imageDecode?.Cancel();imageDecode?.Dispose();var decode=new CancellationTokenSource();imageDecode=decode;var path=pictures.Count>0?pictures[imageIndex]:null;
  PreviousImageButton.Visibility=NextImageButton.Visibility=pictures.Count>1?Visibility.Visible:Visibility.Collapsed;
  if(path==null){LyricsImageCount.Text="没有图片";Picture.BeginAnimation(OpacityProperty,null);Picture.Source=PicturePrevious.Source=null;LyricsPicture.Source=LyricsPicturePrevious.Source=null;NoPicture.Visibility=Visibility.Visible;ImageName.Text="暂无作品图片";return;}
  try
  {
   var source=await imageCache.LoadAsync(path,decode.Token);if(version!=imageRequest||closing)return;
   var previous=Picture.Source;var priorLyric=LyricsPicture.Source;LyricsPicture.BeginAnimation(OpacityProperty,null);LyricsPicturePrevious.Source=priorLyric;LyricsPicture.Source=source;LyricsImageCount.Text=$"{imageIndex+1} / {pictures.Count}";LyricsPreviousImage.Visibility=LyricsNextImage.Visibility=pictures.Count>1?Visibility.Visible:Visibility.Collapsed;if(priorLyric!=null&&Motion.Enabled(settings)&&IsVisible&&WindowState!=WindowState.Minimized&&fullscreen==null&&CurrentPage=="lyrics"){var cross=new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(400));cross.Completed+=(_,_)=>{if(version==imageRequest)LyricsPicturePrevious.Source=null;};LyricsPicture.BeginAnimation(OpacityProperty,cross);}else LyricsPicturePrevious.Source=null;Picture.BeginAnimation(OpacityProperty,null);PicturePrevious.Source=previous;Picture.Source=source;Picture.Opacity=1;NoPicture.Visibility=Visibility.Collapsed;
   ImageName.Text=$"{imageIndex+1} / {pictures.Count} · {Path.GetFileName(path)}";ResizeImage();
   if(previous!=null&&Motion.Enabled(settings)&&IsVisible&&CurrentPage=="detail"&&WindowState!=WindowState.Minimized)
   {
    var fade=new DoubleAnimation(0,1,TimeSpan.FromMilliseconds(400));fade.Completed+=(_,_)=>{if(version==imageRequest)PicturePrevious.Source=null;};Picture.BeginAnimation(OpacityProperty,fade);
   }else PicturePrevious.Source=null;
  }
  catch(Exception ex){if(version==imageRequest){ImageDiscovery.Log(path,ex);LyricsImageCount.Text="图片读取失败";Status.Text="图片存在但无法读取："+ex.Message;Picture.Source=PicturePrevious.Source=LyricsPicture.Source=LyricsPicturePrevious.Source=null;NoPicture.Visibility=Visibility.Visible;ImageName.Text="图片无法显示："+ex.Message;}}
 }
 void PreviousImage(object sender,RoutedEventArgs e)=>BrowsePicture(-1);
 void NextImage(object sender,RoutedEventArgs e)=>BrowsePicture(1);
 internal void BrowsePicture(int delta){if(pictures.Count<2)return;imageIndex=(imageIndex+pictures.Count+delta)%pictures.Count;ImageReady=ShowPicture();}
 void ZoomOut(object sender,RoutedEventArgs e){imageFit=false;imageZoom=Math.Max(0.1,imageZoom/1.25);ResizeImage();}
 void ZoomIn(object sender,RoutedEventArgs e){imageFit=false;imageZoom=Math.Min(5,imageZoom*1.25);ResizeImage();}
 void FitImage(object sender,RoutedEventArgs e){imageFit=true;ResizeImage();}
 void ResizeImage(){if(Picture.Source==null)return;var source=Picture.Source;if(imageFit){Picture.Width=Math.Max(20,PictureScroll.ActualWidth-18);Picture.Height=Math.Max(20,PictureScroll.ActualHeight-18);imageZoom=Math.Min(Picture.Width/source.Width,Picture.Height/source.Height);}else{Picture.Width=source.Width*imageZoom;Picture.Height=source.Height*imageZoom;}PicturePrevious.Width=Picture.Width;PicturePrevious.Height=Picture.Height;}
 void PictureViewportChanged(object sender,SizeChangedEventArgs e){if(imageFit)ResizeImage();}
 void SetCover(object sender,RoutedEventArgs e){if(pictures.Count>0&&currentWork!=null)ChooseCover(pictures[imageIndex]);}
 void PickCover(object sender,RoutedEventArgs e){if(currentWork==null)return;var d=new OpenFileDialog{Title="选择分类封面",Filter="图片|*.jpg;*.jpeg;*.png;*.webp;*.bmp"};if(d.ShowDialog(this)==true)ChooseCover(d.FileName);}
 internal void ChooseCover(string path){if(currentWork==null)return;settings.WorkCovers[currentWork.Path]=path;currentWork.Cover=path;RenderHome();UpdateBackground();Save();}
 internal void UpdateBackground(){Backdrop.Source=null;UpdateVisuals();} // Global background is always the starfield; artwork stays inside its own viewport.
}
