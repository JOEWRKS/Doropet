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
    public void Eligible_held_preview_maintains_taskbar_layer_after_render_and_stops_outside_range() => CheekProductTests.Sta(() =>
    {
        using var h = new Harness(realPresenter: true, perchEnabled: true);
        var calls = 0;
        var host = typeof(PetLoop).GetField("_host", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(h.Loop)!;
        typeof(PetLoopHost).GetProperty("MaintainPerchLayer", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(host, (Action)(() =>
        {
            Assert.True(h.Direct.IsPerchReady); // host has already rendered this tick's cue
            Assert.Equal(EdgePerchPhase.None, h.Platforms.PerchPhase);
            calls++;
        }));
        h.Start(); h.Tick();
        h.Native.Scene = Scene(x: 0, y: 186);
        h.Press(0); h.Tick(80);
        Assert.Equal(0, calls);
        h.Pointer = new(true, h.Pointer.Position + new PointD(100, 0)); h.Tick(80);
        Assert.True(h.Direct.IsPerchReady);
        Assert.Equal(1, calls);
        var held = h.Position;
        h.Tick();
        Assert.Equal(2, calls); // a later shell raise can be repaired during the hold
        Assert.Equal(held, h.Position);
        h.Native.Scene = Scene(x: 0, y: held.Y + 95); h.Tick(80);
        Assert.False(h.Direct.IsPerchReady);
        Assert.Equal(2, calls);
        h.Down = false; h.Tick();
        Assert.Equal(EdgePerchPhase.None, h.Platforms.PerchPhase);
        Assert.Equal(2, calls);
    });

    [Fact]
    public void Perch_layer_maintenance_runs_after_ready_or_active_render_including_attached_cheek() => CheekProductTests.Sta(() =>
    {
        using var h = new Harness(realPresenter: true, perchEnabled: true);
        var calls = 0;
        var host = typeof(PetLoop).GetField("_host", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(h.Loop)!;
        typeof(PetLoopHost).GetProperty("MaintainPerchLayer", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(host, (Action)(() =>
        {
            Assert.True(h.Direct.IsPerchReady || h.Platforms.PerchPhase != EdgePerchPhase.None);
            Assert.True(h.Direct.IsPerchReady || h.Presenter!.EdgePerchImage.Visibility == System.Windows.Visibility.Visible || h.Direct.IsAttachedCheek);
            calls++;
        }));
        h.Start(); h.Tick();
        Assert.Equal(0, calls);
        h.Native.Scene = Scene(x: 0, y: 186);
        h.Press(0); h.Tick(80);
        h.Pointer = new(true, h.Pointer.Position + new PointD(100, 0)); h.Tick(80);
        Assert.Equal(1, calls);
        h.Down = false; h.Tick();
        Assert.Equal(EdgePerchPhase.Entering, h.Platforms.PerchPhase);
        Assert.Equal(2, calls);
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
