using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Controls;

public sealed class CheekProductTests
{
    [Theory]
    [InlineData("rest",0,0,false)]
    [InlineData("small",4,0,false)]
    [InlineData("half",10,0,false)]
    [InlineData("max",20,0,false)]
    [InlineData("inward",-10,0,false)]
    [InlineData("release055",20,55,false)]
    [InlineData("release110",20,110,false)]
    [InlineData("release165",20,165,false)]
    [InlineData("release220",20,220,false)]
    [InlineData("cancel",20,0,true)]
    public void Product_raster_matches_approved_review_pixels(string name,double pull,double release,bool cancel) => Sta(() =>
    {
        var source=Read("canonical.png"); var saved=(byte[])source.Clone();
        Assert.Equal(Read(name+"-native.png"),Raster(source,pull,release,cancel));
        Assert.Equal(saved,source);
    });

    [Theory]
    [InlineData(false,16,58,(int)DirectInteractionTarget.RightCheek)]
    [InlineData(true,16,58,(int)DirectInteractionTarget.RightCheek)]
    [InlineData(false,24,52,(int)DirectInteractionTarget.RightCheek)]
    [InlineData(false,28,59,(int)DirectInteractionTarget.RightCheek)]
    [InlineData(false,39,56,(int)DirectInteractionTarget.None)]
    [InlineData(false,56,56,(int)DirectInteractionTarget.LeftCheek)]
    public void Selected_cheek_hit_includes_user_marked_eye_mouth_and_rejects_unassigned_face(bool mirror,double x,double y,int expected) => Sta(() =>
    {
        var presenter=new DororongPresenter();
        presenter.Render(new(PetState.Walk,new(100,100),mirror?FacingDirection.Left:FacingDirection.Right,.1,false,null),DirectInteractionSnapshot.None);
        Layout(presenter);
        Assert.Equal((DirectInteractionTarget)expected,presenter.ClassifyOpaqueSourcePoint(new(x,y),true));
        Assert.Equal(DirectInteractionTarget.None,presenter.ClassifyOpaqueSourcePoint(new(x,y),false));
    });

    internal static byte[] Raster(byte[] source,double pull,double release=0,bool cancel=false)
    {
        var type=typeof(DororongPresenter).Assembly.GetType("Dororong.App.Controls.CheekPullRenderer");
        Assert.NotNull(type); // Missing runtime behavior is RED, not a compile error.
        var method=type.GetMethod("Render",BindingFlags.Static|BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<byte[]>(method.Invoke(null,[source,pull,0d,release,cancel]));
    }
    internal static byte[] Read(string name)
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"DororongDesktopPet.sln")))directory=directory.Parent;
        Assert.NotNull(directory);
        var path=Path.Combine(directory.FullName,"tests","Dororong.App.Tests","Fixtures","CheekOutline",name);
        var decoder=BitmapDecoder.Create(new Uri(path),BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
        var bitmap=new FormatConvertedBitmap(decoder.Frames[0],PixelFormats.Bgra32,null,0);
        var pixels=new byte[96*96*4];bitmap.CopyPixels(pixels,384,0);return pixels;
    }
    internal static void Layout(FrameworkElement view){view.Measure(new Size(144,144));view.Arrange(new Rect(0,0,144,144));view.UpdateLayout();}
    internal static void Sta(Action action){Exception? error=null;var thread=new Thread(()=>{try{action();}catch(Exception e){error=e;}});thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();if(error is not null)ExceptionDispatchInfo.Capture(error).Throw();}
}
