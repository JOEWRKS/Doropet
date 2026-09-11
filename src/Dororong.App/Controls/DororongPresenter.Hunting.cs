using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Media;

namespace Dororong.App.Controls;

public partial class DororongPresenter
{
    private readonly PounceSession _pounce = new();
    private double _pounceLaunchAge = 1.5;
    private Transform? _pounceImageTransform;
    private Point _pounceImageOrigin;
    internal PouncePose Pounce => _pounce.Current;
    internal int HuntingFrame => _huntFrame;
    internal bool UpdateHuntingWithPounce(PetSnapshot snapshot, DirectInteractionSnapshot direct,
        PointerSample pointer, TimeSpan elapsed, bool pressed) => UpdateHuntingCore(snapshot, direct, pointer, elapsed, pressed, true);
    private readonly HuntingSession _huntSession = new();
    private readonly HuntGaze _huntGaze = new();
    private HuntRenderer? _huntRenderer;
    private BitmapSource? _huntingImage;
    private PointD? _huntPointer;
    private double _huntDelta;
    private bool _huntNear;
    private int _huntFrame;
    private HuntGazePose _huntRenderedGaze;
    private bool _huntClosed;
    private Matrix _huntHeadTransform = Matrix.Identity;
    private bool IsHuntingImage => _huntingImage is not null && ReferenceEquals(DororongImage.Source,_huntingImage);

    private PointD HuntHeadPoint(PointD point)
    {
        if(!IsHuntingImage)return point;
        var inverse=_huntHeadTransform;inverse.Invert();var p=inverse.Transform(new Point(point.X,point.Y));
        return new(p.X,p.Y);
    }

    private DirectInteractionTarget? ClassifyHuntingHead(PointD source)
    {
        if(!IsHuntingImage)return null;
        var p=HuntHeadPoint(source);
        if(!InteractionHitMap.InSource(p) || HuntFrames.Instance.Head(_huntClosed?1:0).Span[((int)p.Y*96+(int)p.X)*4+3]==0)return null;
        if(InteractionHitMap.IsSelectedCheek(p))return DirectInteractionTarget.RightCheek;
        if(InteractionHitMap.IsUpperHead(p))return DirectInteractionTarget.Body;
        return FrameInteractionDescriptor.Canonical.Classify(p,FacingDirection.Right,true)==DirectInteractionTarget.LeftCheek
            ? DirectInteractionTarget.LeftCheek : DirectInteractionTarget.None;
    }

    private CheekPullCapture CaptureHuntingCheek(byte[] pixels,Matrix sourceToWindow)
    {
        var frame=_huntFrame;var gaze=_huntRenderedGaze;var closed=_huntClosed;
        var renderer=new HuntRenderer();
        byte[] Render(double pull,double eye,double hair)
        {
            if(pull<=0)return (byte[])pixels.Clone();
            var bitmap=renderer.Render(frame,gaze,closed,pull,eye,hair);
            var result=new byte[96*96*4];new FormatConvertedBitmap(bitmap,PixelFormats.Bgra32,null,0).CopyPixels(result,384,0);
            return result;
        }
        return new(pixels,sourceToWindow,_activeFacing,Render,new(_huntHeadTransform.M11,_huntHeadTransform.M12));
    }

    internal bool UpdateHunting(PetSnapshot snapshot, DirectInteractionSnapshot direct,
        PointerSample pointer, TimeSpan elapsed, bool pressed) => UpdateHuntingCore(snapshot, direct, pointer, elapsed, pressed, false);

    private bool UpdateHuntingCore(PetSnapshot snapshot, DirectInteractionSnapshot direct,
        PointerSample pointer, TimeSpan elapsed, bool pressed, bool pounceEnabled)
    {
        var blocked = pressed || _sittingRequested || _locomotionBlocked || _edgePerch.IsAttached ||
            snapshot.State is not (PetState.Idle or PetState.Walk or PetState.Sleep) || snapshot.IsDirectInteractionPending ||
            direct.Target != DirectInteractionTarget.None || direct.HeadLanding is not null;
        _huntDelta = Math.Clamp(elapsed.TotalSeconds, 0, .1);
        _huntPointer = pointer.IsAvailable && double.IsFinite(pointer.Position.X) && double.IsFinite(pointer.Position.Y)
            ? pointer.Position - snapshot.Position : null;
        // Authored 96px image begins at presenter (24,24). Use its stable
        // registration, not animated alpha bounds, for proximity and gaze.
        var radiusScale = _huntNear ? 1.2 : 1;
        _huntNear = !blocked && _huntPointer is { } p &&
            Math.Pow((p.X-72)/(77.5*radiusScale),2)+Math.Pow((p.Y-81)/(50*radiusScale),2)<1;
        var oldPounce = Pounce;
        if (pounceEnabled)
        {
            _pounce.Advance(_huntDelta, _huntNear, _huntPointer?.X - 72 ?? double.NaN, snapshot.Facing, blocked);
            if (!oldPounce.IsJump && Pounce.IsJump) _pounceLaunchAge = Math.Clamp(_huntSession.FrameAgeSeconds, .7, 1.65);
        }
        if (Pounce.IsEngaged) _huntSession.Reset();
        else _huntSession.Advance(_huntDelta,_huntNear,blocked);
        if (blocked) _huntGaze.Reset();
        return _huntSession.IsActive || Pounce.IsEngaged;
    }

    private void ApplyHunting(PetSnapshot snapshot, DirectInteractionSnapshot direct, bool closed)
    {
        _huntingImage = null;
        if ((!_huntSession.IsActive && !Pounce.IsEngaged) || _sittingRequested || _locomotionBlocked || _edgePerch.IsAttached ||
            snapshot.State is not (PetState.Idle or PetState.Walk) || snapshot.IsDirectInteractionPending ||
            direct.Target != DirectInteractionTarget.None || direct.HeadLanding is not null) return;
        _postBodyClickIdleFacing = _postCheekIdleFacing = _postHeadLandingIdleFacing = null;
        // RenderDesktop has already converted screen facing to art parity.
        // Old click/cheek recovery-facing overrides must not defeat tracking.
        var flip = snapshot.Facing == FacingDirection.Left;
        if (_huntingLastFlip is { } previousFlip && previousFlip != flip) _huntGaze.Reset();
        _huntingLastFlip = flip;
        PointD? target = _huntPointer is { } p ? new((p.X-(flip?88:56))*2,(p.Y-77)*2) : null;
        var gaze = _huntGaze.Advance(_huntDelta,target,flip,Pounce.Phase == PouncePhase.Track ? target is not null : _huntNear);
        _huntDelta = 0; // An immediate extra render must not advance gaze twice.
        var age = _huntSession.FrameAgeSeconds;
        if (Pounce.Phase == PouncePhase.Flight)
        {
            var u = Math.Clamp(Pounce.Age / .34, 0, 1);
            age = _pounceLaunchAge + (2.05 - _pounceLaunchAge) * u * u * (3 - 2 * u);
        }
        else if (Pounce.Phase == PouncePhase.Landing) age = 2.05 + .55 * Math.Clamp((Pounce.Age - .34) / .28, 0, 1);
        else if (Pounce.Phase == PouncePhase.Track) age = 2.6;
        _huntFrame=(int)Math.Clamp(Math.Round(age*60),0,156);
        _huntRenderedGaze=gaze;_huntClosed=closed;
        _huntHeadTransform=HuntRenderer.HeadTransform(_huntFrame,gaze);
        _huntingImage = (_huntRenderer ??= new()).Render(_huntFrame,gaze,closed);
        DororongImage.Source = _huntingImage;
        BodyScaleTransform.ScaleX = flip ? -1 : 1;
        BodyScaleTransform.ScaleY = 1;
        BodyRotateTransform.Angle = 0;
        ImageBreathingScaleTransform.ScaleX = ImageBreathingScaleTransform.ScaleY = 1;
        BodyTranslateTransform.X = BodyTranslateTransform.Y = 0;
        if (Pounce.IsJump)
        {
            _pounceImageTransform = DororongImage.RenderTransform;
            _pounceImageOrigin = DororongImage.RenderTransformOrigin;
            var transform = Matrix.Identity;
            transform.ScaleAt(Pounce.ScaleX, Pounce.ScaleY, 31, 86);
            transform.RotateAt(Pounce.AngleRadians * 180 / Math.PI, 31, 86);
            DororongImage.RenderTransformOrigin = new(0,0);
            DororongImage.RenderTransform = new MatrixTransform(transform);
        }
    }

    private void RestorePounceTransform()
    {
        if (_pounceImageTransform is null) return;
        DororongImage.RenderTransform = _pounceImageTransform;
        DororongImage.RenderTransformOrigin = _pounceImageOrigin;
        _pounceImageTransform = null;
    }

    private void ResetHunting()
    {
        _huntSession.Reset();
        _pounce.Reset();
        RestorePounceTransform();
        _huntGaze.Reset();
        _huntPointer = null;
        _huntingImage = null;
        _huntNear = false;
        _huntingLastFlip = null;
    }

    private bool? _huntingLastFlip;
}
