using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;

namespace Dororong.App.Tests.Controls;

public class TrackingGaitHandoffTests
{
    [Theory]
    [InlineData(FacingDirection.Left,false)] [InlineData(FacingDirection.Right,false)]
    [InlineData(FacingDirection.Left,true)] [InlineData(FacingDirection.Right,true)]
    public void Resumed_walk_moves_legs_while_head_is_still_recovering(FacingDirection facing,bool preparing) => EdgePerchPresentationTests.Sta(()=>
    {
        var p=new DororongPresenter();var pose=new PetSnapshot(PetState.Walk,new(100,100),facing,0,false,null);
        var dt=TimeSpan.FromMilliseconds(16);
        for(var i=0;i<50;i++)
        {
            p.UpdateHunting(pose,DirectInteractionSnapshot.None,new(true,preparing?new(230,181):new(340,100)),dt,false);
            p.RenderDesktop(pose,DirectInteractionSnapshot.None,dt);
        }
        byte[] Pixels()=>PerchExpressionTests.Pixels((BitmapSource)((Image)p.FindName("DororongImage")).Source);
        var before=PerchExpressionTests.Pixels(new HuntRenderer().Render(0,default));
        Assert.False(p.UpdateHunting(pose,DirectInteractionSnapshot.None,PointerSample.Unavailable,dt,false));
        pose=pose with {Position=pose.Position+new Dororong.Core.Geometry.PointD(facing==FacingDirection.Left?-.4:.4,0)};
        p.RenderDesktop(pose,DirectInteractionSnapshot.None,dt);
        var after=Pixels();long delta=0;
        for(var y=77;y<88;y++)for(var x=15;x<70;x++)for(var c=0;c<4;c++)delta+=Math.Abs(after[(y*96+x)*4+c]-before[(y*96+x)*4+c]);
        Assert.True(delta>500,$"Moving legs stayed in standing pose: delta {delta}");
        var gaze=(HuntGazePose)typeof(DororongPresenter).GetField("_huntRenderedGaze",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(p)!;
        Assert.True(Math.Abs(gaze.Roll)>.01); // Do not fix sliding by snapping the head home.
    });
}
