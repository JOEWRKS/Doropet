using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;

namespace Dororong.App.Tests.Controls;

public class SharedSkinTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Pounce_endpoints_keep_the_ordinary_scale_and_pivot(bool landing) => EdgePerchPresentationTests.Sta(()=>
    {
        var p=new DororongPresenter();var pose=new PetSnapshot(PetState.Idle,new(100,100),FacingDirection.Left,.4,false,null);
        p.RenderDesktop(pose,DirectInteractionSnapshot.None,TimeSpan.Zero);
        var image=(System.Windows.Controls.Image)p.FindName("DororongImage");
        System.Windows.Point VisiblePoint()
        {
            var pivot=new System.Windows.Point(image.Width*image.RenderTransformOrigin.X,image.Height*image.RenderTransformOrigin.Y);
            var point=image.RenderTransform.Transform(new System.Windows.Point(20-pivot.X,30-pivot.Y));
            return new(point.X+pivot.X,point.Y+pivot.Y);
        }
        var ordinary=VisiblePoint();
        void Step(double seconds)
        {
            var dt=TimeSpan.FromSeconds(seconds);
            p.UpdateHuntingWithPounce(pose,DirectInteractionSnapshot.None,new(true,new(230,181)),dt,false);
            p.RenderDesktop(pose,DirectInteractionSnapshot.None,dt);
        }
        for(var i=0;i<15;i++)Step(.1);
        Assert.Equal(PouncePhase.Flight,p.Pounce.Phase);
        if(landing)
        {
            for(var i=0;i<6;i++)Step(.1);
            Assert.Equal(PouncePhase.Landing,p.Pounce.Phase);
        }
        Assert.InRange(Math.Abs(VisiblePoint().X-ordinary.X),0,.000001);
        Assert.InRange(Math.Abs(VisiblePoint().Y-ordinary.Y),0,.000001);
        if(landing)
        {
            Step(.02);Assert.Equal(PouncePhase.Track,p.Pounce.Phase);
            Assert.InRange(Math.Abs(VisiblePoint().X-ordinary.X),0,.000001);
            Assert.InRange(Math.Abs(VisiblePoint().Y-ordinary.Y),0,.000001);
        }
    });

    [Theory]
    [InlineData(0)] [InlineData(156)]
    public void Upright_tracking_rear_matches_the_ordinary_silhouette(int frame) => EdgePerchPresentationTests.Sta(()=>
    {
        _=new DororongPresenter();
        var ordinary=new BitmapImage(new Uri("pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png"));
        var expected=PerchExpressionTests.Pixels(ordinary);
        var actual=PerchExpressionTests.Pixels(new HuntRenderer().Render(frame,default));
        int Edge(byte[] p,int y)=>Enumerable.Range(65,20).Where(x=>p[(y*96+x)*4+3]>=128).DefaultIfEmpty(-1).Max();
        // Allow one native pixel for resampling, not a new protruding rump.
        for(var y=48;y<=74;y++)Assert.InRange(Edge(actual,y)-Edge(expected,y),-1,1);
        Assert.Equal(75,Enumerable.Range(48,27).Max(y=>Edge(actual,y)));
    });

    [Theory]
    [InlineData(.2)] [InlineData(.4)] [InlineData(.8)]
    public void Upright_tracking_preserves_ordinary_breathing_scale(double phase) => EdgePerchPresentationTests.Sta(()=>
    {
        var p=new DororongPresenter();var pose=new PetSnapshot(PetState.Idle,new(100,100),FacingDirection.Left,phase,false,null);
        var dt=TimeSpan.FromMilliseconds(16);
        p.RenderDesktop(pose,DirectInteractionSnapshot.None,dt);
        var scale=(ScaleTransform)p.FindName("ImageBreathingScaleTransform");var x=scale.ScaleX;var y=scale.ScaleY;
        for(var i=0;i<20;i++)
        {
            p.UpdateHunting(pose,DirectInteractionSnapshot.None,new(true,new(340,100)),dt,false);
            p.RenderDesktop(pose,DirectInteractionSnapshot.None,dt);
            Assert.Equal(x,scale.ScaleX);Assert.Equal(y,scale.ScaleY);
        }
    });
}
