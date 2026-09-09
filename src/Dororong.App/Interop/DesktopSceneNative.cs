using Dororong.App.Runtime;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;

namespace Dororong.App.Interop;

internal sealed record NativeWindow(long Handle, uint ProcessId, RectD Bounds, bool Visible = true,
    bool Minimized = false, bool Cloaked = false, bool Excluded = false, bool CanSupport = true,
    bool Taskbar = false, bool HorizontalTaskbar = false, string ClassName = "");
internal sealed record NativeTaskbarMetadata(long MonitorId, uint Edge, long AutoHideHandle);
internal sealed record NativeDesktop(IReadOnlyList<DesktopMonitor> Monitors, IReadOnlyList<NativeWindow> Windows)
{
    internal IReadOnlyList<NativeTaskbarMetadata> Taskbars { get; init; } = Array.Empty<NativeTaskbarMetadata>();
    internal RectD? SystemTaskbarBounds { get; init; }
}
internal interface IDesktopMetadataReader { NativeDesktop? Read(); }
internal sealed class DesktopMetadataException(string operation, int code) : Exception
{
    internal string Diagnostic => $"{operation}:{code}";
}
internal sealed class DesktopMetadataReader(nint petHandle) : IDesktopMetadataReader
{
    internal static IReadOnlyList<DesktopMonitor> ReadMonitorBounds()
    {
        var previous=Api.SetThreadDpiAwarenessContext(new nint(-4));
        if(previous==0) throw Failure("DpiContext");
        try {return ReadMonitors().AsReadOnly();}
        finally {if(Api.SetThreadDpiAwarenessContext(previous)==0) throw Failure("RestoreDpiContext");}
    }

    private static List<DesktopMonitor> ReadMonitors()
    {
        var monitors = new List<DesktopMonitor>();
        var monitorError = false;
        Api.MonitorCallback callback = (nint monitor, nint dc, ref Api.Rect rectangle, nint data) =>
        {
            var info = new Api.MonitorInfo { Size = Marshal.SizeOf<Api.MonitorInfo>() };
            if (!Api.GetMonitorInfo(monitor,ref info)) { monitorError=true; return false; }
            monitors.Add(new(monitor.ToInt64(),info.Monitor.ToRect())); return monitors.Count < 128;
        };
        if (!Api.EnumDisplayMonitors(0,0,callback,0) || monitorError || monitors.Count == 0)
            throw Failure("MonitorEnumeration");
        return monitors;
    }
    // Class names are shell heuristics, not a universal Windows contract. Unknown classes
    // stay ordinary windows. Executable identity excludes other pets, never all WPF apps.
    public NativeDesktop? Read()
    {
        var previous = Api.SetThreadDpiAwarenessContext(new nint(-4)); // PER_MONITOR_AWARE_V2
        if (previous == 0) throw Failure("DpiContext");
        try
        {
            var monitors = ReadMonitors();
            var handles = new HashSet<nint>();
            if (!Api.EnumWindows((handle,_) => { handles.Add(handle); return handles.Count < 4096; },0))
                throw Failure("WindowEnumeration");
            // Explicit bounded z-order traversal; EnumWindows alone is not used as an ordering contract.
            var windows = new List<NativeWindow>(); var visited = new HashSet<nint>();
            for (var handle=Api.GetTopWindow(0); handle != 0; handle=Api.GetWindow(handle,2))
            {
                if (!visited.Add(handle) || visited.Count > 4096) throw Failure("ZOrderTraversal");
                if (!handles.Remove(handle)) continue;
                var window=ReadWindow(handle);
                if (window is not null) windows.Add(window);
            }
            if (handles.Any(Api.IsWindow)) throw Failure("ZOrderChanged");
            var taskbars = new List<NativeTaskbarMetadata>();
            foreach (var monitor in monitors)
            for (uint edge=0; edge<4; edge++)
            {
                var data=Api.AppbarData.Create(); data.Edge=edge; data.Rectangle=Api.Rect.From(monitor.Bounds);
                // NULL means no appbar OR error, so it never proves current hidden state.
                var handle=Api.SHAppBarMessage(0xB,ref data);
                taskbars.Add(new(monitor.Id,edge,unchecked((long)handle.ToUInt64())));
            }
            var systemBar=Api.AppbarData.Create();
            RectD? systemBounds=Api.SHAppBarMessage(5,ref systemBar) != UIntPtr.Zero ? systemBar.Rectangle.ToRect() : null;
            return new(monitors.AsReadOnly(),windows.AsReadOnly()) { Taskbars=taskbars.AsReadOnly(),SystemTaskbarBounds=systemBounds };
        }
        finally
        {
            if (Api.SetThreadDpiAwarenessContext(previous)==0) throw Failure("RestoreDpiContext");
        }
    }

    internal NativeWindow? ReadWindow(nint handle)
    {
        if (!Api.IsWindow(handle)) return null;
        var thread=Api.GetWindowThreadProcessId(handle,out var pid);
        if (thread==0 || pid==0) return DestroyedOrThrow(handle,"OwnerPid");
        var name=new StringBuilder(256);
        if (Api.GetClassName(handle,name,name.Capacity)==0) return DestroyedOrThrow(handle,"Class");
        var className=name.ToString();
        var excluded=handle==petHandle || className is "Progman" or "WorkerW" or "Shell_DesktopWnd";
        var visible=Api.IsWindowVisible(handle); var minimized=Api.IsIconic(handle);
        if (!excluded && visible && !minimized) excluded=IsPetProcess(pid);
        var taskbar=className is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
        var bounds=new RectD(); var cloaked=false;
        if (!excluded && visible && !minimized)
        {
            var hr=Api.DwmCloaked(handle,14,out var cloak, sizeof(int));
            if (hr<0) return DestroyedOrThrow(handle,"DwmCloaked",hr);
            cloaked=cloak!=0;
            if (!cloaked)
            {
                hr=Api.DwmFrame(handle,9,out var frame,Marshal.SizeOf<Api.Rect>());
                if (hr<0) return DestroyedOrThrow(handle,"DwmFrame",hr);
                bounds=frame.ToRect();
                // Shell appbar geometry must describe the currently exposed HWND, not its reserved position.
                if (taskbar)
                {
                    if (!Api.GetWindowRect(handle,out var rectangle)) return DestroyedOrThrow(handle,"TaskbarRect");
                    bounds=rectangle.ToRect();
                }
            }
        }
        if (!Api.IsWindow(handle)) return null;
        if (Api.GetWindowThreadProcessId(handle,out var finalPid)==0 || finalPid!=pid)
            throw Failure("OwnerChanged");
        return new(handle.ToInt64(),pid,bounds,visible,minimized,cloaked,excluded,
            className is not "#32768" and not "tooltips_class32",taskbar,taskbar && bounds.Width>bounds.Height,className);
    }
    private static bool IsPetProcess(uint pid)
    {
        var process=Api.OpenProcess(0x1000,false,pid);
        if (process==0) throw Failure("ProcessIdentity");
        try
        {
            var path=new StringBuilder(32768); uint size=(uint)path.Capacity;
            if (!Api.QueryFullProcessImageName(process,0,path,ref size)) throw Failure("ProcessIdentity");
            return string.Equals(Path.GetFileName(path.ToString()),"Dororong.App.exe",StringComparison.OrdinalIgnoreCase);
        }
        finally { Api.CloseHandle(process); }
    }
    private static NativeWindow? DestroyedOrThrow(nint handle,string operation,int? code=null)
    {
        var error=code ?? Marshal.GetLastWin32Error();
        if (!Api.IsWindow(handle)) return null;
        throw new DesktopMetadataException(operation,error);
    }
    private static DesktopMetadataException Failure(string operation) => new(operation,Marshal.GetLastWin32Error());
    internal static class Api
    {
        [StructLayout(LayoutKind.Sequential)] internal struct Rect
        {
            internal int Left,Top,Right,Bottom;
            internal readonly RectD ToRect()=>new(Left,Top,Right-Left,Bottom-Top);
            internal static Rect From(RectD r)=>new(){Left=(int)r.X,Top=(int)r.Y,Right=(int)r.Right,Bottom=(int)r.Bottom};
        }
        [StructLayout(LayoutKind.Sequential)] internal struct MonitorInfo { internal int Size; internal Rect Monitor,Work; internal uint Flags; }
        [StructLayout(LayoutKind.Sequential)] internal struct AppbarData
        {
            internal uint Size; internal nint Handle; internal uint Callback,Edge; internal Rect Rectangle; internal nint Param;
            internal static AppbarData Create()=>new(){Size=(uint)Marshal.SizeOf<AppbarData>()};
        }
        internal delegate bool WindowCallback(nint handle,nint data);
        internal delegate bool MonitorCallback(nint handle,nint dc,ref Rect rectangle,nint data);
        [DllImport("user32.dll",SetLastError=true)] internal static extern nint SetThreadDpiAwarenessContext(nint context);
        [DllImport("user32.dll")] internal static extern nint GetThreadDpiAwarenessContext();
        [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool EnumDisplayMonitors(nint dc,nint clip,MonitorCallback callback,nint data);
        [DllImport("user32.dll",EntryPoint="GetMonitorInfoW",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool GetMonitorInfo(nint monitor,ref MonitorInfo info);
        [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool EnumWindows(WindowCallback callback,nint data);
        [DllImport("user32.dll")] internal static extern nint GetTopWindow(nint handle);
        [DllImport("user32.dll")] internal static extern nint GetWindow(nint handle,uint command);
        [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindow(nint handle);
        [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool IsWindowVisible(nint handle);
        [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool IsIconic(nint handle);
        [DllImport("user32.dll",SetLastError=true)] internal static extern uint GetWindowThreadProcessId(nint handle,out uint pid);
        [DllImport("user32.dll",EntryPoint="GetClassNameW",CharSet=CharSet.Unicode,SetLastError=true)] internal static extern int GetClassName(nint handle,StringBuilder name,int size);
        [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool GetWindowRect(nint handle,out Rect rectangle);
        [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")] internal static extern int DwmFrame(nint handle,uint attribute,out Rect value,int size);
        [DllImport("dwmapi.dll",EntryPoint="DwmGetWindowAttribute")] internal static extern int DwmCloaked(nint handle,uint attribute,out int value,int size);
        [DllImport("shell32.dll")] internal static extern UIntPtr SHAppBarMessage(uint message,ref AppbarData data);
        [DllImport("kernel32.dll",SetLastError=true)] internal static extern nint OpenProcess(uint access,[MarshalAs(UnmanagedType.Bool)]bool inherit,uint pid);
        [DllImport("kernel32.dll",EntryPoint="QueryFullProcessImageNameW",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool QueryFullProcessImageName(nint process,uint flags,StringBuilder path,ref uint size);
        [DllImport("kernel32.dll")] [return:MarshalAs(UnmanagedType.Bool)] internal static extern bool CloseHandle(nint handle);
    }
}
internal sealed class DesktopSceneNative(IDesktopMetadataReader reader) : IDesktopSceneNative
{
    private Dictionary<long, (uint Pid, long Generation, TimeSpan? VisibleSince)> identities = new();
    private long generation;
    private long revision;
    internal DesktopSceneNative(nint petHandle) : this(new DesktopMetadataReader(petHandle)) { }
    public DesktopScene? Capture(TimeSpan now)
    {
        var metadata = reader.Read();
        if (metadata is null) return null;
        var next = new Dictionary<long, (uint Pid, long Generation, TimeSpan? VisibleSince)>();
        var windows = new List<DesktopWindow>();
        foreach (var window in metadata.Windows)
        {
            if (window.Handle == 0) continue;
            if (!identities.TryGetValue(window.Handle, out var identity) || identity.Pid != window.ProcessId)
                identity = (window.ProcessId, ++generation, null);
            var bounds = window.Bounds;
            // Hover previews, tooltips and their separate decorative shadow
            // HWNDs can cross an existing surface edge. Classify SysShadow
            // independently of whether its tooltip is also enumerated.
            // They are neither perches nor holes in perches. Keep actual menus,
            // unknown application classes and real taskbars on the normal path.
            var hoverOverlay = window.ClassName is "TaskListThumbnailWnd" or "TaskListOverlayWnd" or "tooltips_class32" or "SysShadow";
            var support = window.CanSupport && !hoverOverlay;
            if (window.Taskbar)
            {
                var clips = metadata.Monitors.Select(m => Intersect(bounds,m.Bounds)).Where(r => r.Width > 0 && r.Height > 0);
                bounds = clips.OrderByDescending(r => r.Width*r.Height).FirstOrDefault();
                var thickness = window.HorizontalTaskbar ? bounds.Height : bounds.Width;
                if (!window.Visible || window.Minimized || window.Cloaked || thickness <= 2)
                    identity.VisibleSince = null;
                else
                    identity.VisibleSince ??= now;
                support &= window.HorizontalTaskbar && identity.VisibleSince is { } since && now-since >= TimeSpan.FromMilliseconds(80);
            }
            next[window.Handle] = identity;
            windows.Add(new(new(window.Handle,window.ProcessId,identity.Generation),bounds,windows.Count,
                window.Visible,window.Minimized,window.Cloaked,window.Excluded,support,!hoverOverlay,window.Taskbar,window.HorizontalTaskbar));
        }
        identities = next; // Only successful absence retires an observed identity.
        return new(++revision,now,Array.AsReadOnly(metadata.Monitors.ToArray()),windows.AsReadOnly());
    }
    private static RectD Intersect(RectD a, RectD b) => new(Math.Max(a.X,b.X),Math.Max(a.Y,b.Y),
        Math.Max(0,Math.Min(a.Right,b.Right)-Math.Max(a.X,b.X)),Math.Max(0,Math.Min(a.Bottom,b.Bottom)-Math.Max(a.Y,b.Y)));
}
