using System.Runtime.ExceptionServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;

namespace Dororong.App.Tests.Controls;

public sealed class PremultipliedFrameSequenceTests
{
    [Fact]
    public void Sample_reuses_the_exact_endpoint_source_objects()
    {
        RunOnSta(() =>
        {
            var first = Frame(1, 2, 3, 4);
            var last = Frame(5, 6, 7, 8);
            var sequence = new PremultipliedFrameSequence([first, last]);

            Assert.Same(first.Source, sequence.Sample(0));
            Assert.Same(last.Source, sequence.Sample(1));
        });
    }

    [Fact]
    public void Sample_selects_adjacent_segments_across_three_frames()
    {
        RunOnSta(() =>
        {
            var first = Frame(0, 0, 0, 0);
            var middle = Frame(100, 100, 100, 100);
            var last = Frame(200, 200, 200, 200);
            var sequence = new PremultipliedFrameSequence([first, middle, last]);

            Assert.Same(middle.Source, sequence.Sample(0.5));
            Assert.Equal([50, 50, 50, 50], Pixels(sequence.Sample(0.25)));
            Assert.Equal([150, 150, 150, 150], Pixels(sequence.Sample(0.75)));
        });
    }

    [Fact]
    public void Sample_smoothsteps_and_interpolates_every_premultiplied_bgra_channel()
    {
        RunOnSta(() =>
        {
            var sequence = new PremultipliedFrameSequence(
            [
                Frame(0, 20, 40, 60),
                Frame(160, 180, 200, 220)
            ]);

            var sampled = sequence.Sample(0.5);

            Assert.Equal(PixelFormats.Pbgra32, sampled.Format);
            Assert.True(sampled.IsFrozen);
            Assert.Equal([80, 100, 120, 140], Pixels(sampled));
        });
    }

    [Fact]
    public void Constructor_rejects_frames_with_different_dimensions_or_strides()
    {
        RunOnSta(() =>
        {
            var canonical = Frame(1, 2, 3, 4);
            var differentDimensions = Frame(5, 6, 7, 8, width: 2, height: 1);
            var differentStride = new PremultipliedFrame(canonical.Source, canonical.Pixels, canonical.Stride + 4);

            Assert.Throws<ArgumentException>(() => new PremultipliedFrameSequence([canonical, differentDimensions]));
            Assert.Throws<ArgumentException>(() => new PremultipliedFrameSequence([canonical, differentStride]));
        });
    }

    [Fact]
    public void Sample_preserves_disjoint_outgoing_and_incoming_pixel_alpha_at_midpoint()
    {
        RunOnSta(() =>
        {
            var outgoing = Frame([0, 0, 0, 255, 0, 0, 0, 0], width: 2, height: 1);
            var incoming = Frame([0, 0, 0, 0, 0, 0, 0, 255], width: 2, height: 1);
            var sequence = new PremultipliedFrameSequence([outgoing, incoming]);

            Assert.Equal([0, 0, 0, 128, 0, 0, 0, 128], Pixels(sequence.Sample(0.5)));
        });
    }

    private static PremultipliedFrame Frame(byte blue, byte green, byte red, byte alpha, int width = 1, int height = 1) =>
        Frame(Enumerable.Repeat(new[] { blue, green, red, alpha }, width * height).SelectMany(pixel => pixel).ToArray(), width, height);

    private static PremultipliedFrame Frame(byte[] pixels, int width = 1, int height = 1) =>
        PremultipliedFrame.From(BitmapSource.Create(
            width,
            height,
            96,
            96,
            PixelFormats.Pbgra32,
            null,
            pixels,
            width * 4));

    private static byte[] Pixels(BitmapSource source)
    {
        var pixels = new byte[source.PixelHeight * source.PixelWidth * 4];
        source.CopyPixels(pixels, source.PixelWidth * 4, 0);
        return pixels;
    }

    private static void RunOnSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
