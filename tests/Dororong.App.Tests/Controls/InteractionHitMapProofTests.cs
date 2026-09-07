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

public sealed class InteractionHitMapProofTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void All_visible_pixels_have_consistent_routing_and_no_lower_head_fallback(bool mirror) => CheekProductTests.Sta(() =>
    {
        var p=InteractionHitRoutingTests.Setup(PetState.Walk,mirror,.1);var pixels=InteractionHitRoutingTests.Pixels(p);
        var ownership=new byte[96*96*4];var cheekOverlay=(byte[])pixels.Clone();
        var counts=new Dictionary<string,int>();var bodyExtensions=new List<object>();var oldCheekCount=0;
        for(var y=0;y<96;y++)for(var x=0;x<96;x++)
        {
            var point=new PointD(x,y);var i=(y*96+x)*4;var opaque=pixels[i+3]!=0;
            var target=p.ClassifyOpaqueSourcePoint(point,opaque);
            if(!opaque){Assert.Equal(DirectInteractionTarget.None,target);continue;}
            if(target==DirectInteractionTarget.Body)
                Assert.True(y<48&&x>=6&&x<64,$"Lower pixel ({x},{y}) became head drag");
            if(y>=66)Assert.NotEqual(DirectInteractionTarget.Body,target);
            var oldBody=BodyRegionMap.Pick(point,pixels);
            if(oldBody!=BodyRegion.None)Assert.Equal(DirectInteractionTarget.FiveRegionBody,target);
            var region=InteractionHitMap.PickBody(point,pixels);
            Assert.Equal(target==DirectInteractionTarget.RightCheek,p.TryCreateCheekPullCapture(point,out _));
            Assert.Equal(target==DirectInteractionTarget.FiveRegionBody,p.TryCreateBodyPullCapture(point,out var capture));
            if(capture is not null)Assert.Equal(region,capture.Region);
            if(oldBody==BodyRegion.None&&region!=BodyRegion.None)bodyExtensions.Add(new{x,y,region=region.ToString()});
            if(CheekPullRenderer.Rows.Any(r=>y==r.Y&&y<64&&x>=r.Skin&&x<r.Root))
            {oldCheekCount++;Assert.Equal(DirectInteractionTarget.RightCheek,target);}
            var key=target==DirectInteractionTarget.FiveRegionBody?region.ToString():target.ToString();
            counts[key]=counts.GetValueOrDefault(key)+1;
            var color=target switch
            {
                DirectInteractionTarget.Body=>Color.FromRgb(237,183,62),
                DirectInteractionTarget.RightCheek=>Color.FromRgb(242,65,92),
                DirectInteractionTarget.LeftCheek=>Color.FromRgb(151,141,203),
                DirectInteractionTarget.FiveRegionBody=>region switch
                {
                    BodyRegion.FrontPaw=>Color.FromRgb(40,186,200),BodyRegion.MiddlePaw=>Color.FromRgb(73,137,229),
                    BodyRegion.RightPaw=>Color.FromRgb(184,91,190),BodyRegion.Belly=>Color.FromRgb(112,180,81),
                    _=>Color.FromRgb(232,135,54)
                },
                _=>Color.FromRgb(135,147,161)
            };
            ownership[i]=color.B;ownership[i+1]=color.G;ownership[i+2]=color.R;ownership[i+3]=pixels[i+3];
            if(target==DirectInteractionTarget.RightCheek)
            {cheekOverlay[i]=(byte)((pixels[i]+color.B)/2);cheekOverlay[i+1]=(byte)((pixels[i+1]+color.G)/2);cheekOverlay[i+2]=(byte)((pixels[i+2]+color.R)/2);}
        }
        Assert.True(counts[nameof(DirectInteractionTarget.RightCheek)]>oldCheekCount*3);
        Assert.NotEmpty(bodyExtensions);
        Assert.Equal(pixels,InteractionHitRoutingTests.Pixels(p));
        var destination=Environment.GetEnvironmentVariable("DORORONG_HIT_MAP_PROOF");
        if(destination is not null)
        {
            destination=Path.Combine(destination,mirror?"mirrored":"normal");Assert.False(Directory.Exists(destination));Directory.CreateDirectory(destination);
            Save(Path.Combine(destination,"routing-native.png"),Bitmap(ownership));
            Save(Path.Combine(destination,"cheek-hit-overlay-native.png"),Bitmap(cheekOverlay));
            var visual=new DrawingVisual();RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.NearestNeighbor);
            using(var dc=visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.WhiteSmoke,null,new Rect(0,0,1536,768));
                dc.DrawImage(Bitmap(cheekOverlay),new Rect(0,0,768,768));dc.DrawImage(Bitmap(ownership),new Rect(768,0,768,768));
            }
            var sheet=new RenderTargetBitmap(1536,768,96,96,PixelFormats.Pbgra32);sheet.Render(visual);Save(Path.Combine(destination,"routing-review-8x.png"),sheet);
            File.WriteAllText(Path.Combine(destination,"ownership.json"),JsonSerializer.Serialize(new{counts,oldCheekCount,bodyExtensions,mirroredFacing=mirror,coordinates="Unmirrored source96 pixels; visible screen mapping tested separately",headRule="Explicit upper region only",artChanged=false},new JsonSerializerOptions{WriteIndented=true}));
        }
    });

    private static BitmapSource Bitmap(byte[] pixels)=>BitmapSource.Create(96,96,96,96,PixelFormats.Bgra32,null,pixels,384);
    private static void Save(string path,BitmapSource source)
    {var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));using var stream=File.Create(path);encoder.Save(stream);}
}
