using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Runtime;

internal sealed class PetLoopClock
{
    private readonly Func<TimeSpan> _getElapsed;
    private readonly Action _start;
    private readonly Action _stop;

    internal PetLoopClock(Func<TimeSpan> getElapsed, Action start, Action stop)
    {
        _getElapsed = getElapsed ?? throw new ArgumentNullException(nameof(getElapsed));
        _start = start ?? throw new ArgumentNullException(nameof(start));
        _stop = stop ?? throw new ArgumentNullException(nameof(stop));
    }

    internal TimeSpan Elapsed => _getElapsed();
    internal void Start() => _start();
    internal void Stop() => _stop();
}

internal sealed class PetLoopTimer
{
    private readonly Action<EventHandler> _attach;
    private readonly Action<EventHandler> _detach;
    private readonly Action _start;
    private readonly Action _stop;

    internal PetLoopTimer(
        Action<EventHandler> attach,
        Action<EventHandler> detach,
        Action start,
        Action stop)
    {
        _attach = attach ?? throw new ArgumentNullException(nameof(attach));
        _detach = detach ?? throw new ArgumentNullException(nameof(detach));
        _start = start ?? throw new ArgumentNullException(nameof(start));
        _stop = stop ?? throw new ArgumentNullException(nameof(stop));
    }

    internal void Attach(EventHandler tick) => _attach(tick);
    internal void Detach(EventHandler tick) => _detach(tick);
    internal void Start() => _start();
    internal void Stop() => _stop();
}

internal sealed class PetLoopHost
{
    private readonly Func<RectD> _getWorkArea;
    private readonly Func<SizeD> _getPetSize;
    private readonly Func<SizeD> _getDragThreshold;
    private readonly Func<PointerSample> _samplePointer;
    private readonly Func<bool> _isPrimaryButtonDown;
    private readonly Func<PointD> _getWindowPosition;
    private readonly Action<PointD> _setWindowPosition;
    private readonly Action<PetSnapshot> _render;
    private readonly Func<bool> _captureMouse;
    private readonly Action _releaseMouseCapture;

    internal PetLoopHost(
        Func<RectD> getWorkArea,
        Func<SizeD> getPetSize,
        Func<SizeD> getDragThreshold,
        Func<PointerSample> samplePointer,
        Func<bool> isPrimaryButtonDown,
        Func<PointD> getWindowPosition,
        Action<PointD> setWindowPosition,
        Action<PetSnapshot> render,
        Func<bool> captureMouse,
        Action releaseMouseCapture)
    {
        _getWorkArea = getWorkArea ?? throw new ArgumentNullException(nameof(getWorkArea));
        _getPetSize = getPetSize ?? throw new ArgumentNullException(nameof(getPetSize));
        _getDragThreshold = getDragThreshold ?? throw new ArgumentNullException(nameof(getDragThreshold));
        _samplePointer = samplePointer ?? throw new ArgumentNullException(nameof(samplePointer));
        _isPrimaryButtonDown = isPrimaryButtonDown ?? throw new ArgumentNullException(nameof(isPrimaryButtonDown));
        _getWindowPosition = getWindowPosition ?? throw new ArgumentNullException(nameof(getWindowPosition));
        _setWindowPosition = setWindowPosition ?? throw new ArgumentNullException(nameof(setWindowPosition));
        _render = render ?? throw new ArgumentNullException(nameof(render));
        _captureMouse = captureMouse ?? throw new ArgumentNullException(nameof(captureMouse));
        _releaseMouseCapture = releaseMouseCapture ?? throw new ArgumentNullException(nameof(releaseMouseCapture));
    }

    internal RectD GetWorkArea() => _getWorkArea();
    internal SizeD GetPetSize() => _getPetSize();
    internal SizeD GetDragThreshold() => _getDragThreshold();
    internal PointerSample SamplePointer() => _samplePointer();
    internal bool IsPrimaryButtonDown() => _isPrimaryButtonDown();
    internal PointD GetWindowPosition() => _getWindowPosition();
    internal void SetWindowPosition(PointD position) => _setWindowPosition(position);
    internal void Render(PetSnapshot snapshot) => _render(snapshot);
    internal bool CaptureMouse() => _captureMouse();
    internal void ReleaseMouseCapture() => _releaseMouseCapture();
}
