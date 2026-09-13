using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.CompilerServices;

namespace Dororong.App.Controls;

// The user's numbered artwork, not the historical body-drag entry/settle assets.
// Source resources remain exact100x100 PNG copies of the supplied JPEG decodes.
internal sealed class SuppliedBodyDragFrames
{
    private readonly BitmapSource[] _frames;
    private sealed record Position(double Value);
    private readonly ConditionalWeakTable<BitmapSource, Position> _positions = new();

    internal SuppliedBodyDragFrames(PremultipliedFrame footReference)
    {
        _frames = Enumerable.Range(1, 8).Select(number =>
        {
            var source = new BitmapImage();
            source.BeginInit();
            source.UriSource = new Uri($"pack://application:,,,/Dororong.App;component/Assets/user-body-drag/{number:D2}.png");
            source.CacheOption = BitmapCacheOption.OnLoad;
            source.EndInit();
            source.Freeze();
            return Prepare(PremultipliedFrame.From(source), footReference, number);
        }).ToArray();
    }

    internal BitmapSource Sample(double progress)
    {
        if (!double.IsFinite(progress)) throw new ArgumentOutOfRangeException(nameof(progress));
        var value = Math.Clamp(progress, 0, 1);
        var key = value * 7;
        var nearest = (int)Math.Round(key);
        var source = Math.Abs(key-nearest) < 1e-9 ? _frames[nearest] : LayeredPullFrames.Sample(value);
        _positions.GetValue(source, _ => new(value));
        return source;
    }

    internal Func<double, BitmapSource> CreateRecovery(BitmapSource source, BitmapSource canonical)
    {
        if (!_positions.TryGetValue(source, out var position))
            return new HeadRecoveryFrame([source, canonical]).Sample;
        // One original-key interval for the final pose01 -> canonical bridge.
        // The rest retraces the very same dense bank used during the drag.
        var span = position.Value * 7;
        var bridge = new HeadRecoveryFrame([_frames[0], canonical], suppliedSource: 1);
        return progress =>
        {
            if (!double.IsFinite(progress)) throw new ArgumentOutOfRangeException(nameof(progress));
            if (progress <= 0) return source;
            var remaining = span - Math.Clamp(progress, 0, 1) * (span + 1);
            return remaining >= 0 ? Sample(remaining / 7) : bridge.Sample(-remaining);
        };
    }

    internal BitmapSource[] RecoveryFrom(BitmapSource source, BitmapSource canonical)
    {
        var index = Array.IndexOf(_frames, source);
        return index < 0 ? [source, canonical]
            : _frames.Take(index + 1).Reverse().Append(canonical).ToArray();
    }

    internal int? AnatomicalKey(BitmapSource source)
    {
        var index = Array.IndexOf(_frames, source);
        return index < 0 ? null : index + 1;
    }

    internal static BitmapSource Prepare(PremultipliedFrame source, PremultipliedFrame footReference, int anatomicalKey = 0)
    {
        if (source.Source.PixelWidth != 100 || source.Source.PixelHeight != 100)
            throw new ArgumentException("Supplied body-drag artwork must be100x100 pixels.", nameof(source));
        var pixels = (byte[])source.Pixels.Clone();
        var exterior = new bool[100 * 100];
        var queue = new Queue<int>();
        void Visit(int x, int y)
        {
            if (x < 0 || x >= 100 || y < 0 || y >= 100) return;
            var index = y * 100 + x;
            if (exterior[index]) return;
            // The middle ribbon loop in authored08 has a light/open lower corner.
            // Protect its eight measured interior texels from the background flood;
            // retain their original RGB, not a white rectangle or a new outline.
            // This source-coordinate ownership is deliberately limited to that key.
            if (anatomicalKey == 8 && (y switch
                {
                    37 or 39 => x is 74 or 75,
                    38 => x is >= 74 and <= 76,
                    40 => x == 75,
                    _ => false
                })) return;
            var pixel = y * source.Stride + x * 4;
            if (pixels[pixel] < 240 || pixels[pixel + 1] < 240 || pixels[pixel + 2] < 240) return;
            exterior[index] = true;
            queue.Enqueue(index);
        }
        for (var edge = 0; edge < 100; edge++)
        {
            Visit(edge, 0); Visit(edge, 99); Visit(0, edge); Visit(99, edge);
        }
        while (queue.TryDequeue(out var index))
        {
            var x = index % 100; var y = index / 100;
            Visit(x - 1, y); Visit(x + 1, y); Visit(x, y - 1); Visit(x, y + 1);
        }
        // Establish the original cutout and foot registration first. Matting must
        // not change the registration when a sole edge becomes partly transparent.
        for (var index = 0; index < exterior.Length; index++)
            if (exterior[index]) Array.Clear(pixels, index / 100 * source.Stride + index % 100 * 4, 4);
        var transparent = BitmapSource.Create(100, 100, 96, 96, PixelFormats.Pbgra32, null, pixels, source.Stride);
        transparent.Freeze();
        var aligned = BodyDragFootAlignment.Align(new(transparent, pixels, source.Stride), footReference);
        var output = new byte[96 * 96 * 4];
        for (var y = 0; y < 100; y++)
        for (var x = 0; x < 100; x++)
        {
            var input = y * aligned.Stride + x * 4;
            if (aligned.Pixels[input + 3] == 0) continue;
            // Same fixedX translation for all8 images; never recenter each head.
            var destinationX = x + 3;
            if (destinationX >= 96 || y >= 96)
                throw new InvalidOperationException("Supplied artwork would be clipped; resizing is not permitted.");
            Array.Copy(aligned.Pixels, input, output, (y * 96 + destinationX) * 4, 4);
        }
        var corrected = RemoveExteriorWhiteMatte(output);
        var result = BitmapSource.Create(96, 96, 96, 96, PixelFormats.Pbgra32, null, corrected, 384);
        result.Freeze();
        return anatomicalKey is >= 1 and <= 8 ? AuthoredHeadContour.Pull(result, (anatomicalKey-1)/7d) : result;
    }

    private static byte[] RemoveExteriorWhiteMatte(byte[] original)
    {
        var corrected = (byte[])original.Clone();
        for (var y = 0; y < 96; y++)
        for (var x = 0; x < 96; x++)
        {
            var index = (y * 96 + x) * 4;
            if (original[index + 3] != 255) continue;
            var minimum = Math.Min(original[index], Math.Min(original[index + 1], original[index + 2]));
            if (minimum >= 240) continue; // Preserve white face/body/ribbon pixels.

            var boundary = false;
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
            {
                var nx = x + dx; var ny = y + dy;
                if (nx < 0 || nx >= 96 || ny < 0 || ny >= 96 || original[(ny * 96 + nx) * 4 + 3] == 0)
                    boundary = true;
            }
            if (!boundary) continue; // Strictly one exterior pixel layer, never the interior.

            // JPEG has no original alpha. Estimate coverage from a nearby darker
            // outline sample; ambiguous/light-only neighborhoods stay untouched.
            // Always sample the immutable input so traversal cannot propagate edits.
            var outline = (int)minimum;
            for (var dy = -2; dy <= 2; dy++)
            for (var dx = -2; dx <= 2; dx++)
            {
                var nx = x + dx; var ny = y + dy;
                if (nx < 0 || nx >= 96 || ny < 0 || ny >= 96) continue;
                var neighbor = (ny * 96 + nx) * 4;
                if (original[neighbor + 3] != 255) continue;
                outline = Math.Min(outline, Math.Min(original[neighbor], Math.Min(original[neighbor + 1], original[neighbor + 2])));
            }
            if (outline > 160 || minimum - outline < 8) continue;

            var alpha = (byte)Math.Round(255.0 * (255 - minimum) / (255 - outline), MidpointRounding.AwayFromZero);
            var white = 255 - alpha;
            // P = source - (1-alpha)*white. Re-compositing on white exactly
            // reproduces source RGB, removing the estimated white contribution on dark backgrounds.
            // Positive coverage preserves support, and 0<=P<=alpha preserves Pbgra.
            for (var channel = 0; channel < 3; channel++)
                corrected[index + channel] = (byte)(original[index + channel] - white);
            corrected[index + 3] = alpha;
        }
        return corrected;
    }
}
