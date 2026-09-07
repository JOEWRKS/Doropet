using System.Windows;
using System.IO;
using System.Text.Json;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Xunit.Abstractions;

namespace Dororong.App.Tests.Controls;

public sealed class HeadSwingSymmetryTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, 25.25, 37.5, false)]
    [InlineData(false, 37.5, 28.25, false)]
    [InlineData(false, 50.25, 48.5, false)]
    [InlineData(true, 25.25, 37.5, false)]
    [InlineData(true, 37.5, 28.25, false)]
    [InlineData(true, 50.25, 48.5, false)]
    [InlineData(false, 25.25, 37.5, true)]
    [InlineData(false, 37.5, 28.25, true)]
    [InlineData(false, 50.25, 48.5, true)]
    [InlineData(true, 25.25, 37.5, true)]
    [InlineData(true, 37.5, 28.25, true)]
    [InlineData(true, 50.25, 48.5, true)]
    public void Equal_opposite_requested_angles_have_one_safe_displayed_magnitude(bool mirror, double x, double y, bool moving) => CheekProductTests.Sta(() =>
    {
        var positive = Held(mirror, new(x,y), 35, moving);
        var negative = Held(mirror, new(x,y), -35, moving);
        output.WriteLine($"mirror={mirror} grab=({x},{y}) moving={moving}: +{positive.Angle:F6} / {negative.Angle:F6}");
        Assert.InRange(Math.Abs(positive.Angle)-Math.Abs(negative.Angle), -.02, .02);
        Assert.True(positive.Angle > 0 && negative.Angle < 0);
        var destination = Environment.GetEnvironmentVariable("DORORONG_SWING_PROOF");
        if(destination is not null && !mirror && x==37.5 && moving)
        {
            Assert.False(Directory.Exists(destination));Directory.CreateDirectory(destination);
            var left=HeadTiltSamplingTests.Snapshot(negative.Presenter);
            var right=HeadTiltSamplingTests.Snapshot(positive.Presenter);
            Save(Path.Combine(destination,"negative-native.png"),left);Save(Path.Combine(destination,"positive-native.png"),right);
            var visual=new DrawingVisual();RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.NearestNeighbor);
            using(var drawing=visual.RenderOpen())
            {
                drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(245,245,245)),null,new Rect(0,0,1152,576));
                drawing.DrawImage(left,new Rect(0,0,576,576));drawing.DrawImage(right,new Rect(576,0,576,576));
            }
            var sheet=new RenderTargetBitmap(1152,576,96,96,PixelFormats.Pbgra32);sheet.Render(visual);
            Save(Path.Combine(destination,"equal-left-right-4x.png"),sheet);
            File.WriteAllText(Path.Combine(destination,"angles.json"),JsonSerializer.Serialize(new{negative=negative.Angle,positive=positive.Angle,requestedMagnitude=35,sourceGrab=new{x,y},moving,physicalMouseTest="NOT PERFORMED"},new JsonSerializerOptions{WriteIndented=true}));
        }
    });

    [Theory]
    [InlineData(5)]
    [InlineData(12)]
    [InlineData(18)]
    [InlineData(25)]
    public void Submaximum_input_stays_symmetric_without_scaling_small_swings(double request) => CheekProductTests.Sta(() =>
    {
        var positive=Held(false,new(37.5,28.25),request,false);
        var negative=Held(false,new(37.5,28.25),-request,false);
        Assert.InRange(Math.Abs(positive.Angle)-Math.Abs(negative.Angle),-.02,.02);
        if(request<=18)Assert.Equal(request,positive.Angle,6);
    });

    private static void Save(string path,BitmapSource source)
    {
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));using var stream=File.Create(path);encoder.Save(stream);
    }

    internal static (DororongPresenter Presenter, double Angle) Held(bool mirror, PointD grab, double requested, bool moving)
    {
        var presenter = new DororongPresenter();
        var pet = new PetSnapshot(PetState.Walk,new(100,100),mirror?FacingDirection.Left:FacingDirection.Right,0,false,null);
        presenter.Render(pet,DirectInteractionSnapshot.None);CheekProductTests.Layout(presenter);
        var image = (Image)presenter.FindName("DororongImage");
        var local = image.TranslatePoint(new Point(grab.X,grab.Y),presenter);
        var press = pet.Position+new PointD(local.X,local.Y);
        var direct = new DirectInteractionSnapshot(DirectInteractionTarget.Body,DirectInteractionPhase.BodyDragHold,press,press,1,0,true){HeadSwingDegrees=requested};
        for(var i=0;i<(moving?24:1);i++)
        {
            presenter.Render(pet,direct with{PointerPosition=press+new PointD(moving?Math.Sign(requested)*i*8:0,0)},TimeSpan.FromMilliseconds(moving?16:0));
        }
        CheekProductTests.Layout(presenter);
        var angle = ((RotateTransform)presenter.FindName("BodyRotateTransform")).Angle;
        Assert.True(HeadPullAnchoring.FitsViewport(image,presenter),"The complete warped sprite must stay within the viewport");
        var actual = image.TranslatePoint(new Point(grab.X+11,grab.Y-16),presenter);
        Assert.Equal(local.X,actual.X,6);Assert.Equal(local.Y,actual.Y,6);
        Assert.Equal(BitmapScalingMode.Linear,RenderOptions.GetBitmapScalingMode(image));
        return(presenter,angle);
    }
}
