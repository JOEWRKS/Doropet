using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Geometry;

internal static class Program
{
    private static void VerifyV4(string root,string dest)
    {
        var sourceImage=new FormatConvertedBitmap(new BitmapImage(new Uri(Path.Combine(root,"src/Dororong.App/Assets/dororong-canonical.png"))),PixelFormats.Pbgra32,null,0);
        var source=new byte[96*96*4];sourceImage.CopyPixels(source,384,0);
        BodyRegion[] regions=[BodyRegion.FrontPaw,BodyRegion.MiddlePaw,BodyRegion.RightPaw,BodyRegion.Belly,BodyRegion.Rump];
        PointD[] anchors=[new(23,77),new(42,82),new(66,80),new(53,70),new(70,61)];
        PointD[] directions=[new(-12,-12),new(0,-16),new(12,-12),new(-16,0),new(16,0),new(-12,12),new(0,16),new(12,12)];
        for(var r=0;r<regions.Length;r++)for(var d=0;d<directions.Length;d++)
        {
            var stored=new FormatConvertedBitmap(new BitmapImage(new Uri(Path.Combine(dest,$"{regions[r]}-{d}-native.png"))),PixelFormats.Pbgra32,null,0);
            var expected=new byte[160*160*4];stored.CopyPixels(expected,640,0);
            var actual=BodyPullRenderer.Render(source,regions[r],directions[d],anchors[r]);
            // PNG stores straight alpha: use the same encode/decode roundtrip as
            // the saved proof before comparing premultiplied bytes.
            using var stream=new MemoryStream();var encoder=new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(Image(actual,160,160)));encoder.Save(stream);stream.Position=0;
            var decoded=new FormatConvertedBitmap(BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0],PixelFormats.Pbgra32,null,0);
            decoded.CopyPixels(actual,640,0);
            if(!actual.AsSpan().SequenceEqual(expected))throw new InvalidOperationException($"Proof differs: {regions[r]} {d}");
        }
        Console.WriteLine("PASS: current renderer matches all 40 immutable v4 native poses byte-for-byte.");
    }
    [STAThread]
    private static void Main(string[] args)
    {
        var root=Path.GetFullPath(args.Length==0?".":args[0]);
        var dest=Path.Combine(root,"artifacts/repro/five-body-product-port-20260906-attempt-1");
        if(args.Length>1 && args[1]=="--benchmark") {Benchmark(root,dest,args.Length>2?args[2]:"v1");return;}
        if(args.Length>1 && args[1]=="--verify-v4") {VerifyV4(root,Path.Combine(dest,"proof-v4"));return;}
        if(args.Length>1) {if(args[1].IndexOfAny(['/', '\\', ':'])>=0 || args[1].Contains("..")) throw new ArgumentException("Use a single proof revision folder name.");dest=Path.Combine(dest,args[1]);}
        if(File.Exists(Path.Combine(dest,"ownership-native.png")))throw new IOException("Proof already exists. Supply a fresh revision folder to preserve prior evidence.");
        Directory.CreateDirectory(dest);
        var bitmap=new BitmapImage(new Uri(Path.Combine(root,"src/Dororong.App/Assets/dororong-canonical.png")));
        var converted=new FormatConvertedBitmap(bitmap,PixelFormats.Pbgra32,null,0); var source=new byte[96*96*4]; converted.CopyPixels(source,384,0);
        BodyRegion[] regions=[BodyRegion.FrontPaw,BodyRegion.MiddlePaw,BodyRegion.RightPaw,BodyRegion.Belly,BodyRegion.Rump];
        PointD[] anchors=[new(23,77),new(42,82),new(66,80),new(53,70),new(70,61)];
        PointD[] directions=[new(-12,-12),new(0,-16),new(12,-12),new(-16,0),new(16,0),new(-12,12),new(0,16),new(12,12)];
        Color[] colors=[Colors.Transparent,Colors.HotPink,Colors.Goldenrod,Colors.DeepSkyBlue,Colors.LimeGreen,Colors.MediumPurple];
        var overlay=(byte[])source.Clone();
        for(var y=0;y<96;y++)for(var x=0;x<96;x++)
        {
            var region=BodyRegionMap.Pick(new(x,y),source); if(region==BodyRegion.None) continue;
            var color=colors[(int)region]; var i=(y*96+x)*4;
            overlay[i]=(byte)((overlay[i]+color.B*source[i+3]/255)/2);overlay[i+1]=(byte)((overlay[i+1]+color.G*source[i+3]/255)/2);overlay[i+2]=(byte)((overlay[i+2]+color.R*source[i+3]/255)/2);
        }
        Save(dest,"ownership-native.png",overlay,96,96);
        var visual=new DrawingVisual(); using(var dc=visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.LightSlateGray,null,new Rect(0,0,768,800));
            var image=Replicate(Image(overlay,96,96),8); dc.DrawImage(image,new Rect(0,0,768,768));
            var pen=new Pen(Brushes.Cyan,1);
            foreach(var region in regions.Take(3))
            {
                var geometry=PawGeometry.For(region);
                for(var n=0;n<geometry.Mask.Length;n++) dc.DrawLine(pen,P(geometry.Mask[n],8),P(geometry.Mask[(n+1)%geometry.Mask.Length],8));
                dc.DrawEllipse(Brushes.Red,null,P(geometry.Root,8),3,3);dc.DrawEllipse(Brushes.Yellow,null,P(geometry.Tip,8),3,3);
                var curve=new PathGeometry([new PathFigure(P(geometry.Closure[0],8),[new BezierSegment(P(geometry.Closure[1],8),P(geometry.Closure[2],8),P(geometry.Closure[3],8),true)],false)]);
                dc.DrawGeometry(null,new Pen(Brushes.OrangeRed,2),curve);
                var left=geometry.Mask.Min(p=>p.X);var right=geometry.Mask.Max(p=>p.X);
                for(var n=0;n<32;n++)
                {
                    var u0=-1+2*n/32d;var u1=-1+2*(n+1)/32d;
                    dc.DrawLine(new Pen(Brushes.Magenta,2),P(new((left+right)/2+u0*(right-left)/2,geometry.Root.Y+1.5*(1-u0*u0)),8),P(new((left+right)/2+u1*(right-left)/2,geometry.Root.Y+1.5*(1-u1*u1)),8));
                }
            }
            Text(dc,"AUTHORED: cyan mask, red root, yellow tip, orange closure, magenta depth gate",8,774,14);
        }
        SaveVisual(dest,"ownership-geometry-8x.png",visual,768,800);
        File.WriteAllText(Path.Combine(dest,"upward-fold-tip-mapping.json"),JsonSerializer.Serialize(regions.Take(3).Select(region=>
        {
            var g=PawGeometry.For(region);
            return new{region=region.ToString(),rawPull=new PointD(0,-16),rawTip=g.Tip+new PointD(0,-16),renderedTip=g.Vertex(g.Tip,new(0,-16))};
        }),new JsonSerializerOptions{WriteIndented=true}));
        for(var r=0;r<regions.Length;r++)
        {
            var sheet=new DrawingVisual(); RenderOptions.SetBitmapScalingMode(sheet,BitmapScalingMode.NearestNeighbor);
            using(var dc=sheet.RenderOpen())
            {
                Checker(dc,640,368);
                for(var d=0;d<8;d++)
                {
                    var output=BodyPullRenderer.Render(source,regions[r],directions[d],anchors[r]);
                    Save(dest,$"{regions[r]}-{d}-native.png",output,160,160);
                    var x=(d%4)*160;var y=(d/4)*184;
                    dc.DrawImage(Image(output,160,160),new Rect(x,y,160,160)); Text(dc,$"{regions[r]} {directions[d].X},{directions[d].Y}",x+4,y+162,12);
                }
            }
            var native=SaveVisual(dest,$"{regions[r]}-sheet-native.png",sheet,640,368);
            SaveNearest(dest,$"{regions[r]}-sheet-nearest3x.png",native,3);
        }
        var frames=new List<object>(); Directory.CreateDirectory(Path.Combine(dest,"playback-frames"));
        for(var r=0;r<regions.Length;r++)
        {
            var session=new BodyPullSession(); var window=new PointD(300,130); var anchor=anchors[r];var press=window+anchor+new PointD(24,24);
            session.Begin(new(regions[r],anchor,new Matrix(1,0,0,1,24,24),source),window,press);
            for(var n=0;n<120;n++)
            {
                if(n<30)session.Move(press+new PointD(14,4));
                else if(n<60)session.Move(press+new PointD(85,20));
                else if(n<90)session.Move(press+new PointD(-55,-16));
                else if(n==90)session.Release();
                session.Tick(16);session.Reconcile(new RectD(0,0,800,400).ClampTopLeft(session.Current.WindowPosition,new SizeD(144,144)));
                var s=session.Current;var file=$"playback-frames/{r}-{n:D3}.png";
                Save(dest,file,BodyPullRenderer.Render(source,regions[r],s.PullSource,anchor),160,160);
                frames.Add(new {file,x=s.WindowPosition.X+24-32,y=s.WindowPosition.Y+24-32,region=regions[r].ToString(),phase=s.Phase.ToString(),pull=s.PullSource,time=frames.Count*16});
            }
        }
        File.WriteAllText(Path.Combine(dest,"playback.json"),JsonSerializer.Serialize(frames,new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(Path.Combine(dest,"playback.html"),"""
<!doctype html><meta charset="utf-8"><title>Five-region product renderer — deterministic playback</title>
<style>body{font:16px system-ui;background:#dce2e6;color:#222;margin:24px}canvas{background:repeating-conic-gradient(#ddd 0 25%,#eee 0 50%) 0/16px 16px;image-rendering:pixelated;width:800px;height:400px}button{padding:8px}#status{white-space:pre}</style>
<h2>Product renderer: deterministic 16 ms playback</h2><p>Actual C# source-coordinate renderer and session. Normal speed; not a live Windows capture or visual acceptance.</p>
<button id="play">Pause</button> <button id="restart">Restart</button><span id="status"></span><br><canvas width="800" height="400"></canvas>
<script>const frames=FRAME_DATA;const images=frames.map(f=>{const i=new Image;i.src=f.file;return i});const c=document.querySelector('canvas'),ctx=c.getContext('2d');ctx.imageSmoothingEnabled=false;let running=true,start=performance.now(),elapsed=0;document.querySelector('#play').onclick=()=>{running=!running;if(running)start=performance.now()-elapsed;document.querySelector('#play').textContent=running?'Pause':'Play'};document.querySelector('#restart').onclick=()=>{elapsed=0;start=performance.now()};function draw(now){if(running)elapsed=now-start;const n=Math.floor(elapsed/16)%frames.length,f=frames[n];ctx.clearRect(0,0,800,400);if(images[n].complete)ctx.drawImage(images[n],f.x,f.y);document.querySelector('#status').textContent=`  ${f.region}: ${f.phase}  pull ${f.pull.X.toFixed(2)}, ${f.pull.Y.toFixed(2)}`;requestAnimationFrame(draw)}Promise.all(images.map(i=>i.decode())).then(()=>{start=performance.now();requestAnimationFrame(draw)});</script>
""".Replace("FRAME_DATA",JsonSerializer.Serialize(frames)));
        Console.WriteLine($"Generated ownership + 40 native poses + 10 sheets + 600 deterministic 16ms frames at {dest}");
    }
    private static Point P(PointD p,double s)=>new(p.X*s,p.Y*s);
    private static void Benchmark(string root,string dest,string tag)
    {
        if(tag.Length>32||tag.Any(c=>!char.IsLetterOrDigit(c)&&c!='-'))throw new ArgumentException("Use a simple benchmark evidence tag.");
        var path=Path.Combine(dest,$"renderer-benchmark-{tag}.json");
        if(File.Exists(path))throw new IOException("Benchmark evidence already exists.");
        var bitmap=new BitmapImage(new Uri(Path.Combine(root,"src/Dororong.App/Assets/dororong-canonical.png")));
        var converted=new FormatConvertedBitmap(bitmap,PixelFormats.Pbgra32,null,0);var source=new byte[96*96*4];converted.CopyPixels(source,384,0);
        BodyRegion[] regions=[BodyRegion.FrontPaw,BodyRegion.MiddlePaw,BodyRegion.RightPaw,BodyRegion.Belly,BodyRegion.Rump];
        PointD[] anchors=[new(23,77),new(42,82),new(66,80),new(53,70),new(70,61)];var rows=new List<object>();
        for(var r=0;r<5;r++)
        {
            for(var n=0;n<12;n++)BodyPullRenderer.Render(source,regions[r],new(12,12),anchors[r]);
            var samples=new double[80];var allocated=GC.GetAllocatedBytesForCurrentThread();
            for(var n=0;n<samples.Length;n++)
            {
                var pull=new PointD(16*Math.Cos(n*Math.PI/4),16*Math.Sin(n*Math.PI/4));
                var start=Stopwatch.GetTimestamp();var output=BodyPullRenderer.Render(source,regions[r],pull,anchors[r]);
                var image=BitmapSource.Create(160,160,96,96,PixelFormats.Pbgra32,null,output,640);image.Freeze();
                samples[n]=Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            }
            var perFrameBytes=(GC.GetAllocatedBytesForCurrentThread()-allocated)/samples.Length;Array.Sort(samples);
            rows.Add(new{region=regions[r].ToString(),warmups=12,frames=80,p50ms=samples[40],p95ms=samples[76],maxms=samples[^1],allocatedBytesPerFrame=perFrameBytes});
        }
        var result=new{method="Release warmed C# renderer + BitmapSource creation/freeze; Stopwatch; excludes WPF layout/composition/input scheduling, no live app",runtime=Environment.Version.ToString(),rows};
        var json=JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true});File.WriteAllText(path,json);Console.WriteLine(json);
    }
    private static BitmapSource Image(byte[] p,int w,int h){var b=BitmapSource.Create(w,h,96,96,PixelFormats.Pbgra32,null,p,w*4);b.Freeze();return b;}
    private static void Save(string root,string name,byte[] p,int w,int h)=>SaveBitmap(Path.Combine(root,name),Image(p,w,h));
    private static void SaveBitmap(string path,BitmapSource b){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(path);encoder.Save(f);}
    private static BitmapSource SaveVisual(string root,string name,DrawingVisual v,int w,int h){var b=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);b.Render(v);SaveBitmap(Path.Combine(root,name),b);return b;}
    private static void SaveNearest(string root,string name,BitmapSource b,int s)=>SaveBitmap(Path.Combine(root,name),Replicate(b,s));
    private static BitmapSource Replicate(BitmapSource b,int scale)
    {
        var w=b.PixelWidth;var h=b.PixelHeight;var source=new byte[w*h*4];b.CopyPixels(source,w*4,0);var output=new byte[w*h*4*scale*scale];
        for(var y=0;y<h*scale;y++)for(var x=0;x<w*scale;x++)source.AsSpan(((y/scale)*w+x/scale)*4,4).CopyTo(output.AsSpan((y*w*scale+x)*4,4));
        return Image(output,w*scale,h*scale);
    }
    private static void Text(DrawingContext dc,string text,double x,double y,double size)=>dc.DrawText(new FormattedText(text,System.Globalization.CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Consolas"),size,Brushes.Black,1),new Point(x,y));
    private static void Checker(DrawingContext dc,int w,int h){for(var y=0;y<h;y+=8)for(var x=0;x<w;x+=8)dc.DrawRectangle(((x+y)/8)%2==0?Brushes.LightGray:Brushes.WhiteSmoke,null,new Rect(x,y,8,8));}
}
