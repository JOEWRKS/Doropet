using Dororong.App.Runtime;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public class BackgroundSceneTests
{
    // Reverting background acquisition to an inline Capture makes this fail:
    // the next read publishes revision 2 instead of retaining revision 1.
    [Fact]
    public void Refresh_does_not_wait_for_native_capture_on_the_animation_thread()
    {
        using var release = new ManualResetEventSlim();
        using var entered = new ManualResetEventSlim();
        using var completed = new ManualResetEventSlim();
        var calls = 0;
        var caller = Environment.CurrentManagedThreadId;
        var worker = caller;
        var source = new DesktopSceneSource(new Native(now =>
        {
            var call = Interlocked.Increment(ref calls);
            if (call == 1) return Scene(1, now);
            if (call > 2) return null;
            worker = Environment.CurrentManagedThreadId;
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(2));
            completed.Set();
            return Scene(2, now);
        }), background: true);
        Assert.Equal(SceneReadHealth.Fresh, source.Read(Ms(0)).Health);
        try
        {
            var read = source.Read(Ms(80));
            Assert.Equal(1, read.Scene!.Revision);
            Assert.Equal(SceneReadHealth.Cached, read.Health);
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            Assert.NotEqual(caller, worker);
            Assert.Equal(1, source.Read(Ms(160)).Scene!.Revision);
            Assert.Equal(2, Volatile.Read(ref calls)); // no overlapping query
        }
        finally { release.Set(); Assert.True(completed.Wait(TimeSpan.FromSeconds(5))); }
        Assert.True(SpinWait.SpinUntil(() => source.Read(Ms(161)).Scene?.Revision == 2, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Delayed_refresh_expires_from_request_time_not_delivery_time()
    {
        using var release = new ManualResetEventSlim();
        using var completed = new ManualResetEventSlim();
        var calls = 0;
        var source = new DesktopSceneSource(new Native(now =>
        {
            var call = Interlocked.Increment(ref calls);
            if (call == 1) return Scene(1, now);
            if (call > 2) return null;
            release.Wait(TimeSpan.FromSeconds(2));
            completed.Set();
            return Scene(2, now);
        }), background: true);
        source.Read(Ms(0));
        try
        {
            Assert.Equal(1, source.Read(Ms(80)).Scene!.Revision);
            Assert.Equal(SceneReadHealth.Expired, source.Read(Ms(580)).Health);
        }
        finally { release.Set(); Assert.True(completed.Wait(TimeSpan.FromSeconds(5))); }
        // Completing a five-hundred-ms-old read cannot revive a disappeared edge.
        Assert.True(SpinWait.SpinUntil(() =>
        {
            var read = source.Read(Ms(580));
            Assert.Equal(SceneReadHealth.Expired, read.Health);
            return Volatile.Read(ref calls) >= 3; // previous result was consumed
        }, TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Background_exception_retains_support_without_leaking_message_then_recovers()
    {
        var calls = 0;
        var source = new DesktopSceneSource(new Native(now => Interlocked.Increment(ref calls) switch
        {
            1 => Scene(1, now),
            2 => throw new InvalidOperationException("private title"),
            _ => Scene(2, now)
        }), background: true);
        source.Read(Ms(0));
        source.Read(Ms(80));
        Assert.True(SpinWait.SpinUntil(() => source.Read(Ms(81)).Health == SceneReadHealth.TemporarilyUnavailable, TimeSpan.FromSeconds(5)));
        Assert.Equal(1, source.Read(Ms(82)).Scene!.Revision);
        Assert.Equal("InvalidOperationException", source.LastFailure);
        source.Read(Ms(160));
        Assert.True(SpinWait.SpinUntil(() => source.Read(Ms(161)).Scene?.Revision == 2, TimeSpan.FromSeconds(5)));
        Assert.Null(source.LastFailure);
    }

    [Fact]
    public void Finishing_inflight_capture_after_last_read_does_not_schedule_more_work()
    {
        using var release = new ManualResetEventSlim();
        using var completed = new ManualResetEventSlim();
        var calls = 0;
        var source = new DesktopSceneSource(new Native(now =>
        {
            var call = Interlocked.Increment(ref calls);
            if (call == 1) return Scene(1, now);
            if (call > 2) return null;
            release.Wait(TimeSpan.FromSeconds(2));
            completed.Set();
            return Scene(2, now);
        }), background: true);
        source.Read(Ms(0));
        source.Read(Ms(80));
        release.Set();
        Assert.True(completed.Wait(TimeSpan.FromSeconds(5)));
        Thread.Sleep(120); // longer than polling cadence, with no further UI reads
        Assert.Equal(2, Volatile.Read(ref calls));
    }

    private static TimeSpan Ms(int value) => TimeSpan.FromMilliseconds(value);
    private static DesktopScene Scene(long revision, TimeSpan now) => new(revision, now,
        [new DesktopMonitor(1, new(0, 0, 1920, 1080))], []);
    private sealed class Native(Func<TimeSpan, DesktopScene?> capture) : IDesktopSceneNative
    {
        public DesktopScene? Capture(TimeSpan now) => capture(now);
    }
}
