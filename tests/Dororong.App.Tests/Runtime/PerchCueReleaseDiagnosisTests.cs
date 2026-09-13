using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.App.Tests.Controls;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public partial class PetLoopPlatformTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(0.6666666666666666)]
    public void Bottom_cue_during_fresh_swing_is_a_valid_release(double readbackStep) => CheekProductTests.Sta(() =>
    {
        var failures=new List<string>();var readyCases=0;
        foreach(var facing in new[]{FacingDirection.Left,FacingDirection.Right})
        foreach(var fling in new[]{-60,0,60})
        foreach(var hold in Enumerable.Range(1,20))
        {
            using var h=new Harness(realPresenter:true,perchEnabled:true,readbackStep:readbackStep);
            h.Native.Scene=new(2,TimeSpan.Zero,[new(1,new(0,0,800,600))],
                [new(new(2,2,2),new(0,560,800,40),0,true,false,false,false,true,true,true,true)]);
            h.Start();var p=h.Presenter!;
            p.Render(h.Core with{State=PetState.Idle,Facing=facing,Phase=0},DirectInteractionSnapshot.None);
            PerchExpressionTests.Layout(p);
            PressImage(h,(AlphaHitTestImage)p.FindName("DororongImage"),new(25,38));h.Tick();
            h.Pointer=new(true,h.Pointer.Position+new PointD(100,0));h.Tick();h.Tick();
            h.Pointer=new(true,new(h.Pointer.Position.X+fling,599));
            for(var i=0;i<hold;i++)h.Tick();
            if(!h.Direct.IsPerchReady)continue;
            readyCases++;
            var before=h.Position;var sole=h.Platforms.Contact.SoleY;
            var eligible=h.Platforms.CanBeginPerch(before,h.Direct.PressFacing??h.Core.Facing,true,true);
            h.Down=false;h.Tick();
            if(h.Platforms.PerchPhase!=EdgePerchPhase.Entering)
                failures.Add($"{facing} fling={fling} hold={hold} pos={before} sole={sole:F6} afterRenderEligible={eligible} release={h.Platforms.PerchPhase}");
        }
        Assert.Equal(120,readyCases); // Hiding readiness must not make this regression pass.
        Assert.True(failures.Count==0,$"{failures.Count}/{readyCases} ready releases failed:\n"+string.Join("\n",failures));
    });

    [Fact]
    public void Displayed_taskbar_cue_still_attaches_on_next_release_without_pointer_movement() => CheekProductTests.Sta(() =>
    {
        var failures=new List<string>();
        var readyCases=0;
        foreach(var facing in new[]{FacingDirection.Left,FacingDirection.Right})
        foreach(var step in new[]{1,4,20})
        foreach(var pause in new[]{0,1,4,10})
        {
            using var h=new Harness(realPresenter:true,perchEnabled:true);
            h.Native.Scene=new(2,TimeSpan.Zero,[new(1,new(0,0,800,600))],
                [new(new(2,2,2),new(0,560,800,40),0,true,false,false,false,true,true,true,true)]);
            h.Start();var p=h.Presenter!;
            p.Render(h.Core with{State=PetState.Idle,Facing=facing,Phase=0},DirectInteractionSnapshot.None);
            PerchExpressionTests.Layout(p);
            PressImage(h,(AlphaHitTestImage)p.FindName("DororongImage"),new(25,38));h.Tick();
            h.Pointer=new(true,h.Pointer.Position+new PointD(100,0));h.Tick();h.Tick();
            // Real cursor stays inside a 600px monitor, unlike the older +1000px fixture.
            while(!h.Direct.IsPerchReady && h.Pointer.Position.Y<599)
            {
                h.Pointer=new(true,new(h.Pointer.Position.X,Math.Min(599,h.Pointer.Position.Y+step)));
                h.Tick();
            }
            for(var i=0;i<pause;i++)h.Tick();
            if(!h.Direct.IsPerchReady)continue;
            readyCases++;
            var before=h.Position;
            var sole=h.Platforms.Contact.SoleY;
            var afterRenderEligible=h.Platforms.CanBeginPerch(before,h.Direct.PressFacing??h.Core.Facing,true,true);
            h.Down=false;h.Tick();
            if(h.Platforms.PerchPhase!=EdgePerchPhase.Entering)
                failures.Add($"{facing} step={step} pause={pause} cursor={h.Pointer.Position} pos={before} sole={sole:F6} afterRenderEligible={afterRenderEligible} released={h.Platforms.PerchPhase} newPos={h.Position}");
        }
        Assert.True(readyCases>0,"Sweep never reached a visible cue.");
        Assert.True(failures.Count==0,$"{failures.Count}/{readyCases} visible cues failed release:\n"+string.Join("\n",failures));
    });
}
