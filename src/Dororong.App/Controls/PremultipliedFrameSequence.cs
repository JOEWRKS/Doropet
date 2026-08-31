using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dororong.App.Controls;

internal sealed class PremultipliedFrameSequence
{
    private readonly IReadOnlyList<PremultipliedFrame> _frames;

    internal PremultipliedFrameSequence(IReadOnlyList<PremultipliedFrame> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count == 0)
        {
            throw new ArgumentException("A frame sequence requires at least one frame.", nameof(frames));
        }

        var first = frames[0] ?? throw new ArgumentException("A frame sequence cannot contain null frames.", nameof(frames));
        for (var index = 1; index < frames.Count; index++)
        {
            var frame = frames[index] ?? throw new ArgumentException("A frame sequence cannot contain null frames.", nameof(frames));
            if (frame.Source.PixelWidth != first.Source.PixelWidth ||
                frame.Source.PixelHeight != first.Source.PixelHeight ||
                frame.Stride != first.Stride)
            {
                throw new ArgumentException("All frames must have equal dimensions and stride.", nameof(frames));
            }
        }

        _frames = frames;
    }

    internal BitmapSource Sample(double progress)
    {
        if (progress <= 0 || _frames.Count == 1)
        {
            return _frames[0].Source;
        }

        if (progress >= 1)
        {
            return _frames[^1].Source;
        }

        var scaledProgress = Math.Clamp(progress, 0, 1) * (_frames.Count - 1);
        var fromIndex = (int)Math.Floor(scaledProgress);
        var localProgress = scaledProgress - fromIndex;
        if (localProgress <= 0)
        {
            return _frames[fromIndex].Source;
        }

        var opacity = localProgress * localProgress * (3 - (2 * localProgress));
        var from = _frames[fromIndex];
        var to = _frames[fromIndex + 1];
        var pixels = new byte[from.Pixels.Length];
        for (var index = 0; index < pixels.Length; index++)
        {
            pixels[index] = (byte)Math.Round(
                from.Pixels[index] + ((to.Pixels[index] - from.Pixels[index]) * opacity),
                MidpointRounding.AwayFromZero);
        }

        var blended = BitmapSource.Create(
            from.Source.PixelWidth,
            from.Source.PixelHeight,
            96,
            96,
            PixelFormats.Pbgra32,
            null,
            pixels,
            from.Stride);
        blended.Freeze();
        return blended;
    }
}
