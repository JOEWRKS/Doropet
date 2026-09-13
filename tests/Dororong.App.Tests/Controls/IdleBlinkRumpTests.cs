using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;

namespace Dororong.App.Tests.Controls;

public class IdleBlinkRumpTests
{
    [Fact]
    public void Embedded_standing_blink_changes_eyes_only() => EdgePerchPresentationTests.Sta(()=>
    {
        var open=PerchExpressionTests.Pixels(LocomotionFrames.Walk(0,0));
        var closed=PerchExpressionTests.Pixels(LocomotionFrames.Walk(0,0,true));
        var changed=0;
        for(var y=0;y<96;y++)for(var x=0;x<96;x++)
        {
            var i=(y*96+x)*4;
            if(open.AsSpan(i,4).SequenceEqual(closed.AsSpan(i,4)))continue;
            Assert.True(x>=14&&x<50&&y>=40&&y<64,$"Blink changed body at {x},{y}");changed++;
        }
        Assert.True(changed>100,"Closed eyes must actually render");
    });

    [Theory]
    [InlineData(FacingDirection.Left)] [InlineData(FacingDirection.Right)]
    public void Post_walk_idle_blink_never_adds_a_rump_spur(FacingDirection facing) => EdgePerchPresentationTests.Sta(()=>
    {
        var p=new DororongPresenter();var pose=new PetSnapshot(PetState.Idle,new(100,100),facing,0,false,null);
        var dt=TimeSpan.FromMilliseconds(16);
        for(var i=0;i<20;i++)p.RenderDesktop(pose with{State=PetState.Walk},DirectInteractionSnapshot.None,dt);
        for(var i=0;i<20;i++)p.RenderDesktop(pose,DirectInteractionSnapshot.None,dt);
        var reference=PerchExpressionTests.Pixels((BitmapSource)((Image)p.FindName("DororongImage")).Source);
        var sawClosed=false;
        for(var t=16;t<=5008;t+=16)
        {
            p.RenderDesktop(pose with{Phase=(t%4000)/4000d},DirectInteractionSnapshot.None,dt);
            var source=(BitmapSource)((Image)p.FindName("DororongImage")).Source;
            sawClosed|=ReferenceEquals(source,LocomotionFrames.Walk(0,0,true));
            var pixels=PerchExpressionTests.Pixels(source);
            for(var y=42;y<76;y++)for(var x=64;x<82;x++)
            {
                var i=(y*96+x)*4;
                Assert.True(reference.AsSpan(i,4).SequenceEqual(pixels.AsSpan(i,4)),$"Idle rump changed at {x},{y}, t{t}");
            }
        }
        Assert.True(sawClosed);
    });
}
