using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.Core.Geometry;

namespace Dororong.App.Controls;

internal sealed class HuntRenderer
{
    private readonly HuntEyes _eyes = new();
    private readonly DrawingVisual _drawing = new();
    private readonly RenderTargetBitmap _joins = new(128,128,96,96,PixelFormats.Pbgra32);
    private readonly WriteableBitmap _maskedJoins = new(128,128,96,96,PixelFormats.Pbgra32,null);
    private readonly byte[] _joinPixels = new byte[128*128*4];
    private readonly RenderTargetBitmap _composite = new(256,256,96,96,PixelFormats.Pbgra32);
    private readonly WriteableBitmap _finished = new(256,256,96,96,PixelFormats.Pbgra32,null);
    private readonly RenderTargetBitmap _native = new(96,96,96,96,PixelFormats.Pbgra32);
    private readonly byte[] _pixels = new byte[256*256*4];
    private readonly byte[] _referencePixels = new byte[256*256*4];
    private readonly byte[] _headPixels = new byte[256*256*4];
    private static readonly Pen Outline = new(new SolidColorBrush(Color.FromRgb(80,52,61)),1.15)
        { StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round };
    static HuntRenderer() => Outline.Freeze();
    internal HuntRenderer() => RenderOptions.SetBitmapScalingMode(_drawing,BitmapScalingMode.Linear);
    private static Point P(PointD p)=>new(p.X,p.Y);
    private static void Shape(DrawingContext dc,Point start,(Point,Point,Point)[] curves,params Point[] closing)
    {
        StreamGeometry Make(bool fill)
        {
            var g=new StreamGeometry();using(var c=g.Open())
            {c.BeginFigure(start,fill,fill);foreach(var (a,b,end) in curves)c.BezierTo(a,b,end,true,false);if(fill)foreach(var p in closing)c.LineTo(p,true,false);}
            g.Freeze();return g;
        }
        dc.DrawGeometry(Brushes.White,null,Make(true));dc.DrawGeometry(null,Outline,Make(false));
    }
    internal static Matrix HeadTransform(int frame,HuntGazePose gaze)
    {
        var pose=HuntPose.At(frame/60d);
        var angle=gaze.Roll<0?gaze.Roll*(1-pose.Amount)+Math.Max(gaze.Roll,-.08)*pose.Amount:gaze.Roll;
        PointD Head(double x,double y) => pose.Head(43+(x-43)*Math.Cos(angle)-(y-66)*Math.Sin(angle),
            66+(x-43)*Math.Sin(angle)+(y-66)*Math.Cos(angle));
        var o=Head(0,0);var x=Head(1,0);var y=Head(0,1);
        return new(x.X-o.X,x.Y-o.Y,y.X-o.X,y.Y-o.Y,o.X,o.Y);
    }
    internal BitmapSource Render(int frame,HuntGazePose gaze,bool closed=false,double cheekPull=0,double eyePull=0,double hairPull=0,double? readinessPhase=null,BitmapSource? walkingFrame=null)
    {
        if(frame<0||frame>156||!double.IsFinite(gaze.Roll)||!double.IsFinite(gaze.EyeX)||!double.IsFinite(gaze.EyeY))throw new ArgumentOutOfRangeException(nameof(frame));
        var pose=HuntPose.At(frame/60d);var amount=pose.Amount;
        var angle=gaze.Roll<0?gaze.Roll*(1-amount)+Math.Max(gaze.Roll,-.08)*amount:gaze.Roll;
        Point Head(double x,double y,double? rotation=null)
        {
            var a=rotation??angle;var dx=x-43;var dy=y-66;
            return P(pose.Head(43+dx*Math.Cos(a)-dy*Math.Sin(a),66+dx*Math.Sin(a)+dy*Math.Cos(a)));
        }
        var front=Head(16,68);
        // Match the root of the foreleg replacement baked into the body bank.
        var mappedRoot=pose.Map(17,68);
        var root=new Point(mappedRoot.X+(23-mappedRoot.X)*amount,mappedRoot.Y+(68-mappedRoot.Y)*amount);
        var u=Math.Min(1,amount/.2);var blend=u*u*(3-2*u);
        var oldChest=pose.Map(17.2,73);
        var chest=new Point(oldChest.X+(root.X-oldChest.X)*blend,oldChest.Y+(root.Y-oldChest.Y)*blend);
        var frontMaskY=front.Y<root.Y?Math.Max(65,root.Y+1):65;
        using(var dc=_drawing.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(16,16));
            // A rotating head can uncover the inner neck between the outer joins.
            // Keep this backing inside the head/body surfaces and underneath their ink.
            var neck=new StreamGeometry();
            using(var c=neck.Open())
            {
                c.BeginFigure(Head(43,52),true,true);
                c.LineTo(Head(62,52),true,false);
                c.LineTo(P(pose.Map(62,70)),true,false);
                c.LineTo(P(pose.Map(43,70)),true,false);
            }
            neck.Freeze();dc.DrawGeometry(Brushes.White,null,neck);
            Shape(dc,front,[(new(front.X-1,front.Y+(chest.Y-front.Y)*.35),new(chest.X-.7,chest.Y-1.8),chest)],P(pose.Map(46,75)),Head(47,65));
            // Upright registration follows the canonical rear (right edge75),
            // not the wider crouch connection. Blend into the approved crouch
            // curve with pose amount so tracking alone cannot inflate the body.
            var a=Head(69,47.5,Math.Min(angle,0));var apex=P(pose.Map(75+2*amount,59+2.5*amount));var b=P(pose.Map(69.8,74));
            var shoulder=a.Y+6;apex.Y=(apex.Y+shoulder+Math.Sqrt(Math.Pow(apex.Y-shoulder,2)+1))/2;
            Shape(dc,a,[(new(a.X+(apex.X-a.X)*(.35+.2*amount),a.Y+(apex.Y-a.Y)*(.18-.08*amount)),new(apex.X,apex.Y-(apex.Y-a.Y)*.55),apex),
                (new(apex.X,apex.Y+(b.Y-apex.Y)*.55),new(b.X,b.Y-4),b)],P(pose.Map(59,74)),Head(59,48));
            dc.Pop();
        }
        _joins.Clear();_joins.Render(_drawing);
        _joins.CopyPixels(_joinPixels,512,0);
        for(var yy=81;yy<128;yy++)for(var xx=0;xx<68;xx++)
        {
            // A raised head still needs the short neck above the current paw
            // root; the canonical Y65 cut otherwise removes its outer ink.
            if(yy+.5<frontMaskY+16)continue;
            var coverage=xx<62?1:Math.Clamp(1-(xx+.5-62)/6,0,1);
            for(var c=0;c<4;c++){var i=(yy*128+xx)*4+c;_joinPixels[i]=(byte)Math.Round(_joinPixels[i]*(1-blend*coverage));}
        }
        _maskedJoins.WritePixels(new Int32Rect(0,0,128,128),_joinPixels,512,0);
        var head=_eyes.Paint(gaze.EyeX,gaze.EyeY,closed,cheekPull,eyePull,hairPull);
        BitmapSource body=HuntFrames.Instance.Body(frame);
        if(readinessPhase is { } phase)
        {
            // Animate only the pose-mapped body. The rotated head is composited
            // afterwards, so canonical paw masks can never cut its cheek/hair.
            using(var dc=_drawing.RenderOpen())dc.DrawImage(body,new Rect(-16,-16,128,128));
            _native.Clear();_native.Render(_drawing);
            var flutter=ForelegFlutterFrame.Render(PremultipliedFrame.From(_native),false,phase,null,pose);
            body=BitmapSource.Create(96,96,96,96,PixelFormats.Pbgra32,null,flutter,384);
        }
        using(var dc=_drawing.RenderOpen())
        {
            dc.DrawImage(_maskedJoins,new Rect(0,0,256,256));
            dc.DrawImage(body,readinessPhase.HasValue?new Rect(32,32,192,192):new Rect(0,0,256,256));
        }
        _composite.Clear();_composite.Render(_drawing);_composite.CopyPixels(_pixels,1024,0);
        UprightRumpSource.Apply(_pixels,amount);
        if(walkingFrame is not null && amount==0)
        {
            // Walking frames contain a head too. Only use their lower body,
            // below its authored coverage; then draw the recovering head over
            // the gait, so a downward-looking fringe still owns its pixels.
            if(readinessPhase is { } gaitPhase)
            {
                // A frozen cheek capture keeps its hindlegs when readiness
                // starts; animate only the forelegs on that same captured gait.
                var gaitFlutter=ForelegFlutterFrame.Render(PremultipliedFrame.From(walkingFrame),false,gaitPhase,null,pose);
                walkingFrame=BitmapSource.Create(96,96,96,96,PixelFormats.Pbgra32,null,gaitFlutter,384);
            }
            using(var dc=_drawing.RenderOpen())dc.DrawImage(walkingFrame,new Rect(32,32,192,192));
            _composite.Clear();_composite.Render(_drawing);_composite.CopyPixels(_referencePixels,1024,0);
            for(var yy=180;yy<256;yy++)
            {
                var mix=Math.Clamp((yy+.5-180)/4,0,1);mix=mix*mix*(3-2*mix);
                for(var xx=0;xx<256;xx++)for(var c=0;c<4;c++)
                {
                    var i=(yy*256+xx)*4+c;
                    _pixels[i]=(byte)Math.Round(_pixels[i]+(_referencePixels[i]-_pixels[i])*mix);
                }
            }
        }
        _finished.WritePixels(new Int32Rect(0,0,256,256),_pixels,1024,0);
        var origin=Head(0,0);var x=Head(1,0);var y=Head(0,1);
        var matrix=new Matrix((x.X-origin.X)*2,(x.Y-origin.Y)*2,(y.X-origin.X)*2,(y.Y-origin.Y)*2,(origin.X+16)*2,(origin.Y+16)*2);
        if(amount>0&&readinessPhase.HasValue)
        {
            using(var dc=_drawing.RenderOpen())
            {dc.PushTransform(new MatrixTransform(matrix));dc.DrawImage(head,new Rect(0,0,96,96));dc.Pop();}
            _composite.Clear();_composite.Render(_drawing);_composite.CopyPixels(_headPixels,1024,0);
            using(var dc=_drawing.RenderOpen())
            {
                dc.DrawImage(_maskedJoins,new Rect(0,0,256,256));
                dc.DrawImage(HuntFrames.Instance.Body(frame),new Rect(0,0,256,256));
                dc.PushTransform(new MatrixTransform(matrix));dc.DrawImage(head,new Rect(0,0,96,96));dc.Pop();
            }
            _composite.Clear();_composite.Render(_drawing);_composite.CopyPixels(_referencePixels,1024,0);
        }
        using(var dc=_drawing.RenderOpen())
        {
            dc.DrawImage(_finished,new Rect(0,0,256,256));
            dc.PushTransform(new MatrixTransform(matrix));dc.DrawImage(head,new Rect(0,0,96,96));dc.Pop();
        }
        _composite.Clear();_composite.Render(_drawing);
        _composite.CopyPixels(_pixels,1024,0);
        // The authored head includes white neck backing. Resolve ink after that
        // backing, while flutter uses a stable reference underneath the head.
        if(amount>0)HuntOutline.Finish(_pixels,amount,readinessPhase.HasValue?_referencePixels:null,readinessPhase.HasValue?_headPixels:null);
        _finished.WritePixels(new Int32Rect(0,0,256,256),_pixels,1024,0);
        using(var dc=_drawing.RenderOpen())dc.DrawImage(_finished,new Rect(-16,-16,128,128));
        _native.Clear();_native.Render(_drawing);
        // Interaction captures and AlphaHitTestImage cache by source identity.
        // Never mutate a frame they already captured or cached.
        var frameImage=_native.CloneCurrentValue();frameImage.Freeze();return frameImage;
    }
}
