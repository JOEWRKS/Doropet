using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;
using Dororong.App.Interaction;

namespace Dororong.App.Controls;

internal sealed class EdgePerchPresentation
{
    private const double RestoreMilliseconds = 120;
    private const double PawLeft = 28;
    private const double PawRight = 58;
    private const double GripY = 64;
    private static readonly BitmapSource PerchFrame = PerchExpressionFrames.Open;
    private static readonly double SourceVisibleTop = ReadVisibleTop(PerchFrame);
    private readonly Canvas _root;
    private readonly Canvas _layer;
    private readonly AlphaHitTestImage _canonical;
    private bool _active;
    private bool _restoring;
    private bool _ownsCanonicalVisibility;
    private Visibility _canonicalVisibility;
    private double _canonicalOpacity;
    private Geometry? _canonicalClip;
    private double _restoreElapsed;
    private readonly PerchExpressionMotion _motion = new();
    private readonly CheekPullPresentation _cheek;
    internal FacingDirection Facing { get; private set; } = FacingDirection.Right;
    internal bool IsAttached { get; private set; }
    internal bool IsLocalCheekActive => _cheek.Capture is not null;

    internal EdgePerchPresentation(Canvas root, AlphaHitTestImage canonical, MouseButtonEventHandler pressed)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(canonical);
        ArgumentNullException.ThrowIfNull(pressed);
        _root = root;
        _canonical = canonical;
        Image = new AlphaHitTestImage
        {
            Width = 100,
            Height = 100,
            Stretch = Stretch.Uniform,
            Source = PerchFrame,
            Visibility = Visibility.Collapsed,
            RenderTransformOrigin = new Point(.5, .5),
            RenderTransform = Transform.Identity
        };
        if (canonical.Parent is FrameworkElement canonicalHost)
            Image.ContextMenu = canonicalHost.ContextMenu;
        // Right-facing image09 shifted (-8,+6) against the canonical source has
        // a 0.99858 pink-head-mask IoU. Facing applies the mirrored registration.
        Canvas.SetLeft(Image, 16);
        Canvas.SetTop(Image, 30);
        _layer = new Canvas { Width = 144, Height = 144, Background = null };
        Panel.SetZIndex(_layer, 20);
        _layer.Children.Add(Image);
        Image.MouseLeftButtonDown += pressed;
        _cheek = new(root, Image);
    }

    internal AlphaHitTestImage Image { get; }
    internal AlphaHitTestImage? VisibleImage => Image.Visibility == Visibility.Visible ? Image : null;
    internal void RenderLocalCheek(CheekPullSnapshot? cheek, DirectInteractionPhase phase, TimeSpan delta)
    {
        if(cheek is null){_cheek.Restore();return;}
        _motion.OpenEyes();
        Image.Source=PerchFrame;
        _cheek.Render(cheek,phase,delta.TotalMilliseconds);
    }

    internal bool TryCaptureCheek(PointD sourcePoint, FrameworkElement ancestor, out CheekPullCapture? capture)
    {
        capture=null;
        if(!IsAttached || !InteractionHitMap.IsSelectedCheek(new(sourcePoint.X-8,sourcePoint.Y+6)))return false;
        var scale=Math.Min(Image.ActualWidth/100,Image.ActualHeight/100);
        if(!double.IsFinite(scale)||scale<=0)return false;
        var ox=(Image.ActualWidth-100*scale)/2;var oy=(Image.ActualHeight-100*scale)/2;
        if(!Image.TryGetOpaqueSourcePoint(new(ox+sourcePoint.X*scale,oy+sourcePoint.Y*scale),out _))return false;
        var transform=Image.TransformToAncestor(ancestor);
        // normalized(x,y) maps back to image09(x+8,y-6), THEN through its
        // actual composed transform. This includes the distinct left placement.
        var a=transform.Transform(new Point(ox+8*scale,oy-6*scale));
        var b=transform.Transform(new Point(ox+9*scale,oy-6*scale));
        var c=transform.Transform(new Point(ox+8*scale,oy-5*scale));
        var matrix=new Matrix(b.X-a.X,b.Y-a.Y,c.X-a.X,c.Y-a.Y,a.X,a.Y);
        capture=new(PerchExpressionFrames.RegisteredOpen(),matrix,Facing);return true;
    }
    internal PerchContact? Measure(FacingDirection facing, FrameworkElement ancestor)
    {
        ArgumentNullException.ThrowIfNull(ancestor);
        var detachAfterMeasure = _layer.Parent is null;
        EnsureAttached();
        var before = Image.Visibility;
        if (before == Visibility.Collapsed)
        {
            Image.Visibility = Visibility.Hidden;
            ancestor.UpdateLayout();
        }
        var previousTransform=Image.RenderTransform;
        var previousLeft=Canvas.GetLeft(Image);
        SetFacing(facing, neutral:true);
        ancestor.UpdateLayout();
        try
        {
            if (Image.Source is not BitmapSource source || source.PixelWidth <= 0 || source.PixelHeight <= 0)
                return null;
            var width = Image.ActualWidth > 0 ? Image.ActualWidth : Image.Width;
            var height = Image.ActualHeight > 0 ? Image.ActualHeight : Image.Height;
            var scale = Math.Min(width / source.PixelWidth, height / source.PixelHeight);
            if (!double.IsFinite(scale) || scale <= 0) return null;
            var offsetX = (width - source.PixelWidth * scale) / 2;
            var offsetY = (height - source.PixelHeight * scale) / 2;
            var transform = Image.TransformToAncestor(ancestor);
            Point Map(double x, double y) => transform.Transform(new(offsetX + x * scale, offsetY + y * scale));
            var a = Map(PawLeft, GripY); var b = Map(PawRight, GripY);
            var topA = Map(0, SourceVisibleTop); var topB = Map(source.PixelWidth, SourceVisibleTop);
            var left = Math.Min(a.X, b.X); var right = Math.Max(a.X, b.X);
            var grip = (a.Y + b.Y) / 2; var top = Math.Min(topA.Y, topB.Y);
            return Finite(left, right, grip, top) ? new(left, right, grip, top) : null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        finally
        {
            Image.Visibility = before;
            Image.RenderTransform=previousTransform;
            Canvas.SetLeft(Image,previousLeft);
            if (detachAfterMeasure) Detach();
        }
    }

    internal void Apply(EdgePerchPhase phase, FacingDirection facing, TimeSpan delta, bool immediateRestore = false)
    {
        if (phase != EdgePerchPhase.None)
        {
            EnsureAttached();
            if (!_active)
            {
                _motion.Reset();
                _canonicalVisibility = _canonical.Visibility;
                _canonicalOpacity = _canonical.Opacity;
                _canonicalClip = _canonical.Clip;
                _ownsCanonicalVisibility = true;
            }
            _active = true; _restoring = false; _restoreElapsed = 0;
            IsAttached=phase==EdgePerchPhase.Attached;
            Facing=facing;
            if(!IsLocalCheekActive)_motion.Advance(delta);
            SetFacing(facing);
            _canonical.Visibility = Visibility.Hidden;
            Image.Visibility = IsLocalCheekActive ? Visibility.Hidden : Visibility.Visible;
            Image.Source=IsLocalCheekActive ? PerchFrame : _motion.Eye switch {1=>PerchExpressionFrames.Squint,2=>PerchExpressionFrames.Closed,_=>PerchFrame};
            Image.Opacity = 1;
            Image.Clip = null;
            if (_ownsCanonicalVisibility) _canonical.Clip = _canonicalClip;
            return;
        }
        if (immediateRestore)
        {
            Restore();
            return;
        }
        if (_active)
        {
            IsAttached=false;
            _cheek.Restore();
            _active = false; _restoring = true; _restoreElapsed = 0;
        }
        if (!_restoring) return;
        var milliseconds = double.IsFinite(delta.TotalMilliseconds) ? Math.Max(0, delta.TotalMilliseconds) : 0;
        _restoreElapsed = Math.Min(RestoreMilliseconds, _restoreElapsed + milliseconds);
        var progress = _restoreElapsed / RestoreMilliseconds;
        // Complementary clips are an opaque one-source-at-a-time handoff: no
        // doubled head and no whole-character opacity dip.
        var boundary = 48 + 70 * progress;
        _canonical.Visibility = _canonicalVisibility;
        _canonical.Opacity = _canonicalOpacity;
        _canonical.Clip = new RectangleGeometry(new Rect(0, 0, 96, Math.Clamp(boundary - 24, 0, 96)));
        Image.Visibility = Visibility.Visible;
        Image.Opacity = 1;
        var imageTop = Math.Clamp(boundary - 30, 0, 100);
        Image.Clip = new RectangleGeometry(new Rect(0, imageTop, 100, 100 - imageTop));
        if (progress >= 1) Restore();
    }

    internal void Restore()
    {
        _cheek.Restore();
        IsAttached=false;
        _motion.Reset();
        if (_ownsCanonicalVisibility)
        {
            _canonical.Visibility = _canonicalVisibility;
            _canonical.Opacity = _canonicalOpacity;
            _canonical.Clip = _canonicalClip;
        }
        _ownsCanonicalVisibility = false; _active = false; _restoring = false; _restoreElapsed = 0;
        Image.Visibility = Visibility.Collapsed;
        Image.Opacity = 1;
        Image.Clip = null;
        Image.RenderTransform = Transform.Identity;
        Image.Source=PerchFrame;
        Detach();
    }

    private void EnsureAttached()
    {
        if (_layer.Parent is null) _root.Children.Add(_layer);
    }

    private void Detach()
    {
        if (ReferenceEquals(_layer.Parent, _root)) _root.Children.Remove(_layer);
    }

    private void SetFacing(FacingDirection facing, bool neutral=false)
    {
        Canvas.SetLeft(Image, facing == FacingDirection.Left ? 20 : 16);
        var sy=neutral ? 1 : _motion.ScaleY;
        // RenderTransformOrigin is (.5,.5), hence source grip64 is centreY14.
        Image.RenderTransform = new ScaleTransform(facing==FacingDirection.Left ? -1 : 1,sy,0,14);
    }

    private static double ReadVisibleTop(BitmapSource source)
    {
        var bitmap = source.Format == PixelFormats.Bgra32 ? source : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = checked(bitmap.PixelWidth * 4);
        var pixels = new byte[checked(stride * bitmap.PixelHeight)];
        bitmap.CopyPixels(pixels, stride, 0);
        for (var y = 0; y < bitmap.PixelHeight; y++)
            for (var x = 0; x < bitmap.PixelWidth; x++)
                if (pixels[y * stride + x * 4 + 3] > 0) return y;
        return 0;
    }

    private static bool Finite(params double[] values) => values.All(double.IsFinite);
}
