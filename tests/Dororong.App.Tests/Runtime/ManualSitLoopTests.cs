using System.Windows;
using System.Windows.Controls;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.App.Runtime;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public sealed class ManualSitLoopTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Product_seated_body_drag_keeps_the_seated_art_and_hold(bool supported) => Controls.EdgePerchPresentationTests.Sta(() =>
    {
        using var h = new Harness(supported); h.Loop.Start();
        if (supported) h.Tick(40);
        h.Sit(); h.Tick(15);
        var held = h.Position; var source = h.Image.Source;
        var press = Assert.IsType<DirectInteractionPressEventArgs>(h.Presenter.CreateDirectPress((AlphaHitTestImage)h.Image, new(53, 70)));
        Assert.Equal(DirectInteractionTarget.ClickOnly, press.Target);
        h.Down = true; h.Pointer = new(true, h.Position + press.WindowLocalPosition);
        h.Loop.NotifyDirectInteractionPressed(press); h.Tick();
        h.Pointer = new(true, h.Pointer.Position + new PointD(90, -50)); h.Tick(8);
        Assert.Equal(held, h.Position);
        Assert.Same(source, h.Image.Source);
        h.Down = false; h.Tick(40);
        Assert.Equal(held, h.Position);
        Assert.True(ReferenceEquals(LocomotionFrames.Sit(1), h.Image.Source) || ReferenceEquals(LocomotionFrames.Sit(1, true), h.Image.Source));
    });

    [Fact]
    public void Sit_command_stops_an_active_walk_and_blinks_while_held() => Controls.EdgePerchPresentationTests.Sta(() =>
    {
        using var h = new Harness(); h.Loop.Start(); h.Tick(23);
        var heldPosition = h.Position;
        h.Sit(); h.Tick(7);
        Assert.Equal(heldPosition, h.Position);
        Assert.Same(LocomotionFrames.Sit(1), h.Image.Source);
        var frames = new HashSet<object>();
        for (var i = 0; i < 40; i++) { h.Tick(); frames.Add(h.Image.Source); Assert.Equal(heldPosition, h.Position); }
        Assert.Contains(LocomotionFrames.Sit(1, true), frames);
        Assert.Contains(LocomotionFrames.Sit(1), frames);
    });

    [Fact]
    public void Mere_head_click_keeps_sitting_and_position_locked() => Controls.EdgePerchPresentationTests.Sta(() =>
    {
        using var h = new Harness(); h.Loop.Start(); h.Sit(); h.Tick(7);
        var heldPosition = h.Position;
        h.PressHead(); h.Tick(); h.Down = false; h.Tick(25);
        Assert.Equal(heldPosition, h.Position);
        Assert.True(ReferenceEquals(LocomotionFrames.Sit(1), h.Image.Source) || ReferenceEquals(LocomotionFrames.Sit(1, true), h.Image.Source));
    });

    [Fact]
    public void Actual_head_drag_releases_hold_and_autonomous_walking_resumes() => Controls.EdgePerchPresentationTests.Sta(() =>
    {
        using var h = new Harness(); h.Loop.Start(); h.Sit(); h.Tick(7);
        Assert.Same(LocomotionFrames.Sit(1), h.Image.Source);
        var heldPosition = h.Position;
        h.PressHead(); h.Tick();
        h.Pointer = new(true, h.Pointer.Position + new PointD(30, -20)); h.Tick();
        Assert.Equal(PetState.Dragged, h.Snapshot.State);
        Assert.NotEqual(heldPosition, h.Position);
        h.Down = false; h.Tick(40);
        var afterRelease = h.Position; h.Tick(10);
        Assert.NotEqual(afterRelease, h.Position);
        Assert.NotSame(LocomotionFrames.Sit(1), h.Image.Source);
    });

    [Fact]
    public void Whole_paw_carry_releases_hold() => Controls.EdgePerchPresentationTests.Sta(() =>
    {
        using var h = new Harness(); h.Loop.Start(); h.Sit(); h.Tick(7);
        var held = h.Position;
        h.PressPart(false); h.Tick();
        h.Pointer = new(true, h.Pointer.Position + new PointD(-60, -15)); h.Tick(10);
        Assert.NotEqual(held, h.Position);
        h.Down = false; h.Tick(40);
        var released = h.Position; h.Tick(10);
        Assert.NotEqual(released, h.Position);
        Assert.NotSame(LocomotionFrames.Sit(1), h.Image.Source);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Seated_cheek_long_pull_turns_without_moving_or_releasing_hold(bool supported) => Controls.EdgePerchPresentationTests.Sta(() =>
    {
        using var h = new Harness(supported); h.Loop.Start();
        if (supported) h.Tick(40);
        h.Sit(); h.Tick(15);
        var held = h.Position;
        if (supported) Assert.Equal(PlatformPhase.Supported, h.LastPose?.Phase);
        h.PressPart(true); h.Tick(); var press = h.Pointer.Position;
        Point? supportCenter = null;
        foreach (var dx in new[] { -80d, 80d, -100d, 100d })
        {
            h.Pointer = new(true, press + new PointD(dx, -30)); h.Tick(8);
            Assert.Equal(held.X, h.Position.X, 6);
            Assert.Equal(held.Y, h.Position.Y, 6);
            if (supported) Assert.Equal(PlatformPhase.Supported, h.LastPose?.Phase);
            var cheek = Assert.IsType<CheekPullSnapshot>(h.Direct.CheekPull);
            Assert.Equal(20, cheek.PullDips);
            Assert.Equal(Math.Sign(dx), Math.Sign(cheek.Capture.OutwardUnit.X));
            var center = cheek.Capture.SourceToWindow.Transform(new Point(48, 86));
            supportCenter ??= center;
            Assert.Equal(supportCenter.Value, center);
        }
        h.Down = false; h.Tick(30);
        Assert.Equal(held.X, h.Position.X, 6);
        Assert.Equal(held.Y, h.Position.Y, 6);
        if (supported) Assert.Equal(PlatformPhase.Supported, h.LastPose?.Phase);
        Assert.True(ReferenceEquals(LocomotionFrames.Sit(1), h.Image.Source) || ReferenceEquals(LocomotionFrames.Sit(1, true), h.Image.Source));
        var face = h.Image.TranslatePoint(new Point(30, 55), h.Presenter);
        var rump = h.Image.TranslatePoint(new Point(65, 65), h.Presenter);
        Assert.True(face.X > rump.X, "The last rightward cheek turn must survive release while seated.");
    });

    [Fact]
    public void Local_cheek_tug_does_not_release_hold() => Controls.EdgePerchPresentationTests.Sta(() =>
    {
        using var h = new Harness(); h.Loop.Start(); h.Sit(); h.Tick(7);
        var held = h.Position;
        h.PressPart(true); h.Tick();
        h.Pointer = new(true, h.Pointer.Position + new PointD(-10, 0)); h.Tick(10);
        Assert.Equal(held, h.Position);
        h.Down = false;
        for (var i = 0; i < 10; i++)
        {
            h.Tick();
            if (h.Direct.Target == DirectInteractionTarget.None) break;
        }
        Assert.Equal(DirectInteractionTarget.None, h.Direct.Target);
        Assert.True(ReferenceEquals(LocomotionFrames.Sit(1), h.Image.Source) || ReferenceEquals(LocomotionFrames.Sit(1, true), h.Image.Source),
            "First frame after the captured cheek retires must still be fully seated.");
        h.Tick(40);
        Assert.Equal(held, h.Position);
        Assert.True(ReferenceEquals(LocomotionFrames.Sit(1), h.Image.Source) || ReferenceEquals(LocomotionFrames.Sit(1, true), h.Image.Source));
    });

    private sealed class Harness : IDisposable
    {
        internal readonly DororongPresenter Presenter = new();
        internal readonly PetLoop Loop;
        internal PointD Position = new(100, 100);
        internal PetSnapshot Snapshot;
        internal DirectInteractionSnapshot Direct;
        internal PlatformPose? LastPose;
        internal bool Down;
        internal PointerSample Pointer = PointerSample.Unavailable;
        internal Image Image => (Image)Presenter.FindName("DororongImage");
        private TimeSpan _elapsed;
        private EventHandler? _tick;
        internal Harness(bool supported = false)
        {
            void Render(PetSnapshot snapshot, DirectInteractionSnapshot direct, TimeSpan delta)
            {
                Snapshot = snapshot; Direct = direct; Presenter.RenderDesktop(snapshot, direct, delta);
                Presenter.Measure(new Size(144, 144)); Presenter.Arrange(new Rect(0, 0, 144, 144)); Presenter.UpdateLayout();
            }
            var host = new PetLoopHost(() => new(0, 0, 800, 600), () => new(144, 144), () => new(4, 4), () => Pointer, () => Down,
                () => Position, p => Position = p, (s, d) => Render(s, d, TimeSpan.Zero), () => true, () => { }, Render)
            {
                HoldLocomotionWalk = Presenter.HoldLocomotionWalk,
                SetLocomotionBlocked = Presenter.SetLocomotionBlocked,
                SetSittingRequested = Presenter.SetSittingRequested,
                Platforms = supported ? new PetPlatformRuntime(new(new TaskbarScene()), _ => new(new(), new(), 1, 1),
                    Presenter.MeasurePlatformGeometry, (pose, sole) => { LastPose = pose; Presenter.ApplyPlatformPose(pose, sole); }) : null
            };
            Loop = new(new(() => _elapsed, () => { }, () => { }), new(h => _tick += h, h => _tick -= h, () => { }, () => { }), host,
                _ => new(BehaviorTuning.Default with { IdleMin = TimeSpan.FromSeconds(2), IdleMax = TimeSpan.FromSeconds(2), IdleToWalkProbability = 1 }, new SeededRandomSource(1), Position));
            Loop.Faulted += (_, error) => throw new Exception("Loop fault", error);
            Presenter.SitRequested += (_, _) => Loop.NotifySitRequested();
        }
        internal void Sit() => ((FrameworkElement)Presenter.FindName("BodyGroup")).ContextMenu.Items.OfType<MenuItem>()
            .Single(item => Equals(item.Header, "앉아")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        internal void PressHead()
        {
            Down = true; Pointer = new(true, Position + new PointD(45, 45));
            Loop.NotifyDirectInteractionPressed(new(DirectInteractionTarget.Body, new(45, 45), new(21, 21), 1));
        }
        internal void PressPart(bool cheek)
        {
            var at = cheek ? new PointD(16, 58) : new PointD(23, 77);
            var local = Image.TranslatePoint(new Point(at.X, at.Y), Presenter);
            var windowPoint = new PointD(local.X, local.Y);
            Down = true; Pointer = new(true, Position + windowPoint);
            if (cheek)
            {
                Assert.True(Presenter.TryCreateCheekPullCapture(at, out var capture));
                Loop.NotifyDirectInteractionPressed(new(DirectInteractionTarget.RightCheek, windowPoint, at, 1) { CheekCapture = capture });
            }
            else
            {
                Assert.True(Presenter.TryCreateBodyPullCapture(at, out var capture));
                Loop.NotifyDirectInteractionPressed(new(DirectInteractionTarget.FiveRegionBody, windowPoint, at, 1, capture));
            }
        }
        internal void Tick(int count = 1) { for (var i = 0; i < count; i++) { _elapsed += TimeSpan.FromMilliseconds(100); _tick?.Invoke(this, EventArgs.Empty); } }
        public void Dispose() => Loop.Dispose();
    }

    private sealed class TaskbarScene : IDesktopSceneNative
    {
        public DesktopScene? Capture(TimeSpan now) => new(1, now,
            [new(1, new(0, 0, 800, 600))],
            [new(new(1, 1, 1), new(0, 560, 800, 40), 0, true, false, false, false, true, true, true, true)]);
    }
}
