using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.App.Runtime;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;
using Dororong.Core.Platforms;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace Dororong.App.Tests.Runtime;

public sealed class PounceLoopTests
{
    [Theory]
    [InlineData(-1)] [InlineData(1)]
    public void Outer_tracking_turns_body_without_walking_or_crouching_on_support(int direction) => Controls.CheekProductTests.Sta(() =>
    {
        using var h=new Harness(BehaviorTuning.Default with
        {
            IdleMin=TimeSpan.FromSeconds(2), IdleMax=TimeSpan.FromSeconds(2), IdleToWalkProbability=1
        });
        h.Start(); var origin=h.Position;
        foreach(var side in new[]{direction,-direction})
        {
            h.Pointer=new(true,origin+new PointD(72+side*250,81));
            h.Ticks(35);
            Assert.Equal(side>0?FacingDirection.Right:FacingDirection.Left,h.Snapshot.Facing);
            Assert.Equal(156,h.Presenter.HuntingFrame);
            Assert.Equal(PouncePhase.Watch,h.Presenter.Pounce.Phase);
            Assert.Equal(origin,h.Position);
            Assert.Equal(560,h.WorldSole,4);
        }
        h.Pointer=PointerSample.Unavailable;
        h.Ticks(35);
        Assert.NotEqual(origin.X,h.Position.X); // Same brain really resumes walking outside tracking.
    });

    [Fact]
    public void Quick_body_click_between_ticks_also_releases_airborne_support_without_snap() => Controls.CheekProductTests.Sta(() =>
    {
        using var h=new Harness(); h.Start(); h.Pointer=new(true,h.Position+new PointD(126,81));
        h.Ticks(15); h.Tick(.17); var airborne=h.Position; var sole=h.WorldSole;
        h.Pointer=new(true,airborne+new PointD(70,90));
        h.Loop.NotifyDirectInteractionPressed(new(DirectInteractionTarget.ClickOnly,new(70,90),new(46,66),0));
        h.Tick(.016); // Button was released before this tick observed it down.
        Assert.Equal(PetState.ClickReaction,h.Snapshot.State);
        Assert.InRange(h.WorldSole,sole,sole+.5);
        Assert.Equal(PlatformPhase.Falling,h.PlatformPose.Phase);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void Midair_sit_or_body_click_falls_from_visible_height_instead_of_snapping_to_support(bool bodyClick) => Controls.CheekProductTests.Sta(() =>
    {
        using var h=new Harness(); h.Start(); h.Pointer=new(true,h.Position+new PointD(126,81));
        h.Ticks(15); h.Tick(.17); var airborne=h.Position; var sole=h.WorldSole;
        Assert.Equal(548,sole,4);
        if(bodyClick)
        {
            h.Down=true; h.Pointer=new(true,airborne+new PointD(70,90));
            h.Loop.NotifyDirectInteractionPressed(new(DirectInteractionTarget.ClickOnly,new(70,90),new(46,66),0));
        }
        else h.Loop.NotifySitRequested();
        h.Tick(.016);
        Assert.Equal(airborne.X,h.Position.X,6);
        Assert.InRange(h.WorldSole,sole,sole+.5);
        Assert.Equal(PlatformPhase.Falling,h.PlatformPose.Phase);
        h.Pointer=PointerSample.Unavailable; h.Down=false; h.Ticks(30);
        Assert.Equal(560,h.WorldSole,4);
        Assert.Equal(PouncePhase.Watch,h.Presenter.Pounce.Phase);
    });

    [Theory]
    [InlineData(-1)] [InlineData(1)]
    public void Supported_product_pounces_then_tracks_upright_for_three_seconds(int direction) => Controls.CheekProductTests.Sta(() =>
    {
        using var h = new Harness(); h.Start(); var origin = h.Position;
        h.Pointer = new(true, origin + new PointD(72 + direction * 54, 81));
        h.Ticks(14); Assert.Equal(origin, h.Position);
        h.Tick(); Assert.Equal(PouncePhase.Flight, h.Presenter.Pounce.Phase);
        h.Tick(.17);
        Assert.Equal(origin.X + direction * 14, h.Position.X, 4);
        Assert.Equal(548, h.WorldSole, 4);
        Assert.Equal(PlatformPhase.Supported, h.PlatformPose.Phase);
        h.Pointer = new(true, h.Position + new PointD(72 - direction * 54, 81));
        h.Tick(.17);
        Assert.Equal(PouncePhase.Landing, h.Presenter.Pounce.Phase);
        Assert.Equal(direction > 0 ? FacingDirection.Right : FacingDirection.Left, h.Snapshot.Facing);
        h.Tick(.28);
        Assert.Equal(PouncePhase.Track, h.Presenter.Pounce.Phase);
        Assert.Equal(560, h.WorldSole, 4);
        var landed = h.Position;
        h.Pointer = new(true, landed + new PointD(72 - direction * 250, 30));
        h.Tick();
        Assert.Equal(direction > 0 ? FacingDirection.Left : FacingDirection.Right, h.Snapshot.Facing);
        Assert.Equal(156, h.Presenter.HuntingFrame);
        h.Ticks(28);
        Assert.Equal(PouncePhase.Track, h.Presenter.Pounce.Phase);
        Assert.Equal(landed, h.Position);
        h.Pointer = new(true, landed + new PointD(72 + direction * 54, 81));
        h.Tick(); Assert.Equal(PouncePhase.Watch, h.Presenter.Pounce.Phase);
        h.Ticks(14); Assert.Equal(PouncePhase.Watch, h.Presenter.Pounce.Phase);
        h.Tick(); Assert.Equal(PouncePhase.Flight, h.Presenter.Pounce.Phase);
        Assert.Equal(1, h.WritesThisTick);
    });

    [Fact]
    public void Head_press_interrupts_midair_at_displayed_position_and_still_carries() => Controls.CheekProductTests.Sta(() =>
    {
        using var h = new Harness(); h.Start(); h.Pointer = new(true, h.Position + new PointD(126,81));
        h.Ticks(16); var airborne = h.Position;
        Assert.Equal(PouncePhase.Flight, h.Presenter.Pounce.Phase);
        h.Down = true;
        var image = (AlphaHitTestImage)h.Presenter.FindName("DororongImage");
        var press = Assert.IsType<DirectInteractionPressEventArgs>(h.Presenter.CreateDirectPress(image, new(40,40)));
        Assert.Equal(DirectInteractionTarget.Body, press.Target);
        h.Pointer = new(true, airborne + press.WindowLocalPosition);
        h.Loop.NotifyDirectInteractionPressed(press);
        h.Tick(); Assert.Equal(airborne, h.Position);
        Assert.Equal(PouncePhase.Watch, h.Presenter.Pounce.Phase);
        h.Pointer = new(true, h.Pointer.Position + new PointD(30,-20)); h.Tick();
        Assert.Equal(PetState.Dragged, h.Snapshot.State);
        Assert.Equal(airborne + new PointD(30,-20), h.Position);
    });

    [Fact]
    public void Support_loss_during_flight_yields_to_falling_and_does_not_return_to_takeoff() => Controls.CheekProductTests.Sta(() =>
    {
        using var h = new Harness(); h.Start(); var origin = h.Position;
        h.Pointer = new(true, h.Position + new PointD(126,81)); h.Ticks(16);
        Assert.True(h.Position.X > origin.X);
        var airborne = h.Position;
        h.Native.ShowBar = false; h.Tick(); h.Tick();
        Assert.Equal(PouncePhase.Watch, h.Presenter.Pounce.Phase);
        Assert.True(h.Position.X >= airborne.X);
        h.Pointer = PointerSample.Unavailable; h.Ticks(30);
        Assert.Equal(600, h.WorldSole, 4);
        Assert.Equal(PlatformPhase.Supported, h.PlatformPose.Phase);
    });

    [Fact]
    public void Native_rendered_flight_landing_and_track_remain_visible_and_save_contact_sheets() => Controls.CheekProductTests.Sta(() =>
    {
        var folder = Path.Combine(Controls.EdgePerchPresentationTests.ProjectRoot(), "artifacts/repro/pounce-product-20260911");
        Directory.CreateDirectory(folder);
        foreach (var dark in new[]{false,true})
        {
            var sheet = new Canvas { Width=1200, Height=360, Background=dark?Brushes.Black:Brushes.White };
            foreach (var direction in new[]{-1,1})
            {
                using var h = new Harness(); h.Start(); var origin=h.Position;
                h.Pointer=new(true,origin+new PointD(72+direction*54,81));
                var now=0d; var column=0;
                foreach (var (time,label) in new[]{(0d,"idle"),(1.4,"ready"),(1.67,"flight"),(1.94,"landing"),(2.12,"track")})
                {
                    h.Tick(time-now); now=time;
                    var geometry=h.Presenter.MeasurePlatformGeometry()!.Value.Bounds;
                    Assert.True(geometry.Width>35 && geometry.Height>30);
                    Assert.InRange(geometry.X,0,144); Assert.InRange(geometry.Right,0,144);
                    var bitmap=new RenderTargetBitmap(144,144,96,96,PixelFormats.Pbgra32); bitmap.Render(h.Presenter); bitmap.Freeze();
                    var tile=new Canvas {Width=240,Height=180};
                    var image=new Image {Source=bitmap,Width=144,Height=144};
                    Canvas.SetLeft(image,48+h.Position.X-origin.X); Canvas.SetTop(image,20+h.Position.Y-origin.Y); tile.Children.Add(image);
                    var line=new System.Windows.Shapes.Line {X1=0,X2=240,Y1=20+560-origin.Y,Y2=20+560-origin.Y,Stroke=Brushes.Gray,StrokeThickness=.5};
                    tile.Children.Add(line);
                    tile.Children.Add(new TextBlock {Text=$"{label} {time:F2}s",Foreground=dark?Brushes.White:Brushes.Black});
                    Canvas.SetLeft(tile,column++*240); Canvas.SetTop(tile,direction<0?0:180); sheet.Children.Add(tile);
                }
            }
            sheet.Measure(new(1200,360));sheet.Arrange(new Rect(0,0,1200,360));sheet.UpdateLayout();
            var output=new RenderTargetBitmap(1200,360,96,96,PixelFormats.Pbgra32);output.Render(sheet);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(output));
            using var file=File.Create(Path.Combine(folder,dark?"native-black.png":"native-white.png"));encoder.Save(file);
        }
    });

    [Fact]
    public void Sit_cancels_preparation_and_cannot_autonomously_launch() => Controls.CheekProductTests.Sta(() =>
    {
        using var h = new Harness(); h.Start(); h.Pointer = new(true, h.Position + new PointD(126,81));
        h.Ticks(14); h.Loop.NotifySitRequested(); h.Tick(); var held = h.Position;
        h.Ticks(55);
        Assert.Equal(PouncePhase.Watch, h.Presenter.Pounce.Phase);
        Assert.Equal(held, h.Position);
        var image = (Image)h.Presenter.FindName("DororongImage");
        Assert.True(ReferenceEquals(image.Source, LocomotionFrames.Sit(1)) || ReferenceEquals(image.Source, LocomotionFrames.Sit(1,true)));
    });

    internal sealed class Harness : IDisposable
    {
        internal readonly DororongPresenter Presenter = new();
        internal readonly PetLoop Loop;
        internal PointD Position = new(300,100);
        internal PetSnapshot Snapshot;
        internal DirectInteractionSnapshot Direct;
        internal PlatformPose PlatformPose;
        internal PointerSample Pointer = PointerSample.Unavailable;
        internal bool Down;
        internal int WritesThisTick;
        internal int MaxWritesPerTick;
        internal int Captures, Releases;
        internal readonly Scene Native = new();
        private TimeSpan _elapsed;
        private EventHandler? _tick;
        internal double WorldSole => Position.Y + Presenter.MeasurePlatformContact()!.Value.SoleY;
        internal Harness(BehaviorTuning? tuning = null)
        {
            void Render(PetSnapshot s, DirectInteractionSnapshot d, TimeSpan dt)
            {
                Snapshot=s; Direct=d; Presenter.RenderDesktop(s,d,dt);
                Presenter.Measure(new Size(144,144)); Presenter.Arrange(new Rect(0,0,144,144)); Presenter.UpdateLayout();
            }
            var host = new PetLoopHost(()=>new(0,0,800,600),()=>new(144,144),()=>new(4,4),()=>Pointer,()=>Down,
                ()=>Position,p=>{ Position=p; WritesThisTick++; },(s,d)=>Render(s,d,TimeSpan.Zero),()=>{Captures++;return true;},()=>Releases++,Render)
            {
                UpdateHunting=Presenter.UpdateHuntingWithPounce, GetPouncePose=()=>Presenter.Pounce,
                HoldLocomotionWalk=Presenter.HoldLocomotionWalk, SetLocomotionBlocked=Presenter.SetLocomotionBlocked,
                SetSittingRequested=Presenter.SetSittingRequested,
                Platforms=new(new(Native),_=>new(new(),new(),1,1),Presenter.MeasurePlatformGeometry,
                    (pose,sole)=>{ PlatformPose=pose??default; Presenter.ApplyPlatformPose(pose,sole); })
            };
            Loop=new(new(()=>_elapsed,()=>{},()=>{}),new(h=>_tick+=h,h=>_tick-=h,()=>{},()=>{}),host,
                _=>new(tuning ?? BehaviorTuning.Default with { IdleMin=TimeSpan.FromMinutes(10),IdleMax=TimeSpan.FromMinutes(10) },new SeededRandomSource(1),Position));
            Loop.Faulted+=(_,e)=>throw new Exception("Pounce loop fault",e);
        }
        internal void Start() { Loop.Start(); Ticks(40); Assert.Equal(PlatformPhase.Supported,PlatformPose.Phase); }
        internal void Tick(double seconds=.1)
        {
            while (seconds > 1e-9)
            {
                var step = Math.Min(.1,seconds); seconds -= step;
                WritesThisTick=0; _elapsed+=TimeSpan.FromSeconds(step); _tick?.Invoke(this,EventArgs.Empty);
                MaxWritesPerTick=Math.Max(MaxWritesPerTick,WritesThisTick);
            }
        }
        internal void Ticks(int count) { for(var i=0;i<count;i++)Tick(); }
        public void Dispose()=>Loop.Dispose();
        internal sealed class Scene : IDesktopSceneNative
        {
            internal bool ShowBar = true;
            public DesktopScene? Capture(TimeSpan now)=>new(1,now,[new(1,new(0,0,800,600))],
                ShowBar?[new(new(1,1,1),new(0,560,800,40),0,true,false,false,false,true,true,true,true)]:[]);
        }
    }
}
