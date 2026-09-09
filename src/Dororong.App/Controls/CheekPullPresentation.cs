using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Interaction;

namespace Dororong.App.Controls;

internal sealed class CheekPullPresentation(Canvas canvas, AlphaHitTestImage original)
{
    private Image? _overlay;
    internal Image? VisibleImage => _overlay;
    private Visibility _originalVisibility;
    private readonly FaceFollowMotion _follow = new();
    private bool _releasing;
    internal CheekPullCapture? Capture { get; private set; }

    internal void Render(CheekPullSnapshot snapshot, DirectInteractionPhase phase, double elapsedMilliseconds)
    {
        if (!ReferenceEquals(Capture, snapshot.Capture))
        {
            _follow.Reset();
            _releasing = false;
        }
        var firstRelease = phase == DirectInteractionPhase.CheekRelease && !_releasing;
        if (firstRelease)
        {
            _follow.BeginRelease();
            _releasing = true;
        }
        // The controller starts cheek release at progress zero: this tick holds
        // the displayed follow offsets too; later ticks consume the real clock.
        _follow.Step(snapshot.PullDips, firstRelease ? 0 : elapsedMilliseconds);
        if (_overlay is null)
        {
            _originalVisibility = original.Visibility;
            _overlay = new Image { Width = 96, Height = 96, Stretch = Stretch.None, IsHitTestVisible = false };
            RenderOptions.SetBitmapScalingMode(_overlay, BitmapScalingMode.NearestNeighbor);
            canvas.Children.Add(_overlay); original.Visibility = Visibility.Hidden;
        }
        Capture = snapshot.Capture;
        var pixels = snapshot.Capture.Render(snapshot.PullDips, _follow.Eye, _follow.Hair);
        var source = BitmapSource.Create(96, 96, 96, 96, PixelFormats.Bgra32, null, pixels, 384); source.Freeze();
        _overlay.Source = source;
        _overlay.RenderTransform = new MatrixTransform(snapshot.Capture.SourceToWindow);
    }

    internal void Restore()
    {
        _follow.Reset();
        _releasing = false;
        if (_overlay is null) return;
        canvas.Children.Remove(_overlay); _overlay = null; Capture = null;
        original.Visibility = _originalVisibility;
    }
}
