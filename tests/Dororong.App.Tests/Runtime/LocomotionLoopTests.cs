using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.App.Runtime;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Runtime;

public sealed class LocomotionLoopTests
{
    [Fact]
    public void Autonomous_walk_no_longer_waits_for_an_automatic_sit() => Controls.EdgePerchPresentationTests.Sta(() =>
    {
        using var h=new Harness();h.Loop.Start();
        for(var i=0;i<20;i++)h.Tick();
        Assert.Equal(PetState.Walk,h.Snapshot.State);
        Assert.Equal(new PointD(100,100),h.Position);
        Assert.False(h.Presenter.HoldLocomotionWalk(h.Snapshot));
        h.Tick();Assert.NotEqual(100,h.Position.X);
    });

    [Fact]
    public void Manual_press_still_runs_during_walking() => Controls.EdgePerchPresentationTests.Sta(() =>
    {
        using var h=new Harness();h.Loop.Start();for(var i=0;i<20;i++)h.Tick();
        h.Down=true;h.Pointer=new(true,new(145,145));
        h.Loop.NotifyDirectInteractionPressed(new(DirectInteractionTarget.Body,new(45,45),new(21,21),1));
        h.Tick();
        Assert.True(h.Snapshot.IsDirectInteractionPending);
        Assert.False(h.Presenter.HoldLocomotionWalk(h.Snapshot));
        var image=(System.Windows.Controls.Image)h.Presenter.FindName("DororongImage");
        Assert.False(LocomotionFrames.Contains(image.Source));
    });

    private sealed class Harness : IDisposable
    {
        internal readonly DororongPresenter Presenter=new();
        internal readonly PetLoop Loop;
        internal PointD Position=new(100,100);
        internal PetSnapshot Snapshot;
        internal bool Down;
        internal PointerSample Pointer=PointerSample.Unavailable;
        private TimeSpan _elapsed;
        private EventHandler? _tick;
        internal Harness()
        {
            void Render(PetSnapshot snapshot,DirectInteractionSnapshot direct,TimeSpan delta)
            { Snapshot=snapshot;Presenter.Render(snapshot,direct,delta); }
            var host=new PetLoopHost(()=>new(0,0,800,600),()=>new(144,144),()=>new(4,4),()=>Pointer,()=>Down,
                ()=>Position,p=>Position=p,(s,d)=>Render(s,d,TimeSpan.Zero),()=>true,()=>{},Render)
            {HoldLocomotionWalk=Presenter.HoldLocomotionWalk,SetLocomotionBlocked=Presenter.SetLocomotionBlocked};
            Loop=new(new(()=>_elapsed,()=>{},()=>{}),new(h=>_tick+=h,h=>_tick-=h,()=>{},()=>{}),host,
                _=>new(BehaviorTuning.Default with{IdleMin=TimeSpan.FromSeconds(2),IdleMax=TimeSpan.FromSeconds(2),IdleToWalkProbability=1},new SeededRandomSource(1),new(100,100)));
            Loop.Faulted+=(_,error)=>throw new Exception("Loop fault",error);
        }
        internal void Tick(){_elapsed+=TimeSpan.FromMilliseconds(100);_tick?.Invoke(this,EventArgs.Empty);}
        public void Dispose()=>Loop.Dispose();
    }
}
