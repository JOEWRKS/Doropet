using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;

namespace Dororong.App.Tests.Controls;

public class ForelegFlutterTests
{
    [Theory]
    [InlineData(true,false)] [InlineData(false,false)] [InlineData(false,true)]
    public void Flutter_reaches_forward_with_connected_paws_and_preserves_premultiplied_alpha(bool hanging,bool padded) => CheekProductTests.Sta(() =>
    {
        var p=new DororongPresenter();
        var canonical=(BitmapSource)((Image)p.FindName("DororongImage")).Source;
        var source=hanging?Source():canonical;
        var width=padded?160:96;var pad=padded?32:0;
        if(padded)
        {
            var bytes=new byte[160*160*4];var input=PremultipliedFrame.From(source).Pixels;
            for(var y=0;y<96;y++)Array.Copy(input,y*384,bytes,((y+32)*160+32)*4,384);
            source=BitmapSource.Create(160,160,96,96,PixelFormats.Pbgra32,null,bytes,640);
        }
        var before=PerchExpressionTests.Pixels(source);
        var image=new Image{Source=source};var cue=new PerchReadinessPresentation();
        for(var step=0;step<16;step++)
        {
            cue.Apply(image,true,hanging,TimeSpan.FromMilliseconds(16));
            var after=PerchExpressionTests.Pixels((BitmapSource)image.Source);
            var premultiplied=PremultipliedFrame.From((BitmapSource)image.Source).Pixels;
            for(var i=0;i<premultiplied.Length;i+=4)for(var c=0;c<3;c++)Assert.True(premultiplied[i+c]<=premultiplied[i+3]);
            var reached=0;
            for(var y=hanging?56:70;y<(hanging?69:84);y++)for(var x=5;x<(hanging?31:17);x++)
                if(after[((y+pad)*width+x+pad)*4+3]>128 && before[((y+pad)*width+x+pad)*4+3]<128)reached++;
            Assert.True(reached>=3,"Forepaw must extend forward throughout flutter, not return to the vertical dangle.");
            var connected=new bool[width*width];var queue=new Queue<int>();
            var seed=(73+pad)*width+55+pad;queue.Enqueue(seed);connected[seed]=true;
            while(queue.TryDequeue(out var at))
            {
                foreach(var next in new[]{at-1,at+1,at-width,at+width})
                {
                    if(next<0||next>=connected.Length||connected[next]||after[next*4+3]<128)continue;
                    connected[next]=true;queue.Enqueue(next);
                }
            }
            for(var y=hanging?56:70;y<(hanging?69:91);y++)for(var x=5;x<53;x++)
            {
                var i=(y+pad)*width+x+pad;
                if(after[i*4+3]>=128&&before[i*4+3]<128)Assert.True(connected[i],$"Detached new paw pixel {x},{y} at step{step}");
            }
            if(step==3&&!padded)Export((BitmapSource)image.Source,hanging?"hang-quarter":"canonical-quarter");
        }
        cue.Apply(image,false,hanging,TimeSpan.Zero);Assert.Same(source,image.Source);
    });

    [Fact]
    public void Flutter_does_not_cut_out_or_duplicate_the_hair_overlapping_the_shoulders() => CheekProductTests.Sta(() =>
    {
        var source=Source();var before=PerchExpressionTests.Pixels(source);
        var image=new Image{Source=source};var cue=new PerchReadinessPresentation();
        for(var step=0;step<16;step++)
        {
            cue.Apply(image,true,true,TimeSpan.FromMilliseconds(16));
            var after=PerchExpressionTests.Pixels((BitmapSource)image.Source);
            for(var y=53;y<=55;y++)for(var x=49;x<58;x++)
                Assert.Equal(before.AsSpan((y*96+x)*4,4).ToArray(),after.AsSpan((y*96+x)*4,4).ToArray());
            for(var y=56;y<69;y++)for(var x=22;x<63;x++)
            {
                var i=(y*96+x)*4;
                Assert.True(after[i+2] <= after[i+1]+12,"Pink hair must not travel with the white paw cutout.");
            }
        }
    });

    [Fact]
    public void Flutter_leaves_torso_contour_below_and_beside_paws_exact() => CheekProductTests.Sta(() =>
    {
        var source = Source();
        Export(source, "source");
        var before = PerchExpressionTests.Pixels(source);
        var image = new Image { Source = source }; var cue = new PerchReadinessPresentation();
        cue.Apply(image, true, true, TimeSpan.FromMilliseconds(180));
        Export((BitmapSource)image.Source,"flutter-180");
        var after = PerchExpressionTests.Pixels((BitmapSource)image.Source);
        // The right-hand body contour is not a limb even within the old rectangle.
        for(var y=54;y<96;y++) for(var x=63;x<96;x++)
            Assert.Equal(before.AsSpan((y*96+x)*4,4).ToArray(), after.AsSpan((y*96+x)*4,4).ToArray());
        for(var y=69;y<96;y++)
            Assert.Equal(before.AsSpan(y*384,384).ToArray(), after.AsSpan(y*384,384).ToArray());
    });

    [Fact]
    public void Flutter_repeats_four_times_per_second_without_resetting_raised_arms() => CheekProductTests.Sta(() =>
    {
        var image = new Image { Source = Source() }; var cue = new PerchReadinessPresentation();
        cue.Apply(image,true,true,TimeSpan.FromMilliseconds(250));
        var first = PerchExpressionTests.Pixels((BitmapSource)image.Source);
        cue.Apply(image,true,true,TimeSpan.FromMilliseconds(62.5));
        Assert.False(first.SequenceEqual(PerchExpressionTests.Pixels((BitmapSource)image.Source)));
        cue.Apply(image,true,true,TimeSpan.FromMilliseconds(187.5));
        Assert.Equal(first,PerchExpressionTests.Pixels((BitmapSource)image.Source));
    });

    internal static BitmapSource Source()
    {
        var p=new DororongPresenter();
        return new SuppliedBodyDragFrames(PremultipliedFrame.From((BitmapSource)((Image)p.FindName("DororongImage")).Source)).Sample(1);
    }

    internal static void Export(BitmapSource source, string name)
    {
        var directory=Path.Combine(EdgePerchPresentationTests.ProjectRoot(),"artifacts","repro","perch-flutter-outline-20260909","proof");
        Directory.CreateDirectory(directory);
        var image=new Image{Source=source,Width=768,Height=768};
        RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.NearestNeighbor);
        image.Measure(new(768,768)); image.Arrange(new(0,0,768,768));
        var bitmap=new RenderTargetBitmap(768,768,96,96,PixelFormats.Pbgra32);bitmap.Render(image);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream=File.Create(Path.Combine(directory,name+".png"));encoder.Save(stream);
    }
}
