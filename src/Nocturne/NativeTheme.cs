using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace Nocturne;
static class NativeTheme
{
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int size);
 public static void Attach(Window window){window.Icon=Brand.Artwork;window.SourceInitialized+=(_,_)=>{int dark=1;var h=new WindowInteropHelper(window).Handle;DwmSetWindowAttribute(h,20,ref dark,4);int round=2;DwmSetWindowAttribute(h,33,ref round,4);};}
}
