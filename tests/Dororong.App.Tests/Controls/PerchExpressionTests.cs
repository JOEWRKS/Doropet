using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.IO;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Controls;

public class PerchExpressionTests
{
    [Theory]
    [InlineData(FacingDirection.Left)]
    [InlineData(FacingDirection.Right)]
    public void Perched_blink_uses_only_open_and_closed_with_ordinary_timing(FacingDirection facing) => CheekProductTests.Sta(() =>
    {
        var p = Create(facing);
        // Observe two complete cycles, including the old half-closed intervals.
        // A squint route or the previous short cycle must fail this check.
        for (var milliseconds = 1; milliseconds <= 10000; milliseconds++)
        {
            p.ApplyEdgePerch(EdgePerchPhase.Attached, facing, TimeSpan.FromMilliseconds(1), false);
            var closed = milliseconds is >= 2000 and < 2360 or >= 7000 and < 7360;
            Assert.Same(closed ? PerchExpressionFrames.Closed : PerchExpressionFrames.Open, p.EdgePerchImage.Source);
        }
    });

    [Fact]
    public void Registered_blink_copies_only_the_authored_changed_pixels_and_keeps_every_opaque_source_pixel() => CheekProductTests.Sta(() =>
    {
        var original=Pixels(PerchExpressionFrames.Open);
        var canonical=Pixels(new BitmapImage(new Uri("pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png")));
        var registered=PerchExpressionFrames.RegisteredOpen();
        // Registration must preserve the cleaned runtime frame, not resurrect
        // the raw source's removed matte pixels or discard repaired chest texels.
        Assert.Equal(Enumerable.Range(0,100*100).Count(i=>original[i*4+3]!=0),
            Enumerable.Range(0,96*96).Count(i=>registered[i*4+3]!=0));
        for(var y=0;y<100;y++)for(var x=0;x<100;x++)
            if(original[(y*100+x)*4+3]!=0)
                Assert.Equal(original.AsSpan((y*100+x)*4,4).ToArray(),registered.AsSpan(((y+6)*96+x-8)*4,4).ToArray());
        foreach(var (name,frame,count) in new[]{("dororong-closed-eyes.png",PerchExpressionFrames.Closed,157)})
        {
            var authored=Pixels(new BitmapImage(new Uri($"pack://application:,,,/Dororong.App;component/Assets/{name}")));
            var actual=Pixels(frame);var changed=0;
            for(var y=0;y<100;y++)for(var x=0;x<100;x++)
            {
                var i=(y*100+x)*4;var nx=x-8;var ny=y+6;
                var eye=nx>=0&&nx<96&&ny>=0&&ny<96 && !canonical.AsSpan((ny*96+nx)*4,4).SequenceEqual(authored.AsSpan((ny*96+nx)*4,4));
                if(eye){Assert.Equal(authored.AsSpan((ny*96+nx)*4,4).ToArray(),actual.AsSpan(i,4).ToArray());changed++;}
                else Assert.Equal(original.AsSpan(i,4).ToArray(),actual.AsSpan(i,4).ToArray());
            }
            Assert.Equal(count,changed);
        }
    });

    [Fact]
    public void Negative_and_long_clock_deltas_stay_bounded_and_reset_restores_owned_visuals() => CheekProductTests.Sta(() =>
    {
        var p=Create(FacingDirection.Left);var image=p.EdgePerchImage;
        var start=image.TranslatePoint(new(28,64),p);
        p.ApplyEdgePerch(EdgePerchPhase.Attached,FacingDirection.Left,TimeSpan.FromMilliseconds(-500),false);Layout(p);
        Assert.Equal(start,image.TranslatePoint(new(28,64),p));
        p.ApplyEdgePerch(EdgePerchPhase.Attached,FacingDirection.Left,TimeSpan.MaxValue,false);Layout(p);
        Assert.Equal(start,image.TranslatePoint(new(28,64),p));
        var upper=image.TranslatePoint(new(40,25),p);Assert.Equal(new Point(80,55),upper);
        p.ApplyEdgePerch(EdgePerchPhase.None,FacingDirection.Left,TimeSpan.Zero,true);
        Assert.Equal(Visibility.Collapsed,image.Visibility);Assert.Null(image.Clip);
        Assert.Equal(Visibility.Visible,((AlphaHitTestImage)p.FindName("DororongImage")).Visibility);
    });

    [Fact]
    public void Wpf_native_and_enlarged_entry_blink_local_pull_release_sequences() => CheekProductTests.Sta(() =>
    {
        const int count=13;
        var sheet=new Canvas { Width=count*144,Height=2*176,Background=System.Windows.Media.Brushes.WhiteSmoke };
        for(var row=0;row<2;row++)
        {
            var facing=row==0?FacingDirection.Right:FacingDirection.Left;
            var p=Create(facing);var column=0;
            void Capture(string label)
            {
                Layout(p);var bitmap=new RenderTargetBitmap(144,144,96,96,PixelFormats.Pbgra32);bitmap.Render(p);bitmap.Freeze();
                var tile=new Canvas {Width=144,Height=176};Canvas.SetLeft(tile,column++*144);Canvas.SetTop(tile,row*176);sheet.Children.Add(tile);
                var text=new TextBlock {Text=$"{facing}: {label}",FontSize=11};Canvas.SetTop(text,4);tile.Children.Add(text);
                var source=new Image {Source=bitmap,Width=144,Height=144};Canvas.SetTop(source,24);tile.Children.Add(source);
                var edge=new System.Windows.Shapes.Line {X1=0,X2=144,Y1=118,Y2=118,Stroke=System.Windows.Media.Brushes.SlateGray,StrokeThickness=.5};tile.Children.Add(edge);
            }
            Capture("contact 0ms");
            foreach(var (delta,label) in new[]{(80,"80ms"),(100,"180ms"),(140,"320ms"),(1679,"open 1999ms"),(1,"closed 2000ms"),(359,"closed 2359ms"),(1,"open 2360ms")})
            {p.ApplyEdgePerch(EdgePerchPhase.Attached,facing,TimeSpan.FromMilliseconds(delta),false);Capture(label);}
            var press=Assert.IsType<DirectInteractionPressEventArgs>(p.CreateDirectPress(p.EdgePerchImage,new(24,52)));
            var controller=new DirectInteractionController();controller.BeginCheekPull(press.CheekCapture!,press.WindowLocalPosition);controller.SetPressContext(facing,true);
            var pet=new PetSnapshot(PetState.Idle,new(100,100),facing,0,false,null);
            void Present(){p.Render(pet,controller.Current,TimeSpan.FromMilliseconds(16));p.ApplyEdgePerch(EdgePerchPhase.Attached,facing,TimeSpan.FromMilliseconds(16),false);}
            var sign=facing==FacingDirection.Left?1:-1;
            controller.Advance(TimeSpan.FromMilliseconds(16),new(true,press.WindowLocalPosition+new Dororong.Core.Geometry.PointD(sign*20,0)),true,PetState.Idle,PetState.Idle);Present();Capture("pull20DIP");
            controller.Advance(TimeSpan.FromMilliseconds(80),new(true,press.WindowLocalPosition+new Dororong.Core.Geometry.PointD(sign*100,0)),true,PetState.Idle,PetState.Idle);Present();Capture("pull100DIP");
            controller.Advance(TimeSpan.Zero,default,false,PetState.Idle,PetState.Idle);Present();Capture("release0");
            controller.Advance(TimeSpan.FromMilliseconds(110),default,false,PetState.Idle,PetState.Idle);Present();Capture("release110");
            controller.Advance(TimeSpan.FromMilliseconds(110),default,false,PetState.Idle,PetState.Idle);Present();Capture("restored09");
        }
        sheet.Measure(new(sheet.Width,sheet.Height));sheet.Arrange(new(0,0,sheet.Width,sheet.Height));sheet.UpdateLayout();
        var directory=Path.Combine(EdgePerchPresentationTests.ProjectRoot(),"artifacts","repro","perch-expressions-20260909","proof");Directory.CreateDirectory(directory);
        foreach(var scale in new[]{1,4})
        {
            var bitmap=new RenderTargetBitmap((int)sheet.Width*scale,(int)sheet.Height*scale,96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(sheet);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var stream=File.Create(Path.Combine(directory,$"perch-expressions-{scale}x.png"));encoder.Save(stream);
        }
    });
    [Theory]
    [InlineData(FacingDirection.Left)]
    [InlineData(FacingDirection.Right)]
    public void Pending_press_preserves_composed_visible_orientation(FacingDirection facing) => CheekProductTests.Sta(() =>
    {
        var p = new DororongPresenter();
        var pet = new PetSnapshot(PetState.Walk, new(100,100), facing, 0, false, null);
        p.Render(pet, DirectInteractionSnapshot.None); Layout(p);
        var image = (AlphaHitTestImage)p.FindName("DororongImage");
        var before = Math.Sign(image.TranslatePoint(new(60,40), p).X - image.TranslatePoint(new(20,40), p).X);
        var direct = new DirectInteractionSnapshot(DirectInteractionTarget.Body, DirectInteractionPhase.BodyPending,
            new(140,130), new(140,130), 0, 0, false);
        p.Render(pet with { State = PetState.Idle }, direct); Layout(p);
        var after = Math.Sign(image.TranslatePoint(new(60,40), p).X - image.TranslatePoint(new(20,40), p).X);
        Assert.Equal(before, after);
    });

    [Theory]
    [InlineData(FacingDirection.Left)]
    [InlineData(FacingDirection.Right)]
    public void Entry_moves_upper_head_but_pins_source_grip_and_returns(FacingDirection facing) => CheekProductTests.Sta(() =>
    {
        var p = Create(facing);
        var image = p.EdgePerchImage;
        var grip = image.TranslatePoint(new(28,64),p);
        var head = image.TranslatePoint(new(40,25),p);
        p.ApplyEdgePerch(EdgePerchPhase.Attached,facing,TimeSpan.FromMilliseconds(80),false); Layout(p);
        Assert.Equal(grip,image.TranslatePoint(new(28,64),p));
        Assert.NotEqual(head,image.TranslatePoint(new(40,25),p));
        p.ApplyEdgePerch(EdgePerchPhase.Attached,facing,TimeSpan.FromMilliseconds(100),false); Layout(p);
        Assert.Equal(grip,image.TranslatePoint(new(28,64),p));
        p.ApplyEdgePerch(EdgePerchPhase.Attached,facing,TimeSpan.FromMilliseconds(140),false); Layout(p);
        Assert.Equal(head,image.TranslatePoint(new(40,25),p));
    });

    [Fact]
    public void Attached_clock_blinks_without_changing_paws() => CheekProductTests.Sta(() =>
    {
        var p = Create(FacingDirection.Right);
        var open = Pixels((BitmapSource)p.EdgePerchImage.Source);
        byte[]? closed = null;
        for(var i=0;i<300;i++)
        {
            p.ApplyEdgePerch(EdgePerchPhase.Attached,FacingDirection.Right,TimeSpan.FromMilliseconds(16),false);
            var frame=Pixels((BitmapSource)p.EdgePerchImage.Source);
            if(!open.SequenceEqual(frame)){closed=frame;break;}
        }
        Assert.NotNull(closed);
        Assert.Equal(open.Skip(64*100*4),closed!.Skip(64*100*4));
    });

    [Theory]
    [InlineData(FacingDirection.Left)]
    [InlineData(FacingDirection.Right)]
    public void Attached_source_cheek_is_classified_and_captured_from_image09(FacingDirection facing) => CheekProductTests.Sta(() =>
    {
        var p=Create(facing);
        Assert.Equal(DirectInteractionTarget.RightCheek,p.ClassifyOpaqueSourcePoint(new(24,52),true));
        Assert.True(p.TryCreateCheekPullCapture(new(24,52),out var capture));
        Assert.Equal(facing,capture!.Facing);
        var before=p.EdgePerchImage.TranslatePoint(new(28,64),p);
        Assert.Equal(before,capture.SourceToWindow.Transform(new Point(20,70)));
    });

    internal static DororongPresenter Create(FacingDirection facing)
    {
        var p=new DororongPresenter();
        p.Render(new(PetState.Walk,new(100,100),facing,0,false,null),DirectInteractionSnapshot.None);
        Layout(p);p.ApplyEdgePerch(EdgePerchPhase.Attached,facing,TimeSpan.Zero,false);Layout(p);return p;
    }
    internal static void Layout(FrameworkElement p){p.Measure(new(144,144));p.Arrange(new(0,0,144,144));p.UpdateLayout();}
    internal static byte[] Pixels(BitmapSource source)
    {
        var b=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);
        var bytes=new byte[b.PixelWidth*b.PixelHeight*4];b.CopyPixels(bytes,b.PixelWidth*4,0);return bytes;
    }
}
