using System.Windows;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Controls;

public class StrongLandingPresentationTests
{
    [Theory]
    [InlineData(.30,.70,1.15)] [InlineData(-.10,1.10,.95)]
    public void Approved_extreme_squash_and_extension_reach_the_visible_sprite_without_sole_drift(double squash,double height,double width) => CheekProductTests.Sta(() =>
    {
        var presenter=new DororongPresenter();
        presenter.Render(new(PetState.Idle,new(100,100),FacingDirection.Right,0,false,null),DirectInteractionSnapshot.None);
        presenter.Measure(new(144,144));presenter.Arrange(new Rect(0,0,144,144));presenter.UpdateLayout();
        var before=presenter.MeasurePlatformGeometry()!.Value;
        presenter.ApplyPlatformPose(new(PlatformPhase.Landing,new(100,100),new(0,0,1),squash,0),before.Contact.SoleY);
        var after=presenter.MeasurePlatformGeometry()!.Value;
        Assert.Equal(before.Bounds.Height*height,after.Bounds.Height,6);
        Assert.Equal(before.Bounds.Width*width,after.Bounds.Width,6);
        Assert.Equal(before.Contact.SoleY,after.Contact.SoleY,6);
    });
}
