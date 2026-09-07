using System.IO;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.App.Runtime;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Tests.Controls;

public sealed class SurroundingPullRuntimeTests
{
    [Fact]
    public void Production_loop_forwards_raw_clock_gaps_to_timed_render_without_changing_snapshots()
    {
        var elapsed=TimeSpan.Zero;EventHandler? tick=null;var observed=new List<TimeSpan>();var position=default(PointD);
        var host=new PetLoopHost(()=>new(0,0,1000,800),()=>new(144,144),()=>new(4,4),()=>PointerSample.Unavailable,
            ()=>false,()=>position,q=>position=q,(_,_)=>{},()=>true,()=>{},(_,_,dt)=>observed.Add(dt));
        using var loop=new PetLoop(new(()=>elapsed,()=>{},()=>{}),new(h=>tick+=h,h=>tick-=h,()=>{},()=>{}),host,
            q=>new PetBrain(BehaviorTuning.Default,new SeededRandomSource(17),q));
        loop.Start();foreach(var milliseconds in new[]{8,24,1000}){elapsed+=TimeSpan.FromMilliseconds(milliseconds);tick!(null,EventArgs.Empty);}
        Assert.Equal(new[]{8d,24d,1000d},observed.Select(t=>t.TotalMilliseconds));
    }

    [Fact]
    public void Head_resume_gap_does_not_invent_a_velocity_impulse()=>Sta(()=>
    {
        var source=Rest();var key=new SuppliedBodyDragFrames(PremultipliedFrame.From(source)).Sample(1);
        var anchor=new CapturedHeadAnchor(new(2.5,-13.75),default,FacingDirection.Right);
        var adapter=new HeadSurroundingPresentation();
        var direct=new DirectInteractionSnapshot(DirectInteractionTarget.Body,DirectInteractionPhase.BodyDragHold,default,new(80,0),1,0,true);
        adapter.Advance(direct,16);for(var i=0;i<250;i++)adapter.Advance(direct,16);
        adapter.Advance(direct with{PointerPosition=new(900,0)},1000);
        Assert.Equal(PremultipliedFrame.From(key).Pixels,PremultipliedFrame.From(adapter.Render(key,anchor,0)).Pixels);
    });

    [Fact]
    public void Secondary_body_reach_keeps_actual_presenter_ink_inside_the_window()=>Sta(()=>
    {
        foreach(var state in new[]{PetState.Idle,PetState.Walk,PetState.Startled})foreach(var mirror in new[]{false,true})
        {
            var p=new DororongPresenter();p.Render(new(state,default,mirror?FacingDirection.Left:FacingDirection.Right,.31,false,null),DirectInteractionSnapshot.None);Layout(p);
            foreach(var anchor in new PointD[]{new(23,77),new(42,82),new(66,80),new(53,70),new(70,61)})
            {
                Assert.True(p.TryCreateBodyPullCapture(anchor,out var capture));
                var(root,tip)=BodyRegionMap.Limb(capture!.Region,anchor);
                for(var d=0;d<8;d++)
                {
                    var pull=new PointD(32*Math.Cos(d*Math.PI/4),32*Math.Sin(d*Math.PI/4))-(tip-root);
                    var output=SurroundingPullRenderer.Body(capture.Pixels.ToArray(),capture.Region,pull,anchor,pull,pull);
                    for(var y=0;y<160;y++)for(var x=0;x<160;x++)if(output[(y*160+x)*4+3]>0)
                    {
                        var q=capture.SourceToWindow.Transform(new Point(x-32+.5,y-32+.5));
                        Assert.True(q.X>=0&&q.X<144&&q.Y>=0&&q.Y<144,$"{state}/{mirror}/{capture.Region}/{d}: {q}");
                    }
                }
            }
        }
    });

    // Optional exported evidence is generated from real WPF Presenter output, not
    // a second animation renderer. Tests still exercise these poses without export.
    [Fact]
    public void Actual_presenter_proof_has_six_regions_and_exact_restoration()=>Sta(()=>
    {
        var destination=Environment.GetEnvironmentVariable("DORORONG_SURROUNDING_PROOF");
        if(destination is not null){Assert.False(Directory.Exists(destination));Directory.CreateDirectory(destination);}
        var proof=new List<BitmapSource>();
        foreach(var anchor in new PointD[]{new(37.5,28.25),new(23,79),new(43,85),new(66,82),new(53,69),new(69,61)})
        {
            var p=new DororongPresenter();var pet=new PetSnapshot(PetState.Idle,new(100,100),FacingDirection.Right,0,false,null);
            p.Render(pet,DirectInteractionSnapshot.None);Layout(p);var original=PremultipliedFrame.From((BitmapSource)((Image)p.FindName("DororongImage")).Source).Pixels;
            Save();
            if(anchor.Y<40)
            {
                var local=((Image)p.FindName("DororongImage")).TranslatePoint(new Point(anchor.X,anchor.Y),p);
                var press=pet.Position+new PointD(local.X,local.Y);
                var d=new DirectInteractionSnapshot(DirectInteractionTarget.Body,DirectInteractionPhase.BodyDragEntry,press,press+new PointD(0,-42),.5,0,true);
                for(var i=0;i<24;i++)p.Render(pet,d);Layout(p);Save();
                d=d with{Phase=DirectInteractionPhase.BodyDragHold,Strength=1};
                for(var i=0;i<24;i++)p.Render(pet,d with{PointerPosition=press+new PointD(80+i*4,-80),HeadSwingDegrees=16});Layout(p);Save();
                for(var i=0;i<24;i++)p.Render(pet,d with{PointerPosition=press+new PointD(176-i*4,-80),HeadSwingDegrees=-16});Layout(p);Save();
            }
            else
            {
                Assert.True(p.TryCreateBodyPullCapture(anchor,out var capture));
                var b=new BodyPullSnapshot(capture!.Region,BodyPullPhase.Pulling,pet.Position,new(-10,6),anchor,capture);
                var d=new DirectInteractionSnapshot(DirectInteractionTarget.FiveRegionBody,DirectInteractionPhase.BodyLocalPull,default,default,0,0,true,b);
                for(var i=0;i<24;i++)p.Render(pet,d);Layout(p);Save();
                for(var i=0;i<24;i++)p.Render(pet,d with{BodyPull=b with{PullSource=new(10,-8)}});Layout(p);Save();
                p.Render(pet,d with{Phase=DirectInteractionPhase.BodyLocalSettle,BodyPull=b with{Phase=BodyPullPhase.Settling,PullSource=new(5,-4)}},TimeSpan.FromMilliseconds(100));Layout(p);Save();
            }
            p.Render(pet,DirectInteractionSnapshot.None);Layout(p);Save();
            Assert.Equal(original,PremultipliedFrame.From((BitmapSource)((Image)p.FindName("DororongImage")).Source).Pixels);
            void Save(){var bitmap=new RenderTargetBitmap(144,144,96,96,PixelFormats.Pbgra32);bitmap.Render(p);bitmap.Freeze();proof.Add(bitmap);}
        }
        Assert.Equal(30,proof.Count);
        if(destination is not null)
        {
            for(var i=0;i<proof.Count;i++)SavePng(Path.Combine(destination,$"presenter-{i:D2}.png"),proof[i]);
            var visual=new DrawingVisual();RenderOptions.SetBitmapScalingMode(visual,BitmapScalingMode.NearestNeighbor);
            using(var drawing=visual.RenderOpen())
            {
                drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(17,19,27)),null,new Rect(0,0,1440,1728));
                for(var i=0;i<proof.Count;i++)drawing.DrawImage(proof[i],new Rect(i%5*288,i/5*288,288,288));
            }
            var sheet=new RenderTargetBitmap(1440,1728,96,96,PixelFormats.Pbgra32);sheet.Render(visual);SavePng(Path.Combine(destination,"actual-presenter-sheet-2x.png"),sheet);
        }
    });
    static void SavePng(string path,BitmapSource source){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));using var file=File.Create(path);encoder.Save(file);}
    static BitmapSource Rest(){var p=new DororongPresenter();p.Render(new(PetState.Idle,default,FacingDirection.Right,0,false,null),DirectInteractionSnapshot.None);return (BitmapSource)((Image)p.FindName("DororongImage")).Source;}
    static void Layout(FrameworkElement p){p.Measure(new Size(144,144));p.Arrange(new Rect(0,0,144,144));p.UpdateLayout();}
    static void Sta(Action action){Exception? error=null;var t=new Thread(()=>{try{action();}catch(Exception e){error=e;}});t.SetApartmentState(ApartmentState.STA);t.Start();t.Join();if(error is not null)ExceptionDispatchInfo.Capture(error).Throw();}
}
