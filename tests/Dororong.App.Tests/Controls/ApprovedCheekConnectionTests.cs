using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;

namespace Dororong.App.Tests.Controls;

public sealed class ApprovedCheekConnectionTests
{
    [Fact]
    public void Actual_presenter_matches_all_41_frozen_approved_motion_frames() => CheekProductTests.Sta(() =>
    {
        var presenter = CheekLiveConnectionTests.Idle();
        var capture = (CheekPullCapture)CheekLiveConnectionTests.Capture(presenter);
        for (var ms = 0; ms <= 800; ms += 20)
        {
            var pull = ms < 240 ? ms / 12d : 20;
            if (ms > 400) { var t = Math.Clamp((ms - 400) / 220d, 0, 1); pull = 20 * (1 - t * t * (3 - 2 * t)); }
            Draw(presenter, capture, pull, ms > 400, ms == 0 ? 0 : 20);
            Assert.True(Read($"motion-{ms:D3}.png").SequenceEqual(Pixels(presenter)), $"Approved motion mismatch at {ms} ms");
            if (ms == 400) Draw(presenter, capture, pull, true, 0);
        }
    });

    [Fact]
    public void Actual_maximum_is_rounded_and_matches_approved_outline_not_old_pointed_renderer() => CheekProductTests.Sta(() =>
    {
        var presenter = CheekLiveConnectionTests.Idle();
        var capture = (CheekPullCapture)CheekLiveConnectionTests.Capture(presenter);
        Draw(presenter, capture, 20, false, 10000);
        var pixels = Pixels(presenter);
        Assert.True(Enumerable.Range(53, 12).Count(y => pixels[(y * 96 + 3) * 4 + 3] >= 128) >= 4, "Approved tip must have a rounded, non-pointed span");
        Assert.Equal(Read("max-native.png"), pixels);
        Assert.NotEqual(CheekProductTests.Read("max-native.png"), pixels);
    });

    [Fact]
    public void Timed_render_drives_delayed_eye_hair_and_first_release_tick_holds_them() => CheekProductTests.Sta(() =>
    {
        var presenter = CheekLiveConnectionTests.Idle();
        var capture = (CheekPullCapture)CheekLiveConnectionTests.Capture(presenter);
        Draw(presenter, capture, 20, false, 0); var initial = Pixels(presenter);
        Draw(presenter, capture, 20, false, 100); var followed = Pixels(presenter);
        Assert.False(initial.SequenceEqual(followed), "Eye/hair follow must consume actual elapsed time");
        Draw(presenter, capture, 20, true, 73); Assert.Equal(followed, Pixels(presenter));
        Draw(presenter, capture, 0, true, 220); Assert.Equal(Read("rest-native.png"), Pixels(presenter));
        presenter.Render(new(PetState.Idle, new(100, 100), FacingDirection.Right, 0, false, null), DirectInteractionSnapshot.None);
        Draw(presenter, capture, 20, false, 0); Assert.Equal(initial, Pixels(presenter));
    });

    [Fact]
    public void Captured_pixels_stay_frozen_and_zero_follow_does_not_change_body_or_accessories() => CheekProductTests.Sta(() =>
    {
        var source = CheekProductTests.Read("canonical.png"); var saved = (byte[])source.Clone();
        var capture = new CheekPullCapture(source, Matrix.Identity, FacingDirection.Right);
        Array.Clear(source);
        Assert.Equal(Read("max-native.png"), capture.Render(20));
        Assert.Equal(saved, capture.Render(0));
        Assert.Equal(CheekProductTests.Raster(saved, -10), capture.Render(-10));
    });

    internal static void Draw(DororongPresenter presenter, CheekPullCapture capture, double pull, bool release, double elapsed)
    {
        var direct = new DirectInteractionSnapshot(DirectInteractionTarget.RightCheek,
            release ? DirectInteractionPhase.CheekRelease : DirectInteractionPhase.CheekPull,
            new(100, 100), new(80, 100), 1, 0, !release) { CheekPull = new(capture, pull) };
        presenter.Render(new(PetState.Idle, new(100, 100), FacingDirection.Right, 0, false, null), direct, TimeSpan.FromMilliseconds(elapsed));
    }

    [Theory]
    [InlineData(.67)]
    [InlineData(.71)]
    public void Blink_capture_retains_its_own_eye_art_and_protected_source_through_release(double phase) => CheekProductTests.Sta(() =>
    {
        var presenter = new DororongPresenter();
        var pet = new PetSnapshot(PetState.Idle, new(100, 100), FacingDirection.Right, phase, false, null);
        presenter.Render(pet, DirectInteractionSnapshot.None, TimeSpan.FromMilliseconds(2000)); CheekProductTests.Layout(presenter);
        var original = CheekLiveConnectionTests.Pixels((Image)presenter.FindName("DororongImage"));
        Assert.NotEqual(Read("rest-native.png"), original);
        var capture = (CheekPullCapture)CheekLiveConnectionTests.Capture(presenter);
        Draw(presenter, capture, 20, false, 10000);
        var pulled = Pixels(presenter);
        Assert.NotEqual(Read("max-native.png"), pulled);
        for (var y = 0; y < 96; y++) for (var x = 0; x < 96; x++)
            if (x >= 54 || y < 23 || y >= 71)
                Assert.Equal(original.AsSpan((y * 96 + x) * 4, 4).ToArray(), pulled.AsSpan((y * 96 + x) * 4, 4).ToArray());
        Draw(presenter, capture, 20, true, 16);
        Draw(presenter, capture, 0, true, 220); Assert.Equal(original, Pixels(presenter));
        presenter.Render(pet, DirectInteractionSnapshot.None); CheekProductTests.Layout(presenter);
        Assert.Equal(original, CheekLiveConnectionTests.Pixels((Image)presenter.FindName("DororongImage")));
    });

    [Fact]
    public void Actual_presenter_export_and_diagnostic_timing() => CheekProductTests.Sta(() =>
    {
        var presenter = CheekLiveConnectionTests.Idle();
        var capture = (CheekPullCapture)CheekLiveConnectionTests.Capture(presenter);
        var frames = new List<BitmapSource>();
        foreach (var pull in new[] { 0d, 10, 20 })
        {
            Draw(presenter, capture, pull, false, 10000); CheekProductTests.Layout(presenter);
            var actual = (BitmapSource)CheekLiveConnectionTests.Overlay(presenter).Source;
            Assert.Equal(Read(pull == 0 ? "rest-native.png" : pull == 10 ? "half-native.png" : "max-native.png"), Pixels(presenter));
            frames.Add(actual);
        }
        var destination = Environment.GetEnvironmentVariable("DORORONG_CHEEK_PROOF");
        if (destination is null) return;
        Assert.False(Directory.Exists(destination)); Directory.CreateDirectory(destination);
        for (var i = 0; i < frames.Count; i++) Save(Path.Combine(destination, $"actual-{i}.png"), frames[i]);
        var desktopSize = new RenderTargetBitmap(144, 144, 96, 96, PixelFormats.Pbgra32); desktopSize.Render(presenter);
        Save(Path.Combine(destination, "actual-presenter-max.png"), desktopSize);
        var old = CheekProductTests.Read("max-native.png");
        var oldImage = BitmapSource.Create(96, 96, 96, 96, PixelFormats.Bgra32, null, old, 384);
        var visual = new DrawingVisual(); RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(17, 19, 27)), null, new Rect(0, 0, 768, 384));
            drawing.DrawImage(oldImage, new Rect(0, 0, 384, 384));
            drawing.DrawImage(frames[2], new Rect(384, 0, 384, 384));
        }
        var sheet = new RenderTargetBitmap(768, 384, 96, 96, PixelFormats.Pbgra32); sheet.Render(visual);
        Save(Path.Combine(destination, "old-left-applied-right-4x.png"), sheet);
        for (var i = 0; i < 10; i++) Draw(presenter, capture, 20, false, 16);
        var timings = new double[80]; var allocation = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < timings.Length; i++)
        {
            var timer = Stopwatch.StartNew(); Draw(presenter, capture, 20, false, 16); timer.Stop(); timings[i] = timer.Elapsed.TotalMilliseconds;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocation; Array.Sort(timings);
        File.WriteAllText(Path.Combine(destination, "diagnostic-timing.json"), JsonSerializer.Serialize(new
        {
            kind = "local synthetic Presenter.Render; not live desktop FPS", samples = timings.Length,
            medianMs = timings[40], p95Ms = timings[75], maximumMs = timings[^1], bytesPerFrame = allocated / timings.Length
        }, new JsonSerializerOptions { WriteIndented = true }));
    });

    private static void Save(string path, BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }

    internal static byte[] Pixels(DororongPresenter presenter) => CheekLiveConnectionTests.Pixels(CheekLiveConnectionTests.Overlay(presenter));
    internal static byte[] Read(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DororongDesktopPet.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var path = Path.Combine(directory.FullName, "tests", "Dororong.App.Tests", "Fixtures", "ApprovedRoundedCheek", name);
        var decoder = BitmapDecoder.Create(new Uri(path), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var bitmap = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
        var pixels = new byte[96 * 96 * 4]; bitmap.CopyPixels(pixels, 384, 0); return pixels;
    }
}
