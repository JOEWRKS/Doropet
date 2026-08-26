using System.Windows;
using System.Windows.Interop;
using Dororong.Core.Geometry;

namespace Dororong.App.Interop;

internal sealed class DesktopInput
{
    internal bool TryGetPointerInDips(HwndSource source, out PointD pointer)
    {
        pointer = default;

        if (!NativeMethods.GetCursorPos(out var physicalPoint))
        {
            return false;
        }

        var compositionTarget = source.CompositionTarget;
        if (compositionTarget is null)
        {
            return false;
        }

        var dipPoint = compositionTarget.TransformFromDevice.Transform(
            new Point(physicalPoint.X, physicalPoint.Y));
        pointer = new PointD(dipPoint.X, dipPoint.Y);
        return true;
    }

    internal bool IsPrimaryButtonDown() =>
        (NativeMethods.GetAsyncKeyState(NativeMethods.VkLButton) & 0x8000) != 0;
}
