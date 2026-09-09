using System.Windows.Media.Imaging;
using System.Windows.Media;
using Dororong.App.Controls;

namespace Dororong.App.Tests.Controls;

public class AnatomicalRecoveryTests
{
    [Fact]
    public void Contour_refinement_chooses_the_edge_nearest_the_original_landmark() => CheekProductTests.Sta(() =>
    {
        PremultipliedFrame Edges(params int[] rows)
        {
            var pixels=new byte[96*96*4];
            foreach(var y in rows) pixels[(y*96+56)*4+3]=255;
            var bitmap=BitmapSource.Create(96,96,96,96,PixelFormats.Pbgra32,null,pixels,384);
            bitmap.Freeze();return PremultipliedFrame.From(bitmap);
        }
        var mesh=new AnatomicalRecoveryMesh(2,0,new(40,40),new(40,40),Edges(73,75),Edges(73));
        var point=mesh.Sample(.5)[74*96+56]!.Value;
        Assert.Equal(75,point.From.Y,6);
        Assert.Equal(73,point.To.Y,6);
    });

    [Theory]
    [InlineData(.125)]
    [InlineData(.25)]
    [InlineData(.375)]
    public void Actual_short_pull_chain_keeps_one_rear_paw_contour(double progress) => CheekProductTests.Sta(() =>
    {
        _ = new DororongPresenter();
        var canonical = new BitmapImage(new Uri("pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png"));
        var frames = new SuppliedBodyDragFrames(PremultipliedFrame.From(canonical));
        var chain = frames.RecoveryFrom(frames.Sample(1d/7),canonical);
        var pixels = PremultipliedFrame.From(new HeadRecoveryFrame(chain, suppliedSource: 2).Sample(progress)).Pixels;
        int White(int y) => pixels[(y*96+66)*4]+255-pixels[(y*96+66)*4+3];
        Assert.Equal(1,Enumerable.Range(72,17).Count(y=>White(y)<230 && White(y-1)-White(y)>10 && White(y+1)-White(y)>10));
        Assert.True(Enumerable.Range(72,17).Min(White)<180,"Rear paw ink must not disappear.");
    });

    [Fact]
    public void Short_pull_middle_paw_does_not_leave_a_second_faded_tip() => CheekProductTests.Sta(() =>
    {
        _ = new DororongPresenter();
        var canonical = new BitmapImage(new Uri("pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png"));
        var frames = new SuppliedBodyDragFrames(PremultipliedFrame.From(canonical));
        var recovery = new HeadRecoveryFrame([frames.Sample(1d/7), canonical], suppliedSource: 2);
        var pixels = PremultipliedFrame.From(recovery.Sample(.5)).Pixels;
        // A soft antialias pixel below the tip is legitimate. Two separated
        // dark minima on a white background are two visible outline strokes.
        int White(int y) => pixels[(y*96+43)*4]+255-pixels[(y*96+43)*4+3];
        var outlines=Enumerable.Range(81,8).Count(y=>White(y)<230 && White(y-1)-White(y)>10 && White(y+1)-White(y)>10);
        Assert.Equal(1,outlines);
        Assert.True(Enumerable.Range(81,8).Min(White)<150,"The single outline must retain dark ink, not fade away.");
    });

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void Every_partial_pose_keeps_exact_endpoints_and_protected_upper_head(int key) => CheekProductTests.Sta(() =>
    {
        _ = new DororongPresenter();
        var canonical = new BitmapImage(new Uri("pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png"));
        var frames = new SuppliedBodyDragFrames(PremultipliedFrame.From(canonical));
        var held=frames.Sample((key-1)/7d);
        var chain=frames.RecoveryFrom(held,canonical);
        var recovery=new HeadRecoveryFrame(chain,key);
        var legacy=new HeadRecoveryFrame(chain);
        Assert.Same(held,recovery.Sample(0));Assert.Same(canonical,recovery.Sample(1));
        for(var i=1;i<40;i++)
        {
            var p=i/40d;
            var actual=PremultipliedFrame.From(recovery.Sample(p)).Pixels;
            var previous=PremultipliedFrame.From(legacy.Sample(p)).Pixels;
            Assert.Equal(previous.AsSpan(0,40*384).ToArray(),actual.AsSpan(0,40*384).ToArray());
            for(var j=0;j<actual.Length;j+=4)
            {
                Assert.True(actual[j]<=actual[j+3] && actual[j+1]<=actual[j+3] && actual[j+2]<=actual[j+3]);
            }
        }
    });
}
