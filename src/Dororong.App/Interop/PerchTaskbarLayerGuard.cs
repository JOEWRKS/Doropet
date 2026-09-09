using System.ComponentModel;
using System.Text;

namespace Dororong.App.Interop;

internal static class PerchTaskbarLayerGuard
{
    // Called only after an active perch is rendered. TOPMOST is a band, not an
    // ordering guarantee against the shell's own topmost taskbar.
    internal static void EnsureAboveOverlappingTaskbar(nint pet)
    {
        try
        {
            if (pet == 0 || DesktopMetadataReader.Api.GetWindowThreadProcessId(pet, out var pid) == 0 ||
                pid != Environment.ProcessId || !DesktopMetadataReader.Api.IsWindowVisible(pet) ||
                (NativeMethods.GetWindowLongPtr(pet, NativeMethods.GwlExStyle).ToInt64() & NativeMethods.WsExTopmost) == 0 ||
                !DesktopMetadataReader.Api.GetWindowRect(pet, out var petRect)) return;

            var name = new StringBuilder(256);
            var visited = new HashSet<nint>();
            var candidate = DesktopMetadataReader.Api.GetWindow(pet, 3); // GW_HWNDPREV: above pet only.
            for (var count = 0; candidate != 0 && count < 128 && visited.Add(candidate); count++)
            {
                if (DesktopMetadataReader.Api.GetClassName(candidate, name, name.Capacity) != 0 &&
                    name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd" &&
                    DesktopMetadataReader.Api.IsWindowVisible(candidate) &&
                    (NativeMethods.GetWindowLongPtr(candidate, NativeMethods.GwlExStyle).ToInt64() & NativeMethods.WsExTopmost) != 0 &&
                    DesktopMetadataReader.Api.GetWindowRect(candidate, out var taskbar) &&
                    petRect.Left < taskbar.Right && petRect.Right > taskbar.Left &&
                    petRect.Top < taskbar.Bottom && petRect.Bottom > taskbar.Top)
                {
                    // Only our HWND changes position within the topmost band;
                    // preserve its geometry, activation and any owner's order.
                    NativeMethods.SetWindowPosChecked(pet, new(-1), 0, 0, 0, 0,
                        NativeMethods.SwpNoMove | NativeMethods.SwpNoSize |
                        NativeMethods.SwpNoActivate | NativeMethods.SwpNoOwnerZOrder);
                    return;
                }
                candidate = DesktopMetadataReader.Api.GetWindow(candidate, 3);
            }
        }
        catch (Win32Exception)
        {
            // Shell HWNDs may disappear during this bounded metadata read.
            // An active perch can recheck on its next tick.
        }
    }
}
