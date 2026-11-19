using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace Nocturne;
// Only this 36-DIP tool window receives input while lyrics stay WS_EX_TRANSPARENT.
sealed class UnlockGlyph : Window,IDisposable
{
 internal Button Button{get;}bool fading;
 public UnlockGlyph(Action edit)
 {
  Width=Height=36;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;Focusable=false;Title="月咲 Shion · 解锁字幕";
  Button=new Button{Content=new Symbol{Kind="Lock",Width=17,Height=17},ToolTip="解锁 / 编辑桌面字幕",Padding=new Thickness(7),Margin=new Thickness(1),MinHeight=32,Background=new SolidColorBrush(Color.FromArgb(225,45,38,59))};System.Windows.Automation.AutomationProperties.SetName(Button,"解锁桌面字幕");Content=Button;Button.Click+=(_,e)=>{e.Handled=true;Hide();edit();};
  SourceInitialized+=(_,_)=>{var h=new WindowInteropHelper(this).Handle;OverlayWindow.SetWindowLong(h,-20,OverlayWindow.GetWindowLong(h,-20)|0x08000080);HwndSource.FromHwnd(h)?.AddHook(Hook);};
 }
 IntPtr Hook(IntPtr h,int message,IntPtr wp,IntPtr lp,ref bool handled){if(message==0x21){handled=true;return new IntPtr(3);}if(message==0x84&&Opacity<.1){handled=true;return new IntPtr(-1);}return IntPtr.Zero;}
 internal void Reveal(bool topmost,double left,double top){if(IsVisible&&!fading&&Left==left&&Top==top)return;Left=left;Top=top;if(fading){BeginAnimation(OpacityProperty,null);fading=false;}Opacity=1;if(!IsVisible)Show();OverlayWindow.SetWindowPos(new WindowInteropHelper(this).Handle,topmost?new IntPtr(-1):new IntPtr(-2),0,0,0,0,0x53);}
 internal void Conceal(bool instant){if(!IsVisible)return;if(instant){BeginAnimation(OpacityProperty,null);Hide();fading=false;return;}if(fading)return;fading=true;var fade=new DoubleAnimation(0,TimeSpan.FromMilliseconds(260));fade.Completed+=(_,_)=>{if(fading){Hide();fading=false;BeginAnimation(OpacityProperty,null);}};BeginAnimation(OpacityProperty,fade);}
 public void Dispose(){fading=false;BeginAnimation(OpacityProperty,null);Close();}
}
