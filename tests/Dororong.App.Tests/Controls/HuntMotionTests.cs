using Dororong.App.Controls;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Controls;

public sealed class HuntMotionTests
{
    [Fact]
    public void Crouch_starts_without_notice_delay_and_finishes_in_250ms()
    {
        var s=new HuntingSession();s.Advance(0,true);
        Assert.Equal(0,HuntPose.At(s.FrameAgeSeconds).Amount,10);
        s.Advance(.125,true);Assert.Equal(.5,HuntPose.At(s.FrameAgeSeconds).Amount,10);
        s.Advance(.125,true);Assert.Equal(1,HuntPose.At(s.FrameAgeSeconds).Amount,10);
        s.Advance(.5,true);Assert.Equal(.75,s.FrameAgeSeconds,10);
        s.Advance(.25,true);s.Advance(0,false);s.Advance(1.325,false);
        Assert.Equal(2.325,s.FrameAgeSeconds,10);
        Assert.Equal(.5,HuntPose.At(s.FrameAgeSeconds).Amount,10);
    }
    [Theory]
    [InlineData(1.75)] [InlineData(1.9)] [InlineData(2.0)] [InlineData(2.3)]
    public void Reentry_preserves_rump_offset_as_well_as_crouch_depth(double exitAge)
    {
        var s=new HuntingSession();s.Advance(0,true);s.Advance(1,true);
        s.Advance(0,false);
        s.Advance(exitAge-1,false);
        Assert.Equal(exitAge,s.AgeSeconds,8);
        var before=HuntPose.At(s.AgeSeconds);
        s.Advance(0,true);
        Assert.Equal(before,HuntPose.At(s.AgeSeconds));
        s.Advance(.01,true);
        Assert.InRange(Math.Abs(HuntPose.At(s.AgeSeconds).Sway-before.Sway),0,.3);
        var reversed=s.AgeSeconds;s.Advance(.01,false);
        Assert.Equal(reversed+.01,s.AgeSeconds,8);
    }
    [Fact]
    public void Reapproach_during_recovery_reverses_without_finishing_the_exit()
    {
        var session=new HuntingSession();session.Advance(0,true);
        for(var i=0;i<100;i++)session.Advance(.01,true);
        while(session.AgeSeconds<2.3)session.Advance(.01,false);
        var previous=HuntPose.At(session.AgeSeconds).Amount;
        session.Advance(.01,true);
        var next=HuntPose.At(session.AgeSeconds).Amount;
        Assert.True(next>=previous,"Reapproach must deepen crouch immediately, not continue standing up.");
        Assert.InRange(next-previous,0,.05);
        for(var i=0;i<150;i++)session.Advance(.01,true);
        Assert.True(session.IsActive);Assert.Equal(1,HuntPose.At(session.AgeSeconds).Amount,8);
    }
    [Theory]
    [InlineData(-1, 0, 0, 0, 0)]
    [InlineData(0.475, 0.5, 3.15, -0.035, 0)]
    [InlineData(0.7, 1, 6.3, -0.07, 0)]
    [InlineData(1.25, 1, 6.3, -0.07, 1.425)]
    [InlineData(1.45, 1, 6.3, -0.07, -1.425)]
    [InlineData(2.325, 0.5, 3.15, -0.035, 0)]
    [InlineData(2.6, 0, 0, 0, 0)]
    public void Pose_matches_literal_javascript_samples(
        double time, double amount, double bob, double roll, double sway)
    {
        var pose = HuntPose.At(time);

        Assert.Equal(amount, pose.Amount, 12);
        Assert.Equal(bob, pose.HeadBob, 12);
        Assert.Equal(roll, pose.HeadRoll, 12);
        Assert.Equal(sway, pose.Sway, 12);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Pose_rejects_nonfinite_time(double time) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => HuntPose.At(time));

    [Fact]
    public void Head_and_map_match_literal_javascript_geometry()
    {
        var pose = HuntPose.At(1.45);

        AssertPoint(new PointD(43, 54.3), pose.Head(43, 48));
        AssertPoint(new PointD(12, 79), pose.Map(23, 79));
        AssertPoint(new PointD(66, 82), pose.Map(66, 82));
        AssertPoint(new PointD(35.770311473369745, 75.08482272701204), pose.Map(35, 72));
    }

    [Fact]
    public void First_nearby_advance_starts_at_age_zero_and_long_hold_loops_settled_span()
    {
        var session = new HuntingSession();
        session.Advance(4, near: false);
        Assert.False(session.IsActive);

        session.Advance(0.5, near: true);
        Assert.True(session.IsActive);
        Assert.Equal(0, session.AgeSeconds, 12);

        session.Advance(120, near: true);
        Assert.Equal(1.6, session.AgeSeconds, 12);
    }

    [Fact]
    public void Idle_time_cannot_overflow_the_session_clock()
    {
        var session = new HuntingSession();
        session.Advance(double.MaxValue, near: false);
        session.Advance(double.MaxValue, near: false);

        session.Advance(0, near: true);

        Assert.True(session.IsActive);
        Assert.Equal(0, session.AgeSeconds, 12);
    }

    [Fact]
    public void Departure_synchronizes_release_to_next_matching_sway_peak()
    {
        var session = new HuntingSession();
        session.Advance(0, near: true);
        session.Advance(1.8, near: true);
        session.Advance(0, near: false);
        Assert.Equal(1.4, session.AgeSeconds, 12);

        session.Advance(0.25, near: false);
        Assert.Equal(1.65, session.AgeSeconds, 12);
        session.Advance(0.95, near: false);
        Assert.Equal(2.6, session.AgeSeconds, 12);
        Assert.False(session.IsActive);
    }

    [Fact]
    public void Reentry_before_release_peak_cancels_release_and_block_resets_immediately()
    {
        var session = new HuntingSession();
        session.Advance(0, near: true);
        session.Advance(1.8, near: true);
        session.Advance(0, near: false);
        session.Advance(0.1, near: true);
        Assert.Equal(1.5, session.AgeSeconds, 12);

        session.Advance(0.1, near: true, blocked: true);
        Assert.False(session.IsActive);
        Assert.Equal(2.6, session.AgeSeconds, 12);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Stateful_motion_rejects_invalid_delta(double delta)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HuntingSession().Advance(delta, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HuntGaze().Advance(delta, new PointD(0, 0), false, true));
    }

    [Fact]
    public void Gaze_matches_literal_eye_and_slower_head_lag()
    {
        var gaze = new HuntGaze();

        var pose = gaze.Advance(0.1, new PointD(110, 85), flip: false, active: true);

        Assert.Equal(0.6259424326016323, pose.EyeX, 12);
        Assert.Equal(0.47866186022477764, pose.EyeY, 12);
        Assert.Equal(-0.015384842082285532, pose.Roll, 12);
    }

    [Fact]
    public void Gaze_mirrors_horizontal_target_and_invalid_target_returns_toward_neutral()
    {
        var right = new HuntGaze().Advance(0.1, new PointD(110, 0), false, true);
        var left = new HuntGaze().Advance(0.1, new PointD(110, 0), true, true);
        Assert.Equal(right.EyeX, -left.EyeX, 12);
        Assert.Equal(right.Roll, -left.Roll, 12);

        var gaze = new HuntGaze();
        gaze.Advance(0.1, new PointD(110, 85), false, true);
        var neutralizing = gaze.Advance(0.1, new PointD(double.NaN, 0), false, true);
        Assert.Equal(0.16499663385898637, neutralizing.EyeX, 12);
        Assert.Equal(0.12617389648040138, neutralizing.EyeY, 12);
    }

    [Fact]
    public void Gaze_is_frame_rate_independent_and_reset_is_neutral()
    {
        var single = new HuntGaze().Advance(1, new PointD(54, -24), false, true);
        var steppedGaze = new HuntGaze();
        HuntGazePose stepped = default;
        for (var i = 0; i < 100; i++)
            stepped = steppedGaze.Advance(0.01, new PointD(54, -24), false, true);

        Assert.Equal(single.EyeX, stepped.EyeX, 12);
        Assert.Equal(single.EyeY, stepped.EyeY, 12);
        Assert.Equal(single.Roll, stepped.Roll, 12);

        steppedGaze.Reset();
        Assert.Equal(default, steppedGaze.Advance(0, null, false, false));
    }

    private static void AssertPoint(PointD expected, PointD actual)
    {
        Assert.Equal(expected.X, actual.X, 12);
        Assert.Equal(expected.Y, actual.Y, 12);
    }
}
