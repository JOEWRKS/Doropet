using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Controls;

public sealed class BodyPullPresentationTests
{
    [Fact]
    public void Supported_reach_fits_the_unchanged_window_for_actual_awake_visible_transforms() => Sta(() =>
    {
        var p = new DororongPresenter();
        foreach (var state in new[] { PetState.Idle, PetState.Walk, PetState.Curious, PetState.Startled })
            foreach (var facing in new[] { FacingDirection.Left, FacingDirection.Right })
            {
                p.Render(new(state, default, facing, .31, false, null), DirectInteractionSnapshot.None); Layout(p);
                foreach (var anchor in new PointD[] { new(23, 77), new(42, 82), new(66, 80), new(53, 70), new(70, 61) })
                {
                    Assert.True(p.TryCreateBodyPullCapture(anchor, out var capture));
                    for (var d = 0; d < 8; d++)
                    {
                        var (root, tip) = BodyRegionMap.Limb(capture!.Region, anchor);
                        var pull = new PointD(32 * Math.Cos(d * Math.PI / 4), 32 * Math.Sin(d * Math.PI / 4)) - (tip - root);
                        var output = BodyPullRenderer.Render(capture.Pixels, capture.Region, pull, anchor);
                        for (var y = 0; y < 160; y++) for (var x = 0; x < 160; x++)
                            {
                                if (output[(y * 160 + x) * 4 + 3] == 0) continue;
                                var q = capture.SourceToWindow.Transform(new Point(x - 32 + .5, y - 32 + .5));
                                Assert.True(q.X >= 0 && q.X < 144 && q.Y >= 0 && q.Y < 144, $"{state} {facing} {capture.Region} direction{d}: outside144 at {q}");
                            }
                    }
                }
            }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Captured_visible_transform_and_frozen_source_survive_facing_change_then_restore(bool mirror) => Sta(() =>
    {
        var p = new DororongPresenter();
        p.Render(new(PetState.Walk, new(100, 100), mirror ? FacingDirection.Left : FacingDirection.Right, .1, false, null), DirectInteractionSnapshot.None);
        Layout(p);
        Assert.Equal(DirectInteractionTarget.FiveRegionBody, p.ClassifyOpaqueSourcePoint(new(23, 77), true));
        Assert.True(p.TryCreateBodyPullCapture(new(23.25, 77.5), out var captured));
        var capture = captured!;
        Assert.Equal(mirror ? -1 : 1, Math.Sign(capture.SourceToWindow.M11));
        var oldSource = ((Image)p.FindName("DororongImage")).Source;
        var s = new BodyPullSession(); s.Begin(capture, new(100, 100), new(123, 177)); s.Move(new(133, 177)); s.Tick(160);
        var direct = new DirectInteractionSnapshot(DirectInteractionTarget.FiveRegionBody, DirectInteractionPhase.BodyLocalPull, default, default, 0, 0, true, s.Current);
        p.Render(new(PetState.Idle, new(100, 100), FacingDirection.Right, .7, false, null), direct); Layout(p);
        var original = (Image)p.FindName("DororongImage");
        Assert.Equal(Visibility.Hidden, original.Visibility);
        var overlay = ((Canvas)p.Content).Children.OfType<AlphaHitTestImage>().Single();
        Assert.Equal(160, ((BitmapSource)overlay.Source).PixelWidth);
        Assert.False(overlay.TryGetOpaqueSourcePoint(new Point(0, 0), out _));
        var actual = overlay.TransformToAncestor(p).Transform(new(BodyPullRenderer.Pad + 23.25, BodyPullRenderer.Pad + 77.5));
        var expected = capture.SourceToWindow.Transform(new Point(23.25, 77.5));
        Assert.Equal(expected.X, actual.X, 6); Assert.Equal(expected.Y, actual.Y, 6);
        p.Render(new(PetState.Idle, new(100, 100), FacingDirection.Right, 0, false, null), DirectInteractionSnapshot.None); Layout(p);
        Assert.Equal(Visibility.Visible, original.Visibility);
        Assert.Empty(((Canvas)p.Content).Children.OfType<AlphaHitTestImage>());
        Assert.Equal(96, original.Width); Assert.Equal(144, p.Width);
    });

    [Fact]
    public void Noncanonical_sleep_and_transparent_points_cannot_start_five_body_capture() => Sta(() =>
    {
        var p = new DororongPresenter(); p.Render(new(PetState.Idle, default, FacingDirection.Right, 0, false, null), DirectInteractionSnapshot.None); Layout(p);
        Assert.False(p.TryCreateBodyPullCapture(new(70, 90), out _));
        Assert.Equal(DirectInteractionTarget.None, p.ClassifyOpaqueSourcePoint(new(23, 77), false));
        p.Render(new(PetState.Sleep, default, FacingDirection.Right, .2, false, null), DirectInteractionSnapshot.None);
        Assert.False(p.TryCreateBodyPullCapture(new(23, 77), out _));
        Assert.Equal(DirectInteractionTarget.Body, p.ClassifyOpaqueSourcePoint(new(23, 77), true));
    });

    private static void Layout(FrameworkElement p) { p.Measure(new Size(144, 144)); p.Arrange(new Rect(0, 0, 144, 144)); p.UpdateLayout(); }
    private static void Sta(Action action)
    {
        Exception? error = null; var t = new Thread(() => { try { action(); } catch (Exception e) { error = e; } }); t.SetApartmentState(ApartmentState.STA); t.Start(); t.Join();
        if (error != null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
