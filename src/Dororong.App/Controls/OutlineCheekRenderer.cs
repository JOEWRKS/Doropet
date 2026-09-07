// Ported from the user-accepted cheek-round-outline preview; raster math unchanged.
namespace Dororong.App.Controls;

internal static class OutlineCheekRenderer
{
    internal static byte[] Render(byte[] source,double pull,double eyePull=20,double hairPull=20)
    {
        var result=PolishedCheekRenderer.Render(source,pull,eyePull,hairPull);
        if(pull<=0)return result;
        var strength=Smooth(Math.Clamp(pull/4,0,1));
        var p=Math.Clamp(pull/20,0,1);var h=Math.Clamp(hairPull/20,0,1);
        var boundary=new (double X,double Y)[89];
        for(int i=0;i<boundary.Length;i++){
            var y=53+i*.125;var x=PolishedCheekRenderer.Edge(y,p);var q=(X:x,Y:y);
            // Invert the existing inverse-sampling field at this curve point.
            // No source pixels/alpha or animation displacement are changed.
            for(int j=0;j<8;j++){
                var weight=Bump(q.X,3,7,10,16)*Bump(q.Y,30,41,59,69);
                q=(x-.8*h*weight,y+.35*h*weight);
            }
            boundary[i]=q;
        }
        var centres=new (double X,double Y)[boundary.Length];
        for(int i=0;i<boundary.Length;i++){
            var a=boundary[Math.Max(0,i-1)];var b=boundary[Math.Min(boundary.Length-1,i+1)];
            var dx=b.X-a.X;var dy=b.Y-a.Y;var length=Math.Sqrt(dx*dx+dy*dy);
            // Centre ink slightly INSIDE the existing coverage edge, so clipping
            // at fractional alpha does not remove the entire dark core.
            centres[i]=(boundary[i].X+.60*dy/length,boundary[i].Y-.60*dx/length);
        }
        int anchor=(54*96+12)*4; // observed source outline RGBA(95,61,72,255)
        for(int y=53;y<=64;y++)for(int x=0;x<24;x++){
            if(y<=59&&x>=16)continue;
            int pixel=(y*96+x)*4;if(result[pixel+3]==0)continue;
            double distance=double.PositiveInfinity;
            for(int j=1;j<centres.Length;j++){
                var a=centres[j-1];var b=centres[j];var dx=b.X-a.X;var dy=b.Y-a.Y;
                var t=Math.Clamp(((x-a.X)*dx+(y-a.Y)*dy)/(dx*dx+dy*dy),0,1);
                distance=Math.Min(distance,Math.Sqrt(Math.Pow(x-a.X-t*dx,2)+Math.Pow(y-a.Y-t*dy,2)));
            }
            var coverage=Math.Clamp(1.25-distance,0,1)*strength;
            if(coverage==0)continue;
            for(int c=0;c<3;c++)result[pixel+c]=(byte)Math.Round(Math.Min(result[pixel+c],result[pixel+c]*(1-coverage)+source[anchor+c]*coverage));
        }
        return result;
    }
    static double Smooth(double t){t=Math.Clamp(t,0,1);return t*t*(3-2*t);}
    static double Bump(double x,double a,double b,double c,double d)=>Smooth((x-a)/(b-a))*Smooth((d-x)/(d-c));
}
