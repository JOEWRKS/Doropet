using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Xunit.Abstractions;

namespace Dororong.App.Tests.Controls;

public sealed class SurroundingPullSafetyTests(ITestOutputHelper output)
{
    [Fact]
    public void Face_adjacent_grab_has_a_continuous_pinned_displacement_field()
    {
        var center=new PointD(46.1819803747,26.2479928635);var grab=new PointD(48,31);
        var at=SurroundingPullRenderer.HeadDisplacement(new(48,34),center,grab,new(14,0),new(14,0));
        var after=SurroundingPullRenderer.HeadDisplacement(new(48,34.000001),center,grab,new(14,0),new(14,0));
        Assert.Equal(default,at);Assert.InRange(BodyPullSession.Length(after-at),0,.00001);
    }

    [Fact]
    public void Secondary_motion_is_time_partition_independent_and_release_reset_is_exact()
    {
        var a=new SurroundingPullMotion();var b=new SurroundingPullMotion();
        for(var i=0;i<30;i++)a.Step(new(12,-6),8);
        for(var i=0;i<10;i++)b.Step(new(12,-6),24);
        Assert.Equal(a.Current.X,b.Current.X,10);Assert.Equal(a.Far.Y,b.Far.Y,10);
        a.Release();a.Step(new(999,999),200);
        Assert.Equal(default,a.Current);Assert.Equal(default,a.Far);
        a.Reset();a.Step(new(-8,4),16);Assert.True(a.Current.X<0);
    }

    [Fact]
    public void All_eight_head_keys_all_directions_keep_nontransparent_pixels_inside_native_crop()=>Sta(()=>
    {
        var source=Rest();var frames=new SuppliedBodyDragFrames(PremultipliedFrame.From(source));
        PointD[] grabs=[new(25.25,37.5),new(37.5,28.25),new(50.25,48.5)];
        PointD[] shifts=[new(0,-1),new(0,-3),new(1,-7),new(2,-11),new(4,-13),new(6,-15),new(8,-15),new(11,-16)];
        for(var key=0;key<8;key++)foreach(var grab in grabs)for(var angle=0;angle<8;angle++)
        {
            var pull=new PointD(14*Math.Cos(angle*Math.PI/4),14*Math.Sin(angle*Math.PI/4));
            var pixels=SurroundingPullRenderer.Head(PremultipliedFrame.From(frames.Sample(key/7d)).Pixels,grab+shifts[key],pull,pull);
            for(var y=0;y<160;y++)for(var x=0;x<160;x++)if(x<32||x>=128||y<32||y>=128)
                Assert.True(pixels[(y*160+x)*4+3]==0,$"Clipped alpha at key{key+1} grab{grab} angle{angle}: {x-32},{y-32}");
            for(var i=0;i<pixels.Length;i+=4)for(var c=0;c<3;c++)Assert.True(pixels[i+c]<=pixels[i+3]);
        }
    });

    // Diagnostic timings are evidence, not a hardware-independent performance assertion.
    [Fact]
    public void Record_real_renderer_cost_after_warmup()=>Sta(()=>
    {
        var source=PremultipliedFrame.From(Rest()).Pixels;
        Measure("primary-only",()=>BodyPullRenderer.Render(source,BodyRegion.FrontPaw,new(-10,6),new(23,79)));
        Measure("head",()=>SurroundingPullRenderer.Head(source,new(37,28),new(12,6),new(10,4)));
        foreach(var item in new[]{(BodyRegion.FrontPaw,new PointD(23,79)),(BodyRegion.MiddlePaw,new PointD(43,85)),(BodyRegion.RightPaw,new PointD(66,82)),(BodyRegion.Belly,new PointD(53,69)),(BodyRegion.Rump,new PointD(69,61))})
            Measure(item.Item1.ToString(),()=>SurroundingPullRenderer.Body(source,item.Item1,new(-10,6),item.Item2,new(-9,5),new(-7,3)));
    });

    [Fact]
    public void Runtime_optimization_is_byte_exact_against_approved_field()=>Sta(()=>
    {
        var source=PremultipliedFrame.From(Rest()).Pixels;
        foreach(var pull in new PointD[]{new(-10,6),new(9,-12),new(5,8)})
        {
            Assert.Equal(ApprovedSurroundingPullRenderer.Head(source,new(37,28),pull),SurroundingPullRenderer.Head(source,new(37,28),pull));
            foreach(var item in new[]{(BodyRegion.FrontPaw,new PointD(23,79)),(BodyRegion.MiddlePaw,new PointD(43,85)),(BodyRegion.RightPaw,new PointD(66,82)),(BodyRegion.Belly,new PointD(53,69)),(BodyRegion.Rump,new PointD(69,61))})
                Assert.Equal(ApprovedSurroundingPullRenderer.Body(source,item.Item1,pull,item.Item2,pull),SurroundingPullRenderer.Body(source,item.Item1,pull,item.Item2,pull));
        }
    });
    void Measure(string label,Func<byte[]> render)
    {
        for(var i=0;i<3;i++)render();
        var times=new double[12];for(var i=0;i<times.Length;i++){var clock=Stopwatch.StartNew();var pixels=render();times[i]=clock.Elapsed.TotalMilliseconds;Assert.Equal(160*160*4,pixels.Length);}
        Array.Sort(times);output.WriteLine($"{label}: median={times[6]:F3}ms p95={times[11]:F3}ms samples={times.Length}");
    }
    static BitmapSource Rest(){var p=new DororongPresenter();p.Render(new(PetState.Idle,default,FacingDirection.Right,0,false,null),DirectInteractionSnapshot.None);return (BitmapSource)((Image)p.FindName("DororongImage")).Source;}
    static void Sta(Action action){Exception? error=null;var t=new Thread(()=>{try{action();}catch(Exception e){error=e;}});t.SetApartmentState(ApartmentState.STA);t.Start();t.Join();if(error is not null)ExceptionDispatchInfo.Capture(error).Throw();}
}
