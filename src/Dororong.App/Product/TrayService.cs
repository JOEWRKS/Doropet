using Dororong.App.Runtime;
using DrawingIcon = System.Drawing.Icon;
using FormsContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using FormsNotifyIcon = System.Windows.Forms.NotifyIcon;
using FormsToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace Dororong.App.Product;

internal sealed class TrayService : IDisposable
{
    private readonly DrawingIcon icon;
    private readonly FormsNotifyIcon notifyIcon;
    private readonly FormsContextMenuStrip menu;
    private readonly FormsToolStripMenuItem openLogsItem;
    private readonly FormsToolStripMenuItem exitItem;
    private readonly EventHandler openLogsHandler;
    private readonly EventHandler exitHandler;
    private int disposed;

    internal TrayService(Action openLogs, Action exit, bool visible = true) : this(
        LoadIcon(),
        new FormsNotifyIcon(),
        new FormsContextMenuStrip(),
        openLogs,
        exit,
        visible)
    {
    }

    internal TrayService(
        DrawingIcon icon,
        FormsNotifyIcon notifyIcon,
        FormsContextMenuStrip menu,
        Action openLogs,
        Action exit,
        bool visible)
    {
        this.icon = icon ?? throw new ArgumentNullException(nameof(icon));
        this.notifyIcon = notifyIcon ?? throw new ArgumentNullException(nameof(notifyIcon));
        this.menu = menu ?? throw new ArgumentNullException(nameof(menu));
        ArgumentNullException.ThrowIfNull(openLogs);
        ArgumentNullException.ThrowIfNull(exit);

        var versionItem = new FormsToolStripMenuItem($"도로롱 {ProductIdentity.Version}")
        {
            Enabled = false
        };
        openLogsItem = new FormsToolStripMenuItem("로그 폴더 열기");
        exitItem = new FormsToolStripMenuItem("종료");
        openLogsHandler = (_, _) => openLogs();
        exitHandler = (_, _) => exit();
        openLogsItem.Click += openLogsHandler;
        exitItem.Click += exitHandler;

        try
        {
            menu.Items.AddRange([versionItem, openLogsItem, exitItem]);
            notifyIcon.Icon = icon;
            notifyIcon.Text = ProductIdentity.DisplayName;
            notifyIcon.ContextMenuStrip = menu;
            notifyIcon.Visible = visible;
        }
        catch
        {
            CleanupSequence.Run(notifyIcon.Dispose, menu.Dispose, icon.Dispose);
            throw;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        openLogsItem.Click -= openLogsHandler;
        exitItem.Click -= exitHandler;
        notifyIcon.Visible = false;
        notifyIcon.ContextMenuStrip = null;
        var cleanupException = CleanupSequence.Run(
            notifyIcon.Dispose,
            menu.Dispose,
            icon.Dispose);
        if (cleanupException is not null)
        {
            throw cleanupException;
        }
    }

    private static DrawingIcon LoadIcon()
    {
        const string resourceName = "Dororong.App.Assets.dororong.ico";
        using var stream = typeof(TrayService).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The product icon resource '{resourceName}' is unavailable.");
        using var loaded = new DrawingIcon(stream);
        return (DrawingIcon)loaded.Clone();
    }
}
