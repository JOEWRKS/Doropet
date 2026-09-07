using System.Runtime.ExceptionServices;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Controls;

public sealed class BodyDragFootAnchorTests
{
    // Literal offsets measured from the approved source rasters, not the alignment helper.
    [Theory]
    [InlineData(false, 0, "body-drag-entry-00-press.png", 0)]
    [InlineData(false, 1, "body-drag-entry-01-release.png", 0)]
    [InlineData(false, 2, "body-drag-entry-02-lengthen.png", 9)]
    [InlineData(false, 3, "body-drag-entry-03-drop.png", 6)]
    [InlineData(false, 4, "body-drag-entry-04-stretch.png", 3)]
    [InlineData(false, 5, "body-drag-entry-05-dangle.png", 0)]
    [InlineData(false, 6, "body-drag-entry-06-near-hang.png", -3)]
    [InlineData(false, 7, "body-drag-entry-07-hang.png", -6)]
    [InlineData(true, 0, "body-drag-settle-00-hang.png", -6)]
    [InlineData(true, 1, "body-drag-settle-01-lift.png", -1)]
    [InlineData(true, 2, "body-drag-settle-02-gather.png", 4)]
    [InlineData(true, 3, "body-drag-settle-03-land.png", 9)]
    [InlineData(true, 4, "body-drag-settle-04-recover.png", 0)]
    public void Historical_asset_registration_keeps_the_rest_sole_height_and_original_texels(
        bool settle, int index, string asset, int offsetY)
    {
        RunOnSta(() =>
        {
            var presenter = new DororongPresenter();
            var actual = Render(presenter, settle, index / (settle ? 4.0 : 7.0));
            Assert.Equal(86, SoleRow(actual));
            var original = PremultipliedFrame.From(new BitmapImage(new Uri(
                $"pack://application:,,,/Dororong.App;component/Assets/{asset}")));
            var expected = new byte[96 * 96 * 4];
            for (var y = 0; y < 96; y++)
            for (var x = 0; x < 96; x++)
            {
                var sourceIndex = (y * 96 + x) * 4;
                var destinationY = y + offsetY;
                if (destinationY < 0 || destinationY >= 96)
                {
                    Assert.Equal(0, original.Pixels[sourceIndex + 3]);
                    continue;
                }
                Array.Copy(original.Pixels, sourceIndex, expected, (destinationY * 96 + x) * 4, 4);
            }
            Assert.Equal(expected, actual.Pixels);
        });
    }

    [Fact]
    public void Historical_registration_remains_compatible_with_the_unchanged_generic_blender()
    {
        RunOnSta(() =>
        {
            var presenter = new DororongPresenter();
            foreach (var settle in new[] { false, true })
            {
                // Includes every midpoint and several sub-key samples on both playback routes.
                var segments = settle ? 4 : 7;
                for (var step = 0; step <= segments * 8; step++)
                {
                    var frame = Render(presenter, settle, step / (segments * 8.0));
                    // Different foot shapes can fade below alpha128 at the last
                    // antialiased row even when both keys share the same anchor.
                    // The exact blend check below forbids post-blend shifting.
                    Assert.InRange(SoleRow(frame), 85, 86);
                }
            }

            var outgoing = Render(presenter, false, 1.0 / 7);
            var incoming = Render(presenter, false, 2.0 / 7);
            var middle = Render(presenter, false, 1.5 / 7);
            var expected = outgoing.Pixels.Zip(incoming.Pixels,
                (a, b) => (byte)Math.Round((a + b) / 2.0, MidpointRounding.AwayFromZero)).ToArray();
            Assert.Equal(expected, middle.Pixels);
        });
    }

    private static PremultipliedFrame Render(DororongPresenter presenter, bool settle, double progress)
    {
        // Historical utility coverage only. Runtime selection of the user's eight
        // frames is tested separately by SuppliedBodyDragFramesTests.
        string[] keys = settle
            ? ["settle-00-hang", "settle-01-lift", "settle-02-gather", "settle-03-land", "settle-04-recover"]
            : ["entry-00-press", "entry-01-release", "entry-02-lengthen", "entry-03-drop", "entry-04-stretch", "entry-05-dangle", "entry-06-near-hang", "entry-07-hang"];
        var reference = PremultipliedFrame.From(new BitmapImage(new Uri("pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png")));
        var frames = keys.Select(key => BodyDragFootAlignment.Align(PremultipliedFrame.From(new BitmapImage(new Uri(
            $"pack://application:,,,/Dororong.App;component/Assets/body-drag-{key}.png"))), reference)).ToArray();
        return PremultipliedFrame.From(new PremultipliedFrameSequence(frames).Sample(progress));
    }

    [Fact]
    public void Sole_registration_ignores_but_preserves_faint_fringe_pixels()
    {
        RunOnSta(() =>
        {
            var reference = TinyFrame(0, 0, 255, 0);
            var original = TinyFrame(0, 255, 20, 0);
            var aligned = BodyDragFootAlignment.Align(original, reference);
            Assert.Equal(new byte[] { 0, 0, 255, 20 },
                Enumerable.Range(0, 4).Select(y => aligned.Pixels[y * 4 + 3]).ToArray());
            Assert.Equal(new byte[] { 0, 255, 20, 0 },
                Enumerable.Range(0, 4).Select(y => original.Pixels[y * 4 + 3]).ToArray());
            Assert.Same(reference, BodyDragFootAlignment.Align(reference, reference));
        });
    }

    [Fact]
    public void Registration_rejects_clipping_even_one_faint_pixel()
    {
        RunOnSta(() =>
        {
            Assert.Throws<InvalidOperationException>(() =>
                BodyDragFootAlignment.Align(TinyFrame(0, 255, 0, 1), TinyFrame(0, 0, 255, 0)));
            Assert.Throws<InvalidOperationException>(() =>
                BodyDragFootAlignment.Align(TinyFrame(0, 0, 0, 0), TinyFrame(0, 0, 255, 0)));
        });
    }

    private static PremultipliedFrame TinyFrame(params byte[] alpha)
    {
        var pixels = new byte[alpha.Length * 4];
        for (var y = 0; y < alpha.Length; y++) pixels[y * 4 + 3] = alpha[y];
        return PremultipliedFrame.From(BitmapSource.Create(1, alpha.Length, 96, 96,
            System.Windows.Media.PixelFormats.Pbgra32, null, pixels, 4));
    }

    private static int SoleRow(PremultipliedFrame frame)
    {
        for (var y = frame.Source.PixelHeight - 1; y >= 0; y--)
        for (var x = 0; x < frame.Source.PixelWidth; x++)
            if (frame.Pixels[y * frame.Stride + x * 4 + 3] >= 128) return y;
        return -1;
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { failure = ex; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
