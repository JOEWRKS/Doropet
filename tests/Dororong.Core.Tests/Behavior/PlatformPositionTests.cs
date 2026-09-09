using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.Core.Tests.Behavior;

public class PlatformPositionTests
{
    [Fact]
    public void Suspension_freezes_existing_startled_state_phase_and_position()
    {
        var brain=TestSupport.PetTestInput.CreateReactionBrain();
        brain.Update(TestSupport.PetTestInput.At(.1,pointer:new PointD(460,150)));
        brain.Update(TestSupport.PetTestInput.At(.1,pointer:new PointD(260,150)));
        var before=brain.Current;
        Assert.Equal(PetState.Startled,before.State);
        var input=TestSupport.PetTestInput.At(.1,pointer:new PointD(200,150)) with {SuspendAutonomousMotion=true};
        var actual=brain.Update(input);
        Assert.Equal(before,actual);
    }
    private static PetBrain Brain(BehaviorTuning? tuning = null) => new(tuning ?? BehaviorTuning.Default, new SeededRandomSource(12), new(30,40));
    private static PetInput Input(bool suspend = false) => new(TimeSpan.FromMilliseconds(100),new(0,0,1000,800),new(144,144),PointerSample.Unavailable,false,false,null,new(4,4),SuspendAutonomousMotion:suspend);

    [Fact]
    public void Reconciliation_persists_through_next_idle_tick_and_preserves_phase()
    {
        var brain=Brain(); var before=brain.Update(Input());
        var moved=brain.ApplyPlatformPosition(new(120,160));
        Assert.Equal(before with {Position=new(120,160)},moved);
        Assert.Equal(new PointD(120,160),brain.Update(Input()).Position);
    }

    [Theory]
    [InlineData(double.NaN,20)] [InlineData(20,double.PositiveInfinity)]
    public void Nonfinite_reconciliation_rejects_without_mutation(double x,double y)
    {
        var brain=Brain(); var before=brain.Current;
        Assert.Throws<ArgumentOutOfRangeException>(()=>brain.ApplyPlatformPosition(new(x,y)));
        Assert.Equal(before,brain.Current);
    }

    [Fact]
    public void Suspended_walk_neither_moves_nor_clamps_physics_position()
    {
        var brain=Brain(BehaviorTuning.Default with {IdleMin=TimeSpan.FromMilliseconds(100),IdleMax=TimeSpan.FromMilliseconds(100),IdleToWalkProbability=1});
        Assert.Equal(PetState.Walk,brain.Update(Input()).State);
        brain.ApplyPlatformPosition(new(120,900));
        Assert.Equal(new PointD(120,900),brain.Update(Input(true)).Position);
    }

    [Fact]
    public void Suspension_keeps_press_drag_release_and_grab_offset_operational()
    {
        var brain=Brain();
        var press=Input(true) with {BodyPressPosition=new(40,50),PrimaryButtonDown=true,Pointer=new(true,new(40,50))};
        var pending=brain.Update(press);
        Assert.True(pending.IsDirectInteractionPending);
        var reconciled=brain.ApplyPlatformPosition(new(120,160));
        Assert.Equal(pending.GrabOffset,reconciled.GrabOffset);
        Assert.Equal(new PointD(120,160),reconciled.Position);
        var drag=brain.Update(press with {BodyPressPosition=null,Pointer=new(true,new(200,210))});
        Assert.Equal(PetState.Dragged,drag.State); Assert.Equal(new PointD(190,200),drag.Position);
        Assert.Equal(PetState.Idle,brain.Update(Input(true)).State);
    }
}
