using Dororong.App.Product;
using DrawingIcon = System.Drawing.Icon;
using FormsContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using FormsNotifyIcon = System.Windows.Forms.NotifyIcon;

namespace Dororong.App.Tests.Product;

public class TrayServiceTests
{
    [Fact]
    public void Icon_acquisition_failure_does_not_create_later_resources()
    {
        var trace = new List<string>();

        Assert.Throws<InvalidOperationException>(() => TrayServiceResources.Acquire(
            () =>
            {
                trace.Add("icon");
                throw new InvalidOperationException("injected icon allocation failure");
            },
            () =>
            {
                trace.Add("notify");
                return new TrackingNotifyIcon();
            },
            () =>
            {
                trace.Add("menu");
                return new FormsContextMenuStrip();
            }));

        Assert.Equal(["icon"], trace);
    }

    [Fact]
    public void Notify_icon_acquisition_failure_disposes_the_previously_acquired_icon()
    {
        var icon = (DrawingIcon)System.Drawing.SystemIcons.Application.Clone();
        var menuFactoryCalls = 0;

        Assert.Throws<InvalidOperationException>(() => TrayServiceResources.Acquire(
            () => icon,
            () => throw new InvalidOperationException("injected notify allocation failure"),
            () =>
            {
                menuFactoryCalls++;
                return new FormsContextMenuStrip();
            }));

        Assert.Equal(0, menuFactoryCalls);
        Assert.ThrowsAny<Exception>(() => icon.ToBitmap());
    }

    [Fact]
    public void Menu_acquisition_failure_disposes_the_previously_acquired_notify_icon_and_icon()
    {
        var icon = (DrawingIcon)System.Drawing.SystemIcons.Application.Clone();
        var notifyIcon = new TrackingNotifyIcon();

        Assert.Throws<InvalidOperationException>(() => TrayServiceResources.Acquire(
            () => icon,
            () => notifyIcon,
            () => throw new InvalidOperationException("injected menu allocation failure")));

        Assert.Equal(1, notifyIcon.DisposeCount);
        Assert.ThrowsAny<Exception>(() => icon.ToBitmap());
    }

    [Fact]
    public void Resource_cleanup_attempts_every_step_when_hiding_and_detaching_fail()
    {
        var icon = (DrawingIcon)System.Drawing.SystemIcons.Application.Clone();
        var notifyIcon = new TrackingNotifyIcon
        {
            ThrowWhenHiding = true,
            ThrowWhenClearingMenu = true
        };
        using var menu = new FormsContextMenuStrip();
        var menuDisposeCount = 0;
        menu.Disposed += (_, _) => menuDisposeCount++;
        var resources = new TrayServiceResources(icon, notifyIcon, menu);

        var error = Assert.Throws<AggregateException>(resources.Dispose);

        Assert.Equal(2, error.InnerExceptions.Count);
        Assert.Equal(["hide", "detach", "notify-dispose"], notifyIcon.Trace);
        Assert.Equal(1, menuDisposeCount);
        Assert.ThrowsAny<Exception>(() => icon.ToBitmap());
        Assert.Null(Record.Exception(resources.Dispose));
    }

    [Fact]
    public void Real_notify_icon_boundary_builds_the_three_approved_menu_items_without_showing_it()
    {
        using var icon = (DrawingIcon)System.Drawing.SystemIcons.Application.Clone();
        using var notifyIcon = new FormsNotifyIcon();
        using var menu = new FormsContextMenuStrip();
        using var service = new Dororong.App.Product.TrayService(
            (DrawingIcon)icon.Clone(), notifyIcon, menu, () => { }, () => { }, visible: false);

        Assert.False(notifyIcon.Visible);
        Assert.Equal("도로롱 (Dororong)", notifyIcon.Text);
        Assert.Same(menu, notifyIcon.ContextMenuStrip);
        Assert.Collection(menu.Items.Cast<System.Windows.Forms.ToolStripItem>(),
            item =>
            {
                Assert.Equal("도로롱 0.1.0", item.Text);
                Assert.False(item.Enabled);
            },
            item => Assert.Equal("로그 폴더 열기", item.Text),
            item => Assert.Equal("종료", item.Text));
    }

    [Fact]
    public void Menu_commands_invoke_the_supplied_callbacks_once()
    {
        using var icon = (DrawingIcon)System.Drawing.SystemIcons.Application.Clone();
        using var notifyIcon = new FormsNotifyIcon();
        using var menu = new FormsContextMenuStrip();
        var openCount = 0;
        var exitCount = 0;
        using var service = new Dororong.App.Product.TrayService(
            (DrawingIcon)icon.Clone(), notifyIcon, menu,
            () => openCount++, () => exitCount++, visible: false);

        menu.Items[1].PerformClick();
        menu.Items[2].PerformClick();

        Assert.Equal(1, openCount);
        Assert.Equal(1, exitCount);
    }

    [Fact]
    public void Disposal_hides_real_notify_icon_and_is_idempotent()
    {
        using var icon = (DrawingIcon)System.Drawing.SystemIcons.Application.Clone();
        using var notifyIcon = new FormsNotifyIcon();
        using var menu = new FormsContextMenuStrip();
        var service = new Dororong.App.Product.TrayService(
            (DrawingIcon)icon.Clone(), notifyIcon, menu, () => { }, () => { }, visible: false);

        service.Dispose();
        var repeatedError = Record.Exception(service.Dispose);

        Assert.False(notifyIcon.Visible);
        Assert.Null(repeatedError);
    }

    [Fact]
    public void Compiled_product_icon_constructs_at_the_hidden_notify_icon_boundary()
    {
        using var service = new Dororong.App.Product.TrayService(() => { }, () => { }, visible: false);
    }

    private sealed class TrackingNotifyIcon : ITrayNotifyIcon
    {
        private bool visible;
        private FormsContextMenuStrip? contextMenu;

        public List<string> Trace { get; } = [];
        public int DisposeCount { get; private set; }
        public bool ThrowWhenHiding { get; init; }
        public bool ThrowWhenClearingMenu { get; init; }
        public DrawingIcon? Icon { private get; set; }
        public string Text { private get; set; } = string.Empty;

        public FormsContextMenuStrip? ContextMenuStrip
        {
            private get => contextMenu;
            set
            {
                if (value is null)
                {
                    Trace.Add("detach");
                    if (ThrowWhenClearingMenu)
                    {
                        throw new InvalidOperationException("injected detach failure");
                    }
                }

                contextMenu = value;
            }
        }

        public bool Visible
        {
            get => visible;
            set
            {
                if (!value)
                {
                    Trace.Add("hide");
                    if (ThrowWhenHiding)
                    {
                        throw new InvalidOperationException("injected hide failure");
                    }
                }

                visible = value;
            }
        }

        public void Dispose()
        {
            Trace.Add("notify-dispose");
            DisposeCount++;
        }
    }
}
