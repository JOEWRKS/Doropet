using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;

namespace Dororong.App.Tests.Controls;

public class ContactAffineTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Empty_transform_group_keeps_Wpf_failure_semantics(bool nested) => CheekProductTests.Sta(()=>
    {
        var image=BitmapSource.Create(1,1,96,96,PixelFormats.Pbgra32,null,new byte[]{255,255,255,255},4);image.Freeze();
        var group=new GeneralTransformGroup();
        if(nested) group.Children.Add(new GeneralTransformGroup());
        Assert.Throws<InvalidOperationException>(()=>new PlatformContactPresentation().Measure(image,group));
    });
    [Theory]
    [InlineData(2,20,24)]
    [InlineData(-2,16,20)]
    public void Nested_affine_contact_preserves_composition_and_refreshes_mutable_transform(double sx,double top,double sole)
        => CheekProductTests.Sta(()=>
    {
        var pixels=Enumerable.Repeat((byte)255,16).ToArray();
        var image=BitmapSource.Create(2,2,96,96,PixelFormats.Pbgra32,null,pixels,8);image.Freeze();
        var scale=new ScaleTransform(sx,2);
        var inner=new GeneralTransformGroup();inner.Children.Add(scale);inner.Children.Add(new RotateTransform(90));
        var group=new GeneralTransformGroup();group.Children.Add(inner);group.Children.Add(new TranslateTransform(10,20));
        var p=new PlatformContactPresentation();var c=p.Measure(image,group);
        Assert.Equal(6,c.Left,8);Assert.Equal(10,c.Right,8);Assert.Equal(top,c.VisibleTop,8);Assert.Equal(sole,c.SoleY,8);
        scale.ScaleY=3;c=p.Measure(image,group);
        Assert.Equal(4,c.Left,8);Assert.Equal(10,c.Right,8);Assert.Equal(top,c.VisibleTop,8);Assert.Equal(sole,c.SoleY,8);
    });
}
