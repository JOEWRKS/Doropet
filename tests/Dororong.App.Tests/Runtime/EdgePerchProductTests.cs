using System.IO;
using System.Security.Cryptography;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public partial class PetLoopPlatformTests
{
    [Fact]
    public void Released_full_head_carry_enters_image09_perch_without_platform_landing() => Controls.CheekProductTests.Sta(() =>
    {
        using var h = new Harness(realPresenter: true, perchEnabled: true);
        h.Start();
        h.Native.Scene = Scene(x: 0, y: 186);
        h.Press(0);
        h.Tick(80);
        h.Pointer = new PointerSample(true, h.Pointer.Position + new PointD(100, 0));
        h.Tick(80);
        Assert.Equal(DirectInteractionPhase.BodyDragHold, h.Direct.Phase);
        Assert.Equal(new PointD(200, 100), h.Position);

        h.Down = false;
        h.Tick();
        for (var i = 0; i < 10; i++) h.Tick();

        Assert.Equal(new PointD(200, 92), h.Position);
        Assert.Equal(DirectInteractionTarget.None, h.Direct.Target);
        Assert.Null(h.LastPose);
        Assert.Equal(h.Position, h.Core.Position);
        Assert.Equal(1, h.WritesThisTick);
    });

    [Fact]
    public void Approved_image09_is_an_exact_embedded_source_copy()
    {
        var root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "src"))) root = Directory.GetParent(root)!.FullName;
        var path = Path.Combine(root, "src", "Dororong.App", "Assets", "dororong-edge-perch-09.png");

        Assert.True(File.Exists(path), $"Missing approved perch source: {path}");
        Assert.Equal(
            "3CE4BE5308759D35BA828237208521A085378180AAD035CA0474DA3E551F57C2",
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Actual_head_and_body_carry_state_can_attach_but_local_sessions_cannot(int target) => Controls.CheekProductTests.Sta(() =>
    {
        using var h = new Harness(realPresenter: true, perchEnabled: true);
        h.Start(); h.Press(target); h.Tick(80);
        h.Pointer = new PointerSample(true, h.Pointer.Position + (target == 6 ? new PointD(-120, 0) : new PointD(120, 0)));
        for (var i = 0; i < (target == 0 ? 2 : 100); i++) h.Tick(16);
        var carried = h.Position;
        Assert.True(Math.Abs(carried.X - 100) > 20);
        h.Native.Scene = Scene(x: carried.X - 100, y: carried.Y + 86);
        h.Tick(80);
        carried = h.Position;
        h.Native.Scene = Scene(x: carried.X - 100, y: carried.Y + 86);
        h.Tick(80);
        carried = h.Position;

        h.Down = false; h.Tick();
        for (var i = 0; i < 10; i++) h.Tick();

        Assert.Equal(EdgePerchPhase.Attached, h.Platforms.PerchPhase);
        Assert.Equal(carried.X, h.Position.X, 5);
        Assert.Equal(carried.Y - 8, h.Position.Y, 5);
        Assert.Null(h.LastPose);
    });

    [Fact]
    public void Local_body_release_and_unavailable_pointer_cannot_create_a_perch() => Controls.CheekProductTests.Sta(() =>
    {
        using var local = new Harness(realPresenter: true, perchEnabled: true);
        local.Start(); local.Press(1); local.Tick(16);
        local.Native.Scene = Scene(x: 0, y: 186); local.Tick(80);
        local.Down = false; local.Tick();
        Assert.Equal(EdgePerchPhase.None, local.Platforms.PerchPhase);
        Assert.NotNull(local.LastPose);

        using var unavailable = new Harness(realPresenter: true, perchEnabled: true);
        unavailable.Start(); unavailable.Press(0); unavailable.Tick(80);
        unavailable.Pointer = new(true, unavailable.Pointer.Position + new PointD(100, 0)); unavailable.Tick(80);
        unavailable.Native.Scene = Scene(x: 0, y: 186); unavailable.Tick(80);
        unavailable.Pointer = PointerSample.Unavailable; unavailable.Down = false; unavailable.Tick();
        Assert.Equal(EdgePerchPhase.None, unavailable.Platforms.PerchPhase);
        Assert.NotNull(unavailable.LastPose);
    });

    [Fact]
    public void Regrab_during_entry_starts_at_the_actual_displayed_position_without_a_stale_pose() => Controls.CheekProductTests.Sta(() =>
    {
        using var h = AttachedHarness(advanceTicks: 3);
        var displayed = h.Position;

        h.Press(0); h.Tick();

        Assert.Equal(EdgePerchPhase.None, h.Platforms.PerchPhase);
        Assert.Equal(displayed, h.Position);
        Assert.True(h.Core.IsDirectInteractionPending);
        Assert.Equal(DirectInteractionPhase.BodyPending, h.Direct.Phase);
        Assert.Null(h.LastPose);
    });

    [Fact]
    public void Regrab_during_natural_handoff_cancels_the_wipe_even_after_the_engine_has_detached() => Controls.CheekProductTests.Sta(() =>
    {
        using var h = AttachedHarness(advanceTicks: 10);
        var presenter = h.Presenter!;
        var canonical = Assert.IsType<Dororong.App.Controls.AlphaHitTestImage>(presenter.FindName("DororongImage"));
        var perch = presenter.EdgePerchImage;
        h.Native.Scene = Scene(windows: false);
        h.Tick(80);
        Assert.Equal(EdgePerchPhase.None, h.Platforms.PerchPhase);
        Assert.Equal(System.Windows.Visibility.Visible, perch.Visibility);
        Assert.NotNull(perch.Clip);
        Assert.NotNull(canonical.Clip);

        h.Press(0);
        h.Tick(16);

        Assert.Equal(DirectInteractionPhase.BodyPending, h.Direct.Phase);
        Assert.Equal(System.Windows.Visibility.Collapsed, perch.Visibility);
        Assert.Null(canonical.Clip);
    });

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    public void Body_or_cheek_regrab_retires_perch_before_the_new_overlay_owns_canonical_visibility(int target) => Controls.CheekProductTests.Sta(() =>
    {
        using var h = AttachedHarness(advanceTicks: 10);
        var presenter = h.Presenter!;
        var canonical = Assert.IsType<Dororong.App.Controls.AlphaHitTestImage>(presenter.FindName("DororongImage"));
        var perch = presenter.EdgePerchImage;
        h.Native.Scene = Scene(windows: false);
        h.Tick(80);
        Assert.Equal(EdgePerchPhase.None, h.Platforms.PerchPhase);
        Assert.Equal(System.Windows.Visibility.Visible, perch.Visibility);

        h.Press(target);
        h.Tick(16);

        Assert.Equal(System.Windows.Visibility.Collapsed, perch.Visibility);
        Assert.Equal(System.Windows.Visibility.Hidden, canonical.Visibility);
        Assert.True(target == 1 ? h.Direct.BodyPull?.Capture is not null : h.Direct.CheekPull is not null);
    });

    [Fact]
    public void Owner_translation_follows_once_and_destruction_falls_once_from_the_visible_anchor() => Controls.CheekProductTests.Sta(() =>
    {
        using var h = AttachedHarness(advanceTicks: 10);
        Assert.Equal(new PointD(200, 92), h.Position);
        h.Native.Scene = Scene(x: 30, y: 176); h.Tick(80);
        Assert.Equal(new PointD(230, 82), h.Position);
        h.Tick(80); Assert.Equal(new PointD(230, 82), h.Position);

        h.Native.Scene = Scene(windows: false); h.Tick(80);
        Assert.Equal(EdgePerchPhase.None, h.Platforms.PerchPhase);
        Assert.Equal(PlatformPhase.Falling, h.LastPose?.Phase);
        Assert.True(h.Position.Y > 82);
        var previous = h.Position.Y; h.Tick(); Assert.True(h.Position.Y > previous);
        var landings = 0; var before = h.LastPose?.Phase;
        for (var i = 0; i < 100; i++)
        {
            h.Tick();
            if (h.LastPose?.Phase == PlatformPhase.Landing && before != PlatformPhase.Landing) landings++;
            before = h.LastPose?.Phase;
        }
        Assert.Equal(1, landings);
    });

    [Fact]
    public void Temporary_scene_failure_freezes_then_expiry_releases_to_the_retained_monitor_floor() => Controls.CheekProductTests.Sta(() =>
    {
        using var h = AttachedHarness(advanceTicks: 10);
        var attached = h.Position; h.Native.Fail = true;
        h.Tick(80); Assert.Equal(attached, h.Position); Assert.Equal(EdgePerchPhase.Attached, h.Platforms.PerchPhase);
        var expiryTicks = 0;
        while (h.Platforms.PerchPhase != EdgePerchPhase.None && expiryTicks++ < 7) h.Tick(80);
        Assert.InRange(expiryTicks, 5, 7);
        Assert.Equal(EdgePerchPhase.None, h.Platforms.PerchPhase);
        Assert.Equal(PlatformPhase.Falling, h.LastPose?.Phase);
        Assert.True(h.Position.Y > attached.Y);
    });

    [Fact]
    public void Capture_cancellation_cannot_be_replayed_as_release_intent_and_dispose_clears_only_perch_visuals() => Controls.CheekProductTests.Sta(() =>
    {
        using (var canceled = new Harness(realPresenter: true, perchEnabled: true))
        {
            canceled.Start(); canceled.Press(0); canceled.Tick(80);
            canceled.Pointer = new(true, canceled.Pointer.Position + new PointD(100, 0)); canceled.Tick(80);
            canceled.Native.Scene = Scene(x: 0, y: 186); canceled.Tick(80);
            canceled.Loop.NotifyDirectInteractionCanceled(); canceled.Down = false; canceled.Tick();
            Assert.Equal(EdgePerchPhase.None, canceled.Platforms.PerchPhase);
            Assert.NotNull(canceled.LastPose);
        }

        var attached = AttachedHarness(advanceTicks: 10);
        var presenter = attached.Presenter!;
        var canonical = Assert.IsType<Dororong.App.Controls.AlphaHitTestImage>(presenter.FindName("DororongImage"));
        var perch = presenter.EdgePerchImage;
        attached.Dispose();
        Assert.Equal(EdgePerchPhase.None, attached.Platforms.PerchPhase);
        Assert.Equal(System.Windows.Visibility.Visible, canonical.Visibility);
        Assert.Equal(System.Windows.Visibility.Collapsed, perch.Visibility);
        Assert.Null(attached.LastPose);
    });

    private static Harness AttachedHarness(int advanceTicks)
    {
        var h = new Harness(realPresenter: true, perchEnabled: true);
        h.Start(); h.Native.Scene = Scene(x: 0, y: 186); h.Press(0); h.Tick(80);
        h.Pointer = new(true, h.Pointer.Position + new PointD(100, 0)); h.Tick(80);
        h.Down = false; h.Tick();
        for (var i = 0; i < advanceTicks; i++) h.Tick();
        Assert.NotEqual(EdgePerchPhase.None, h.Platforms.PerchPhase);
        return h;
    }
}
