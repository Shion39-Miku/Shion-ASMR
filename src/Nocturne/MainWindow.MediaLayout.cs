using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Nocturne;
public partial class MainWindow
{
 readonly Grid leftMedia=new(),rightMedia=new(),compactMedia=new();string mediaLayoutSignature="";bool buildingMedia;
 internal Task AutoVideoReady {get;private set;}=Task.CompletedTask;
 internal bool CompactMedia=>ActualWidth<1100&&settings.ShowPictures&&pictures.Count>0&&settings.ShowVideo&&video.Image!=null;
 internal void ArrangeMedia()
 {
  if(LyricsWorkspace==null||buildingMedia)return;
  bool picture=settings.ShowPictures&&pictures.Count>0,movie=settings.ShowVideo&&video.Image!=null,compact=CompactMedia;bool crowded=compact||picture&&movie&&settings.PictureOnRight==settings.VideoOnRight;
  var signature=$"{picture}/{movie}/{settings.PictureOnRight}/{settings.VideoOnRight}/{compact}";
  LyricsArtwork.Visibility=picture?Visibility.Visible:Visibility.Collapsed;VideoPane.Visibility=movie?Visibility.Visible:Visibility.Collapsed;
  LyricsPicturesButton.Background=(Brush)new BrushConverter().ConvertFromString(picture?"#B66B527C":"#A0403E5C")!;LyricsVideoButton.Background=(Brush)new BrushConverter().ConvertFromString(movie?"#B65A637F":"#A0403E5C")!;
  if(signature!=mediaLayoutSignature)
  {
   buildingMedia=true;mediaLayoutSignature=signature;
   foreach(var panel in new[]{leftMedia,rightMedia,compactMedia}){panel.Children.Clear();panel.RowDefinitions.Clear();panel.ColumnDefinitions.Clear();}
   LyricsWorkspace.Children.Clear();LyricsWorkspace.RowDefinitions.Clear();
   foreach(var column in LyricsWorkspace.ColumnDefinitions){column.MinWidth=0;column.Width=new GridLength(0);}LyricsColumn.MinWidth=320;LyricsColumn.Width=new GridLength(1,GridUnitType.Star);
   foreach(var panel in new FrameworkElement[]{LyricsArtwork,VideoPane,SubtitlePanel,leftMedia,rightMedia,compactMedia,VideoDivider,LeftMediaDivider,CompactMediaDivider}){Grid.SetColumnSpan(panel,1);Grid.SetRowSpan(panel,1);Grid.SetColumn(panel,0);Grid.SetRow(panel,0);}
   void Add(Grid host,UIElement item,int column,int row=0){Grid.SetColumn(item,column);Grid.SetRow(item,row);host.Children.Add(item);}
   VideoDivider.Visibility=LeftMediaDivider.Visibility=CompactMediaDivider.Visibility=Visibility.Collapsed;
   if(compact)
   {
    LyricsWorkspace.RowDefinitions.Add(new(){Height=new GridLength(Math.Clamp(settings.CompactMediaHeight,80,160))});LyricsWorkspace.RowDefinitions.Add(new(){Height=new GridLength(6)});LyricsWorkspace.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
    compactMedia.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});compactMedia.ColumnDefinitions.Add(new(){Width=new GridLength(8)});compactMedia.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});
    Add(compactMedia,LyricsArtwork,settings.PictureOnRight?2:0);Add(compactMedia,VideoPane,settings.PictureOnRight?0:2);Add(LyricsWorkspace,compactMedia,2);Add(LyricsWorkspace,CompactMediaDivider,2,1);CompactMediaDivider.Visibility=Visibility.Visible;Add(LyricsWorkspace,SubtitlePanel,2,2);
   }
   else
   {
    Add(LyricsWorkspace,SubtitlePanel,2);
    var left=new System.Collections.Generic.List<FrameworkElement>();var right=new System.Collections.Generic.List<FrameworkElement>();
    if(picture)(settings.PictureOnRight?right:left).Add(LyricsArtwork);if(movie)(settings.VideoOnRight?right:left).Add(VideoPane);
    void Side(Grid host,System.Collections.Generic.List<FrameworkElement> items,int column)
    {
     if(items.Count==0)return;for(int i=0;i<items.Count;i++){host.RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});if(i>0)items[i].Margin=new Thickness(0,6,0,0);else items[i].Margin=new Thickness(0);Add(host,items[i],0,i);}Add(LyricsWorkspace,host,column);
    }
    Side(leftMedia,left,0);Side(rightMedia,right,4);
    if(left.Count>0){LeftMediaColumn.MinWidth=180;LeftMediaColumn.Width=new GridLength(settings.LeftMediaShare,GridUnitType.Star);LyricsWorkspace.ColumnDefinitions[1].Width=new GridLength(10);Add(LyricsWorkspace,LeftMediaDivider,1);LeftMediaDivider.Visibility=Visibility.Visible;}
    if(right.Count>0){VideoColumn.MinWidth=180;VideoColumn.Width=new GridLength(settings.RightMediaShare,GridUnitType.Star);VideoDividerColumn.Width=new GridLength(10);Add(LyricsWorkspace,VideoDivider,3);VideoDivider.Visibility=Visibility.Visible;}
   }
   // Invisible panes are detached. The video source is managed separately from its sound.
   VideoPane.Padding=new Thickness(crowded?4:10);VideoImage.Margin=new Thickness(0);VideoViewport.Margin=new Thickness(0,crowded?2:8,0,crowded?2:8);
   if(VideoPane.Child is Grid videoGrid)foreach(var header in videoGrid.Children.OfType<DockPanel>())foreach(var button in header.Children.OfType<Button>()){button.Height=crowded?26:38;button.MinHeight=crowded?26:38;button.Padding=new Thickness(crowded?4:10);if(button.Content is Nocturne.Symbol symbol){symbol.Width=symbol.Height=crowded?14:20;}}
   VideoToggle.Height=VideoToggle.MinHeight=crowded?26:38;VideoToggle.Width=crowded?30:42;VideoToggle.Padding=new Thickness(crowded?4:10);VideoPlayIcon.Width=VideoPlayIcon.Height=crowded?16:20;
   VideoControlsGroup.Visibility=crowded?Visibility.Collapsed:Visibility.Visible;VideoTime.Visibility=crowded?Visibility.Collapsed:Visibility.Visible;VideoSync.Visibility=directVideo||crowded?Visibility.Collapsed:Visibility.Visible;
   buildingMedia=false;
  }
  if(compact&&LyricsWorkspace.ActualHeight>0)LyricsWorkspace.RowDefinitions[0].Height=new GridLength(Math.Min(Math.Clamp(settings.CompactMediaHeight,80,160),Math.Max(80,LyricsWorkspace.ActualHeight-120)));
 }
 void ChromeViewportChanged(object sender,SizeChangedEventArgs e){if(ready)CompactChrome();}
 void CompactChrome()
 {
  var entryWidth=Math.Clamp(Math.Floor((Math.Max(640,PageContentHost.ActualWidth)-48)/3),195,300);HomeAdd.Width=HomeListen.Width=HomeVideo.Width=entryWidth;WelcomeTitle.FontSize=MainLayout.ActualWidth<900?34:40;
  bool compact=ActualHeight<740;PlayerBarRow.Height=new GridLength(compact?150:162);PlayerSurface.Padding=new Thickness(20,compact?8:14,20,compact?8:14);PageContentHost.Margin=compact?new Thickness(4,10,4,10):new Thickness(4,20,4,16);
  BrowserSidebar.Width=new GridLength(MainLayout.ActualWidth<950?232:252);
  PlayButton.Height=compact?44:50;PlayButton.Width=compact?50:54;PreviousButton.Height=NextButton.Height=PreviousButton.Width=NextButton.Width=compact?40:46;
 }
 void CompanionBack(object sender,RoutedEventArgs e){if(!directVideo&&!companionSync){video.Seek(video.Position-5);UpdateVideoControls();}}
 void CompanionForward(object sender,RoutedEventArgs e){if(!directVideo&&!companionSync){video.Seek(video.Position+5);UpdateVideoControls();}}
 void MediaWorkspaceSizeChanged(object sender,SizeChangedEventArgs e)=>ArrangeMedia();
 void MediaWidthChanged(object sender,DragCompletedEventArgs e)
 {
  var lyric=Math.Max(1,LyricsColumn.ActualWidth);settings.LeftMediaShare=Math.Clamp(LeftMediaColumn.ActualWidth/lyric,.3,1.6);settings.RightMediaShare=Math.Clamp(VideoColumn.ActualWidth/lyric,.3,1.6);QueueSave();
 }
 void CompactHeightChanged(object sender,DragCompletedEventArgs e){settings.CompactMediaHeight=Math.Clamp(LyricsWorkspace.RowDefinitions[0].ActualHeight,80,160);QueueSave();}
 void TogglePicturesPanel(object sender,RoutedEventArgs e){settings.ShowPictures=!settings.ShowPictures;ApplyLiveSettings();}
 void ToggleVideoPanel(object sender,RoutedEventArgs e){if(video.Path==null){ChooseVideo(sender,e);return;}settings.ShowVideo=!settings.ShowVideo;ApplyLiveSettings();}
 void ToggleVideoSyncMenu(object sender,RoutedEventArgs e){if(sender is MenuItem menu){VideoSync.IsChecked=menu.IsChecked;menu.IsChecked=VideoSync.IsChecked==true;}}
 void VideoMenuOpened(object sender,RoutedEventArgs e){if(sender is ContextMenu menu)foreach(var item in menu.Items.OfType<MenuItem>())if(item.Header is string label&&label.StartsWith("配套视频",StringComparison.Ordinal)){item.IsEnabled=!directVideo&&video.Path!=null;if(item.IsCheckable)item.IsChecked=companionSync;}}
 internal void ApplyLiveSettings()
 {
  CompactChrome();ApplyTypography();overlay.Apply();Wave.Height=ActualHeight<740?20:Math.Clamp(settings.WaveHeight,16,48);WavePanel.Visibility=settings.Visualizer&&!directVideo&&currentTrack!=null?Visibility.Visible:Visibility.Collapsed;ArrangeMedia();UpdateVisuals();UpdateTimer();QueueSave();
 }
 async Task OpenMatchingVideo(string audio)
 {
  if(!settings.ShowVideo)return;var stem=Path.Combine(Path.GetDirectoryName(audio)!,Path.GetFileNameWithoutExtension(audio));
  var match=await Task.Run(()=>new[]{".mp4",".m4v",".wmv",".mov",".avi",".mkv"}.Select(ext=>stem+ext).FirstOrDefault(File.Exists));
  if(currentTrack==audio&&match!=null&&!closing)await OpenVideo(match,false,true,null,false);
 }
}
