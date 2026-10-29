using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;

namespace PassIn.App.Services;

public sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Forms.ToolStripMenuItem _quickAccessItem;

    public TrayIcon(Action open, Action quickAccess, Action lockVault, Action exit, string quickAccessHint)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open PassIn", null, (_, _) => open());
        _quickAccessItem = (Forms.ToolStripMenuItem)menu.Items.Add(quickAccessHint, null, (_, _) => quickAccess());
        menu.Items.Add("Lock", null, (_, _) => lockVault());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => exit());

        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Resources/passin.ico")).Stream;
        _icon = new Forms.NotifyIcon
        {
            Icon = new Icon(stream, Forms.SystemInformation.SmallIconSize),
            Text = "PassIn",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.BalloonTipClicked += (_, _) => open();
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left)
            {
                open();
            }
        };
    }

    public void SetQuickAccessHint(string text) => _quickAccessItem.Text = text;

    public void ShowWarning(string title, string text) =>
        _icon.ShowBalloonTip(5000, title, text, Forms.ToolTipIcon.Warning);

    public void ShowInfo(string title, string text) =>
        _icon.ShowBalloonTip(5000, title, text, Forms.ToolTipIcon.Info);

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
