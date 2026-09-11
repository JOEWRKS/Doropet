using Dororong.App.Controls;
using Dororong.Core.Behavior;

namespace Dororong.App.Tests.Controls;

public sealed class PounceSessionTests
{
    [Fact]
    public void Continuous_dwell_launches_a_short_arc_then_lands_at_the_forward_endpoint()
    {
        var s = new PounceSession();
        Assert.Equal(PouncePhase.Watch, s.Advance(1.49, true, 54, FacingDirection.Right).Phase);
        Assert.Equal(PouncePhase.Flight, s.Advance(.01, true, 54, FacingDirection.Right).Phase);
        var peak = s.Advance(.17, true, -54, FacingDirection.Left);
        Assert.Equal(14, peak.DeltaX, 6);
        Assert.Equal(-12, peak.OffsetY, 6);
        Assert.Equal(FacingDirection.Right, peak.Direction);
        var landed = s.Advance(.17, false, double.NaN, FacingDirection.Left);
        Assert.Equal(PouncePhase.Landing, landed.Phase);
        Assert.Equal(14, landed.DeltaX, 6);
        Assert.Equal(0, landed.OffsetY);
        Assert.Equal(PouncePhase.Track, s.Advance(.28, false, double.NaN, FacingDirection.Left).Phase);
    }

    [Fact]
    public void Departure_resets_dwell_and_close_target_stops_short()
    {
        var s = new PounceSession();
        s.Advance(1.4, true, -20, FacingDirection.Left);
        s.Advance(.01, false, -20, FacingDirection.Left);
        Assert.Equal(PouncePhase.Watch, s.Advance(.2, true, -20, FacingDirection.Left).Phase);
        s.Advance(1.3, true, -20, FacingDirection.Left);
        var landed = s.Advance(.34, true, -20, FacingDirection.Left);
        Assert.Equal(-12, landed.DeltaX, 6);
        Assert.Equal(PouncePhase.Landing, landed.Phase);
    }

    [Fact]
    public void Tracking_gets_three_seconds_and_does_not_count_as_new_preparation()
    {
        var s = new PounceSession();
        var tracking = s.Advance(2.12, true, 54, FacingDirection.Right);
        Assert.Equal(PouncePhase.Track, tracking.Phase);
        Assert.Equal(3, tracking.TrackingRemaining, 6);
        Assert.Equal(28, tracking.DeltaX, 6);
        tracking = s.Advance(2.99, false, -200, FacingDirection.Right);
        Assert.Equal(PouncePhase.Track, tracking.Phase);
        Assert.Equal(FacingDirection.Left, tracking.Direction);
        Assert.Equal(0, tracking.DeltaX);
        Assert.Equal(PouncePhase.Watch, s.Advance(.01, true, 54, FacingDirection.Right).Phase);
        Assert.Equal(PouncePhase.Watch, s.Advance(1.49, true, 54, FacingDirection.Right).Phase);
        Assert.Equal(PouncePhase.Flight, s.Advance(.01, true, 54, FacingDirection.Right).Phase);
    }

    [Fact]
    public void Interruption_discards_motion_without_returning_to_old_origin()
    {
        var s = new PounceSession();
        s.Advance(1.67, true, 54, FacingDirection.Right);
        var canceled = s.Advance(.1, true, 54, FacingDirection.Right, blocked: true);
        Assert.Equal(PouncePhase.Watch, canceled.Phase);
        Assert.Equal(0, canceled.DeltaX);
        Assert.Equal(PouncePhase.Watch, s.Advance(1.4, true, 54, FacingDirection.Right).Phase);
    }

    [Fact]
    public void Invalid_pointer_cannot_build_dwell_or_launch()
    {
        var s = new PounceSession();
        s.Advance(1.4, true, 54, FacingDirection.Right);
        s.Advance(.2, true, double.NaN, FacingDirection.Right);
        Assert.Equal(PouncePhase.Watch, s.Advance(.2, true, 54, FacingDirection.Right).Phase);
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Advance(double.NaN, true, 54, FacingDirection.Right));
    }
}
