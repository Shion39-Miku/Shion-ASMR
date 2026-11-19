using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace Nocturne;

public partial class MainWindow
{
    bool confirmingClose, exitRequested;
    string? trayError;

    void InitializeTray()
    {
        try
        {
            var source = (HwndSource)PresentationSource.FromVisual(this)!;
            var icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!)
                ?? throw new InvalidOperationException("程序图标无法加载。");
            var menu = new Forms.ContextMenuStrip();
            menu.Items.Add("打开" + Brand.Name, null, (_, _) => RestoreMain());
            menu.Items.Add("播放／暂停", null, (_, _) => TogglePlay(this, new RoutedEventArgs()));
            menu.Items.Add("桌面字幕 / 解锁编辑", null, (_, _) => OpenOverlayEditor());
            menu.Items.Add("锁定桌面字幕", null, (_, _) => overlay.EndEditing());
            menu.Items.Add("关闭桌面字幕", null, (_, _) => CloseOverlay());
            menu.Items.Add("退出软件", null, (_, _) => Dispatcher.BeginInvoke(ExitApplication));
            tray = new TrayIcon(source, icon, menu, RestoreFromTray);
            if (!tray.EnsureRegistered()) ShowTrayError();
            UpdateOverlayButtons();
        }
        catch (Exception ex)
        {
            trayError = ex.Message;
            ShowTrayError();
        }
    }

    void RestoreFromTray()
    {
        RestoreMain();
        if (tray?.Error != null) ShowTrayError();
    }

    void ShowTrayError()
    {
        Status.Text = "系统托盘不可用，主窗口将保持打开。" + (tray?.Error ?? trayError ?? "未能初始化托盘图标。");
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (e.Cancel) return;
        if (exitRequested)
        {
            try { Save(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                e.Cancel = true;
                exitRequested = false;
                RestoreMain();
                Status.Text = "保存数据失败：" + ex.Message;
            }
            return;
        }

        e.Cancel = true;
        if (!IsVisible || confirmingClose) return;
        confirmingClose = true;
        Dispatcher.BeginInvoke(ConfirmClose, DispatcherPriority.Normal);
    }

    void ConfirmClose()
    {
        try
        {
            if (exitRequested || closing) return;
            var prompt = new ClosePrompt(this);
            if (prompt.ShowDialog() != true || closing) return;
            if (!prompt.RunInBackground)
            {
                ExitApplication();
                return;
            }
            if (tray?.EnsureRegistered() != true)
            {
                ShowTrayError();
                return;
            }
            Hide();
            UpdateVisuals();
            UpdateTimer();
        }
        finally { confirmingClose = false; }
    }

    internal void ExitApplication()
    {
        if (closing || exitRequested) return;
        exitRequested = true;
        Close();
    }

    internal void PrepareForSessionEnd() => exitRequested = true;

    internal void InvokeTrayExitForTest() => tray?.ContextMenuStrip.Items[^1].PerformClick();
}
