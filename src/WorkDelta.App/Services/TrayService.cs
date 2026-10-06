using System.Drawing;
using Forms = System.Windows.Forms;

namespace WorkDelta.App.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private bool _noticeShown;

    public TrayService(Action showWindow, Func<Task> exit, string tooltip)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开工迹", null, (_, _) => showWindow());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, async (_, _) => await exit());

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = tooltip.Length > 63 ? tooltip[..63] : tooltip,
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => showWindow();
    }

    public void ShowBackgroundNotice()
    {
        if (_noticeShown)
        {
            return;
        }

        _noticeShown = true;
        _notifyIcon.ShowBalloonTip(
            2500,
            "工迹仍在运行",
            "项目变化会继续在本机记录。双击托盘图标可重新打开。",
            Forms.ToolTipIcon.Info);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
