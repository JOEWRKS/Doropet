using System.Windows;
using System.Windows.Interop;
using Dororong.Core.Geometry;
using Dororong.Core.Behavior;

namespace Dororong.App.Interop;

internal sealed class DesktopInput
{
    internal bool TryGetPointerInDips(HwndSource source, out PointD pointer)
    {
        var sample = SamplePointer(source);
        pointer = sample.Position;
        return sample.IsAvailable;
    }

    internal PointerSample SamplePointer(HwndSource source)
    {
        if (!NativeMethods.GetCursorPos(out var physicalPoint))
        {
            return PointerSample.Unavailable;
        }

        var compositionTarget = source.CompositionTarget;
        if (compositionTarget is null)
        {
            return PointerSample.Unavailable;
        }

        var dipPoint = compositionTarget.TransformFromDevice.Transform(
            new Point(physicalPoint.X, physicalPoint.Y));
        return new PointerSample(true, new PointD(dipPoint.X, dipPoint.Y))
        {
            ScreenPixelPosition = new PointD(physicalPoint.X, physicalPoint.Y)
        };
    }

    internal bool IsPrimaryButtonDown() =>
        (NativeMethods.GetAsyncKeyState(NativeMethods.VkLButton) & 0x8000) != 0;
}
