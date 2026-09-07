using System.Windows;
using System.Windows.Controls;
using System.Reflection;
using System.Windows.Interop;
using System.Windows.Media;
using Dororong.App.Interop;
using Dororong.Core.Behavior;
using Dororong.App.Interaction;
using Dororong.App.Controls;
using Dororong.App.Runtime;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Controls;

public sealed class PetWindowViewportTests
{
    [Fact]
    public void Production_host_uses_logical_window_mapping_and_physical_pointer_sample() => CheekProductTests.Sta(() =>
    {
        var presenter=new DororongPresenter();var root=new Grid();root.Children.Add(presenter);
        var window=new Window {Width=144,Height=144,Content=root,WindowStyle=WindowStyle.None,AllowsTransparency=true,Opacity=0,ShowActivated=false,ShowInTaskbar=false};
        try
        {
            window.Show(); // Fully transparent, nonactivating test HWND; no app loop or mouse action.
            var factory=typeof(PetLoop).GetMethod("CreateProductionRuntime",BindingFlags.NonPublic|BindingFlags.Static)!;
            var runtime=factory.Invoke(null,new object[]{window,presenter,new DesktopInput()})!;
            var host=(PetLoopHost)runtime.GetType().GetProperty("Host")!.GetValue(runtime)!;
            Assert.Equal(new SizeD(144,144),host.GetPetSize());
            host.SetWindowPosition(new(300,200));
            Assert.Equal(new PointD(300,200),host.GetWindowPosition());
            Assert.Equal(204,window.Left);Assert.Equal(104,window.Top);
            var sample=host.SamplePointer();
            Assert.True(sample.IsAvailable);Assert.NotNull(sample.ScreenPixelPosition);
            var source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle)!;
            var physical=sample.ScreenPixelPosition!.Value;
            var logical=source.CompositionTarget.TransformFromDevice.Transform(new Point(physical.X,physical.Y));
            Assert.Equal(new PointD(logical.X,logical.Y),sample.Position);
            var pet=new PetSnapshot(PetState.Walk,new(300,200),FacingDirection.Right,0,false,null);
            host.Render(pet,DirectInteractionSnapshot.None,TimeSpan.Zero);HeadWideSwingTests.Layout(root);
            var image=(Image)presenter.FindName("DororongImage");
            var local=image.TranslatePoint(new Point(37.5,28.25),presenter);
            var press=pet.Position+new PointD(local.X,local.Y);
            host.Render(pet,new(DirectInteractionTarget.Body,DirectInteractionPhase.BodyDragHold,press,press,1,0,true){HeadSwingDegrees=88},TimeSpan.Zero);
            Assert.Equal(88,((RotateTransform)presenter.FindName("BodyRotateTransform")).Angle);
            Assert.True(HeadPullAnchoring.FitsViewport(image,root));
        }
        finally {window.Close();}
    });

    [Fact]
    public void Display_padding_does_not_change_logical_size_position_or_pointer_anchor() => CheekProductTests.Sta(() =>
    {
        var presenter=new DororongPresenter();var root=new Grid();root.Children.Add(presenter);
        var window=new Window {Width=144,Height=144,Content=root};
        try
        {
            var viewport=new PetWindowViewport(window,presenter);
            void Set(PointD p)=>viewport.SetPosition(p);
            PointD Get()=>viewport.GetPosition();
            var size=viewport.GetPetSize();
            Assert.Equal(new SizeD(144,144),size);
            Assert.Equal(336,window.Width);Assert.Equal(336,window.Height);
            root.Measure(new Size(window.Width,window.Height));root.Arrange(new Rect(0,0,window.Width,window.Height));root.UpdateLayout();
            foreach(var p in new[]{new PointD(0,0),new PointD(656,456),new PointD(-700,130)})
            {
                Set(p);Assert.Equal(p,Get());
                Assert.Equal(p.X-96,window.Left);Assert.Equal(p.Y-96,window.Top);
                var local=presenter.TranslatePoint(new Point(60,50),root);
                Assert.Equal(p.X+60,window.Left+local.X);Assert.Equal(p.Y+50,window.Top+local.Y);
            }
            Assert.Null(root.InputHitTest(new Point(10,10)));
        }
        finally {window.Close();}
    });
}
