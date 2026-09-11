using Dororong.App.Runtime;
using DrawingIcon = System.Drawing.Icon;
using FormsContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using FormsNotifyIcon = System.Windows.Forms.NotifyIcon;
using FormsToolStripMenuItem = System.Windows.Forms.ToolStripMenuItem;

namespace Dororong.App.Product;

internal sealed class TrayService : IDisposable
{
    private readonly TrayServiceResources resources;
    private FormsToolStripMenuItem? versionItem;
    private FormsToolStripMenuItem? openLogsItem;
    private FormsToolStripMenuItem? exitItem;
    private EventHandler? openLogsHandler;
    private EventHandler? exitHandler;
    private int disposed;

    internal TrayService(Action openLogs, Action exit, bool visible = true) : this(
        AcquireDefaultResources(
            openLogs,
            exit,
            LoadIcon,
            () => new FormsTrayNotifyIcon(new FormsNotifyIcon()),
            () => new FormsContextMenuStrip()),
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
        bool visible) : this(
            new TrayServiceResources(icon, new FormsTrayNotifyIcon(notifyIcon), menu),
            openLogs,
            exit,
            visible)
    {
    }

    private TrayService(
        TrayServiceResources resources,
        Action openLogs,
        Action exit,
        bool visible)
    {
        this.resources = resources ?? throw new ArgumentNullException(nameof(resources));

        try
        {
            ArgumentNullException.ThrowIfNull(openLogs);
            ArgumentNullException.ThrowIfNull(exit);
            versionItem = new FormsToolStripMenuItem($"도로롱 {ProductIdentity.Version}")
            {
                Enabled = false
            };
            openLogsItem = new FormsToolStripMenuItem("로그 폴더 열기");
            exitItem = new FormsToolStripMenuItem("종료");
            openLogsHandler = (_, _) => openLogs();
            exitHandler = (_, _) => exit();
            openLogsItem.Click += openLogsHandler;
            exitItem.Click += exitHandler;
            resources.Menu.Items.AddRange([versionItem, openLogsItem, exitItem]);
            resources.NotifyIcon.Icon = resources.Icon;
            resources.NotifyIcon.Text = ProductIdentity.DisplayName;
            resources.NotifyIcon.ContextMenuStrip = resources.Menu;
            resources.NotifyIcon.Visible = visible;
        }
        catch (Exception initializationException)
        {
            var cleanupException = CleanupSequence.Run(
                DetachOpenLogsHandler,
                DetachExitHandler,
                () => versionItem?.Dispose(),
                () => openLogsItem?.Dispose(),
                () => exitItem?.Dispose(),
                resources.Dispose);
            if (cleanupException is not null)
            {
                throw new AggregateException(initializationException, cleanupException).Flatten();
            }

            throw;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        var cleanupException = CleanupSequence.Run(
            DetachOpenLogsHandler,
            DetachExitHandler,
            resources.Dispose);
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

    private static TrayServiceResources AcquireDefaultResources(
        Action openLogs,
        Action exit,
        Func<DrawingIcon> createIcon,
        Func<ITrayNotifyIcon> createNotifyIcon,
        Func<FormsContextMenuStrip> createMenu)
    {
        ArgumentNullException.ThrowIfNull(openLogs);
        ArgumentNullException.ThrowIfNull(exit);
        return TrayServiceResources.Acquire(createIcon, createNotifyIcon, createMenu);
    }

    private void DetachOpenLogsHandler()
    {
        if (openLogsItem is not null && openLogsHandler is not null)
        {
            openLogsItem.Click -= openLogsHandler;
        }
    }

    private void DetachExitHandler()
    {
        if (exitItem is not null && exitHandler is not null)
        {
            exitItem.Click -= exitHandler;
        }
    }
}

internal interface ITrayNotifyIcon : IDisposable
{
    DrawingIcon? Icon { set; }
    string Text { set; }
    FormsContextMenuStrip? ContextMenuStrip { set; }
    bool Visible { get; set; }
}

internal sealed class FormsTrayNotifyIcon : ITrayNotifyIcon
{
    private readonly FormsNotifyIcon notifyIcon;

    internal FormsTrayNotifyIcon(FormsNotifyIcon notifyIcon)
    {
        this.notifyIcon = notifyIcon ?? throw new ArgumentNullException(nameof(notifyIcon));
    }

    public DrawingIcon? Icon
    {
        set => notifyIcon.Icon = value;
    }

    public string Text
    {
        set => notifyIcon.Text = value;
    }

    public FormsContextMenuStrip? ContextMenuStrip
    {
        set => notifyIcon.ContextMenuStrip = value;
    }

    public bool Visible
    {
        get => notifyIcon.Visible;
        set => notifyIcon.Visible = value;
    }

    public void Dispose() => notifyIcon.Dispose();
}

internal sealed class TrayServiceResources : IDisposable
{
    internal DrawingIcon Icon { get; }
    internal ITrayNotifyIcon NotifyIcon { get; }
    internal FormsContextMenuStrip Menu { get; }

    internal TrayServiceResources(
        DrawingIcon icon,
        ITrayNotifyIcon notifyIcon,
        FormsContextMenuStrip menu)
    {
        Icon = icon ?? throw new ArgumentNullException(nameof(icon));
        NotifyIcon = notifyIcon ?? throw new ArgumentNullException(nameof(notifyIcon));
        Menu = menu ?? throw new ArgumentNullException(nameof(menu));
    }

    internal static TrayServiceResources Acquire(
        Func<DrawingIcon> createIcon,
        Func<ITrayNotifyIcon> createNotifyIcon,
        Func<FormsContextMenuStrip> createMenu)
    {
        ArgumentNullException.ThrowIfNull(createIcon);
        ArgumentNullException.ThrowIfNull(createNotifyIcon);
        ArgumentNullException.ThrowIfNull(createMenu);

        DrawingIcon? icon = null;
        ITrayNotifyIcon? notifyIcon = null;
        FormsContextMenuStrip? menu = null;
        try
        {
            icon = createIcon() ?? throw new InvalidOperationException("The icon factory returned null.");
            notifyIcon = createNotifyIcon()
                ?? throw new InvalidOperationException("The notify-icon factory returned null.");
            menu = createMenu() ?? throw new InvalidOperationException("The tray-menu factory returned null.");
            return new TrayServiceResources(icon, notifyIcon, menu);
        }
        catch (Exception acquisitionException)
        {
            var cleanupException = CleanupSequence.Run(
                () => menu?.Dispose(),
                () => notifyIcon?.Dispose(),
                () => icon?.Dispose());
            if (cleanupException is not null)
            {
                throw new AggregateException(acquisitionException, cleanupException).Flatten();
            }

            throw;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        var cleanupException = CleanupSequence.Run(
            () => NotifyIcon.Visible = false,
            () => NotifyIcon.ContextMenuStrip = null,
            NotifyIcon.Dispose,
            Menu.Dispose,
            Icon.Dispose);
        if (cleanupException is not null)
        {
            throw cleanupException;
        }
    }

    private int disposed;
}
