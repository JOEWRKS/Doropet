using System.Reflection;
using Dororong.App.Controls;
using Dororong.App.Runtime;
using Dororong.App.Tests.Controls;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public partial class PetLoopPlatformTests
{
    [Fact]
    public void Perch_layer_maintenance_runs_only_after_active_perch_render_including_attached_cheek() => CheekProductTests.Sta(() =>
    {
        using var h = new Harness(realPresenter: true, perchEnabled: true);
        var calls = 0;
        var host = typeof(PetLoop).GetField("_host", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(h.Loop)!;
        typeof(PetLoopHost).GetProperty("MaintainPerchLayer", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(host, (Action)(() =>
        {
            Assert.NotEqual(EdgePerchPhase.None, h.Platforms.PerchPhase);
            Assert.True(h.Presenter!.EdgePerchImage.Visibility == System.Windows.Visibility.Visible || h.Direct.IsAttachedCheek);
            calls++;
        }));
        h.Start(); h.Tick();
        Assert.Equal(0, calls);
        h.Native.Scene = Scene(x: 0, y: 186);
        h.Press(0); h.Tick(80);
        h.Pointer = new(true, h.Pointer.Position + new PointD(100, 0)); h.Tick(80);
        Assert.Equal(0, calls);
        h.Down = false; h.Tick();
        Assert.Equal(EdgePerchPhase.Entering, h.Platforms.PerchPhase);
        Assert.Equal(1, calls);
        for (var i = 0; i < 30; i++) h.Tick();
        Assert.Equal(EdgePerchPhase.Attached, h.Platforms.PerchPhase);
        var beforeCheek = calls;
        PressImage(h, h.Presenter!.EdgePerchImage, new(24, 52)); h.Tick();
        Assert.True(h.Direct.IsAttachedCheek);
        Assert.Equal(beforeCheek + 1, calls);
        h.Loop.NotifyDirectInteractionCanceled();
        var afterCancel = calls;
        h.Tick();
        Assert.Equal(EdgePerchPhase.None, h.Platforms.PerchPhase);
        Assert.Equal(afterCancel, calls);
    });
}
