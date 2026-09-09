using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Windows.Media;
using System.IO;
using Dororong.App.Controls;

namespace Dororong.App.Tests.Controls;

public class PerchReadinessTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PerchReadiness_supports_canonical_and_padded_body_carry_without_touching_face(bool padded) => CheekProductTests.Sta(() =>
    {
        var p=new DororongPresenter();var canonical=(BitmapSource)((Image)p.FindName("DororongImage")).Source;
        var originalFrame=PremultipliedFrame.From(canonical);var width=padded?160:96;var pad=padded?32:0;
        var pixels=new byte[width*width*4];
        for(var y=0;y<96;y++)Array.Copy(originalFrame.Pixels,y*384,pixels,((y+pad)*width+pad)*4,384);
        var source=BitmapSource.Create(width,width,96,96,PixelFormats.Pbgra32,null,pixels,width*4);
        var image=new Image{Source=source};var cue=new PerchReadinessPresentation();
        cue.Apply(image,true,false,TimeSpan.FromMilliseconds(180));
        var actual=PerchExpressionTests.Pixels((BitmapSource)image.Source);var before=PerchExpressionTests.Pixels(source);
        Assert.False(before.SequenceEqual(actual));
        for(var y=0;y<width;y++)for(var x=0;x<width;x++)
            if(y<67+pad||y>91+pad||x<12+pad||x>53+pad)
                Assert.Equal(before.AsSpan((y*width+x)*4,4).ToArray(),actual.AsSpan((y*width+x)*4,4).ToArray());
        cue.Apply(image,false,false,TimeSpan.Zero);Assert.Same(source,image.Source);
    });

    [Fact]
    public void PerchReadiness_wpf_sequence_keeps_both_facing_transforms_and_exports_proof() => CheekProductTests.Sta(() =>
    {
        var p=new DororongPresenter();
        var canonical=(BitmapSource)((Image)p.FindName("DororongImage")).Source;
        var source=new SuppliedBodyDragFrames(PremultipliedFrame.From(canonical)).Sample(1);
        var sheet=new Canvas{Width=12*112,Height=2*124,Background=Brushes.WhiteSmoke};
        for(var row=0;row<2;row++)
        {
            var image=new Image{Width=96,Height=96,Source=source,RenderTransformOrigin=new(.5,.5),RenderTransform=new ScaleTransform(row==0?1:-1,1)};
            var cue=new PerchReadinessPresentation();var poses=new List<byte[]>();
            for(var step=0;step<12;step++)
            {
                cue.Apply(image,true,true,TimeSpan.FromMilliseconds(60));
                image.Measure(new(96,96));image.Arrange(new(0,0,96,96));image.UpdateLayout();
                var pose=new RenderTargetBitmap(96,96,96,96,PixelFormats.Pbgra32);pose.Render(image);pose.Freeze();poses.Add(PerchExpressionTests.Pixels(pose));
                var tile=new Image{Width=96,Height=96,Source=pose};Canvas.SetLeft(tile,step*112+8);Canvas.SetTop(tile,row*124+24);sheet.Children.Add(tile);
                var label=new TextBlock{Text=$"{(step+1)*60} ms",FontSize=10};Canvas.SetLeft(label,step*112+8);Canvas.SetTop(label,row*124+4);sheet.Children.Add(label);
                Assert.Equal(row==0?1:-1,((ScaleTransform)image.RenderTransform).ScaleX);
            }
            Assert.False(poses[2].SequenceEqual(poses[8]));
        }
        sheet.Measure(new(sheet.Width,sheet.Height));sheet.Arrange(new(0,0,sheet.Width,sheet.Height));sheet.UpdateLayout();
        var directory=Path.Combine(EdgePerchPresentationTests.ProjectRoot(),"artifacts","repro","perch-flutter-outline-20260909","proof");Directory.CreateDirectory(directory);
        foreach(var scale in new[]{1,3})
        {
            var bitmap=new RenderTargetBitmap((int)sheet.Width*scale,(int)sheet.Height*scale,96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(sheet);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream=File.Create(Path.Combine(directory,$"wave-{scale}x.png"));encoder.Save(stream);
        }
    });

    [Fact]
    public void Readiness_does_not_move_grabbed_pixels_even_when_pointer_is_on_a_foreleg() => CheekProductTests.Sta(() =>
    {
        var p=new DororongPresenter();
        var canonical=(BitmapSource)((Image)p.FindName("DororongImage")).Source;
        var source=new SuppliedBodyDragFrames(PremultipliedFrame.From(canonical)).Sample(1);
        var image=new Image{Source=source};var cue=new PerchReadinessPresentation();
        var before=PerchExpressionTests.Pixels(source);
        cue.Apply(image,true,true,TimeSpan.FromMilliseconds(180),new(33,62));
        var after=PerchExpressionTests.Pixels((BitmapSource)image.Source);
        for(var y=60;y<=64;y++)for(var x=31;x<=35;x++)
            Assert.Equal(before.AsSpan((y*96+x)*4,4).ToArray(),after.AsSpan((y*96+x)*4,4).ToArray());
        Assert.False(before.SequenceEqual(after));
    });

    [Fact]
    public void Ready_waves_only_forelegs_and_stopping_restores_exact_source() => CheekProductTests.Sta(() =>
    {
        var presenter = new DororongPresenter();
        var canonical = (BitmapSource)((Image)presenter.FindName("DororongImage")).Source;
        var source = new SuppliedBodyDragFrames(PremultipliedFrame.From(canonical)).Sample(1);
        var image = new Image { Source = source };
        var cue = new PerchReadinessPresentation();
        var original = PerchExpressionTests.Pixels(source);
        cue.Apply(image, true, true, TimeSpan.FromMilliseconds(120));
        var first = PerchExpressionTests.Pixels((BitmapSource)image.Source);
        Assert.False(original.SequenceEqual(first), "Eligible forelegs must visibly wave, not remain the held image.");
        for (var y=0;y<96;y++) for(var x=0;x<96;x++)
            if(y<52 || y>=69 || x<22 || x>=63)
                Assert.Equal(original.AsSpan((y*96+x)*4,4).ToArray(), first.AsSpan((y*96+x)*4,4).ToArray());
        cue.Restore();
        Assert.Same(source,image.Source);
        cue.Apply(image,true,true,TimeSpan.FromMilliseconds(180));
        Assert.False(first.SequenceEqual(PerchExpressionTests.Pixels((BitmapSource)image.Source)));
        cue.Apply(image,false,true,TimeSpan.FromMilliseconds(16));
        Assert.Same(source,image.Source);
    });

}
