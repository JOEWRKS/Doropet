using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Controls;

public sealed class HuntingCheekReadinessTests
{
    [Theory]
    [InlineData(0,-20,false)]
    [InlineData(60,0,false)]
    [InlineData(60,-20,true)]
    [InlineData(0,20,true)]
    [InlineData(0,-20,false,0,true)]
    [InlineData(60,-20,true,5,true)]
    [InlineData(60,20,false,5,false)]
    [InlineData(0,-20,false,5,false,true)]
    public void Captured_hunting_cheek_keeps_head_intact_while_forelegs_flutter(int frame,double degrees,bool turn,double pull=20,bool closed=false,bool walking=false) => CheekProductTests.Sta(() =>
    {
        var presenter=new DororongPresenter();
        var standing=new PetSnapshot(PetState.Idle,new(100,100),FacingDirection.Left,0,false,null);
        presenter.Render(standing,DirectInteractionSnapshot.None);
        var gaze=new HuntGazePose(0,.4,degrees*Math.PI/180);
        var gait=walking?LocomotionFrames.Walk(1,2.5,closed):null;
        var bitmap=new HuntRenderer().Render(frame,gaze,closed,walkingFrame:gait);
        var transform=HuntRenderer.HeadTransform(frame,gaze);
        // Seed a deterministic already-rendered tracking pose, then exercise the
        // real hit capture -> cheek overlay -> readiness presenter boundary.
        void Set(string name,object value)=>typeof(DororongPresenter).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(presenter,value);
        ((Image)presenter.FindName("DororongImage")).Source=bitmap;
        Set("_huntingImage",bitmap);Set("_huntFrame",frame);Set("_huntRenderedGaze",gaze);Set("_huntHeadTransform",transform);Set("_huntClosed",closed);
        Set("_huntRenderedBlend",1d); // This fixture is fully tracking, not the new source handoff.
        if(gait is not null)Set("_huntWalkingFrame",gait);
        CheekProductTests.Layout(presenter);
        var at=transform.Transform(new Point(18,59));
        Assert.True(presenter.TryCreateCheekPullCapture(new(at.X,at.Y),out var captured));
        if(turn)captured=captured!.TurnToward(-captured.OutwardUnit.X*20);
        var direct=new DirectInteractionSnapshot(DirectInteractionTarget.RightCheek,DirectInteractionPhase.CheekPull,
            new(100,100),new(110,160),1,0,true){CheekPull=new(captured!,pull)};
        presenter.Render(standing,direct,TimeSpan.FromSeconds(10));
        var overlay=CheekLiveConnectionTests.Overlay(presenter);
        var before=PremultipliedFrame.From((BitmapSource)overlay.Source).Pixels;
        // Use the actually pulled head, not the unpulled mask (its cheek moves).
        var headSource=new HuntEyes().Paint(gaze.EyeX,gaze.EyeY,closed,pull,pull,pull);
        var drawing=new DrawingVisual();RenderOptions.SetBitmapScalingMode(drawing,BitmapScalingMode.Linear);
        var doubled=transform;doubled.Scale(2,2);doubled.Translate(32,32);
        using(var dc=drawing.RenderOpen())
        {dc.PushTransform(new MatrixTransform(doubled));dc.DrawImage(headSource,new Rect(0,0,96,96));dc.Pop();}
        var largeMask=new RenderTargetBitmap(256,256,96,96,PixelFormats.Pbgra32);largeMask.Render(drawing);
        using(var dc=drawing.RenderOpen())dc.DrawImage(largeMask,new Rect(-16,-16,128,128));
        var mask=new RenderTargetBitmap(96,96,96,96,PixelFormats.Pbgra32);mask.Render(drawing);
        var head=new byte[96*96*4];mask.CopyPixels(head,384,0);
        for(var phase=0;phase<16;phase++)
        {
            presenter.Render(standing,direct with{IsPerchReady=true},TimeSpan.FromMilliseconds(250d/16));
            var after=PremultipliedFrame.From((BitmapSource)overlay.Source).Pixels;
            if(walking)
                for(var y=77;y<88;y++)for(var x=57;x<80;x++)
                    for(var c=0;c<4;c++)
                        // Normal cheek overlay round-trips through straight
                        // BGRA; readiness stays premultiplied (one byte rounding).
                        Assert.InRange(Math.Abs(before[(y*96+x)*4+c]-after[(y*96+x)*4+c]),0,1);
            var changedHead=0;var animatedBody=0;var changedPoints=new List<string>();
            for(var i=0;i<after.Length;i+=4)
            {
                var changed=Enumerable.Range(0,4).Any(c=>Math.Abs(after[i+c]-before[i+c])>16);
                if(changed&&head[i+3]==255&&before[i+3]==255){changedHead++;changedPoints.Add($"({i/4%96},{i/4/96})");}
                if(changed&&head[i+3]<32)animatedBody++;
            }
            Assert.True(changedHead==0,$"frame{frame},roll{degrees},turn{turn},phase{phase}: {changedHead} opaque head pixels cut into flutter: {string.Join(',',changedPoints)}");
            Assert.True(animatedBody>2,"Readiness must still animate visible paws, not just suppress the cue.");
        }
        presenter.Render(standing,direct,TimeSpan.Zero);
        Assert.Equal(before,PremultipliedFrame.From((BitmapSource)overlay.Source).Pixels);
    });
}
