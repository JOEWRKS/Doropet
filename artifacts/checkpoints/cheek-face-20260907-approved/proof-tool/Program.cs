using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;

internal static class Program
{
    const int N=96;
    [STAThread] static int Main(string[] args)
    {
        var sourcePath=Path.GetFullPath(args[0]);var output=Path.GetFullPath(args[1]);
        if(Directory.Exists(output))throw new IOException("New output directory required");Directory.CreateDirectory(output);
        var hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath)));
        if(hash!="699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78")throw new Exception("Source identity mismatch");
        var b=new FormatConvertedBitmap(BitmapDecoder.Create(new Uri(sourcePath),BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0],PixelFormats.Bgra32,null,0);
        var source=new byte[N*N*4];b.CopyPixels(source,N*4,0);var copy=(byte[])source.Clone();
        byte[] Render(double d,double release=0,bool cancel=false)=>FacialCheekRenderer.Render(source,d,release,cancel);
        var frames=new Dictionary<string,byte[]>{{"rest",Render(0)},{"small",Render(4)},{"half",Render(10)},{"max",Render(20)},{"inward",Render(-10)},{"release055",Render(20,55)},{"release110",Render(20,110)},{"release165",Render(20,165)},{"release220",Render(20,220)},{"cancel",Render(20,0,true)}};
        var checks=new List<object>();var failed=0;
        void Check(string name,bool ok,object observed){checks.Add(new{name,ok,observed});if(!ok)failed++;Console.WriteLine($"{(ok?"OK":"FAIL")} {name}: {JsonSerializer.Serialize(observed)}");}
        var max=frames["max"];
        Check("neutral-exact",Equal(source,frames["rest"]),Changed(source,frames["rest"]));
        Check("release220-exact",Equal(source,frames["release220"]),Changed(source,frames["release220"]));
        Check("cancel-exact",Equal(source,frames["cancel"]),Changed(source,frames["cancel"]));
        Check("source-buffer-unchanged",Equal(source,copy),Changed(source,copy));
        // Shared rigid transfer keeps the selected eye/mouth separation unchanged.
        // These observations do not invoke the new renderer's masks or mapping.
        var eyeErrors=0;var eyeCount=0;
        foreach(var (y,left,right) in new[]{(45,20,23),(46,19,23),(47,19,24),(48,18,24),(49,18,25),(50,17,26),(51,17,27),(52,17,26),(53,18,26),(54,19,26),(55,20,25),(56,21,23)})
            for(var x=left;x<=right;x++){eyeCount++;if(!Pixel(source,x,y).SequenceEqual(Pixel(max,x-1,y+2)))eyeErrors++;}
        Check("whole-selected-eye-rigid-transfer-at-max",eyeErrors==0,new{eyeCount,eyeErrors,delta=new[]{-1,2}});
        var mouthErrors=0;for(var y=57;y<=61;y++)for(var x=25;x<=33;x++)if(!Pixel(source,x,y).SequenceEqual(Pixel(max,x-1,y+2)))mouthErrors++;
        Check("mouth-rigid-transfer-at-max",mouthErrors==0,new{pixels=45,mouthErrors,delta=new[]{-1,2}});
        Check("inner-skin-follows-outside-feature-destinations",!Pixel(source,23,60).SequenceEqual(Pixel(max,23,60)),new{before=Hex(source,23,60),after=Hex(max,23,60),note="outside moved eye and mouth"});
        var approvedMax=CheekPullRenderer.Render(source,20);
        Check("approved-exterior-alpha-envelope-retained",Enumerable.Range(0,N*N).All(i=>max[i*4+3]==approvedMax[i*4+3]),"broader interior, no extra hair occlusion");
        var forbidden=frames.Values.Sum(f=>Enumerable.Range(0,N*N).Count(i=>(i/N<45||i/N>=66||i%N>=35)&&!Pixel(source,i%N,i/N).SequenceEqual(Pixel(f,i%N,i/N))));
        Check("upper-hair-accessories-opposite-face-body-fixed",forbidden==0,forbidden);
        Check("outward-clamp",Equal(max,Render(200)),"20vs200DIP");
        Check("inward-preserves-accepted-renderer",Equal(frames["inward"],CheekPullRenderer.Render(source,-10)),"no new inward art");
        Check("release-has-intermediate",!Equal(frames["release110"],max)&&!Equal(frames["release110"],source),Changed(source,frames["release110"]));
        var alphaErrors=0;var inkErrors=0;var outsideErrors=0;var hairEdgeErrors=0;var samples=0;
        var eyeSource=FacialCheekRenderer.Eye.SelectMany(r=>Enumerable.Range(r.Left,r.Right-r.Left+1).Select(x=>(X:x,Y:r.Y))).ToArray();
        var mouthSource=FacialCheekRenderer.Mouth.SelectMany(r=>Enumerable.Range(r.Left,r.Right-r.Left+1).Select(x=>(X:x,Y:r.Y))).ToArray();
        var featureSweep=eyeSource.Concat(mouthSource).SelectMany(p=>new[]{0,1,2}.SelectMany(dy=>new[]{-1,0}.Select(dx=>(X:p.X+dx,Y:p.Y+dy)))).ToHashSet();
        for(var q=0;q<=80;q++)
        {
            var d=q/4d;var f=Render(d);var old=CheekPullRenderer.Render(source,d);samples++;
            foreach(var p in new[]{(X:19,Y:45),(X:18,Y:46),(X:18,Y:47),(X:17,Y:48)})
                if(!Pixel(f,p.X,p.Y).SequenceEqual(Pixel(old,p.X,p.Y)))hairEdgeErrors++;
            for(var y=0;y<96;y++)for(var x=0;x<96;x++)
            {
                var i=(y*96+x)*4;if(f[i+3]!=old[i+3])alphaErrors++;
                var row=CheekPullRenderer.Rows.FirstOrDefault(r=>r.Y==y);
                var left=row.Y==0?96:row.Skin-(int)Math.Round(d*.5*row.Weight)+1;
                var allowed=featureSweep.Contains((x,y))||(row.Y!=0&&x>=left&&x<35);
                if(!allowed&&!Pixel(f,x,y).SequenceEqual(Pixel(old,x,y)))outsideErrors++;
                // Preserve accepted dark ink in the independent transported-edge corridor.
                if(d>0&&y>=53&&y<=64&&old[i+3]>=128&&(.2126*old[i+2]+.7152*old[i+1]+.0722*old[i])<170)
                {
                    var points=CheekPullRenderer.Rows.Select(r=>(X:r.Edge-(int)Math.Round(d*.5*r.Weight),Y:r.Y)).ToArray();
                    var distance=double.PositiveInfinity;
                    for(var j=1;j<points.Length;j++){var a=points[j-1];var z=points[j];var dx=z.X-a.X;var dy=z.Y-a.Y;var u=Math.Clamp(((x-a.X)*dx+(y-a.Y)*dy)/(double)(dx*dx+dy*dy),0,1);distance=Math.Min(distance,Math.Sqrt(Math.Pow(x-a.X-u*dx,2)+Math.Pow(y-a.Y-u*dy,2)));}
                    if(distance<=1&&!Pixel(f,x,y).SequenceEqual(Pixel(old,x,y)))inkErrors++;
                }
            }
        }
        Check("sweep-alpha-equal-approved-envelope",alphaErrors==0,new{samples,alphaErrors});
        Check("sweep-accepted-contour-ink-exact",inkErrors==0,new{samples,inkErrors});
        Check("sweep-exact-complement-of-declared-editable-set",outsideErrors==0,new{samples,outsideErrors});
        Check("sweep-observed-bang-boundary-pixels-fixed",hairEdgeErrors==0,new{samples,hairEdgeErrors,probes=new[]{"19,45","18,46","18,47","17,48"}});
        var selection=(byte[])source.Clone();
        foreach(var p in featureSweep){var i=(p.Y*96+p.X)*4;selection[i]=40;selection[i+1]=210;selection[i+2]=255;selection[i+3]=255;}
        Save(Path.Combine(output,"feature-source-and-destination-sweep-nearest4x.png"),Enlarge(selection,96,96,4),384,384);
        File.WriteAllText(Path.Combine(output,"annotation.json"),JsonSerializer.Serialize(new{geometrySource="AUTHOR_SELECTED_FROM_OBSERVED_TEXELS",confidence="AUTHORED_NOT_ANATOMICAL_PROOF",eye=eyeSource,mouth=mouthSource,featureTranslationAtMax=new{x=-1,y=2},motionPath="0..10DIP: smoothstep down 0..2px;10..20DIP: smoothstep left 0..1px; same eye/mouth translation",interpolation="local bilinear rigid feature translation only; no feature scale",vacatedPixelFill="nearby original colours; authored reconstruction inside vacated feature footprints",contour="approved renderer envelope retained; exact approved 1.5px geometric boundary collar",input="horizontal outward0..20DIP; gain0.5;220ms restore",inward="unchanged approved algorithm",productApplication="NOT APPLIED"},new JsonSerializerOptions{WriteIndented=true,IncludeFields=true}));
        foreach(var pair in frames){Save(Path.Combine(output,pair.Key+"-native.png"),pair.Value,N,N);Save(Path.Combine(output,pair.Key+"-nearest4x.png"),Enlarge(pair.Value,N,N,4),384,384);}
        var oldMax=CheekPullRenderer.Render(source,20);var comparison=Strip(source,oldMax,max);
        Save(Path.Combine(output,"rest-old-new-native.png"),comparison,288,96);Save(Path.Combine(output,"rest-old-new-nearest4x.png"),Enlarge(comparison,288,96,4),1152,384);
        var progression=Strip(frames["rest"],frames["small"],frames["half"],max);
        Save(Path.Combine(output,"progression-nearest3x.png"),Enlarge(progression,384,96,3),1152,288);
        var playback=new List<object>();var motion=new List<byte[]>();
        for(var ms=0;ms<=800;ms+=20)
        {
            var pixels=ms<240?Render(ms/12d):ms<400?max:Render(20,ms-400);var name=$"motion-{ms:D3}.png";var path=Path.Combine(output,name);Save(path,pixels,N,N);
            playback.Add(new{time=ms,file=name,uri="data:image/png;base64,"+Convert.ToBase64String(File.ReadAllBytes(path))});motion.Add(pixels);
        }
        ApngWriter.Write(Path.Combine(output,"cheek-face-animation.png"),Enumerable.Range(0,41).Select(i=>Path.Combine(output,$"motion-{i*20:D3}.png")).ToArray());
        File.WriteAllText(Path.Combine(output,"playback.html"),"""
        <!doctype html><meta charset="utf-8"><title>Broader cheek-side face — isolated proof</title>
        <style>body{background:#222630;color:#eee;font:16px system-ui;margin:28px}canvas{image-rendering:pixelated;vertical-align:middle}button,input{margin:16px}</style>
        <h2>볼 → 볼살·눈밑·입가</h2><p>한쪽 얼굴 미리보기 · 현재 앱 미적용 · 눈/입 모양 유지, 미세 이동</p>
        <canvas id="native" width="96" height="96"></canvas><canvas id="large" width="96" height="96" style="width:384px;height:384px"></canvas>
        <button id="toggle">Pause</button><input id="scrub" type="range" min="0" max="40" value="0"><span id="label"></span>
        <script>const records=FRAMES,imgs=records.map(r=>{const i=new Image;i.src=r.uri;return i});let elapsed=0,last=0,run=true,index=0;
        toggle.onclick=()=>{run=!run;toggle.textContent=run?'Pause':'Play'};scrub.oninput=()=>{run=false;toggle.textContent='Play';index=+scrub.value;elapsed=index*20};
        function tick(now){if(last&&run)elapsed+=now-last;last=now;if(run)index=Math.min(40,Math.floor((elapsed%1100)/20));for(const c of[native,large]){const x=c.getContext('2d');x.clearRect(0,0,96,96);x.drawImage(imgs[index],0,0)}scrub.value=index;label.textContent=index*20+' ms';requestAnimationFrame(tick)}
        Promise.all(imgs.map(i=>i.decode())).then(()=>requestAnimationFrame(tick));</script>
        """.Replace("FRAMES",JsonSerializer.Serialize(playback)));
        File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(new{sourceSHA256=hash,total=checks.Count,failed,checks,productApplication="NOT APPLIED",visualStatus="UNVERIFIED",userAcceptance="UNVERIFIED"},new JsonSerializerOptions{WriteIndented=true}));
        return failed==0?0:2;
    }
    internal static ReadOnlySpan<byte> Pixel(byte[] p,int x,int y)=>p.AsSpan((y*N+x)*4,4);
    static string Hex(byte[] p,int x,int y)=>Convert.ToHexString(Pixel(p,x,y));
    static bool Equal(byte[] a,byte[] b)=>a.SequenceEqual(b);
    static int Changed(byte[] a,byte[] b)=>Enumerable.Range(0,N*N).Count(i=>!Pixel(a,i%N,i/N).SequenceEqual(Pixel(b,i%N,i/N)));
    static byte[] Strip(params byte[][] items){var result=new byte[96*96*4*items.Length];for(var j=0;j<items.Length;j++)for(var y=0;y<96;y++)Array.Copy(items[j],y*96*4,result,(y*96*items.Length+j*96)*4,96*4);return result;}
    static byte[] Enlarge(byte[] p,int w,int h,int f){var result=new byte[w*h*f*f*4];for(var y=0;y<h*f;y++)for(var x=0;x<w*f;x++)Array.Copy(p,(y/f*w+x/f)*4,result,(y*w*f+x)*4,4);return result;}
    static void Save(string path,byte[] p,int w,int h){var b=BitmapSource.Create(w,h,96,96,PixelFormats.Bgra32,null,p,w*4);var e=new PngBitmapEncoder();e.Frames.Add(BitmapFrame.Create(b));using var stream=File.Create(path);e.Save(stream);}
}
