using System.Drawing;
using WorkDelta.App.Localization;
using Forms = System.Windows.Forms;

namespace WorkDelta.App.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Icon _icon;
    private bool _noticeShown;

    public TrayService(Action showWindow, Func<Task> exit, string tooltip)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(Localizer.Get("TrayOpen"), null, (_, _) => showWindow());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(Localizer.Get("TrayExit"), null, async (_, _) => await exit());

        var iconResource = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/Assets/WorkDelta.ico"))
            ?? throw new InvalidOperationException("The WorkDelta tray icon resource is missing.");
        using (iconResource.Stream)
        using (var resourceIcon = new Icon(iconResource.Stream))
        {
            _icon = (Icon)resourceIcon.Clone();
        }

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
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
            Localizer.Get("TrayStillRunning"),
            Localizer.Get("TrayNotice"),
            Forms.ToolTipIcon.Info);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon.Dispose();
    }
}
