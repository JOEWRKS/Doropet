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
    private double _pull;
    private double _recoilX;
    private double _recoilY;
    internal CheekPullCapture? Capture { get; private set; }
    internal GeneralTransform PhysicsTransform(Visual ancestor)
    {
        var result = new GeneralTransformGroup();
        var current = _overlay!.TransformToAncestor(ancestor);
        result.Children.Add(current);
        // Convert the canvas-space kick back to source space first. The live
        // image transform may include a platform squash/rotation after capture;
        // its linear part must act on the kick too before we can subtract it.
        var inverse = Capture!.SourceToWindow;
        inverse.Invert();
        var local = inverse.Transform(new Vector(_recoilX, _recoilY));
        var recoil = current.Transform(new Point(local.X, local.Y)) - current.Transform(new Point());
        result.Children.Add(new TranslateTransform(-recoil.X, -recoil.Y));
        return result;
    }
    internal BitmapSource? RenderReadiness(double phase) => Capture?.RenderReadiness(_pull,_follow.Eye,_follow.Hair,phase);

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
            _follow.BeginRelease(snapshot.SpringRelease);
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
        _pull = snapshot.PullDips;
        var pixels = snapshot.Capture.Render(snapshot.PullDips, _follow.Eye, _follow.Hair);
        var source = BitmapSource.Create(96, 96, 96, 96, PixelFormats.Bgra32, null, pixels, 384); source.Freeze();
        _overlay.Source = source;
        var matrix=snapshot.Capture.SourceToWindow;
        // Visual recoil only: never feed this offset back into drag/window state.
        _recoilX=-Math.Sign(snapshot.Capture.OutwardUnit.X)*snapshot.RecoilOffset;
        _recoilY=-snapshot.RecoilLift;
        matrix.OffsetX+=_recoilX;
        matrix.OffsetY+=_recoilY;
        _overlay.RenderTransform = new MatrixTransform(matrix);
    }

    internal void Restore()
    {
        _follow.Reset();
        _releasing = false;
        _recoilX = 0;
        _recoilY = 0;
        if (_overlay is null) return;
        canvas.Children.Remove(_overlay); _overlay = null; Capture = null;
        original.Visibility = _originalVisibility;
    }
}
