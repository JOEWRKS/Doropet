using System.Windows;
using System.Windows.Media;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;

namespace Dororong.App.Tests.Controls;

public class SurfaceReleaseFacingTests
{
    [Fact]
    public void Left_head_recovery_keeps_facing_without_a_legacy_landing() => CheekProductTests.Sta(() =>
    {
        var p=new DororongPresenter();
        var core=new PetSnapshot(PetState.Walk,new(100,100),FacingDirection.Left,.2,false,null);
        p.Render(core,DirectInteractionSnapshot.None);
        p.Measure(new(144,144));p.Arrange(new Rect(0,0,144,144));p.UpdateLayout();
        var hold=new DirectInteractionSnapshot(DirectInteractionTarget.Body,DirectInteractionPhase.BodyDragHold,
            new(140,130),new(320,100),1,0,true);
        p.Render(core with {State=PetState.Dragged},hold);
        p.Render(core with {State=PetState.Idle},hold with {Phase=DirectInteractionPhase.BodyDragSettle,RequiresCapture=false,ReleaseProgress=.9});
        Assert.True(((ScaleTransform)p.FindName("BodyScaleTransform")).ScaleX<0);
        p.Render(core with {State=PetState.Idle},DirectInteractionSnapshot.None);
        Assert.True(((ScaleTransform)p.FindName("BodyScaleTransform")).ScaleX<0,
            "Completing shape recovery must not turn a left-facing airborne pet right.");
    });
}
