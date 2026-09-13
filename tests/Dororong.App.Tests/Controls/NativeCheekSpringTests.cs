using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Controls;

public sealed class NativeCheekSpringTests
{
    [Theory]
    [InlineData(5)] [InlineData(20)]
    public void Backward_kick_starts_just_before_first_cheek_return_and_stays_landed(double pull)
    {
        var c=new DirectInteractionController();
        c.BeginCheekPull(new(new byte[96*96*4],Matrix.Identity,FacingDirection.Right),new(100,100));
        c.Advance(TimeSpan.Zero,new(true,new(100-pull,100)),true,PetState.Idle,PetState.Idle);
        c.Advance(TimeSpan.Zero,PointerSample.Unavailable,false,PetState.Idle,PetState.Idle);
        var waiting=c.Advance(TimeSpan.FromMilliseconds(65),PointerSample.Unavailable,false,PetState.Idle,PetState.Idle);
        Assert.InRange(waiting.CheekPull!.PullDips,pull*.27,pull*.29);
        Assert.Equal(0,waiting.CheekPull.RecoilOffset);
        Assert.Equal(0,waiting.CheekPull.RecoilLift);
        var started=c.Advance(TimeSpan.FromMilliseconds(10),PointerSample.Unavailable,false,PetState.Idle,PetState.Idle);
        Assert.InRange(started.CheekPull!.PullDips,pull*.16,pull*.18);
        Assert.True(started.CheekPull!.RecoilOffset>0);
        Assert.True(started.CheekPull.RecoilLift>0);
        var apex=c.Advance(TimeSpan.FromMilliseconds(130),PointerSample.Unavailable,false,PetState.Idle,PetState.Idle);
        Assert.Equal(pull==5?1:4,apex.CheekPull!.RecoilLift,6);
        var landing=c.Advance(TimeSpan.FromMilliseconds(130),PointerSample.Unavailable,false,PetState.Idle,PetState.Idle);
        Assert.InRange(landing.CheekPull!.RecoilOffset,pull==5?1.99:7.97,pull==5?2:8);
        Assert.InRange(landing.CheekPull.RecoilLift,0,.06);
        var landed=c.Advance(TimeSpan.FromMilliseconds(10),PointerSample.Unavailable,false,PetState.Idle,PetState.Idle);
        Assert.Equal(pull==5?2:8,landed.CheekPull!.RecoilOffset,6);
        Assert.Equal(0,landed.CheekPull.RecoilLift);
        var settling=c.Advance(TimeSpan.FromMilliseconds(85),PointerSample.Unavailable,false,PetState.Idle,PetState.Idle);
        Assert.Equal(landed.CheekPull.RecoilOffset,settling.CheekPull!.RecoilOffset);
        Assert.Equal(0,settling.CheekPull.RecoilLift);
        Assert.Equal(DirectInteractionSnapshot.None,c.Advance(TimeSpan.FromMilliseconds(10),PointerSample.Unavailable,false,PetState.Idle,PetState.Idle));
    }
    [Theory]
    [InlineData(5)] [InlineData(20)]
    public void Actual_controller_crosses_rest_twice_then_settles_without_capture(double pull)
    {
        var c=new DirectInteractionController();var capture=new CheekPullCapture(new byte[96*96*4],Matrix.Identity,FacingDirection.Right);
        c.BeginCheekPull(capture,new(100,100));
        c.Advance(TimeSpan.Zero,new(true,new(100-pull,100)),true,PetState.Idle,PetState.Idle);
        var first=c.Advance(TimeSpan.Zero,PointerSample.Unavailable,false,PetState.Idle,PetState.Idle);
        Assert.Equal(pull,first.CheekPull!.PullDips);Assert.False(first.RequiresCapture);
        var recoil=c.Advance(TimeSpan.FromMilliseconds(150),PointerSample.Unavailable,false,PetState.Idle,PetState.Idle);
        Assert.InRange(recoil.CheekPull!.PullDips,-pull*.3,-pull*.2);
        var second=c.Advance(TimeSpan.FromMilliseconds(195),PointerSample.Unavailable,false,PetState.Idle,PetState.Idle);
        Assert.InRange(second.CheekPull!.PullDips,pull*.04,pull*.09);
        Assert.False(c.IsWholeCarry);
        Assert.Equal(DirectInteractionSnapshot.None,c.Advance(TimeSpan.FromMilliseconds(95),PointerSample.Unavailable,false,PetState.Idle,PetState.Idle));
    }

    [Theory]
    [InlineData(0,false,5)] [InlineData(0,true,20)]
    [InlineData(60,false,5)] [InlineData(60,true,20)]
    [InlineData(99,false,20)]
    public void Actual_captured_pose_bounces_back_including_hunt_sway(int frame,bool mirror,double pull) => CheekProductTests.Sta(() =>
    {
        var p=CheekLiveConnectionTests.Idle();var gaze=new HuntGazePose(0,.4,-Math.PI/9);
        var bitmap=new HuntRenderer().Render(frame,gaze);var transform=HuntRenderer.HeadTransform(frame,gaze);
        void Set(string name,object value)=>typeof(DororongPresenter).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(p,value);
        ((Image)p.FindName("DororongImage")).Source=bitmap;
        Set("_huntingImage",bitmap);Set("_huntFrame",frame);Set("_huntRenderedGaze",gaze);Set("_huntHeadTransform",transform);
        CheekProductTests.Layout(p);var at=transform.Transform(new Point(18,59));
        Assert.True(p.TryCreateCheekPullCapture(new(at.X,at.Y),out var capture));
        if(mirror)capture=capture!.TurnToward(-capture.OutwardUnit.X*20);
        var c=new DirectInteractionController();c.BeginCheekPull(capture!,new(100,100));
        var held=c.Advance(TimeSpan.Zero,new(true,new PointD(100+capture!.OutwardUnit.X*pull,100+capture.OutwardUnit.Y*pull)),true,PetState.Idle,PetState.Idle);
        var core=new PetSnapshot(PetState.Idle,new(100,100),FacingDirection.Right,0,false,null);
        p.Render(core,held,TimeSpan.FromSeconds(10));var overlay=CheekLiveConnectionTests.Overlay(p);var original=overlay.RenderTransform.Value;
        var first=c.Advance(TimeSpan.Zero,PointerSample.Unavailable,false,PetState.Idle,PetState.Idle);
        p.Render(core,first,TimeSpan.Zero);Assert.Equal(original,overlay.RenderTransform.Value);
        var release=c.Advance(TimeSpan.FromMilliseconds(205),PointerSample.Unavailable,false,PetState.Idle,PetState.Idle);
        p.Render(core,release,TimeSpan.FromMilliseconds(205));
        var dx=overlay.RenderTransform.Value.OffsetX-original.OffsetX;
        Assert.InRange(dx*-Math.Sign(capture.OutwardUnit.X),pull==5?.99:3.99,pull==5?1.01:4.01);
        Assert.Equal(original.OffsetY-(pull==5?1:4),overlay.RenderTransform.Value.OffsetY,6);
        Assert.False(capture.Render(0).SequenceEqual(capture.Render(-5)),"Hunt capture must not discard the negative recoil pose.");
        Assert.False(release.IsPerchReady);
        CheekProductTests.Layout(p);
        p.ApplyPlatformPose(new(PlatformPhase.Landing,new(100,100),new(0,0,1),.08,.03),112);
        var bouncedGeometry=p.MeasurePlatformGeometry();
        p.Render(core,release with { CheekPull=release.CheekPull! with { RecoilOffset=0,RecoilLift=0 } },TimeSpan.Zero);
        CheekProductTests.Layout(p);
        p.ApplyPlatformPose(new(PlatformPhase.Landing,new(100,100),new(0,0,1),.08,.03),112);
        var fixedGeometry=p.MeasurePlatformGeometry()!.Value;
        Assert.Equal(fixedGeometry.Contact.Left,bouncedGeometry!.Value.Contact.Left,6);
        Assert.Equal(fixedGeometry.Contact.Right,bouncedGeometry.Value.Contact.Right,6);
        Assert.Equal(fixedGeometry.Contact.SoleY,bouncedGeometry.Value.Contact.SoleY,6);
        Assert.Equal(fixedGeometry.Bounds.X,bouncedGeometry.Value.Bounds.X,6);
        Assert.Equal(fixedGeometry.Bounds.Y,bouncedGeometry.Value.Bounds.Y,6);
    });
}
