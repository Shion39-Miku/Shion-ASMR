using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Windows.Media.Animation;
namespace Nocturne;
public sealed class OverlayWindow : Window
{
 [DllImport("user32.dll")]internal static extern int GetWindowLong(IntPtr h,int i);
 [DllImport("user32.dll")]internal static extern int SetWindowLong(IntPtr h,int i,int v);
 [DllImport("user32.dll")]internal static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int cx,int cy,uint flags);
 [StructLayout(LayoutKind.Sequential)]struct CursorPoint{public int X,Y;}
 [DllImport("user32.dll")]static extern bool GetCursorPos(out CursorPoint point);
 [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")]static extern bool IsWindow(IntPtr h);
 const int TransparentStyle=0x20,NoActivateStyle=0x08000000,ToolWindowStyle=0x80;
 const double ResizeEdge=8;
 static readonly Brush resizeSurface=new SolidColorBrush(Color.FromArgb(1,0,0,0));
 readonly Settings settings;readonly Border panel,toolbar;readonly TextBlock text,dragHandle,sizeText;readonly Grid layout;readonly ComboBox fonts,colors;readonly Slider size;
 readonly DispatcherTimer hover=new(){Interval=TimeSpan.FromMilliseconds(90)},saveDelay=new(){Interval=TimeSpan.FromMilliseconds(350)};
 readonly UnlockGlyph unlock;DateTime awayAt;bool closing,applying;bool? renderedEditing;IntPtr previousFocus;
 bool sizingOrMoving,resizingHeight;
 double preferredHeight;
 public event Action<string>? Command;public event Action? ConfigurationChanged;
 public bool ClickThrough{get;private set;}public bool Editing{get;private set;}internal Button PlayTool{get;}internal double ToolbarOpacity=>toolbar.Opacity;
 public OverlayWindow(Settings s)
 {
  settings=s;Title="月咲 Shion · 桌面字幕";WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;Foreground=new SolidColorBrush(Color.FromRgb(233,234,244));FontFamily=new FontFamily("Microsoft YaHei UI, Yu Gothic UI, Segoe UI");UseLayoutRounding=true;ShowInTaskbar=false;ShowActivated=false;Focusable=false;
  preferredHeight=s.OverlayHeight;
  Width=s.OverlayWidth;Height=preferredHeight;MinWidth=280;MinHeight=70;Left=s.OverlayLeft;Top=s.OverlayTop;
  layout=new Grid{VerticalAlignment=VerticalAlignment.Center};layout.RowDefinitions.Add(new(){Height=GridLength.Auto});layout.RowDefinitions.Add(new(){Height=GridLength.Auto});
  dragHandle=new TextBlock{Text="↔",ToolTip="拖动字幕",Width=34,Height=32,TextAlignment=TextAlignment.Center,FontSize=20,Foreground=new SolidColorBrush(Color.FromRgb(210,210,220)),Margin=new Thickness(2),Cursor=Cursors.SizeAll};
  text=new TextBlock{Text="",TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(18,12,18,0)};Grid.SetRow(text,0);layout.Children.Add(text);
  var controls=new StackPanel();var tools=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};controls.Children.Add(tools);tools.Children.Add(dragHandle);
  Button Tool(string kind,string hint,string command){var b=new Button{Content=new Symbol{Kind=kind,Width=17,Height=17},ToolTip=hint,Width=34,Height=32,MinHeight=30,Padding=new Thickness(6),Margin=new Thickness(2),Background=new SolidColorBrush(Color.FromArgb(80,90,81,107))};System.Windows.Automation.AutomationProperties.SetName(b,hint);b.Click+=(_,e)=>{e.Handled=true;if(command=="lock"){EndEditing();Persist();}else if(command=="close"){EndEditing();Hide();Persist();}else Command?.Invoke(command);};tools.Children.Add(b);return b;}
  PlayTool=Tool("Play","播放 / 暂停","play");Tool("Previous","上一首","previous");Tool("Next","下一首","next");Tool("Minus","字幕提前 0.1 秒","earlier");Tool("Plus","字幕延后 0.1 秒","later");Tool("Settings","更多字体与样式设置","settings");Tool("Lock","锁定并穿透 · 悬停小锁可解锁","lock");Tool("Close","关闭桌面字幕","close");
  var styles=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Center,Margin=new Thickness(6,2,6,4)};controls.Children.Add(styles);
  StackPanel? styleGroup=null;void Label(string value){styleGroup=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(3,2,3,2)};styles.Children.Add(styleGroup);styleGroup.Children.Add(new TextBlock{Text=value,FontSize=11,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(4,0,3,0)});}
  Label("字体");fonts=new ComboBox{Width=112,ItemsSource=new[]{"艺术文楷","清晰默认","当前自定义"}};styleGroup!.Children.Add(fonts);
  Label("字号");size=new Slider{Width=90,Minimum=16,Maximum=64,ToolTip="桌面字幕字号"};styleGroup!.Children.Add(size);sizeText=new TextBlock{Width=28,FontSize=11,VerticalAlignment=VerticalAlignment.Center};styleGroup!.Children.Add(sizeText);
  Label("颜色");colors=new ComboBox{Width=78,ItemsSource=new[]{"柔粉","白色","暖黄","浅青","自定义"}};styleGroup!.Children.Add(colors);
  toolbar=new Border{Child=controls,Margin=new Thickness(6,10,6,10),CornerRadius=new CornerRadius(14)};Grid.SetRow(toolbar,1);layout.Children.Add(toolbar);
  text.SizeChanged+=(_,_)=>QueueToolAlignment();
  panel=new Border{Padding=new Thickness(ResizeEdge),Child=layout,CornerRadius=new CornerRadius(8),Background=Brushes.Transparent};Content=panel;unlock=new UnlockGlyph(()=>RestoreEditing());
  fonts.SelectionChanged+=(_,_)=>{if(applying)return;settings.OverlayArt=fonts.SelectedIndex==0;if(fonts.SelectedIndex==1)settings.OverlayFontFamily="Microsoft YaHei UI, Yu Gothic UI, Microsoft JhengHei UI, Segoe UI";Apply();Changed();};
  size.ValueChanged+=(_,e)=>{if(applying)return;settings.OverlayFont=e.NewValue;Apply();Changed();};
  colors.SelectionChanged+=(_,_)=>{if(applying)return;var palette=new[]{"#FFB2D9","#FFFFFF","#FFE2AB","#B9E6E2"};if(colors.SelectedIndex is >=0 and <4)settings.OverlayColor=palette[colors.SelectedIndex];Apply();Changed();};
  hover.Tick+=(_,_)=>PollHover();saveDelay.Tick+=(_,_)=>{saveDelay.Stop();Persist();};SizeChanged+=(_,_)=>{if(Editing){FitEditor();Save();Changed();}};LocationChanged+=(_,_)=>{if(Editing)Save();else PositionUnlock();};
  IsVisibleChanged+=(_,_)=>{if(IsVisible&&!Editing)hover.Start();else{hover.Stop();unlock.Conceal(true);}};
  SourceInitialized+=(_,_)=>{HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(WindowMessage);ClampToScreens(this);Apply();SetClickThrough(!Editing);};
  Closing+=(_,e)=>{if(!closing){e.Cancel=true;EndEditing();Hide();Persist();}};Apply();
 }
 bool alignmentQueued;
 void QueueToolAlignment(){if(alignmentQueued||closing)return;alignmentQueued=true;Dispatcher.BeginInvoke(()=>{alignmentQueued=false;if(!IsVisible||closing)return;var r=GlyphBounds;if(r.IsEmpty)return;var top=text.TranslatePoint(new Point(),this).Y;var gap=r.Bottom-top-text.ActualHeight+10;if(Math.Abs(toolbar.Margin.Top-gap)>.5)toolbar.Margin=new Thickness(6,gap,6,10);PositionUnlock();},DispatcherPriority.Loaded);}
 void Changed(){saveDelay.Stop();saveDelay.Start();}
 void Persist(){Save();ConfigurationChanged?.Invoke();}
 IntPtr WindowMessage(IntPtr h,int message,IntPtr wp,IntPtr lp,ref bool handled)
 {
  if(Editing)
  {
   if(message==0x231){sizingOrMoving=true;resizingHeight=false;}
   if(message==0x214&&wp.ToInt32() is >=3 and <=8)resizingHeight=true;
   if(message==0x232)
   {
    sizingOrMoving=false;
    if(resizingHeight)preferredHeight=ActualHeight;
    FitEditor();
    Persist();
   }
  }
  if(!Editing&&message==0x21){handled=true;return new IntPtr(3);}
  if(!Editing&&message==0x84){handled=true;return new IntPtr(-1);}
  if(Editing&&message==0x84)
  {
   long v=lp.ToInt64();var screen=new Point((short)(v&0xffff),(short)((v>>16)&0xffff));var p=PointFromScreen(screen);
   var hit=ResizeHit(p);
   if(hit!=0){handled=true;return new IntPtr(hit);}
   var target=VisualTreeHelper.HitTest(panel,panel.PointFromScreen(screen))?.VisualHit;
   if(target!=null)
   {
    // Keep native caption dragging on the plate and leave interactive controls to WPF.
    for(var element=target;element!=null&&element!=panel;element=VisualTreeHelper.GetParent(element))
    {
     if(element is Control){handled=true;return new IntPtr(1);}
    }
    handled=true;return new IntPtr(2);
   }
   handled=true;return new IntPtr(1);
  }
  return IntPtr.Zero;
 }
 int ResizeHit(Point p)
 {
  bool left=p.X<ResizeEdge,right=p.X>=ActualWidth-ResizeEdge;
  if(p.Y<ResizeEdge)return left?13:right?14:12;
  if(p.Y>=ActualHeight-ResizeEdge)return left?16:right?17:15;
  return left?10:right?11:0;
 }
 public void Apply()
 {
  applying=true;try{Topmost=settings.OverlayTopmost;Opacity=settings.OverlayOpacity;text.FontSize=settings.OverlayFont;text.FontFamily=settings.OverlayArt?FontBook.ArtWithFallback:new FontFamily(settings.OverlayFontFamily);text.FontWeight=FontWeight.FromOpenTypeWeight(settings.OverlayWeight);text.LineHeight=settings.OverlayFont*settings.OverlayLineSpacing;text.Foreground=(Brush)new BrushConverter().ConvertFromString(settings.OverlayColor)!;text.Effect=settings.OverlayShadow?new DropShadowEffect{Color=Colors.Black,BlurRadius=1.4,ShadowDepth=1,Opacity=.4}:null;
   Background=Editing?resizeSurface:Brushes.Transparent;
   panel.Background=Editing?new SolidColorBrush(Color.FromArgb(170,0,0,0)):Brushes.Transparent;panel.BorderThickness=new Thickness(0);dragHandle.Visibility=Editing?Visibility.Visible:Visibility.Hidden;UpdateToolbarVisibility();toolbar.IsHitTestVisible=Editing;ResizeMode=Editing?ResizeMode.CanResize:ResizeMode.NoResize;MinWidth=Editing?380:280;MinHeight=Editing?210:70;if(Editing)FitEditor();
   fonts.SelectedIndex=settings.OverlayArt?0:settings.OverlayFontFamily.StartsWith("Microsoft YaHei UI")?1:2;size.Value=settings.OverlayFont;sizeText.Text=settings.OverlayFont.ToString("0");colors.SelectedIndex=Array.IndexOf(new[]{"#FFB2D9","#FFFFFF","#FFE2AB","#B9E6E2"},settings.OverlayColor);if(colors.SelectedIndex<0)colors.SelectedIndex=4;
  }finally{applying=false;QueueToolAlignment();}
 }
 void UpdateToolbarVisibility()
 {
  if(renderedEditing==Editing)return;renderedEditing=Editing;var from=toolbar.Opacity;toolbar.BeginAnimation(OpacityProperty,null);toolbar.Opacity=Editing?1:0;
  if(!IsVisible||settings.ReduceMotion){toolbar.Visibility=Editing?Visibility.Visible:Visibility.Hidden;return;}
  toolbar.Visibility=Visibility.Visible;var target=Editing;var fade=new DoubleAnimation(from,Editing?1:0,TimeSpan.FromMilliseconds(260)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseInOut}};
  fade.Completed+=(_,_)=>{if(Editing==target&&!Editing)toolbar.Visibility=Visibility.Hidden;};toolbar.BeginAnimation(OpacityProperty,fade);
 }
 bool fitting;
 void FitEditor()
 {
  if(fitting||!Editing)return;fitting=true;
  try
  {
   var width=Math.Max(1,Width-panel.Padding.Left-panel.Padding.Right);
   var measure=new Size(width,double.PositiveInfinity);
   toolbar.Measure(measure);
   text.MaxHeight=double.PositiveInfinity;text.Measure(measure);
   var work=ScreenWorkArea(this);
   var chrome=panel.Padding.Top+panel.Padding.Bottom+toolbar.DesiredSize.Height-toolbar.Margin.Top+10;
   var textHeight=Math.Max(text.DesiredSize.Height,text.LineHeight*2+text.Margin.Top);
   var minimum=Math.Ceiling(Math.Min(work.Height,Math.Max(210,chrome+textHeight)));
   if(MinHeight!=minimum)MinHeight=minimum;
   if(!sizingOrMoving)
   {
    Height=Math.Min(work.Height,Math.Max(minimum,preferredHeight));
    if(IsVisible)ClampToScreens(this);
   }
   text.MaxHeight=Math.Max(40,Math.Max(Height,minimum)-chrome);
  }finally{fitting=false;}
 }
 string glyphCacheKey="";Rect glyphCache;
 internal Rect GlyphBounds
 {
  get
  {
   if(string.IsNullOrWhiteSpace(text.Text)||text.ActualWidth<1)return Rect.Empty;
   var origin=text.TranslatePoint(new Point(),this);var key=$"{text.Text}|{text.FontSize}|{text.FontFamily.Source}|{text.FontWeight}|{text.LineHeight}|{text.ActualWidth}|{text.ActualHeight}|{origin}";if(key==glyphCacheKey)return glyphCache;glyphCacheKey=key;
   var formatted=new FormattedText(text.Text,System.Globalization.CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(text.FontFamily,text.FontStyle,text.FontWeight,text.FontStretch),text.FontSize,Brushes.White,VisualTreeHelper.GetDpi(text).PixelsPerDip){MaxTextWidth=Math.Max(1,text.ActualWidth),LineHeight=text.LineHeight};
   formatted.TextAlignment=TextAlignment.Center;var ink=formatted.BuildGeometry(new Point()).Bounds;
   if(!ink.IsEmpty&&ink.Width>0){var originInk=text.TranslatePoint(ink.TopLeft,this);return glyphCache=new Rect(originInk,ink.Size);}
   var width=Math.Min(text.ActualWidth,formatted.WidthIncludingTrailingWhitespace);var p=text.TranslatePoint(new Point((text.ActualWidth-width)/2,0),this);
   return glyphCache=new Rect(p.X,p.Y,width,Math.Min(text.ActualHeight,formatted.Height));
  }
 }
 Point UnlockAnchor()
 {
  var r=GlyphBounds;if(r.IsEmpty)return new Point(Left+Width/2-18,Top+Height/2);
  var screen=text.PointToScreen(new Point());var local=PointFromScreen(screen); // All layout calculations remain in DIPs.
  var p=new Point(Left+r.Left+r.Width/2-18,Top+r.Bottom+10);var work=ScreenWorkArea(this);
  p.X=Math.Clamp(p.X,work.Left,Math.Max(work.Left,work.Right-36));p.Y=Math.Clamp(p.Y,work.Top,Math.Max(work.Top,work.Bottom-36));return p;
 }
 void PositionUnlock(){if(!unlock.IsVisible)return;var p=UnlockAnchor();unlock.Left=p.X;unlock.Top=p.Y;}
 void PollHover()
 {
  if(!IsVisible||Editing){hover.Stop();unlock.Conceal(true);return;}if(!GetCursorPos(out var cursor))return;
  var r=GlyphBounds;if(r.IsEmpty){unlock.Conceal(true);return;}var point=PointFromScreen(new Point(cursor.X,cursor.Y));r.Inflate(12,10);
  var anchor=UnlockAnchor();var icon=new Rect(anchor.X-Left-8,anchor.Y-Top-4,52,44);var corridor=new Rect(Math.Min(r.Left,icon.Left),r.Top,Math.Max(r.Right,icon.Right)-Math.Min(r.Left,icon.Left),Math.Max(r.Bottom,icon.Bottom)-r.Top);
  bool inside=r.Contains(point)||unlock.IsVisible&&corridor.Contains(point);
  if(inside){awayAt=DateTime.MinValue;unlock.Reveal(settings.OverlayTopmost,anchor.X,anchor.Y);}
  else if(awayAt==DateTime.MinValue)awayAt=DateTime.UtcNow;else if((DateTime.UtcNow-awayAt).TotalMilliseconds>400)unlock.Conceal(settings.ReduceMotion);
 } internal bool UnlockVisible=>unlock.IsVisible&&unlock.Opacity>.1;internal FrameworkElement UnlockButton=>unlock.Button;
 internal bool TransparentSurface=>AllowsTransparency&&Background==Brushes.Transparent&&panel.Background==Brushes.Transparent;
 internal FontFamily DisplayFont=>text.FontFamily;internal double DisplayFontSize=>text.FontSize;internal Point TextScreenAnchor=>text.PointToScreen(new Point(text.ActualWidth/2,text.ActualHeight/2));internal string DisplayedText=>text.Text;public double DisplayBorder=>panel.BorderThickness.Left;
 internal void SetToolbar(bool show){toolbar.Opacity=Editing&&show?1:0;}
 public void SetPlaying(bool playing){if(PlayTool.Content is Symbol symbol)symbol.Kind=playing?"Pause":"Play";}
 public void SetText(string value){if(text.Text!=value){text.Text=value;if(Editing)FitEditor();QueueToolAlignment();PositionUnlock();}}
 public void SetClickThrough(bool enabled){ClickThrough=enabled;settings.OverlayClickThrough=enabled;var h=new WindowInteropHelper(this).Handle;if(h!=IntPtr.Zero){int style=GetWindowLong(h,-20)|ToolWindowStyle;SetWindowLong(h,-20,enabled?style|TransparentStyle|NoActivateStyle:style&~(TransparentStyle|NoActivateStyle));}}
 public bool NativeClickThrough=>(GetWindowLong(new WindowInteropHelper(this).Handle,-20)&TransparentStyle)!=0;
 public bool NativeNoActivate=>(GetWindowLong(new WindowInteropHelper(this).Handle,-20)&NoActivateStyle)!=0;
 public void ShowPassive(){if(Editing)EndEditing();ShowActivated=false;SetClickThrough(true);Show();SetWindowPos(new WindowInteropHelper(this).Handle,settings.OverlayTopmost?new IntPtr(-1):new IntPtr(-2),0,0,0,0,0x53);hover.Start();}
 public void RestoreEditing(){var foreground=GetForegroundWindow();if(foreground!=new WindowInteropHelper(this).Handle)previousFocus=foreground;hover.Stop();unlock.Conceal(true);Editing=true;settings.OverlayLocked=false;ShowActivated=true;Focusable=true;SetClickThrough(false);Apply();Width=Math.Max(MinWidth,Width);Height=Math.Max(MinHeight,Height);Show();SetClickThrough(false);SetWindowPos(new WindowInteropHelper(this).Handle,new IntPtr(-1),0,0,0,0,0x63);ClampToScreens(this);Activate();ConfigurationChanged?.Invoke();}
 public void EndEditing(){var wasActive=GetForegroundWindow()==new WindowInteropHelper(this).Handle;Editing=false;ShowActivated=false;Focusable=false;settings.OverlayLocked=true;SetClickThrough(true);Apply();if(IsVisible)hover.Start();if(wasActive&&previousFocus!=IntPtr.Zero&&IsWindow(previousFocus))SetForegroundWindow(previousFocus);if(IsVisible)SetWindowPos(new WindowInteropHelper(this).Handle,settings.OverlayTopmost?new IntPtr(-1):new IntPtr(-2),0,0,0,0,0x53);ConfigurationChanged?.Invoke();}
 public void Save(){settings.OverlayLeft=Left;settings.OverlayTop=Top;settings.OverlayWidth=ActualWidth>=MinWidth?ActualWidth:Math.Max(MinWidth,Width);settings.OverlayHeight=preferredHeight;}
 public void Finish(){closing=true;hover.Stop();saveDelay.Stop();unlock.Dispose();Close();}
 static Rect ScreenWorkArea(Window window)
 {
  var dpi=VisualTreeHelper.GetDpi(window);var bounds=new Rect(window.Left,window.Top,window.Width,window.Height);var work=SystemParameters.WorkArea;double largest=0;
  foreach(var screen in System.Windows.Forms.Screen.AllScreens){var a=screen.WorkingArea;var area=new Rect(a.X/dpi.DpiScaleX,a.Y/dpi.DpiScaleY,a.Width/dpi.DpiScaleX,a.Height/dpi.DpiScaleY);var r=Rect.Intersect(bounds,area);if(!r.IsEmpty&&r.Width*r.Height>largest){largest=r.Width*r.Height;work=area;}}
  return work;
 }
 public static void ClampToScreens(Window window)
 {
  var work=ScreenWorkArea(window);window.MinWidth=Math.Min(window.MinWidth,work.Width);window.MinHeight=Math.Min(window.MinHeight,work.Height);window.Width=Math.Min(window.Width,work.Width);window.Height=Math.Min(window.Height,work.Height);window.Left=Math.Clamp(window.Left,work.Left,Math.Max(work.Left,work.Right-window.Width));window.Top=Math.Clamp(window.Top,work.Top,Math.Max(work.Top,work.Bottom-window.Height));
 }
}
