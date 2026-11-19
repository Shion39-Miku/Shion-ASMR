using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Drawing;
using Forms=System.Windows.Forms;

namespace Nocturne;
// Test-only interactive game fixture, launched as a separate process. No hooks are installed.
public sealed class GameProbe : Forms.Form
{
 readonly string output;readonly Forms.Timer frame=new(){Interval=33};int moves,downs,ups,wheels,drags,keys;float ballX=160,ballY=120,dx=4,dy=3,paddle=200;bool dragging;
 public GameProbe(string output,bool borderless)
 {
  this.output=output;Directory.CreateDirectory(output);Text="Nocturne input verification · Pong";DoubleBuffered=true;BackColor=Color.FromArgb(15,25,39);ForeColor=Color.White;
  if(borderless){FormBorderStyle=Forms.FormBorderStyle.None;Bounds=Forms.Screen.PrimaryScreen!.Bounds;}else{StartPosition=Forms.FormStartPosition.Manual;Bounds=new Rectangle(90,90,900,650);}
  Shown+=(_,_)=>Write();frame.Tick+=(_,_)=>{ballX+=dx;ballY+=dy;if(ballX<10||ballX>ClientSize.Width-10)dx=-dx;if(ballY<10||ballY>ClientSize.Height-60&&Math.Abs(ballX-paddle)<90)dy=-dy;if(ballY>ClientSize.Height){ballX=160;ballY=100;}Invalidate();};frame.Start();
  FormClosed+=(_,_)=>{frame.Dispose();System.Windows.Application.Current.Shutdown();};
 }
 protected override void WndProc(ref Forms.Message m)
 {
  switch(m.Msg){case 0x200:moves++;if(dragging)drags++;paddle=(short)(m.LParam.ToInt64()&0xffff);Write();break;case 0x201:downs++;dragging=true;ballX=160;ballY=100;Write();break;case 0x202:ups++;dragging=false;Write();break;case 0x20A:wheels++;dx=Math.Clamp(dx+0.3f,-10,10);Write();break;case 0x100:keys++;paddle+=m.WParam.ToInt32()==0x25?-20:20;Write();break;}
  base.WndProc(ref m);
 }
 void Write()
 {
  var state=new{hwnd=Handle.ToInt64(),left=Left,top=Top,width=Width,height=Height,moves,downs,ups,wheels,drags,keys};var file=Path.Combine(output,"game.json");try{File.WriteAllText(file+".tmp",System.Text.Json.JsonSerializer.Serialize(state));File.Move(file+".tmp",file,true);}catch(IOException){}catch(UnauthorizedAccessException){}
 }
 protected override void OnPaint(Forms.PaintEventArgs e)
 {
  base.OnPaint(e);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using var pale=new SolidBrush(Color.FromArgb(190,192,233));e.Graphics.FillEllipse(pale,ballX-8,ballY-8,16,16);e.Graphics.FillRectangle(pale,paddle-80,ClientSize.Height-45,160,8);using var font=new Font("Segoe UI",13);e.Graphics.DrawString("PONG · move paddle / click restart / wheel speed / arrow keys\nIndependent process for real input routing verification",font,pale,35,35);
 }
}
internal static class NativeProbe
{
 [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")] static extern bool AttachThreadInput(uint from,uint to,bool attach);
 [DllImport("user32.dll")] static extern IntPtr SetFocus(IntPtr handle);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr handle);
 [DllImport("user32.dll")] static extern bool IsIconic(IntPtr handle);
 [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr handle,int command);
 [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr handle,IntPtr after,int x,int y,int width,int height,uint flags);
 internal static bool TargetTestWindow(IntPtr handle)
 {
  // A verifier may be started with SW_HIDE. WPF can still render offscreen; real OS
  // input must target a natively visible test window, not the unrelated app behind it.
  if(IsIconic(handle))ShowWindow(handle,9);else if(!IsWindowVisible(handle))ShowWindow(handle,5);
  SetWindowPos(handle,new IntPtr(-1),0,0,0,0,0x53); // Only the explicitly targeted verifier window.
  if(GetForegroundWindow()==handle)return true;
  var own=GetCurrentThreadId();var other=GetWindowThreadProcessId(GetForegroundWindow(),out _);var attached=own!=other&&AttachThreadInput(own,other,true);
  try{SetForegroundWindow(handle);SetFocus(handle);}finally{if(attached)AttachThreadInput(own,other,false);}return GetForegroundWindow()==handle;
 }
 internal static string ForegroundDescription(){var h=GetForegroundWindow();GetWindowThreadProcessId(h,out var pid);try{return $"hwnd={h.ToInt64()}; pid={pid}; process={System.Diagnostics.Process.GetProcessById((int)pid).ProcessName}";}catch{return $"hwnd={h.ToInt64()}; pid={pid}";}}
 [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll",EntryPoint="SetPhysicalCursorPos")] internal static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll",EntryPoint="GetPhysicalCursorPos")] internal static extern bool GetCursorPos(out Point p);
 [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(Point p);
 [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr handle,uint flags);
 internal static bool PointTargets(IntPtr handle,int x,int y)=>GetAncestor(WindowFromPoint(new Point(x,y)),2)==handle;
 [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr h,int msg,IntPtr wp,IntPtr lp);
 [DllImport("user32.dll")] static extern uint SendInput(uint count,Input[] input,int size);
 [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
 [StructLayout(LayoutKind.Sequential)] struct MouseData{public int x,y;public uint data,flags,time;public IntPtr extra;}
 [StructLayout(LayoutKind.Sequential)] struct KeyData{public ushort key,scan;public uint flags,time;public IntPtr extra;}
 [StructLayout(LayoutKind.Explicit)] struct InputData{[FieldOffset(0)]public MouseData mouse;[FieldOffset(0)]public KeyData key;}
 [StructLayout(LayoutKind.Sequential)] struct Input{public uint type;public InputData data;}
 internal static bool Mouse(uint flags,uint data=0){var input=new Input{type=0,data=new InputData{mouse=new MouseData{flags=flags,data=data}}};return SendInput(1,new[]{input},Marshal.SizeOf<Input>())==1;}
 static Input Move(int x,int y)=>new(){data=new InputData{mouse=new MouseData{x=(int)Math.Clamp((x-GetSystemMetrics(76)+.5)*65536/GetSystemMetrics(78),0,65535),y=(int)Math.Clamp((y-GetSystemMetrics(77)+.5)*65536/GetSystemMetrics(79),0,65535),flags=0xC001}}};
 internal static bool MouseAt(int x,int y,uint flags,uint data=0){var inputs=new[]{Move(x,y),new Input{data=new InputData{mouse=new MouseData{flags=flags,data=data}}}};return SendInput(2,inputs,Marshal.SizeOf<Input>())==2;}
 internal static bool ClickAt(int x,int y){var inputs=new[]{Move(x,y),new Input{data=new InputData{mouse=new MouseData{flags=2}}},new Input{data=new InputData{mouse=new MouseData{flags=4}}}};return SendInput(3,inputs,Marshal.SizeOf<Input>())==3;}
 internal static bool Key(ushort key,bool up){var input=new Input{type=1,data=new InputData{key=new KeyData{key=key,flags=up?2u:0}}};return SendInput(1,new[]{input},Marshal.SizeOf<Input>())==1;}
 internal static bool KeyStroke(ushort key,bool repeat=false){var down=new Input{type=1,data=new InputData{key=new KeyData{key=key}}};var up=new Input{type=1,data=new InputData{key=new KeyData{key=key,flags=2}}};var inputs=repeat?new[]{down,down,down,up}:new[]{down,up};return SendInput((uint)inputs.Length,inputs,Marshal.SizeOf<Input>())==inputs.Length;}
}
