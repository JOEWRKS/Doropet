using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Runtime;

public partial class PetLoopPlatformTests
{
    [Fact]
    public void Ceiling_limited_click_retains_support_but_still_falls_when_window_closes() => Controls.CheekProductTests.Sta(() =>
    {
        using var h=new Harness(realPresenter:true);h.Start();
        var geometry=h.Presenter!.MeasurePlatformGeometry()!.Value;
        var top=geometry.Bounds.Height+5;
        h.Native.Scene=Scene(y:top);h.Position=new(100,top-geometry.Contact.SoleY);h.Tick(80);
        h.PressReal(0,new(40,30));h.Tick();h.Down=false;
        for(var i=0;i<40;i++)
        {
            h.Tick();
            Assert.Equal(PlatformPhase.Supported,h.LastPose?.Phase);
            Assert.True(h.Position.Y+h.Presenter.MeasurePlatformGeometry()!.Value.Bounds.Y>=-.001);
            if(i==18)h.Native.Scene=Scene(y:top+20);
        }
        h.Native.Scene=Scene(windows:false);h.Tick(80);
        Assert.Equal(PlatformPhase.Falling,h.LastPose?.Phase);
    });

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void Ordinary_click_keeps_authored_twelve_pixel_hop_without_losing_surface(int surface) => Controls.CheekProductTests.Sta(() =>
    {
        using var h=new Harness(realPresenter:true);h.Start();
        var sole=h.Presenter!.MeasurePlatformContact()!.Value.SoleY;
        var top=surface==2?600:100+sole;
        h.Native.Scene=surface==2?Scene(windows:false):Scene(y:top);
        if(surface==1) h.Native.Scene=h.Native.Scene with { Windows=h.Native.Scene.Windows.Select(w=>w with {Taskbar=true,HorizontalTaskbar=true}).ToArray() };
        h.Position=new(100,top-sole);h.Tick(80);
        h.PressReal(0,new(40,30));h.Tick();h.Down=false;
        var peak=0d;var clicks=0;
        for(var i=0;i<45;i++)
        {
            h.Tick();
            if(h.Core.State!=PetState.ClickReaction) continue;
            clicks++;
            var p=h.Presenter;
            var image=(Image)p.FindName("DororongImage");
            var measure=new PlatformContactPresentation();
            var contact=measure.Measure((BitmapSource)image.Source,image.TransformToAncestor(p));
            var visualSole=h.Position.Y+contact.SoleY;
            peak=Math.Max(peak,top-visualSole);
            Assert.True(visualSole<=top+.1,$"Click sank through surface: {visualSole}");
        }
        Assert.True(clicks>10);
        Assert.InRange(peak,11.9,12.1);
        Assert.Equal(PlatformPhase.Supported,h.LastPose?.Phase);
        Assert.Equal(top,h.Position.Y+h.Presenter.MeasurePlatformContact()!.Value.SoleY,4);
    });
}
