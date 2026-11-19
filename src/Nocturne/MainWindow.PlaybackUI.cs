using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

using System.Windows.Threading;

namespace Nocturne;
public partial class MainWindow
{
 readonly DispatcherTimer feedbackTimer=new(){Interval=TimeSpan.FromSeconds(4)};
 bool scanRunning;bool? overlayButtonVisible;
 string subtitleHint="未找到匹配字幕";
 void InitializePlaybackUI()
 {
  var descriptor=DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty,typeof(TextBlock));
  descriptor.AddValueChanged(Status,FeedbackChanged);
  feedbackTimer.Tick+=(_,_)=>{feedbackTimer.Stop();if(!scanRunning)StatusSurface.Visibility=Visibility.Collapsed;};
  Closing+=(_,_)=>{feedbackTimer.Stop();descriptor.RemoveValueChanged(Status,FeedbackChanged);};
  UpdateFooterSubtitle();UpdateOverlayButtons();
 }
 void FeedbackChanged(object? sender,EventArgs e)
 {
  if(closing){feedbackTimer.Stop();return;}
  feedbackTimer.Stop();StatusSurface.Visibility=string.IsNullOrWhiteSpace(Status.Text)?Visibility.Collapsed:Visibility.Visible;
  if(StatusSurface.Visibility==Visibility.Visible&&!scanRunning)feedbackTimer.Start();
 }
 void UpdateFooterSubtitle()
 {
  var full=currentTrack==null?"尚未播放":document.Cues.Count==0?subtitleHint:lastSubtitle;
  var compact=full.Replace("\r","").Replace("\n","  ·  ");
  if(FooterSubtitle.Text!=compact){FooterSubtitle.Text=compact;FooterSubtitle.ToolTip=full;}
 }
 void UpdateOverlayButtons()
 {
  var visible=overlay.IsVisible;
  if(tray?.ContextMenuStrip is {Items.Count:>=5} menu){menu.Items[2].Text=visible?(overlay.Editing?"桌面字幕 / 控制面板":"桌面字幕 / 解锁编辑"):"桌面字幕 / 打开编辑";menu.Items[3].Enabled=menu.Items[4].Enabled=visible;if(menu.Items[3] is System.Windows.Forms.ToolStripMenuItem item)item.Checked=visible&&!overlay.Editing;}
  if(overlayButtonVisible==visible)return;overlayButtonVisible=visible;
  DesktopLyricsButton.IsChecked=visible;
  DesktopLyricsButton.ToolTip=visible?"关闭桌面字幕":"开启桌面字幕并进入编辑";
  System.Windows.Automation.AutomationProperties.SetItemStatus(DesktopLyricsButton,visible?"已开启":"已关闭");
 }
}
