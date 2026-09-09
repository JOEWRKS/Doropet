using Dororong.Core.Platforms;

namespace Dororong.App.Runtime;

internal enum SceneReadHealth { Fresh, Cached, TemporarilyUnavailable, Expired }
internal sealed record SceneRead(DesktopScene? Scene, SceneReadHealth Health);
internal interface IDesktopSceneNative { DesktopScene? Capture(TimeSpan now); }
internal sealed class DesktopSceneSource(IDesktopSceneNative native, bool background = false)
{
    private DesktopScene? scene;
    private TimeSpan? lastAttempt;
    private TimeSpan lastSuccess;
    private TimeSpan highWater;
    private bool failed;
    private Task<CaptureResult>? pending;
    internal string? LastFailure { get; private set; }
    // Caller supplies Stopwatch elapsed time on the UI thread. Clamp accidental rollback.
    internal SceneRead Read(TimeSpan now)
    {
        now = highWater = now > highWater ? now : highWater;
        var fresh = false;
        if (pending is { IsCompletedSuccessfully: true })
        {
            Accept(pending.Result);
            pending = null;
        }
        if (pending is null && (lastAttempt is null || now - lastAttempt.Value >= TimeSpan.FromMilliseconds(80)))
        {
            var initial = lastAttempt is null;
            lastAttempt = now;
            // Seed startup placement synchronously before animation starts. After
            // that, at most one metadata-only acquisition can be in flight. It
            // neither touches WPF nor posts callbacks to a possibly closed window.
            if (background && !initial) pending = Task.Run(() => Capture(now));
            else Accept(Capture(now));
        }
        if (scene is null || now - lastSuccess >= TimeSpan.FromMilliseconds(500))
            return new(null, SceneReadHealth.Expired);
        return new(scene, fresh ? SceneReadHealth.Fresh : failed ? SceneReadHealth.TemporarilyUnavailable : SceneReadHealth.Cached);

        void Accept(CaptureResult result)
        {
            LastFailure = result.Failure;
            failed = result.Scene is null;
            if (result.Scene is not { } captured) return;
            scene = captured;
            // A delayed query (including across sleep/resume) must not turn old
            // geometry fresh merely because the UI just received the result.
            lastSuccess = result.RequestedAt;
            fresh = true;
        }
    }

    private CaptureResult Capture(TimeSpan requestedAt)
    {
        try
        {
            var captured = native.Capture(requestedAt);
            return new(requestedAt, captured is null ? null : captured with
            {
                Monitors = Array.AsReadOnly(captured.Monitors.ToArray()),
                Windows = Array.AsReadOnly(captured.Windows.ToArray())
            }, captured is null ? "CaptureUnavailable" : null);
        }
        catch (Exception exception)
        {
            return new(requestedAt, null, exception is Interop.DesktopMetadataException metadata
                ? metadata.Diagnostic : exception.GetType().Name);
        }
    }

    private sealed record CaptureResult(TimeSpan RequestedAt, DesktopScene? Scene, string? Failure);
}
