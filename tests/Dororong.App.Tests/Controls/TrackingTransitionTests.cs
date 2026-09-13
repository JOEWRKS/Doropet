using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Controls;

public sealed class TrackingTransitionTests
{
    private static readonly PetSnapshot Standing=new(PetState.Idle,new(100,100),FacingDirection.Left,0,false,null);
    private static readonly TimeSpan Tick=TimeSpan.FromMilliseconds(16);
    // Outer tracking zone, away from crouch/dwell; positive head rotation.
    private static readonly PointerSample Watch=new(true,new(340,100));

    [Fact]
    public void Neutral_image_handoff_does_not_jump_at_the_end_of_recovery() => EdgePerchPresentationTests.Sta(()=>
    {
        var p=new DororongPresenter();Step(p,PointerSample.Unavailable);var neutral=Pixels(p);
        for(var i=0;i<50;i++)Step(p,Watch);
        var tracked=Pixels(p);var total=Difference(neutral,tracked);var previous=tracked;var sawHandoff=false;
        for(var i=0;i<80;i++)
        {
            Step(p,PointerSample.Unavailable);var current=Pixels(p);
            if(UprightRumpSource.Contains(((Image)p.FindName("DororongImage")).Source))
            {
                Assert.InRange(Difference(previous,current),0,total*.15);
                sawHandoff=true;break;
            }
            previous=current;
        }
        Assert.True(sawHandoff);
    });

    [Fact]
    public void First_tracking_frame_blends_from_the_ordinary_source() => EdgePerchPresentationTests.Sta(()=>
    {
        var p=new DororongPresenter();Step(p,PointerSample.Unavailable);var neutral=Pixels(p);
        Step(p,Watch);var first=Pixels(p);
        for(var i=0;i<50;i++)Step(p,Watch);
        Assert.InRange(Difference(neutral,first),0,Difference(neutral,Pixels(p))*.25);
    });

    [Fact]
    public void Departure_keeps_rendering_a_recovering_head_without_holding_preparation() => EdgePerchPresentationTests.Sta(()=>
    {
        var p=new DororongPresenter();for(var i=0;i<50;i++)Step(p,Watch);
        var before=Gaze(p);Assert.True(before.Roll>.1);
        Assert.False(Step(p,PointerSample.Unavailable)); // Behavior cancels now.
        Assert.False(UprightRumpSource.Contains(((Image)p.FindName("DororongImage")).Source));
        var after=Gaze(p);
        Assert.InRange(after.Roll,before.Roll*.5,before.Roll*.99);
        Assert.InRange(after.EyeX,0,before.EyeX*.999);
        for(var i=0;i<50;i++)Step(p,PointerSample.Unavailable);
        Assert.Same(UprightRumpSource.Standing(false),((Image)p.FindName("DororongImage")).Source);
    });

    [Fact]
    public void Reentry_after_departure_starts_from_neutral_not_a_stale_tracking_pose() => EdgePerchPresentationTests.Sta(()=>
    {
        var p=new DororongPresenter();for(var i=0;i<50;i++)Step(p,Watch);
        var full=Gaze(p);
        for(var i=0;i<60;i++)Step(p,PointerSample.Unavailable);
        Step(p,Watch);var entered=Gaze(p);
        Assert.InRange(entered.Roll,.0001,full.Roll*.15);
        Assert.InRange(entered.EyeX,.0001,full.EyeX*.3);
    });

    [Fact]
    public void Reentry_during_recovery_continues_from_the_displayed_head() => EdgePerchPresentationTests.Sta(()=>
    {
        var p=new DororongPresenter();for(var i=0;i<50;i++)Step(p,Watch);
        var full=Gaze(p);for(var i=0;i<8;i++)Step(p,PointerSample.Unavailable);
        var recovering=Gaze(p);Assert.InRange(recovering.Roll,.001,full.Roll*.7);
        Step(p,Watch);var resumed=Gaze(p);
        Assert.InRange(resumed.Roll,recovering.Roll, recovering.Roll+(full.Roll-recovering.Roll)*.15);
    });

    [Fact]
    public void A_press_interrupts_visual_recovery_immediately() => EdgePerchPresentationTests.Sta(()=>
    {
        var p=new DororongPresenter();for(var i=0;i<20;i++)Step(p,Watch);
        Step(p,PointerSample.Unavailable);
        Assert.False(p.UpdateHunting(Standing,DirectInteractionSnapshot.None,Watch,Tick,true));
        p.RenderDesktop(Standing,DirectInteractionSnapshot.None,Tick);
        Assert.Same(UprightRumpSource.Standing(false),((Image)p.FindName("DororongImage")).Source);
    });

    private static bool Step(DororongPresenter p,PointerSample pointer)
    {
        var active=p.UpdateHunting(Standing,DirectInteractionSnapshot.None,pointer,Tick,false);
        p.RenderDesktop(Standing,DirectInteractionSnapshot.None,Tick);return active;
    }
    // Read the pose actually used by renderer AND interaction hit transforms,
    // without introducing a test-only production API.
    private static HuntGazePose Gaze(DororongPresenter p)=>(HuntGazePose)typeof(DororongPresenter)
        .GetField("_huntRenderedGaze",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(p)!;
    private static byte[] Pixels(DororongPresenter p)
    {
        var source=(BitmapSource)((Image)p.FindName("DororongImage")).Source;
        var bytes=new byte[96*96*4];new FormatConvertedBitmap(source,System.Windows.Media.PixelFormats.Pbgra32,null,0).CopyPixels(bytes,384,0);return bytes;
    }
    private static long Difference(byte[] a,byte[] b)
    {
        long sum=0;for(var i=0;i<a.Length;i++)sum+=Math.Abs(a[i]-b[i]);return sum;
    }
}
