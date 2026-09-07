using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Controls;

public sealed class HeadWideSwingProofTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actual_speed_driven_raster_preserves_ink_outside_original_box(bool mirror) => CheekProductTests.Sta(() =>
    {
        var destination=Environment.GetEnvironmentVariable("DORORONG_WIDE_SWING_PROOF");
        var frames=new List<BitmapSource>();var rows=new List<object>();var mostOutside=0;
        if(destination is not null){destination=Path.Combine(destination,mirror?"mirrored":"normal");Assert.False(Directory.Exists(destination));Directory.CreateDirectory(destination);}
        foreach(var speed in new[]{-1500d,-1000d,-500d,0d,500d,1000d,1500d})
        {
            var (root,presenter,pet,direct,local)=HeadWideSwingTests.Setup(mirror,new(37.5,28.25));
            var controller=new DirectInteractionController();controller.BeginDistanceBody(direct.PressOrigin,new(4,4));
            // Capture the neutral visible point before the initial extension.
            var logical=direct.PressOrigin;
            presenter.Render(pet,controller.Advance(TimeSpan.Zero,new(true,logical){ScreenPixelPosition=new(400,300)},true,PetState.Walk,PetState.Walk),TimeSpan.Zero);
            logical+=new PointD(0,-80);
            var pointer=new PointD(400,220);
            var snapshot=controller.Advance(TimeSpan.FromMilliseconds(16),new(true,logical){ScreenPixelPosition=pointer},true,PetState.Walk,PetState.Dragged);
            presenter.Render(pet with{Position=logical-new PointD(local.X,local.Y)},snapshot,TimeSpan.FromMilliseconds(16));
            for(var i=0;i<180;i++)
            {
                pointer+=new PointD(speed*.016,0);logical+=new PointD(speed*.016,0);
                snapshot=controller.Advance(TimeSpan.FromMilliseconds(16),new(true,logical){ScreenPixelPosition=pointer},true,PetState.Dragged,PetState.Dragged);
                presenter.Render(pet with{Position=logical-new PointD(local.X,local.Y)},snapshot,TimeSpan.FromMilliseconds(16));
            }
            HeadWideSwingTests.Layout(root);
            var image=(Image)presenter.FindName("DororongImage");var angle=((RotateTransform)presenter.FindName("BodyRotateTransform")).Angle;
            var wanted=speed switch{ -1500=>-88,-1000=>-80,-500=>-40,0=>0,500=>40,1000=>80,_=>88};
            Assert.InRange(angle,wanted-.1,wanted+.1);
            var rendered=new RenderTargetBitmap(336,336,96,96,PixelFormats.Pbgra32);rendered.Render(root);rendered.Freeze();
            var pixels=new byte[336*336*4];rendered.CopyPixels(pixels,336*4,0);
            var source=PremultipliedFrame.From((BitmapSource)image.Source);
            long ink=0,reference=0;var outside=0;
            for(var i=3;i<source.Pixels.Length;i+=4)reference+=source.Pixels[i];
            for(var y=0;y<336;y++)for(var x=0;x<336;x++)
            {
                var alpha=pixels[(y*336+x)*4+3];ink+=alpha;
                if(alpha!=0&&(x<96||x>=240||y<96||y>=240))outside++;
                if(x==0||x==335||y==0||y==335)Assert.Equal(0,alpha);
            }
            Assert.InRange(ink/(double)reference,.97,1.03); // Missing inner overflow fails by losing alpha mass.
            mostOutside=Math.Max(mostOutside,outside);
            frames.Add(rendered);rows.Add(new{speedPixelsPerSecond=speed,angle,alphaRatio=ink/(double)reference,outsideLogicalBox=outside,mirror});
            if(destination is not null)Save(Path.Combine(destination,$"speed-{speed}-native.png"),rendered);
        }
        // The crown is off-centre: -88 can fit inside144 while +88 overflows it.
        // Require real overflow in the sweep, not an arbitrary overflow in both signs.
        Assert.True(mostOutside>80,$"The sweep must actually render into the gutter; observed {mostOutside} pixels");
        if(destination is not null)
        {
            var visual=new DrawingVisual();RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.NearestNeighbor);
            using(var dc=visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.WhiteSmoke,null,new Rect(0,0,1344,896));
                // Full native files remain available; this sheet crops transparent gutter only.
                foreach(var (index,slot) in new[]{(0,0),(2,1),(3,2),(6,3),(4,4),(5,5)})
                {
                    var crop=new CroppedBitmap(frames[index],new Int32Rect(56,56,224,224));
                    dc.DrawImage(crop,new Rect((slot%3)*448,(slot/3)*448,448,448));
                }
            }
            var sheet=new RenderTargetBitmap(1344,896,96,96,PixelFormats.Pbgra32);sheet.Render(visual);Save(Path.Combine(destination,"speed-comparison-2x.png"),sheet);
            File.WriteAllText(Path.Combine(destination,"measurements.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));
        }
    });

    private static void Save(string path,BitmapSource source)
    {var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));using var stream=File.Create(path);encoder.Save(stream);}
}
