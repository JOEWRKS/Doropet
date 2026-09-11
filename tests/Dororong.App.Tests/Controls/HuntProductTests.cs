using Dororong.App.Controls;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dororong.App.Tests.Controls;

public sealed class HuntProductTests
{
    [Fact]
    public void Approved_hunting_layers_are_embedded() => Assert.Contains(
        "Dororong.App.Assets.hunting.pbgra.gz", typeof(DororongPresenter).Assembly.GetManifestResourceNames());

    [Fact]
    public void Shared_drawing_resources_work_across_presenter_threads()
    {
        for(var i=0;i<2;i++) EdgePerchPresentationTests.Sta(() =>
            Assert.True(new HuntRenderer().Render(60,new(0,0,0)).IsFrozen));
    }

    [Fact]
    public void Outline_repair_preserves_every_alpha_byte()
    {
        var pixels=new byte[256*256*4];new Random(43).NextBytes(pixels);
        var before=(byte[])pixels.Clone();HuntOutline.Finish(pixels,1);
        for(var i=3;i<pixels.Length;i+=4)Assert.Equal(before[i],pixels[i]);
    }

    [Fact]
    public void Native_renderer_matches_approved_geometry_and_keeps_captures_immutable() => EdgePerchPresentationTests.Sta(() =>
    {
        var renderer=new HuntRenderer();var first=renderer.Render(60,new(0,0,0));
        var bytes=new byte[96*96*4];first.CopyPixels(bytes,384,0);
        Assert.True(first.IsFrozen);Assert.Equal(96,first.PixelWidth);
        var root=EdgePerchPresentationTests.ProjectRoot();var output=Path.Combine(root,"artifacts/repro/hunt-product-20260911");Directory.CreateDirectory(output);
        foreach(var (name,frame,gaze) in new[]{("rest",156,new HuntGazePose(0,0,0)),("hunt",60,new HuntGazePose(0,0,0)),
            ("up",60,new HuntGazePose(-.85,-.65,Math.PI/9)),("down",60,new HuntGazePose(.85,.65,-Math.PI/9))})
        {
            var actual=renderer.Render(frame,gaze);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(actual));
            using(var file=File.Create(Path.Combine(output,"native-"+name+".png")))encoder.Save(file);
            var reference=new BitmapImage(new Uri(Path.Combine(root,"tools/PreviewLocomotion/fixtures/approved-hunt-2799",name+".png")));
            var expected=new byte[bytes.Length];var observed=new byte[bytes.Length];
            new FormatConvertedBitmap(reference,PixelFormats.Pbgra32,null,0).CopyPixels(expected,384,0);actual.CopyPixels(observed,384,0);
            double error=0,intersection=0,union=0;
            for(var i=0;i<observed.Length;i++){error+=Math.Abs(expected[i]-observed[i]);if(i%4==3){if(expected[i]>127||observed[i]>127)union++;if(expected[i]>127&&observed[i]>127)intersection++;}}
            Assert.True(error/observed.Length<8,$"{name} RGBA mean difference {error/observed.Length}");
            Assert.True(intersection/union>.92,$"{name} silhouette intersection {intersection/union}");
        }
        var after=new byte[bytes.Length];first.CopyPixels(after,384,0);Assert.Equal(bytes,after);
        var closed=renderer.Render(60,new(0,0,0),true);closed.CopyPixels(after,384,0);Assert.NotEqual(bytes,after);
    });
}
