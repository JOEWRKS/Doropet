using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.Core.Tests.Behavior;

public class PlatformAirborneBehaviorTests
{
    [Fact]
    public void Airborne_suspension_freezes_autonomous_state_facing_and_phase()
    {
        var tuning=BehaviorTuning.Default with{IdleMin=TimeSpan.FromMilliseconds(100),IdleMax=TimeSpan.FromMilliseconds(100),IdleToWalkProbability=1};
        var brain=new PetBrain(tuning,new FixedRandom(),new(100,100));var before=brain.Current;
        for(var i=0;i<30;i++)brain.Update(Input());
        Assert.Equal(before,brain.Current);
        brain.Update(Input() with{Delta=TimeSpan.FromMilliseconds(100),SuspendAutonomousMotion=false});
        Assert.Equal(PetState.Walk,brain.Current.State);
    }

    [Fact]
    public void Airborne_pointer_reaction_cannot_flip_the_face()
    {
        var brain=TestSupport.PetTestInput.CreateReactionBrain();
        brain.Update(TestSupport.PetTestInput.At(.1,pointer:new PointD(460,150)));
        var before=brain.Current;
        brain.Update(TestSupport.PetTestInput.At(.1,pointer:new PointD(260,150)) with{SuspendAutonomousMotion=true});
        Assert.Equal(before,brain.Current);
    }

    private static PetInput Input()=>new(TimeSpan.FromMilliseconds(16),new(0,0,1000,1000),new(144,144),PointerSample.Unavailable,false,false,null,new(4,4),SuspendAutonomousMotion:true);
    private sealed class FixedRandom:IRandomSource{public double NextUnit()=>.5;}
}
