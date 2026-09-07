using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Geometry;

// Isolated post-primary preview. The original artwork/primary render are immutable inputs.
// Fields are authored visual assumptions, not recovered anatomy. All units are native pixels.
namespace Dororong.App.Tests.Controls;

// Frozen approved preview reference, retained for byte-for-byte optimization parity.
internal static class ApprovedSurroundingPullRenderer
{
    internal static byte[] Head(byte[] source, PointD grab, PointD follow, PointD? distant=null)
    {
        Validate(source,grab,follow,distant??follow);
        var baseline=Pad(source);if(follow==default && (distant??default)==default)return baseline;
        var center=PinkCenter(source);var near=Limit(follow,14);var far=Limit(distant??follow,14);
        PointD Field(PointD p)
        {
            // Keep the grab and soles stationary in the existing registered pose.
            var pin=Smooth((Length(p-grab)-3)/9);var sole=1-Smooth((p.Y-79)/5);
            if(pin==0||sole==0)return default;
            var face=Bump(p.X,center.X-31,center.X-23,center.X+6,center.X+13)*
                Bump(p.Y,center.Y+3,center.Y+8,center.Y+24,center.Y+31);
            // A rigid face plateau: eyes/mouth translate together, without global face scaling.
            var facePin=Smooth((Length(new PointD(center.X-8,center.Y+16)-grab)-3)/9);
            var hair=Math.Exp(-Math.Pow((p.X-grab.X)/25,2)-Math.Pow((p.Y-grab.Y)/24,2));
            var accessoryLock=1-Smooth((p.X-(center.X+12))/7)*(1-Smooth((p.Y-(center.Y+15))/14));
            var headGate=1-Smooth((p.Y-(center.Y+25))/10);
            var h=(hair*pin*(1-face)+.6*face*facePin)*headGate*accessoryLock;
            var body=Smooth((p.Y-(center.Y+24))/12)*Bump(p.X,12,30,72,85)*sole;
            // Negative sign is material lag behind the grabbed point, not a second pointer move.
            return Scale(near,-.12*h*sole)+Scale(far,-.055*body*pin);
        }
        return Warp(baseline,Field);
    }
    internal static byte[] Body(byte[] source, BodyRegion region, PointD pull, PointD anchor, PointD follow,PointD? distant=null)
    {
        Validate(source,anchor,follow,distant??follow);
        var baseline=BodyPullRenderer.Render(source,region,pull,anchor);
        if(region==BodyRegion.None||follow==default && (distant??default)==default)return baseline;
        var near=Limit(follow,18);var far=Limit(distant??follow,18);
        var paw=region is BodyRegion.FrontPaw or BodyRegion.MiddlePaw or BodyRegion.RightPaw;
        var root=paw?PawGeometry.For(region).Root:anchor;
        var grab=paw?PawGeometry.For(region).Vertex(anchor,pull):new BodyFlow(region,pull,anchor).Map(anchor);
        PointD Field(PointD p)
        {
            var protection=Smooth((BodyRegionMap.HeadDistance(p.X,p.Y)-1.5)/7);
            var pin=Smooth((Length(p-grab)-5)/8);
            var dx=(p.X-root.X)/24;var dy=(p.Y-root.Y)/20;
            var proximity=Math.Exp(-dx*dx-dy*dy);
            var envelope=Bump(p.X,3,16,80,99)*Bump(p.Y,48,62,94,108);
            // The attached torso yields; neighboring paws inherit a weaker, later response.
            var lower=Smooth((p.Y-root.Y-1)/10);
            return Scale(near,.10*proximity*(1-.65*lower)*protection*pin*envelope)+
                Scale(far,.035*proximity*lower*protection*pin*envelope);
        }
        var result=Warp(baseline,Field);
        for(var y=0;y<96;y++)for(var x=0;x<96;x++)if(BodyRegionMap.IsProtected(x,y))
            Array.Copy(baseline,((y+32)*160+x+32)*4,result,((y+32)*160+x+32)*4,4);
        return result;
    }
    internal static PointD PinkCenter(byte[] source)
    {
        double sx=0,sy=0,count=0;
        for(var y=0;y<96;y++)for(var x=0;x<96;x++)
        {var i=(y*96+x)*4;if(source[i+3]<128||source[i+2]<=source[i]+12||source[i]<=source[i+1]+12)continue;sx+=x;sy+=y;count++;}
        if(count==0)throw new ArgumentException("No pink reference landmark found.");
        return new(sx/count,sy/count);
    }
    static byte[] Warp(byte[] source,Func<PointD,PointD> field)
    {
        var output=new byte[source.Length];
        for(var y=0;y<160;y++)for(var x=0;x<160;x++)
        {
            var q=new PointD(x-32,y-32);var uv=q;
            if(field(q)==default){Array.Copy(source,(y*160+x)*4,output,(y*160+x)*4,4);continue;}
            for(var i=0;i<8;i++){var next=q-field(uv);if(Length(next-uv)<1e-8){uv=next;break;}uv=next;}
            var u=uv.X+32;var v=uv.Y+32;var ix=(int)Math.Floor(u);var iy=(int)Math.Floor(v);
            var fx=u-ix;var fy=v-iy;var o=(y*160+x)*4;
            for(var c=0;c<4;c++)
            {
                double Sample(int xx,int yy)=>xx>=0&&xx<160&&yy>=0&&yy<160?source[(yy*160+xx)*4+c]:0;
                var value=(Sample(ix,iy)*(1-fx)+Sample(ix+1,iy)*fx)*(1-fy)+(Sample(ix,iy+1)*(1-fx)+Sample(ix+1,iy+1)*fx)*fy;
                output[o+c]=(byte)Math.Clamp(Math.Round(Math.Round(value,10)),0,255);
            }
        }
        return output;
    }
    static void Validate(byte[] source,PointD grab,PointD a,PointD b)
    {
        if(source.Length!=96*96*4)throw new ArgumentException("96x96 premultiplied input required.");
        if(new[]{grab.X,grab.Y,a.X,a.Y,b.X,b.Y}.Any(v=>!double.IsFinite(v)))throw new ArgumentException("Finite points required.");
    }
    static PointD Limit(PointD p,double limit){var length=Length(p);return length>limit?Scale(p,limit/length):p;}
    static double Length(PointD p)=>Math.Sqrt(p.X*p.X+p.Y*p.Y);
    static PointD Scale(PointD p,double q)=>new(p.X*q,p.Y*q);
    static double Smooth(double t){t=Math.Clamp(t,0,1);return t*t*(3-2*t);}
    static double Bump(double p,double a,double b,double c,double d)=>Smooth((p-a)/(b-a))*(1-Smooth((p-c)/(d-c)));
    internal static byte[] Pad(byte[] source)
    {
        var output = new byte[160 * 160 * 4];
        for (var y = 0; y < 96; y++) Array.Copy(source, y * 384, output, ((y + 32) * 160 + 32) * 4, 384);
        return output;
    }
}


