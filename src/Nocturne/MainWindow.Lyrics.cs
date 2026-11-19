using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;

namespace Nocturne;
public partial class MainWindow
{
 void LyricsViewportChanged(object sender,SizeChangedEventArgs e){if(ready&&Preference().FollowLyrics)UpdateSubtitle(true);}
 string lastSubtitle="";List<int> lastIndices=new();int followRequest;bool allowLyricBring;internal string CurrentSubtitle=>lastSubtitle;
 TrackPreference Preference(){if(currentTrack==null)return new();if(!settings.Tracks.TryGetValue(currentTrack,out var p))settings.Tracks[currentTrack]=p=new();return p;}
 void ApplyTypography()
 {
  SubtitleLines.FontSize=settings.FontSize;SubtitleLines.FontFamily=FontBook.For(settings);SubtitleLines.FontWeight=FontWeight.FromOpenTypeWeight(settings.LyricWeight);SubtitleLines.Tag=settings.FontSize*settings.LyricLineSpacing;
  // A small text-only shadow remains bounded to the lyric viewport; no blurred background layers.
  SubtitleLines.Effect=null;if(ready&&Preference().FollowLyrics)UpdateSubtitle(true);
 }
 internal System.Threading.Tasks.Task SubtitleReady {get;private set;}=System.Threading.Tasks.Task.CompletedTask;
 System.Threading.CancellationTokenSource? subtitleCancel;int subtitleRequest;string? subtitleTrack;bool subtitleLoading;
 void LoadSubtitleChoices()=>SubtitleReady=ReloadSubtitle();
 internal System.Threading.Tasks.Task LoadSubtitleChoicesForTest(){LoadSubtitleChoices();return SubtitleReady;}
 async System.Threading.Tasks.Task ReloadSubtitle()
 {
  var request=++subtitleRequest;subtitleCancel?.Cancel();subtitleCancel?.Dispose();var cancel=subtitleCancel=new();var token=cancel.Token;
  var track=currentTrack;if(track==null){subtitleLoading=false;ClearSubtitles();return;}var p=Preference();var encoding=p.Encoding;var saved=p.Subtitle;subtitleLoading=true;
  if(subtitleTrack!=track){subtitleTrack=track;ClearSubtitles("正在查找字幕…");NoLyrics.Text="正在检索匹配字幕…";}
  var catalog=works.ToArray();var owner=catalog.FirstOrDefault(w=>w.Groups.Any(g=>g.Path.Equals(Path.GetDirectoryName(track),StringComparison.OrdinalIgnoreCase)));
  bool Current()=>!closing&&!token.IsCancellationRequested&&request==subtitleRequest&&track==currentTrack;
  try
  {
   var candidates=await System.Threading.Tasks.Task.Run(()=>SubtitleMatcher.Candidates(track,owner,library,saved,catalog),token);if(!Current())return;
   var chosen=candidates.FirstOrDefault();
   if(!File.Exists(saved)&&chosen!=null&&Path.GetDirectoryName(chosen)!=Path.GetDirectoryName(track))
   {
    var equal=candidates.Where(x=>Path.GetExtension(x).Equals(Path.GetExtension(chosen),StringComparison.OrdinalIgnoreCase)).ToList();
    if(equal.Count>1&&!testing)
    {
     var dialog=DialogShell.Create("选择匹配字幕",620,320);dialog.Owner=this;var panel=new StackPanel{Margin=new Thickness(24)};dialog.Content=panel;
     panel.Children.Add(new TextBlock{Text="找到多个同名字幕，请选择一次：",FontSize=17});var choices=new ListBox{ItemsSource=equal,SelectedIndex=0,Height=160};panel.Children.Add(choices);
     var use=new Button{Content="使用并记住"};panel.Children.Add(use);use.Click+=(_,_)=>{chosen=choices.SelectedItem as string;dialog.Close();};dialog.ShowDialog();if(!Current())return;
    }
   }
   var loaded=chosen==null?new SubtitleDocument():await System.Threading.Tasks.Task.Run(()=>SubtitleDocument.Load(chosen,encoding),token);if(!Current())return;
   // The document is authoritative. ComboBox is only a view of its selection, never the source of auto-load state.
   var same=document.Cues.SequenceEqual(loaded.Cues);if(!same)document=loaded;subtitleLoading=false;subtitleHint=chosen==null?"未找到匹配字幕":document.Cues.Count==0?"字幕没有有效台词":"";p.Subtitle=chosen;loadingSubtitle=true;
   var list=candidates.Select(x=>new FileItem(x)).ToList();list.Insert(0,new FileItem("不加载字幕"));SubtitleChoice.ItemsSource=list;SubtitleChoice.SelectedItem=chosen==null?list[0]:list.First(x=>x.Path==chosen);
   foreach(ComboBoxItem item in EncodingChoice.Items)if((string)item.Content==encoding)EncodingChoice.SelectedItem=item;loadingSubtitle=false;
   if(!same||SubtitleLines.ItemsSource==null)ApplyDocument();else UpdateSubtitle(true);NoLyrics.Text=chosen==null?"未找到匹配字幕\n可在下方手动选择 LRC 或 VTT。":document.Cues.Count==0?"字幕已读取，但没有有效时间标签。":"";QueueSave();
  }
  catch(OperationCanceledException){}
  catch(Exception ex){if(Current()){loadingSubtitle=false;subtitleLoading=false;ClearSubtitles("字幕读取失败");NoLyrics.Text="字幕读取失败，请检查文件或选择编码。";Status.Text="字幕读取失败："+ex.Message;QueueLyricsEntryCenter();}}
 }
 void ApplyDocument(){lastClickedCue=null;lastLyricTimestamp=0;lastIndices.Clear();lastSubtitle="";highlighting=true;SubtitleLines.ItemsSource=document.Cues.Prepend(new Cue(0,0,""){Spacer=true}).Append(new Cue(0,0,""){Spacer=true}).ToArray();highlighting=false;NoLyrics.Visibility=document.Cues.Count==0?Visibility.Visible:Visibility.Collapsed;OffsetText.Text=$"{Preference().Offset:+0.0;-0.0;0.0} s";UpdateSubtitle(true);overlay.SetText(lastSubtitle);}
 void SubtitleChanged(object sender,SelectionChangedEventArgs e)
 {
  if(!ready||loadingSubtitle)return;
  if(SubtitleChoice.SelectedItem is FileItem selected&&File.Exists(selected.Path)){Preference().Subtitle=selected.Path;LoadSubtitleChoices();}
  else{++subtitleRequest;subtitleCancel?.Cancel();subtitleLoading=false;Preference().Subtitle=null;ClearSubtitles("未启用字幕");NoLyrics.Text="字幕已关闭；下次播放会重新匹配。";QueueLyricsEntryCenter();}QueueSave();
 }
 void EncodingChanged(object sender,SelectionChangedEventArgs e){if(ready&&!loadingSubtitle&&EncodingChoice.SelectedItem is ComboBoxItem item){Preference().Encoding=(string)item.Content;LoadSubtitleChoices();QueueSave();}}
 internal void LoadSubtitle()=>LoadSubtitleChoices();
 void PickSubtitle(object sender,RoutedEventArgs e){if(currentTrack==null){Status.Text="请先打开一条音轨。";return;}var d=new OpenFileDialog{Filter="字幕文件|*.lrc;*.vtt",Title="选择当前音轨的字幕"};if(d.ShowDialog(this)==true){Preference().Subtitle=d.FileName;LoadSubtitleChoices();QueueSave();}}
 internal void SetOffset(double value){Preference().Offset=Math.Round(Math.Clamp(value,-600,600),2);OffsetText.Text=$"{Preference().Offset:+0.0;-0.0;0.0} s";UpdateSubtitle();QueueSave();}
 void OffsetMinus(object sender,RoutedEventArgs e)=>SetOffset(Preference().Offset-0.1);
 void OffsetPlus(object sender,RoutedEventArgs e)=>SetOffset(Preference().Offset+0.1);
 List<int> ActivePlaybackCues()=>document.Active(PlaybackPosition-Preference().Offset,holdPrevious:!directVideo);
 void UpdateSubtitle(bool forceFollow=false)
 {
  var active=ActivePlaybackCues();var value=string.Join("\n",active.Select(i=>document.Cues[i].Text));bool changed=!active.SequenceEqual(lastIndices);
  if(changed||forceFollow)lastIndices=active;
  if((changed||forceFollow)&&CurrentPage=="lyrics"&&fullscreen==null&&WindowState!=WindowState.Minimized)RefreshLyricHighlights(active);
  if(lyricEntryPending)QueueLyricsEntryCenter();
  if(!lyricEntryPending&&(changed||forceFollow)&&active.Count>0&&(Preference().FollowLyrics||forceFollow)&&CurrentPage=="lyrics"&&(FrontInteraction||overlay.IsActive)&&WindowState!=WindowState.Minimized&&fullscreen==null)
  {
   var request=++followRequest;var track=currentTrack;var cue=document.Cues[active[0]];
   Dispatcher.BeginInvoke(()=>{if(request==followRequest&&Preference().FollowLyrics&&track==currentTrack&&CurrentPage=="lyrics"&&WindowState!=WindowState.Minimized){allowLyricBring=true;try{SmoothScroll.Follow(SubtitleLines,cue,Motion.Enabled(settings));}finally{allowLyricBring=false;}}},System.Windows.Threading.DispatcherPriority.Loaded);
  }
  FollowButton.Content=Preference().FollowLyrics?"正在跟随字幕":"回到当前台词";
  if(value!=lastSubtitle){lastSubtitle=value;overlay.SetText(value);}UpdateFooterSubtitle();UpdateVideoSubtitle();
 }
 readonly System.Runtime.CompilerServices.ConditionalWeakTable<ListBoxItem,object> lyricSizes=new();
 void RefreshLyricHighlights(System.Collections.Generic.List<int> active)
 {
  var cues=active.Select(i=>document.Cues[i]).ToHashSet();
  void Visit(DependencyObject node){for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++){var child=VisualTreeHelper.GetChild(node,i);if(child is ListBoxItem row){if(!lyricSizes.TryGetValue(row,out _)){lyricSizes.Add(row,new object());row.SizeChanged+=(_,_)=>{if(CurrentPage=="lyrics"&&Preference().FollowLyrics&&row.DataContext is Cue current&&!current.Spacer&&ActivePlaybackCues().Any(i=>ReferenceEquals(document.Cues[i],current)))UpdateSubtitle(true);};}var on=row.DataContext is Cue cue&&cues.Contains(cue);row.Tag=on?"active":null;row.FontWeight=FontWeight.FromOpenTypeWeight(on?Math.Min(700,settings.LyricWeight+100):settings.LyricWeight);if(VisualTreeHelper.GetChildrenCount(row)>0)ApplyTextShadow(row);}else Visit(child);}}Visit(SubtitleLines);
 }
 static readonly DropShadowEffect lyricTextShadow=CreateLyricShadow();
 static DropShadowEffect CreateLyricShadow(){var effect=new DropShadowEffect{Color=Colors.Black,BlurRadius=1.5,ShadowDepth=1,Opacity=.4};effect.Freeze();return effect;}
 void ApplyTextShadow(DependencyObject parent){for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is TextBlock text){text.FontSize=settings.FontSize;text.FontFamily=SubtitleLines.FontFamily;text.LineHeight=settings.FontSize*settings.LyricLineSpacing;text.Effect=settings.LyricShadow?lyricTextShadow:null;}else ApplyTextShadow(child);}}
 void SubtitleLineSelected(object sender,SelectionChangedEventArgs e){if(!highlighting&&SubtitleLines.SelectedItem is Cue cue){SetLyricsFollow(false);}}
 internal void SetLyricsFollow(bool follow,bool stopAnimation=true){followRequest++;Preference().FollowLyrics=follow;if(follow)UpdateSubtitle(true);else{CancelLyricsEntry();if(stopAnimation)SmoothScroll.Stop(SubtitleLines);FollowButton.Content="回到当前台词";}QueueSave();}
 void BrowseLyrics(object sender,MouseWheelEventArgs e){CancelLyricClick();SetLyricsFollow(false,false);}
 void BrowseLyricsMouse(object sender,MouseButtonEventArgs e){if(!highlighting)SetLyricsFollow(false);}
 void LyricBringIntoView(object sender,RequestBringIntoViewEventArgs e){if(!allowLyricBring)e.Handled=true;}
 void BrowseLyricsKey(object sender,KeyEventArgs e){if(e.Key is Key.PageDown or Key.PageUp or Key.Up or Key.Down or Key.Home or Key.End)SetLyricsFollow(false);}
 void ResumeFollow(object sender,RoutedEventArgs e)=>SetLyricsFollow(true);
 void OpenLyrics(object sender,RoutedEventArgs e){if(currentTrack==null){Status.Text="请先选择音轨，再打开字幕。";return;}Preference().LyricsOpen=true;Navigate("lyrics");UpdateSubtitle(true);QueueSave();}
 async void CloseLyrics(object sender,RoutedEventArgs e){Preference().LyricsOpen=false;await ReturnBrowse();QueueSave();}
 internal void OpenLyricsForTest()=>OpenLyrics(this,new RoutedEventArgs());
 internal void ApplyFontForTest(bool art){settings.ArtLyrics=art;ApplyTypography();}
 internal void PlayForTest(){PlayMaster();UpdateTimer();}
}
