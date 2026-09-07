using Dororong.App.Interaction;
using Dororong.Core.Behavior;

namespace Dororong.App.Tests.Runtime;

public sealed partial class PetLoopDirectInteractionTests
{
    [Theory]
    [InlineData(24,52,"cheek")]
    [InlineData(28,59,"cheek")]
    [InlineData(23,67,"body")]
    [InlineData(35,67,"body")]
    [InlineData(39,64,"none")]
    [InlineData(30,75,"none")]
    [InlineData(37,28,"head")]
    public void Presenter_owned_press_enters_only_its_runtime_interaction(int x,int y,string owner) => Controls.CheekProductTests.Sta(() =>
    {
        foreach(var mirror in new[]{false,true})
        {
            var presenter=Controls.InteractionHitRoutingTests.Setup(PetState.Walk,mirror,.1);
            using var h=new LoopHarness(CreateIdleBrain);h.Start();h.PressFromPresenter(presenter,new(x,y));h.Tick();
            h.MovePointerBy(new(mirror?24:-24,0));
            for(var tick=0;tick<12;tick++)h.Tick();
            var direct=h.Renders[^1].Direct;
            if(owner=="cheek"){Assert.NotNull(direct.CheekPull);Assert.Null(direct.BodyPull);}
            if(owner=="body"){Assert.NotNull(direct.BodyPull);Assert.Null(direct.CheekPull);}
            if(owner=="head")Assert.Equal(DirectInteractionPhase.BodyDragEntry,direct.Phase);
            if(owner=="none"){Assert.Equal(DirectInteractionSnapshot.None,direct);Assert.Equal(0,h.CaptureCount);}
            if(owner!="head")
            {
                Assert.DoesNotContain(h.Renders,r=>r.Core.IsDirectInteractionPending||r.Core.State==PetState.Dragged);
                Assert.DoesNotContain(h.Renders,r=>r.Direct.Phase is DirectInteractionPhase.BodyPending or DirectInteractionPhase.BodyDragEntry or DirectInteractionPhase.BodyDragHold);
            }
        }
    });
}
