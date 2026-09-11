using DrawingIcon = System.Drawing.Icon;
using FormsContextMenuStrip = System.Windows.Forms.ContextMenuStrip;
using FormsNotifyIcon = System.Windows.Forms.NotifyIcon;

namespace Dororong.App.Tests.Product;

public class TrayServiceTests
{
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
}
