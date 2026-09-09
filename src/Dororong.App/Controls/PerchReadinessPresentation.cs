using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dororong.App.Controls;

internal sealed class PerchReadinessPresentation
{
    private Image? _image;
    private ImageSource? _source;
    private BitmapSource? _drawn;
    private double _elapsed;

    internal void Restore()
    {
        if (_image is not null && ReferenceEquals(_image.Source, _drawn)) _image.Source = _source;
        _image = null; _source = null; _drawn = null;
    }

    internal void Apply(Image image, bool ready, bool hanging, TimeSpan delta, Point? pinnedSource = null)
    {
        Restore();
        if (!ready || image.Visibility != Visibility.Visible || image.Source is not BitmapSource source)
        {
            _elapsed = 0;
            return;
        }
        var milliseconds = double.IsFinite(delta.TotalMilliseconds) ? Math.Clamp(delta.TotalMilliseconds, 0, 250) : 0;
        _elapsed = (_elapsed + milliseconds) % 250;
        if (source.PixelWidth != source.PixelHeight || source.PixelWidth is not (96 or 160)) return;
        var pixels = ForelegFlutterFrame.Render(PremultipliedFrame.From(source), hanging, _elapsed / 250, pinnedSource);
        _source = source; _image = image;
        _drawn = BitmapSource.Create(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Pbgra32,
            null, pixels, source.PixelWidth * 4);
        _drawn.Freeze(); image.Source = _drawn;
    }
}
