using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dororong.App.Controls;

// A temporary deformation of the existing art, not a replacement drawing.
// The monotone horizontal field spreads the lower front/rear paws without
// cutting their roots away from the torso or touching the upper face texture.
internal sealed class ExtremeLandingPresentation(Canvas body, AlphaHitTestImage original)
{
    private AlphaHitTestImage? overlay;
    private byte[]? source;
    private Transform? frozenBody, savedBody;
    private Point frozenOrigin, savedOrigin;
    private Visibility savedVisibility;
    private double lastSpread = double.NaN;
    internal AlphaHitTestImage? VisibleImage => overlay is { Visibility: Visibility.Visible } ? overlay : null;

    internal void Apply(double spread)
    {
        RestoreFrame();
        if (!double.IsFinite(spread)) return;
        spread = Math.Clamp(spread, 0, 1);
        if (source is null)
        {
            if (original.Source is not BitmapSource { PixelWidth: 96, PixelHeight: 96 } bitmap) return;
            source = PremultipliedFrame.From(bitmap).Pixels;
            frozenBody = body.RenderTransform.CloneCurrentValue(); frozenOrigin = body.RenderTransformOrigin;
            var transform = original.TransformToAncestor(body);
            var scale = Math.Min(original.ActualWidth / 96, original.ActualHeight / 96);
            var x0 = (original.ActualWidth - 96 * scale) / 2;
            var y0 = (original.ActualHeight - 96 * scale) / 2;
            var a = transform.Transform(new Point(x0, y0));
            var b = transform.Transform(new Point(x0 + scale, y0));
            var c = transform.Transform(new Point(x0, y0 + scale));
            var matrix = new Matrix(b.X-a.X,b.Y-a.Y,c.X-a.X,c.Y-a.Y,a.X,a.Y);
            matrix.OffsetX -= 32 * (matrix.M11 + matrix.M21);
            matrix.OffsetY -= 32 * (matrix.M12 + matrix.M22);
            overlay = new AlphaHitTestImage { Width = 160, Height = 160, Stretch = Stretch.None,
                RenderTransform = new MatrixTransform(matrix), Visibility = Visibility.Collapsed };
            RenderOptions.SetBitmapScalingMode(overlay, BitmapScalingMode.HighQuality);
            body.Children.Add(overlay);
        }
        if (spread != lastSpread)
        {
            var pixels = Spread(source, spread);
            var bitmap = BitmapSource.Create(160,160,96,96,PixelFormats.Pbgra32,null,pixels,640);
            bitmap.Freeze(); overlay!.Source = bitmap; lastSpread = spread;
        }
        savedBody = body.RenderTransform; savedOrigin = body.RenderTransformOrigin;
        savedVisibility = original.Visibility;
        body.RenderTransform = frozenBody!; body.RenderTransformOrigin = frozenOrigin;
        original.Visibility = Visibility.Hidden; overlay!.Visibility = Visibility.Visible;
        body.UpdateLayout();
    }

    // Restore yesterday's layer before the regular presenter writes today's pose.
    // Keep the frozen art across the one-second hold (including blink/breath cycles).
    internal void RestoreFrame()
    {
        if (savedBody is null) return;
        body.RenderTransform = savedBody; body.RenderTransformOrigin = savedOrigin;
        original.Visibility = savedVisibility; overlay!.Visibility = Visibility.Collapsed;
        savedBody = null;
    }

    internal void Reset()
    {
        RestoreFrame();
        if (overlay is not null) body.Children.Remove(overlay);
        overlay = null; source = null; frozenBody = null; lastSpread = double.NaN;
    }

    private static byte[] Spread(byte[] pixels, double amount)
    {
        var output = SurroundingPullRenderer.Pad(pixels);
        if (amount == 0) return output;
        for (var y = 67; y < 96; y++)
        {
            Array.Clear(output, (y + 32) * 640, 640);
            var reach = 24 * amount * Smooth((y - 66) / 17d);
            for (var x = -32; x < 128; x++)
            {
                // x(u) is strictly increasing: inverse search cannot fold or tear.
                var lo = x - reach; var hi = x + reach;
                for (var i = 0; i < 16; i++)
                {
                    var u = (lo + hi) * .5;
                    var mapped = u + reach * (2 * Smooth((u - 46) / 14) - 1);
                    if (mapped < x) lo = u; else hi = u;
                }
                var sx = (lo + hi) * .5; var ix = (int)Math.Floor(sx); var fraction = sx - ix;
                for (var channel = 0; channel < 4; channel++)
                {
                    var left = ix >= 0 && ix < 96 ? pixels[(y*96+ix)*4+channel] : 0;
                    var right = ix+1 >= 0 && ix+1 < 96 ? pixels[(y*96+ix+1)*4+channel] : 0;
                    output[((y+32)*160+x+32)*4+channel] = (byte)Math.Round(left*(1-fraction)+right*fraction);
                }
            }
        }
        return output;
    }
    private static double Smooth(double t) { t = Math.Clamp(t,0,1); return t*t*(3-2*t); }
}
