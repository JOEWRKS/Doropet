using System.Windows.Media.Imaging;
using Dororong.App.Controls;

namespace Dororong.App.Tests.Controls;

public class AuthoredRibbonTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Perch_and_dangling_keep_the_ordinary_ribbon_fill_and_outer_stroke(bool perch) => CheekProductTests.Sta(() =>
    {
        _ = new DororongPresenter();
        var canonical = new BitmapImage(new Uri("pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png"));
        var frame = perch ? PerchExpressionFrames.Open : new SuppliedBodyDragFrames(PremultipliedFrame.From(canonical)).Sample(1);
        var actual = PremultipliedFrame.From(frame).Pixels;
        var reference = PremultipliedFrame.From(canonical).Pixels;
        var dx = perch ? 8 : 11; var dy = perch ? -6 : -16;
        // Hand-checked missing tail outline, adjacent white fill and middle-loop fill.
        foreach (var (x,y) in new[] { (67,50), (66,52), (65,54), (66,43), (67,45) })
        {
            var at = ((y+dy)*frame.PixelWidth+x+dx)*4;
            Assert.Equal(reference.AsSpan((y*96+x)*4,4).ToArray(),actual.AsSpan(at,4).ToArray());
        }
        // The outer tail stroke is absent here in both broken cutouts.
        Assert.InRange(actual[((51+dy)*frame.PixelWidth+68+dx)*4+3],100,255);
    });

    [Fact]
    public void Tail_fill_and_outline_do_not_disappear_at_any_authored_key() => CheekProductTests.Sta(() =>
    {
        _ = new DororongPresenter();
        var canonical = new BitmapImage(new Uri("pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png"));
        var bank = new SuppliedBodyDragFrames(PremultipliedFrame.From(canonical));
        (int X,int Y)[] shifts=[(0,-1),(0,-3),(1,-7),(2,-11),(4,-13),(6,-15),(8,-15),(11,-16)];
        for(var key=0;key<8;key++) foreach(var dense in new[]{false,true})
        {
            var frame=dense?LayeredPullFrames.Sample(key/7d):bank.Sample(key/7d);
            var p=PremultipliedFrame.From(frame).Pixels;
            // Canonical (67,50): opaque antialiased inner stroke, B=209.
            Assert.Equal(209,p[((50+shifts[key].Y)*96+67+shifts[key].X)*4]);
            Assert.Equal(255,p[((50+shifts[key].Y)*96+67+shifts[key].X)*4+3]);
        }
    });

    [Fact]
    public void Upper_loop_preserves_reference_translucency_instead_of_double_compositing() => CheekProductTests.Sta(() =>
    {
        _ = new DororongPresenter();
        var canonical = new BitmapImage(new Uri("pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png"));
        var reference=PremultipliedFrame.From(canonical).Pixels;
        var actual=PremultipliedFrame.From(PerchExpressionFrames.Open).Pixels;
        // Outer edge is already translucent in the ordinary PNG (alpha102).
        var donor=(42*96+69)*4; var target=(36*100+77)*4;
        Assert.Equal(102,reference[donor+3]);
        Assert.Equal(reference.AsSpan(donor,4).ToArray(),actual.AsSpan(target,4).ToArray());
    });

    [Fact]
    public void Tail_edge_does_not_cut_a_transparent_hole_in_the_early_pose_torso() => CheekProductTests.Sta(() =>
    {
        _ = new DororongPresenter();
        var canonical = new BitmapImage(new Uri("pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png"));
        var bank=new SuppliedBodyDragFrames(PremultipliedFrame.From(canonical));
        var pixels=PremultipliedFrame.From(bank.Sample(0)).Pixels;
        Assert.Equal(255,pixels[(50*96+68)*4+3]);
    });

    [Fact]
    public void All_dense_frames_preserve_premultiplied_coverage_and_ribbon_fill() => CheekProductTests.Sta(() =>
    {
        _ = new DororongPresenter();
        var canonical = new BitmapImage(new Uri("pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png"));
        var reference=PremultipliedFrame.From(canonical).Pixels;
        (int X,int Y)[] shifts=[(0,-1),(0,-3),(1,-7),(2,-11),(4,-13),(6,-15),(8,-15),(11,-16)];
        for(var i=0;i<113;i++)
        {
            var key=Math.Min(6,i/16);var t=(i-key*16)/16d;
            var dx=shifts[key].X+(shifts[key+1].X-shifts[key].X)*t;
            var dy=shifts[key].Y+(shifts[key+1].Y-shifts[key].Y)*t;
            var pixels=PremultipliedFrame.From(LayeredPullFrames.Sample(i/112d)).Pixels;
            for(var at=0;at<pixels.Length;at+=4) for(var c=0;c<3;c++) Assert.InRange(pixels[at+c],0,pixels[at+3]);
            // Four ordinary interior texels, all opaque. Independent bilinear
            // expectation at one moving point catches a missing dense-bank repair.
            var x=(int)Math.Floor(66+dx); var y=(int)Math.Floor(51+dy);
            var rx=x-dx;var ry=y-dy;var ix=(int)Math.Floor(rx);var iy=(int)Math.Floor(ry);
            for(var c=0;c<4;c++)
            {
                double expected=0;
                for(var v=0;v<2;v++)for(var u=0;u<2;u++)
                    expected+=reference[((iy+v)*96+ix+u)*4+c]*(u==0?1-(rx-ix):rx-ix)*(v==0?1-(ry-iy):ry-iy);
                Assert.InRange(Math.Abs(pixels[(y*96+x)*4+c]-Math.Round(expected)),0,1);
            }
        }
    });

    [Fact]
    public void Repaired_ribbon_does_not_pop_at_original_key_boundaries() => CheekProductTests.Sta(() =>
    {
        _ = new DororongPresenter();
        var canonical = new BitmapImage(new Uri("pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png"));
        var bank=new SuppliedBodyDragFrames(PremultipliedFrame.From(canonical));
        (int X,int Y)[] shifts=[(0,-1),(0,-3),(1,-7),(2,-11),(4,-13),(6,-15),(8,-15),(11,-16)];
        for(var key=0;key<8;key++) foreach(var side in new[]{-1,1})
        {
            var exact=PremultipliedFrame.From(bank.Sample(key/7d)).Pixels;
            var near=PremultipliedFrame.From(bank.Sample(Math.Clamp(key/7d+side*1e-6,0,1))).Pixels;
            foreach(var (x,y) in new[]{(66,43),(69,42),(67,50),(68,51),(65,54)})
            {
                var at=((y+shifts[key].Y)*96+x+shifts[key].X)*4;
                for(var c=0;c<4;c++) Assert.InRange(Math.Abs(exact[at+c]-near[at+c]),0,1);
            }
        }
    });
}
