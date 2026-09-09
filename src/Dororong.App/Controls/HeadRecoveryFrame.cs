using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.Core.Geometry;

namespace Dororong.App.Controls;

// Release-only registration: translate the head rigidly, shorten the body down
// to the fixed sole, then blend registered pixels. Do not crossfade displaced
// heads or snap between the independently authored silhouettes.
internal sealed class HeadRecoveryFrame
{
    private const double Sole = 87;
    private readonly PremultipliedFrame[] _frames;
    private readonly PointD[] _heads;
    private readonly AnatomicalRecoveryMesh[]? _anatomy;
    private readonly PointD[]? _registrationHeads;

    internal HeadRecoveryFrame(BitmapSource[] frames, int? suppliedSource = null)
    {
        _frames = frames.Select(PremultipliedFrame.From).ToArray();
        _heads = _frames.Select(Head).ToArray();
        if (suppliedSource is { } key)
        {
            _registrationHeads = frames.Select(HeadPullAnchoring.RegistrationPoint).ToArray();
            _anatomy = Enumerable.Range(0, frames.Length-1).Select(i=>
                new AnatomicalRecoveryMesh(key-i, i==frames.Length-2 ? 0 : key-i-1, _heads[i], _heads[i+1], _frames[i], _frames[i+1])).ToArray();
        }
    }

    internal BitmapSource Sample(double progress)
    {
        if (!double.IsFinite(progress)) throw new ArgumentOutOfRangeException(nameof(progress));
        if (progress <= 0) return _frames[0].Source;
        if (progress >= 1) return _frames[^1].Source;
        // Only neighboring authored poses are registered together, never the
        // distant upright/rest silhouettes (which would double the legs).
        var position = progress * (_frames.Length - 1);
        var index = (int)Math.Floor(position);
        var t = position - index;
        var _from = _frames[index]; var _to = _frames[index + 1];
        var _fromHead = _heads[index]; var _toHead = _heads[index + 1];
        var head = new PointD(_fromHead.X + (_toHead.X - _fromHead.X) * t,
            _fromHead.Y + (_toHead.Y - _fromHead.Y) * t);
        var pixels = new byte[96 * 96 * 4];
        var anatomicalMap = _anatomy?[index].Sample(t);
        for (var y = 0; y < 96; y++)
        for (var x = 0; x < 96; x++)
        {
            var a = Map(x, y, head, _fromHead);
            var b = Map(x, y, head, _toHead);
            if (anatomicalMap is not null && anatomicalMap[y * 96 + x] is { } mapped)
            {
                var blend = Math.Clamp((y-head.Y-20)/5,0,1);
                a = new(a.X+(mapped.From.X-a.X)*blend,a.Y+(mapped.From.Y-a.Y)*blend);
                b = new(b.X+(mapped.To.X-b.X)*blend,b.Y+(mapped.To.Y-b.Y)*blend);
            }
            for (var c = 0; c < 4; c++)
                pixels[(y * 96 + x) * 4 + c] = (byte)Math.Clamp(Math.Round(
                    Read(_from, a.X, a.Y, c) * (1 - t) + Read(_to, b.X, b.Y, c) * t), 0, 255);
        }
        var result = BitmapSource.Create(96, 96, 96, 96, PixelFormats.Pbgra32, null, pixels, 384);
        result.Freeze();
        if (_registrationHeads is { } registration)
            HeadPullAnchoring.RegisterRecoveryPoint(result, new(
                registration[index].X + (registration[index+1].X-registration[index].X)*t,
                registration[index].Y + (registration[index+1].Y-registration[index].Y)*t));
        return result;
    }

    private static PointD Map(double x, double y, PointD target, PointD source)
    {
        // The registration seam is below the pink-hair centroid, at the lower
        // face. Above it, neither eyes nor hair are scaled. Below it, the map is
        // continuous and fixes the already registered source sole at row87.
        var neck = Math.Min(Sole - 1, target.Y + 20);
        var sourceNeck = Math.Min(Sole - 1, source.Y + 20);
        var body = Math.Clamp((y - neck) / (Sole - neck), 0, 1);
        var sy = y <= neck ? y + source.Y - target.Y
            : y >= Sole ? y : sourceNeck + (y - neck) * (Sole - sourceNeck) / (Sole - neck);
        return new(x + (source.X - target.X) * (1 - body), sy);
    }

    private static double Read(PremultipliedFrame frame, double x, double y, int channel)
    {
        var ix = (int)Math.Floor(x); var iy = (int)Math.Floor(y);
        var fx = x - ix; var fy = y - iy;
        double Pixel(int px, int py) => px < 0 || py < 0 || px >= 96 || py >= 96
            ? 0 : frame.Pixels[(py * 96 + px) * 4 + channel];
        return (Pixel(ix, iy) * (1 - fx) + Pixel(ix + 1, iy) * fx) * (1 - fy)
            + (Pixel(ix, iy + 1) * (1 - fx) + Pixel(ix + 1, iy + 1) * fx) * fy;
    }

    private static PointD Head(PremultipliedFrame frame)
    {
        double sx = 0, sy = 0, count = 0;
        for (var y = 0; y < 96; y++) for (var x = 0; x < 96; x++)
        {
            var i = (y * 96 + x) * 4; var p = frame.Pixels;
            if (p[i + 3] < 128 || p[i + 2] <= p[i] + 12 || p[i] <= p[i + 1] + 12) continue;
            sx += x; sy += y; count++;
        }
        return count == 0 ? new(48, 40) : new(sx / count, sy / count);
    }
}
