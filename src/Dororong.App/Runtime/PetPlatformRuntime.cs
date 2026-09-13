using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Runtime;

internal sealed class PetPlatformRuntime
{
    private readonly DesktopSceneSource _source;
    private readonly Func<PointD, DesktopCoordinateMap> _map;
    private readonly Func<(FootContact Contact, RectD Bounds)?> _measure;
    private readonly Action<PlatformPose?,double?> _present;
    private readonly Action<EdgePerchPhase,FacingDirection,TimeSpan,bool>? _presentPerch;
    private readonly Func<IReadOnlyList<DesktopMonitor>>? _fallbackMonitors;
    private readonly PlatformMotion _motion = new();
    private DesktopMonitor[] _physicalMonitors = [];
    private DesktopScene? _scene;
    private DesktopCoordinateMap? _previousMap;
    private RectD _visibleBounds;
    private DesktopMonitor? _monitor;
    private bool _reliable;
    private bool _hasGeometry;
    private PointD? _lastPosition;
    private TimeSpan? _lastMonitorRead;
    private PointD? _framePosition;
    private double? _landingClearanceHeight;
    private bool _needsPresentationRebase;
    private readonly EdgePerchRuntime? _perch;
    private SceneReadHealth _sceneHealth = SceneReadHealth.Expired;
    private TimeSpan _presentationDelta;
    private bool _immediatePerchRestore;

    internal PetPlatformRuntime(DesktopSceneSource source, Func<PointD, DesktopCoordinateMap> map,
        Func<(FootContact Contact, RectD Bounds)?> measure, Action<PlatformPose?,double?> present,
        Func<IReadOnlyList<DesktopMonitor>>? fallbackMonitors = null,
        Func<FacingDirection,PerchContact?>? measurePerch = null,
        Action<EdgePerchPhase,FacingDirection,TimeSpan,bool>? presentPerch = null)
    {
        _source=source; _map=map; _measure=measure; _present=present; _fallbackMonitors=fallbackMonitors;
        _presentPerch=presentPerch;
        if(measurePerch is not null) _perch=new(measurePerch, () => _hasGeometry ? Contact.SoleY : null);
    }
    internal bool SuspendsAutonomousMotion => _perch is { Current.Phase: not EdgePerchPhase.None } || !_reliable || !_hasGeometry ||
        _motion.Current.Phase is PlatformPhase.Falling or PlatformPhase.Landing or PlatformPhase.Lifting;
    internal FootContact Contact { get; private set; }
    internal PointD FramePosition => _framePosition ?? _lastPosition ?? new();
    internal EdgePerchPhase PerchPhase => _perch?.Current.Phase ?? EdgePerchPhase.None;

    internal PointerSample BeginFrame(TimeSpan elapsed, PointD displayed, PointerSample pointer, bool direct,
        bool presentationAtRest = true, bool releasedHeadRecovery = false)
    {
        // Re-sample the actual presenter origin and scale on every tick. WPF may
        // have moved/rebased the host between ticks without a brain update.
        var map=_map(displayed);
        if(direct && _perch is not null) ReleasePerch(displayed, immediate: true);
        if (_previousMap is { } old)
        {
            // HWND placement is pixel-quantized while WPF Left/Top can still
            // expose the fractional value we requested. This is not a DPI rebase.
            if (Math.Abs(old.ScaleX-map.ScaleX)<=.000001 && Math.Abs(old.ScaleY-map.ScaleY)<=.000001 &&
                WithinPixel(old.ToLogical(new()),map.ToLogical(new()),map)) map=old;
            else Reset(displayed);
        }
        _previousMap=map;
        if (_lastPosition is { } last && !WithinPixel(last,displayed,map)) Reset(displayed);
        // Keep our continuous trajectory across readback rounding. A genuine
        // move above that physical-pixel envelope still seeds from actual display.
        _framePosition=direct ? displayed : _lastPosition ?? displayed;
        Measure();
        _needsPresentationRebase |= direct || !presentationAtRest;
        if (!direct && (presentationAtRest || releasedHeadRecovery) && _needsPresentationRebase && _hasGeometry)
        {
            // Released partial heads retire their pinned translation incrementally
            // while the shape recovers. Deferring the entire offset until idle
            // relocates the HWND by 7-15px during a low landing, risking a frame
            // with placement and image compensation out of sync. Others retain their
            // existing rest handoff. Preserve world contact and motion ownership.
            var previousSole=Contact.SoleY;
            _present(null,null);
            Measure();
            // Normalize translation only, not the current impact/sway shape:
            // its wider/rotated foot may be what still overlaps a narrow edge.
            _present(_motion.Current,Contact.SoleY);
            Measure();
            var offset=new PointD(0,previousSole-Contact.SoleY);
            _framePosition+=offset;
            _lastPosition=_framePosition;
            _motion.RebasePosition(offset);
            _needsPresentationRebase=!presentationAtRest;
        }
        if(pointer.IsAvailable && pointer.ScreenPixelPosition is { } pixels)
            pointer=Finite(pixels) ? pointer with {Position=map.ToLogical(pixels)} : PointerSample.Unavailable;
        if(pointer.IsAvailable && !Finite(pointer.Position)) pointer=PointerSample.Unavailable;

        var read=_source.Read(elapsed);
        _sceneHealth=read.Health;
        if(read.Scene is { } physical && physical.Monitors.Count>0)
            _physicalMonitors=physical.Monitors.ToArray();
        if(read.Health==SceneReadHealth.Expired)
        {
            // Window acquisition can fail independently of monitor enumeration.
            // Retain the last real topology when even that read is unavailable.
            try
            {
                if (_lastMonitorRead is null || elapsed - _lastMonitorRead.Value >= TimeSpan.FromMilliseconds(80))
                {
                    _lastMonitorRead=elapsed;
                    if(_fallbackMonitors?.Invoke() is {Count:>0} monitors) _physicalMonitors=monitors.ToArray();
                }
            }
            catch { /* A retained monitor floor is preferable to an unbounded fall. */ }
            _scene=map.ToLogicalScene(new(0,elapsed,_physicalMonitors,[]));
            _reliable=_physicalMonitors.Length>0;
        }
        else
        {
            _scene=read.Scene is { } scene ? map.ToLogicalScene(scene) : null;
            _reliable=read.Health is SceneReadHealth.Fresh or SceneReadHealth.Cached;
        }
        if(_scene is {Monitors.Count:>0})
        {
            var selection=direct && pointer.IsAvailable ? pointer.Position :
                FramePosition+new PointD((_visibleBounds.X+_visibleBounds.Right)/2,(_visibleBounds.Y+_visibleBounds.Bottom)/2);
            _monitor=_scene.Monitors.OrderBy(m=>DistanceTo(m.Bounds,selection)).First();
        }
        return pointer;
    }

    internal bool TryBeginPerch(PointD displayed, FacingDirection facing, bool carryReleased, bool pointerReliable)
    {
        if(_perch is null || _scene is null || _sceneHealth is not (SceneReadHealth.Fresh or SceneReadHealth.Cached)) return false;
        var surfaces=BuildPerchSurfaces();
        var readbackTolerance = _previousMap is { } map
            ? new SizeD(1.000001 / map.ScaleX, 1.000001 / map.ScaleY) : default;
        if(!_perch.TryBegin(displayed,facing,surfaces,_scene.Monitors,carryReleased,pointerReliable,
            sceneReliable:true,readbackTolerance)) return false;
        _motion.Reset(displayed);_lastPosition=displayed;_framePosition=displayed;_immediatePerchRestore=false;
        return true;
    }

    internal bool CanBeginPerch(PointD displayed, FacingDirection facing, bool heldCarry, bool pointerReliable,
        bool advertise = false)
    {
        if (advertise) _perch?.ClearReadiness();
        return heldCarry && pointerReliable && _perch is not null && _scene is not null &&
            _sceneHealth is SceneReadHealth.Fresh or SceneReadHealth.Cached &&
            _perch.CanBegin(displayed, facing, BuildPerchSurfaces(), _scene.Monitors, advertise);
    }

    internal bool TryAdvancePerch(TimeSpan delta,PointD displayed,out PointD position)
    {
        position=displayed;_presentationDelta=delta;
        var perch=_perch;
        if(perch is null || perch.Current.Phase == EdgePerchPhase.None) return false;
        var sceneReliable=_sceneHealth != SceneReadHealth.TemporarilyUnavailable;
        var surfaces=_scene is null ? Array.Empty<PerchSurface>() : BuildPerchSurfaces();
        var monitors=_scene?.Monitors ?? Array.Empty<DesktopMonitor>();
        var pose=perch.Advance(delta,displayed,surfaces,monitors,sceneReliable);
        if(pose.Phase==EdgePerchPhase.None)
        {
            _motion.Reset(pose.Position);_lastPosition=pose.Position;_framePosition=pose.Position;
            return false;
        }
        position=pose.Position;_lastPosition=position;_framePosition=position;return true;
    }

    internal RectD GetMovementArea(PointD position, FootContact contact, SizeD petSize)
    {
        if(_monitor is null || !_hasGeometry)
            return new(position.X,position.Y,petSize.Width,petSize.Height);
        var bounds=_monitor.Bounds;
        var minX=bounds.X-_visibleBounds.X; var maxX=Math.Max(minX,bounds.Right-_visibleBounds.Right);
        var minY=bounds.Y-_visibleBounds.Y; var maxY=Math.Max(minY,bounds.Bottom-contact.SoleY);
        return new(minX,minY,maxX-minX+petSize.Width,maxY-minY+petSize.Height);
    }

    internal PlatformPose Advance(TimeSpan elapsed,TimeSpan delta,PointD displayed,PointD proposed,FootContact contact,bool directOwnsPosition,double supportedOffsetY = 0)
    {
        _presentationDelta=delta;
        if(directOwnsPosition)
        {
            Reset(proposed);
            return _motion.Current;
        }
        if(!_hasGeometry) {Reset(displayed);return _motion.Current;}
        if(_framePosition is { } continuous)
        {
            proposed+=continuous-displayed;
            displayed=continuous;
        }
        var area=GetMovementArea(displayed,contact,new(0,0));
        var clamped=area.ClampTopLeft(displayed,new(0,0));
        if(_monitor is not null && Distance(clamped,displayed)>.001)
        {
            // Monitor removal/OS displacement starts physics at the corrected
            // actual location, never the last brain snapshot or old owner.
            proposed=clamped; displayed=clamped;
            // Our own squash/rebound changes visible bounds. A bounds correction
            // during contact must not retire the owner and replay the impact.
            if (_motion.Current.Phase!=PlatformPhase.Landing &&
                !(_motion.Current.Phase==PlatformPhase.Supported && supportedOffsetY<0)) Reset(displayed);
        }
        var previousPhase=_motion.Current.Phase;
        var clearanceHeight=contact.SoleY-contact.VisibleTop;
        var surfaceHeight=previousPhase==PlatformPhase.Landing && _landingClearanceHeight is { } landingHeight
            ? Math.Min(clearanceHeight,landingHeight) : clearanceHeight;
        var surfaces=_scene is null ? Array.Empty<PlatformSurface>() :
            PlatformGeometry.Build(_scene,surfaceHeight);
        var pose=_motion.Advance(new(delta,displayed,proposed,contact,surfaces,false,_reliable)
            { Monitors = _scene?.Monitors, SupportedOffsetY = supportedOffsetY });
        if(pose.Phase==PlatformPhase.Landing && previousPhase!=PlatformPhase.Landing)
            _landingClearanceHeight=clearanceHeight;
        else if(pose.Phase!=PlatformPhase.Landing) _landingClearanceHeight=null;
        if(_monitor is not null)
        {
            var bounded=area.ClampTopLeft(pose.Position,new(0,0));
            if(Distance(bounded,pose.Position)>.001)
            {
                // Landing rebounds and authored click hops can reach the monitor
                // ceiling. Limit travel without discarding their real support.
                if(pose.Phase!=PlatformPhase.Landing &&
                    !(pose.Phase==PlatformPhase.Supported && supportedOffsetY<0)) Reset(bounded);
                pose=pose with {Position=bounded};
            }
        }
        _lastPosition=pose.Position;
        return pose;
    }

    internal void AfterRender(PlatformPose? pose, double? sole)
    {
        // Render owns capture and retirement of yesterday's platform layer.
        // Measure only after it has emitted the current canonical/direct pose.
        _present(pose,sole);
        Measure();
        _presentPerch?.Invoke(_perch?.Current.Phase ?? EdgePerchPhase.None,
            _perch?.Facing ?? FacingDirection.Right,_presentationDelta,_immediatePerchRestore);
        _immediatePerchRestore=false;
    }

    private void Measure()
    {
        if(_measure() is not { } geometry) return;
        var c=geometry.Contact; var b=geometry.Bounds;
        if(!double.IsFinite(c.Left)||!double.IsFinite(c.Right)||!double.IsFinite(c.SoleY)||!double.IsFinite(c.VisibleTop)||
            !double.IsFinite(b.X)||!double.IsFinite(b.Y)||!double.IsFinite(b.Right)||!double.IsFinite(b.Bottom)||b.Width<=0||b.Height<=0) return;
        Contact=c; _visibleBounds=b; _hasGeometry=true;
    }
    internal void Reset(PointD displayed) {_motion.Reset(displayed);_lastPosition=displayed;_landingClearanceHeight=null;}
    internal void Clear(PointD displayed)
    {
        _perch?.ClearReadiness();
        ReleasePerch(displayed,immediate:true);Reset(displayed);_present(null,null);Measure();
        _presentPerch?.Invoke(EdgePerchPhase.None,_perch?.Facing ?? FacingDirection.Right,TimeSpan.Zero,true);
        _immediatePerchRestore=false;
    }
    private void ReleasePerch(PointD displayed,bool immediate)
    {
        var perch=_perch;
        if(perch is null)return;
        if(perch.Current.Phase!=EdgePerchPhase.None)
        {
            perch.Release(displayed);_motion.Reset(displayed);_lastPosition=displayed;_framePosition=displayed;
        }
        _immediatePerchRestore=immediate;
    }
    private PerchSurface[] BuildPerchSurfaces()
    {
        if(_scene is null)return [];
        var z=_scene.Windows.GroupBy(w=>w.Key).ToDictionary(g=>g.Key,g=>g.Min(w=>w.ZOrder));
        return PlatformGeometry.Build(_scene,0).Select(s=>new PerchSurface(s,z.GetValueOrDefault(s.Key,int.MaxValue))).ToArray();
    }
    private static bool Finite(PointD p)=>double.IsFinite(p.X)&&double.IsFinite(p.Y);
    private static bool WithinPixel(PointD a,PointD b,DesktopCoordinateMap map)=>
        Math.Abs(a.X-b.X)*map.ScaleX<=1.000001 && Math.Abs(a.Y-b.Y)*map.ScaleY<=1.000001;
    private static double Distance(PointD a,PointD b)=>Math.Max(Math.Abs(a.X-b.X),Math.Abs(a.Y-b.Y));
    private static double DistanceTo(RectD b,PointD p)
    {
        var x=Math.Max(b.X-p.X,Math.Max(0,p.X-b.Right));var y=Math.Max(b.Y-p.Y,Math.Max(0,p.Y-b.Bottom));return x*x+y*y;
    }
}
