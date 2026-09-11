using System.Windows;
using System.Windows.Controls;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;

namespace Dororong.App.Tests.Controls;

public sealed class DesktopFacingTests
{
    [Theory]
    [InlineData(FacingDirection.Left)]
    [InlineData(FacingDirection.Right)]
    public void Desktop_walking_face_leads_the_rump_in_the_travel_direction(FacingDirection direction) => EdgePerchPresentationTests.Sta(() =>
    {
        var p = new DororongPresenter();
        p.RenderDesktop(new(PetState.Walk, new(100, 100), direction, 0, false, null), DirectInteractionSnapshot.None, TimeSpan.FromMilliseconds(180));
        p.Measure(new Size(144,144)); p.Arrange(new Rect(0,0,144,144)); p.UpdateLayout();
        var image = (Image)p.FindName("DororongImage");
        var face = image.TranslatePoint(new Point(30,55),p);
        var rump = image.TranslatePoint(new Point(65,65),p);
        Assert.Equal(direction == FacingDirection.Right ? 1 : -1, Math.Sign(face.X-rump.X));
    });
}
