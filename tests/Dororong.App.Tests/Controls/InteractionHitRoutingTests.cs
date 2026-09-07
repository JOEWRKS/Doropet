using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Controls;

public sealed class InteractionHitRoutingTests
{
    // Literal points read on the frozen96px grid and user's red selected-side region.
    [Theory]
    [InlineData(16,58,DirectInteractionTarget.RightCheek)]
    [InlineData(24,52,DirectInteractionTarget.RightCheek)]
    [InlineData(28,59,DirectInteractionTarget.RightCheek)]
    [InlineData(12,51,DirectInteractionTarget.RightCheek)]
    [InlineData(10,57,DirectInteractionTarget.RightCheek)]
    [InlineData(31,61,DirectInteractionTarget.RightCheek)]
    [InlineData(23,64,DirectInteractionTarget.RightCheek)]
    [InlineData(22,46,DirectInteractionTarget.RightCheek)]
    [InlineData(23,67,DirectInteractionTarget.FiveRegionBody)]
    [InlineData(35,67,DirectInteractionTarget.FiveRegionBody)]
    [InlineData(23,77,DirectInteractionTarget.FiveRegionBody)]
    [InlineData(42,82,DirectInteractionTarget.FiveRegionBody)]
    [InlineData(53,70,DirectInteractionTarget.FiveRegionBody)]
    [InlineData(70,61,DirectInteractionTarget.FiveRegionBody)]
    [InlineData(66,80,DirectInteractionTarget.FiveRegionBody)]
    [InlineData(37,28,DirectInteractionTarget.Body)]
    [InlineData(33,39,DirectInteractionTarget.Body)]
    [InlineData(50,40,DirectInteractionTarget.Body)]
    [InlineData(39,64,DirectInteractionTarget.None)]
    [InlineData(39,56,DirectInteractionTarget.None)]
    [InlineData(44,69,DirectInteractionTarget.None)]
    [InlineData(30,75,DirectInteractionTarget.None)]
    [InlineData(56,56,DirectInteractionTarget.LeftCheek)]
    public void Awake_visible_pixel_has_its_intended_owner_not_a_default_head(double x,double y,object expected) => CheekProductTests.Sta(() =>
    {
        foreach(var mirror in new[]{false,true})
        {
            var p=Setup(PetState.Walk,mirror,.1);var pixel=new PointD(x,y);
            Assert.True(IsOpaque(p,pixel),$"Fixture ({x},{y}) must be a visible pixel");
            var target=p.ClassifyOpaqueSourcePoint(pixel,true);
            Assert.Equal((DirectInteractionTarget)expected,target);
            Assert.Equal(DirectInteractionTarget.None,p.ClassifyOpaqueSourcePoint(pixel,false));
            Assert.Equal(target==DirectInteractionTarget.RightCheek,p.TryCreateCheekPullCapture(pixel,out _));
            Assert.Equal(target==DirectInteractionTarget.FiveRegionBody,p.TryCreateBodyPullCapture(pixel,out _));
        }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Upper_body_hit_extension_selects_existing_paw_without_changing_renderer_masks(bool mirror) => CheekProductTests.Sta(() =>
    {
        var p=Setup(PetState.Walk,mirror,.1);
        Assert.True(p.TryCreateBodyPullCapture(new(23.25,67.5),out var front));
        Assert.True(p.TryCreateBodyPullCapture(new(35.25,67.5),out var middle));
        Assert.Equal(BodyRegion.FrontPaw,front!.Region);Assert.Equal(BodyRegion.MiddlePaw,middle!.Region);
        Assert.Equal(new PointD(23.25,67.5),front.Anchor);
        Assert.True(BodyRegionMap.IsProtected(23,67)); // Render lock remains, only input changes.
        Assert.False(p.TryCreateBodyPullCapture(new(30,75),out _)); // Excluded fourth-paw connector remains inactive.
    });

    [Theory]
    [InlineData(PetState.Walk,.1)]
    [InlineData(PetState.Idle,.1)]
    [InlineData(PetState.Idle,.67)]
    [InlineData(PetState.Idle,.71)]
    public void Expanded_cheek_capture_keeps_the_same_source_motion_and_visible_transform(PetState state,double phase) => CheekProductTests.Sta(() =>
    {
        foreach(var mirror in new[]{false,true})
        {
            var p=Setup(state,mirror,phase);
            Assert.True(p.TryCreateCheekPullCapture(new(16,58),out var old));
            Assert.Equal(mirror?FacingDirection.Left:FacingDirection.Right,old!.Facing);
            Assert.Equal(mirror?-1:1,Math.Sign(old.SourceToWindow.M11));
            foreach(var point in new[]{new PointD(24,52),new PointD(28,59),new PointD(10,57),new PointD(23,64)})
            {
                Assert.Equal(DirectInteractionTarget.RightCheek,p.ClassifyOpaqueSourcePoint(point,true));
                Assert.True(p.TryCreateCheekPullCapture(point,out var expanded));
                Assert.Equal(old!.SourceToWindow,expanded!.SourceToWindow);
                Assert.Equal(old.OutwardUnit,expanded.OutwardUnit);
                foreach(var pull in new[]{-10d,0,10,20})Assert.Equal(old.Render(pull,7,4),expanded.Render(pull,7,4));
            }
        }
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Padded_scaled_rotated_view_maps_eye_back_to_the_same_cheek(bool mirror) => CheekProductTests.Sta(() =>
    {
        var p=Setup(PetState.Walk,mirror,.1);var host=new Grid();host.Children.Add(p);
        host.Measure(new Size(336,336));host.Arrange(new Rect(0,0,336,336));host.UpdateLayout();
        var group=new TransformGroup();group.Children.Add(new ScaleTransform(1.5,1.5));group.Children.Add(new RotateTransform(18));
        p.RenderTransform=group;
        var image=(AlphaHitTestImage)p.FindName("DororongImage");
        var screen=image.TranslatePoint(new Point(24.25,52.5),host);
        var toSource=image.TransformToAncestor(host).Inverse!.Transform(screen);
        Assert.True(image.TryGetOpaqueSourcePoint(toSource,out var point));
        Assert.Equal(new PointD(24,52),point);
        Assert.Equal(DirectInteractionTarget.RightCheek,p.ClassifyOpaqueSourcePoint(point,true));
        Assert.True(p.TryCreateCheekPullCapture(point,out var capture));
        Assert.Equal(mirror?-1:1,Math.Sign(capture!.OutwardUnit.X)*-1);
    });

    internal static DororongPresenter Setup(PetState state,bool mirror,double phase)
    {
        var p=new DororongPresenter();
        if(mirror&&state==PetState.Idle)
        {
            // Idle retains the completed interaction's facing, not snapshot.Facing.
            p.Render(new(PetState.Walk,new(100,100),FacingDirection.Left,.1,false,null),DirectInteractionSnapshot.None);
            CheekProductTests.Layout(p);
            Assert.True(p.TryCreateCheekPullCapture(new(16,58),out var capture));
            ApprovedCheekConnectionTests.Draw(p,capture!,0,false,0);
        }
        p.Render(new(state,new(100,100),mirror?FacingDirection.Left:FacingDirection.Right,phase,false,null),DirectInteractionSnapshot.None);
        CheekProductTests.Layout(p);return p;
    }
    internal static byte[] Pixels(DororongPresenter p)
    {
        var image=(Image)p.FindName("DororongImage");var bitmap=new FormatConvertedBitmap((BitmapSource)image.Source,PixelFormats.Bgra32,null,0);
        var pixels=new byte[96*96*4];bitmap.CopyPixels(pixels,384,0);return pixels;
    }
    private static bool IsOpaque(DororongPresenter p,PointD point)=>Pixels(p)[((int)point.Y*96+(int)point.X)*4+3]!=0;
}
