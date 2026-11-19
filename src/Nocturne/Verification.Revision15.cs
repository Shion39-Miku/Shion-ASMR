using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace Nocturne;

public static partial class Verification
{
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wp,IntPtr lp);
 [DllImport("user32.dll")] static extern bool GetCursorInfo(ref CursorInfo info);
 [DllImport("user32.dll")] static extern IntPtr LoadCursor(IntPtr module,IntPtr name);
 [StructLayout(LayoutKind.Sequential)] struct CursorInfo{public int Size,Flags;public IntPtr Cursor;public int X,Y;}

 static async Task VerifyOverlayResize(MainWindow w)
 {
  w.Left=30;w.Top=30;w.Width=920;w.Height=750;w.settings.ReduceMotion=true;
  var overlay=w.overlay;
  overlay.Left=130;overlay.Top=105;overlay.Width=540;
  await ClickElement(w.DesktopLyricsButton);
  var line="夜色渐深，星光沿着窗边慢慢落下。此刻只听见轻轻的风声和熟悉的旋律。";
  overlay.SetText(line);await Delay(180);
  var plate=(Border)overlay.Content;
  Check("编辑底板覆盖完整缩放区域",Math.Abs(plate.ActualWidth-overlay.ActualWidth)<1&&Math.Abs(plate.ActualHeight-overlay.ActualHeight)<1);
  var handle=new WindowInteropHelper(overlay).Handle;
  var edges=new[]{(0.0,.5,10),(1.0,.5,11),(.5,0.0,12),(0.0,0.0,13),(1.0,0.0,14),(.5,1.0,15),(0.0,1.0,16),(1.0,1.0,17)};
  Point Edge(double x,double y)=>plate.PointToScreen(new Point(4+(plate.ActualWidth-8)*x,4+(plate.ActualHeight-8)*y));
  int Hit(Point p)=>SendMessage(handle,0x84,IntPtr.Zero,new IntPtr(unchecked(((int)p.X&0xffff)|(((int)p.Y&0xffff)<<16)))).ToInt32();
  Check("四条边与四角均返回正确缩放命中",edges.All(e=>Hit(Edge(e.Item1,e.Item2))==e.Item3));

  async Task Resize(double x,double y,int dx,int dy,int cursorId,string name)
  {
   var start=Edge(x,y);var width=overlay.ActualWidth;var height=overlay.ActualHeight;
   if(!NativeProbe.TargetTestWindow(handle)||!NativeProbe.PointTargets(handle,(int)start.X,(int)start.Y))
    throw new InvalidOperationException("原生缩放起点未命中桌面字幕窗口");
   NativeProbe.SetCursorPos((int)start.X,(int)start.Y);await Delay(100);
   var cursor=new CursorInfo{Size=Marshal.SizeOf<CursorInfo>()};
   Check(name+"显示缩放光标",GetCursorInfo(ref cursor)&&cursor.Cursor==LoadCursor(IntPtr.Zero,new IntPtr(cursorId)));
   await Task.Run(async()=>
   {
    NativeProbe.Mouse(2);
    try{await Delay(90);NativeProbe.SetCursorPos((int)start.X+dx,(int)start.Y+dy);await Delay(100);}
    finally{NativeProbe.Mouse(4);}
   });
   await Delay(170);
   var dpi=System.Windows.Media.VisualTreeHelper.GetDpi(overlay);
   Check(name+"可原生缩放",Math.Abs(overlay.ActualWidth-width-dx/dpi.DpiScaleX)<4&&Math.Abs(overlay.ActualHeight-height-dy/dpi.DpiScaleY)<4,$"{width:F1}×{height:F1} → {overlay.ActualWidth:F1}×{overlay.ActualHeight:F1} DIP");
  }

  await Resize(.5,1,0,110,32645,"下边放大");
  await Resize(.5,1,0,-45,32645,"下边缩小");
  var initialTextHeight=overlay.GlyphBounds.Height;var font=overlay.DisplayFontSize;
  await Resize(1,.5,170,0,32644,"右边放大");
  var wideTextHeight=overlay.GlyphBounds.Height;
  await Resize(1,.5,-170,0,32644,"右边缩小");
  Check("宽度变化重新换行且字号保持",wideTextHeight<initialTextHeight&&Math.Abs(overlay.GlyphBounds.Height-initialTextHeight)<2&&overlay.DisplayFontSize==font);
  await Resize(1,1,75,65,32642,"右下角放大");
  await Resize(1,1,-45,-35,32642,"右下角缩小");
  var manualWidth=overlay.ActualWidth;var manualHeight=overlay.ActualHeight;
  bool SameSize()=>Math.Abs(overlay.ActualWidth-manualWidth)<2&&Math.Abs(overlay.ActualHeight-manualHeight)<2;
  Check("松手保存尺寸和位置到设置状态",Math.Abs(w.settings.OverlayWidth-manualWidth)<2&&Math.Abs(w.settings.OverlayHeight-manualHeight)<2&&Math.Abs(w.settings.OverlayLeft-overlay.Left)<2&&Math.Abs(w.settings.OverlayTop-overlay.Top)<2);
  overlay.SetText("风停了，星光依旧。");await Delay(140);
  Check("切换台词保留手动尺寸",SameSize());
  overlay.SetText(string.Concat(Enumerable.Repeat(line,4)));await Delay(170);
  Check("长台词的临时最小高度不覆盖已保存尺寸",overlay.ActualHeight>manualHeight&&Math.Abs(w.settings.OverlayHeight-manualHeight)<2);
  overlay.SetText(line);await Delay(170);
  Check("较短台词恢复手动尺寸",SameSize());
  var tools=Visuals<Button>(overlay).Where(b=>b.IsVisible).ToArray();
  Check("缩放后工具栏仍位于台词下方且完整",tools.Length>=8&&tools.All(b=>{var bounds=PageBounds(b,overlay);return bounds.Top>=overlay.GlyphBounds.Bottom&&bounds.Bottom<overlay.ActualHeight&&bounds.Left>0&&bounds.Right<overlay.ActualWidth;}));
  await DragPlate(overlay,plate.PointToScreen(new Point(20,plate.ActualHeight/2)),"缩放后黑底内部");
  var lockButton=tools.Single(b=>b.ToolTip?.ToString()?.StartsWith("锁定并")==true);
  await ClickElement(lockButton);
  Check("锁定去掉缩放命中并恢复穿透",overlay.TransparentSurface&&overlay.NativeClickThrough&&Hit(Edge(1,1))==-1);
  var glyph=overlay.GlyphBounds;var hover=overlay.PointToScreen(new Point(glyph.Left+glyph.Width/2,glyph.Top+glyph.Height/2));
  NativeProbe.SetCursorPos((int)hover.X,(int)hover.Y);await Delay(260);
  var unlock=overlay.UnlockButton;var point=unlock.PointToScreen(new Point(unlock.ActualWidth/2,unlock.ActualHeight/2));
  NativeProbe.ClickAt((int)point.X,(int)point.Y);await Delay(150);
  Check("原生解锁后仍保留手动尺寸",overlay.Editing&&SameSize());
  await ClickElement(tools.Single(b=>Equals(b.ToolTip,"关闭桌面字幕")));
  Check("缩放后的关闭按钮仍可点击",!overlay.IsVisible);
  await ClickElement(w.DesktopLyricsButton);
  Check("重新打开仍保留手动尺寸",overlay.IsVisible&&overlay.Editing&&SameSize());
  w.Save();var saved=State.Read<Settings>("settings.json");
  Check("手动尺寸与位置写入设置文件",Math.Abs(saved.OverlayWidth-manualWidth)<2&&Math.Abs(saved.OverlayHeight-manualHeight)<2&&Math.Abs(saved.OverlayLeft-overlay.Left)<2&&Math.Abs(saved.OverlayTop-overlay.Top)<2);
  overlay.SetText(line);await Delay(140);
  Snapshot(overlay,Path.Combine(State.Home,"desktop-resized.png"));
  var minimumDrag=Edge(1,1);
  NativeProbe.SetCursorPos((int)minimumDrag.X,(int)minimumDrag.Y);await Delay(80);
  await Task.Run(async()=>
  {
   NativeProbe.Mouse(2);
   try{await Delay(90);NativeProbe.SetCursorPos((int)minimumDrag.X-280,(int)minimumDrag.Y-240);await Delay(100);}
   finally{NativeProbe.Mouse(4);}
  });
  await Delay(180);
  Check("拖到最小尺寸后台词及工具栏仍完整",Math.Abs(overlay.ActualWidth-overlay.MinWidth)<2&&Math.Abs(overlay.ActualHeight-overlay.MinHeight)<2&&tools.All(b=>{var bounds=PageBounds(b,overlay);return bounds.Top>=overlay.GlyphBounds.Bottom&&bounds.Bottom<overlay.ActualHeight&&bounds.Left>0&&bounds.Right<overlay.ActualWidth;}),$"{overlay.ActualWidth:F1}×{overlay.ActualHeight:F1} DIP, minimum {overlay.MinWidth:F1}×{overlay.MinHeight:F1} DIP");
 }
}
