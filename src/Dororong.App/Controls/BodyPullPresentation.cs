using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Interaction;

namespace Dororong.App.Controls;

// A single transparent image is added to the existing 144x144 canvas only for this
// interaction. Source pixel scale and the captured affine presentation are retained.
internal sealed class BodyPullPresentation
{
    private readonly Canvas _canvas;
    private readonly AlphaHitTestImage _original;
    private AlphaHitTestImage? _overlay;
    internal Image? VisibleImage => _overlay;
    private Visibility _originalVisibility;
    private readonly SurroundingPullMotion _motion = new();
    private BodyPullCapture? _capture;
    private byte[]? _pixels;
    private bool _settling;
    internal BodyPullPresentation(Canvas canvas, AlphaHitTestImage original) { _canvas = canvas; _original = original; }
    internal void Render(BodyPullSnapshot snapshot, double elapsedMilliseconds = 16)
    {
        if (snapshot.Capture is not { } capture) { Restore(); return; }
        if (!ReferenceEquals(_capture, capture))
        {
            _capture = capture; _pixels = capture.Pixels.ToArray(); _motion.Reset(); _settling = false;
        }
        if (snapshot.Phase == BodyPullPhase.Settling && !_settling)
        {
            // Controller has already advanced primary release by this tick's delta.
            _motion.Release(); _settling = true;
        }
        _motion.Step(snapshot.PullSource, double.IsFinite(elapsedMilliseconds) ? Math.Clamp(elapsedMilliseconds, 0, 250) : 0);
        if (_overlay is null)
        {
            _originalVisibility = _original.Visibility;
            _overlay = new AlphaHitTestImage { Width = BodyPullRenderer.Size, Height = BodyPullRenderer.Size, Stretch = Stretch.None };
            RenderOptions.SetBitmapScalingMode(_overlay, BitmapScalingMode.NearestNeighbor);
            _canvas.Children.Add(_overlay); _original.Visibility = Visibility.Hidden;
        }
        var pixels = SurroundingPullRenderer.Body(_pixels!, capture.Region, snapshot.PullSource, capture.Anchor, _motion.Current, _motion.Far);
        var source = BitmapSource.Create(BodyPullRenderer.Size, BodyPullRenderer.Size, 96, 96, PixelFormats.Pbgra32, null, pixels, BodyPullRenderer.Size * 4); source.Freeze();
        var matrix = capture.SourceToWindow;
        matrix.OffsetX -= BodyPullRenderer.Pad * (matrix.M11 + matrix.M21);
        matrix.OffsetY -= BodyPullRenderer.Pad * (matrix.M12 + matrix.M22);
        _overlay.RenderTransform = new MatrixTransform(matrix);
        _overlay.Source = source;
    }
    internal void Restore()
    {
        _motion.Reset(); _capture = null; _pixels = null; _settling = false;
        if (_overlay is null) return;
        _canvas.Children.Remove(_overlay); _overlay = null;
        _original.Visibility = _originalVisibility;
    }
}
