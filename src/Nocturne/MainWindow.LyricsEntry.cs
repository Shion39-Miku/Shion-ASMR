using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace Nocturne;
public partial class MainWindow
{
 bool lyricEntryPending,lyricEntryQueued;int lyricEntryVersion;
 void CancelLyricsEntry(){SubtitleLines.LayoutUpdated-=LyricsEntryLayoutReady;lyricEntryPending=false;lyricEntryQueued=false;lyricEntryVersion++;followRequest++;}
 void BeginLyricsEntry()
 {
  CancelLyricsEntry();SmoothScroll.Stop(SubtitleLines);SubtitleLines.LayoutUpdated-=LyricsEntryLayoutReady;SubtitleLines.LayoutUpdated+=LyricsEntryLayoutReady;Preference().FollowLyrics=true;lyricEntryPending=true;
  UpdateSubtitle(true);
 }
 void LyricsEntryLayoutReady(object? sender,System.EventArgs e){if(lyricEntryPending)QueueLyricsEntryCenter();else SubtitleLines.LayoutUpdated-=LyricsEntryLayoutReady;}
 void QueueLyricsEntryCenter()
 {
  if(!lyricEntryPending||lyricEntryQueued||CurrentPage!="lyrics"||subtitleTrack!=currentTrack||subtitleLoading||PlaybackPath!=currentTrack||!IsVisible||WindowState==WindowState.Minimized)return;
  lyricEntryQueued=true;var version=lyricEntryVersion;
  // Wait for template/font measurement. Read the clock here, never at button-click time.
  Dispatcher.BeginInvoke(()=>
  {
   if(version!=lyricEntryVersion)return;lyricEntryQueued=false;
   if(closing||!lyricEntryPending||CurrentPage!="lyrics"||subtitleTrack!=currentTrack||subtitleLoading||!Preference().FollowLyrics)return;
   SubtitleLines.UpdateLayout();var active=ActivePlaybackCues();
   if(active.Count>0)
   {
    allowLyricBring=true;
    try{if(!SmoothScroll.CenterNow(SubtitleLines,document.Cues[active[0]]))return;}finally{allowLyricBring=false;}
    RefreshLyricHighlights(active);
   }
   lyricEntryPending=false;SubtitleLines.LayoutUpdated-=LyricsEntryLayoutReady;UpdateSubtitle();
  },DispatcherPriority.ContextIdle);
 }
}
