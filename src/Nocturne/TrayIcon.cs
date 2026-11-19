using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace Nocturne;

// Register directly so Windows can report whether the tray entry actually exists.
sealed class TrayIcon : IDisposable
{
    const uint Add = 0, Delete = 2;
    const int Callback = 0x8001, LeftDoubleClick = 0x203, RightButtonUp = 0x205;
    readonly HwndSource source;
    readonly Icon icon;
    readonly Action restore;
    readonly uint taskbarCreated = RegisterWindowMessage("TaskbarCreated");
    bool registered;

    public Forms.ContextMenuStrip ContextMenuStrip { get; }
    public string Text => Brand.Name;
    public string? Error { get; private set; }

    public TrayIcon(HwndSource source, Icon icon, Forms.ContextMenuStrip menu, Action restore)
    {
        this.source = source;
        this.icon = icon;
        ContextMenuStrip = menu;
        this.restore = restore;
        source.AddHook(WindowMessage);
    }

    IconData Data() => new()
    {
        Size = (uint)Marshal.SizeOf<IconData>(), Window = source.Handle, Id = 1,
        Flags = 7, Callback = Callback, Icon = icon.Handle,
        Tip = Text, Info = "", InfoTitle = ""
    };

    public bool EnsureRegistered()
    {
        var data = Data();
        if (registered && Shell_NotifyIcon(1, ref data)) return true;
        registered = Shell_NotifyIcon(Add, ref data);
        var error = Marshal.GetLastWin32Error();
        Error = registered ? null : error == 0 ? "Windows 系统托盘拒绝注册图标，未返回错误码。" : new Win32Exception(error).Message;
        return registered;
    }

    IntPtr WindowMessage(IntPtr handle, int message, IntPtr wp, IntPtr lp, ref bool handled)
    {
        if (taskbarCreated != 0 && message == taskbarCreated)
        {
            registered = false;
            if (!EnsureRegistered()) restore();
        }
        if (message != Callback || wp != new IntPtr(1)) return IntPtr.Zero;
        if (lp.ToInt64() == LeftDoubleClick) restore();
        if (lp.ToInt64() == RightButtonUp)
        {
            SetForegroundWindow(handle);
            ContextMenuStrip.Show(Forms.Cursor.Position);
            PostMessage(handle, 0, IntPtr.Zero, IntPtr.Zero);
        }
        handled = true;
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (registered)
        {
            var data = Data();
            Shell_NotifyIcon(Delete, ref data);
        }
        source.RemoveHook(WindowMessage);
        ContextMenuStrip.Dispose();
        icon.Dispose();
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct IconData
    {
        public uint Size;
        public IntPtr Window;
        public uint Id, Flags, Callback;
        public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags;
        public Guid Guid;
        public IntPtr BalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool Shell_NotifyIcon(uint command, ref IconData data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")]
    static extern bool PostMessage(IntPtr window, int message, IntPtr wp, IntPtr lp);
}
