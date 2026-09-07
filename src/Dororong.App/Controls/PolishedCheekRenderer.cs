// Ported from the user-accepted cheek-round-outline preview; raster math unchanged.
namespace Dororong.App.Controls;

internal static class PolishedCheekRenderer
{
    internal static byte[] Render(byte[] source,double pull,double eyePull=20,double hairPull=20)
    {
        if(!double.IsFinite(eyePull)||!double.IsFinite(hairPull))throw new ArgumentException("Finite follow input required");
        var accepted=FacialCheekRenderer.Render(source,pull);
        if(pull<=0)return accepted;
        var p=Math.Clamp(pull/20,0,1);
        var rounded=RoundTip(source,accepted,p);
        var output=(byte[])rounded.Clone();
        var eye=Math.Clamp(eyePull/20,0,1);var hair=Math.Clamp(hairPull/20,0,1);
        // Continuous inverse warp: no cut-out hair layer or empty vacated seam.
        // The entire iris lies in a constant-translation plateau; displacement
        // tapers through surrounding skin and roots, rather than scaling the eye.
        for(int y=23;y<71;y++)for(int x=2;x<54;x++)
        {
            var e=Bump(x,33,34,43,47)*Bump(y,43,47,58,63);
            var top=Bump(x,5,14,42,50)*Bump(y,23,29,36,47);
            var left=Bump(x,3,7,10,16)*Bump(y,30,41,59,69);
            var right=Bump(x,44,48,50,54)*Bump(y,44,48,60,70);
            var h=Math.Max(top,Math.Max(left,right));
            // Rose's observed upper-left edge stays outside the hair field.
            if(x>=50&&y<44)h=0;
            var dx=-.60*eye*e-.80*hair*h;
            var dy=.65*eye*e+.35*hair*h;
            if(dx==0&&dy==0)continue;
            SamplePremultiplied(rounded,x-dx,y-dy,output,(y*96+x)*4);
        }
        return output;
    }

    static byte[] RoundTip(byte[] source,byte[] accepted,double amount)
    {
        var result=(byte[])accepted.Clone();var mix=Smooth(amount);
        var points=Enumerable.Range(0,45).Select(i=>{double y=53+i*.25;return(X:Edge(y,amount),Y:y);}).ToArray();
        foreach(var row in CheekPullRenderer.Rows)
        {
            if(row.Y==64)continue;
            var edge=Edge(row.Y,amount);var oldEdge=row.Edge-(int)Math.Round(amount*10*row.Weight);
            // Stop at the original contour-band width. Extra padding would
            // overwrite the accepted near-eye corner at(16,53).
            var maxX=Math.Min(24,Math.Max((int)Math.Ceiling(edge),oldEdge)+(row.Skin-row.Edge));
            for(int x=0;x<=maxX;x++)
            {
                var i=(row.Y*96+x)*4;
                var cover=Math.Clamp(x-edge+.5,0,1);
                var distance=double.PositiveInfinity;
                for(int j=1;j<points.Length;j++)
                {
                    var a=points[j-1];var b=points[j];var dx=b.X-a.X;var dy=b.Y-a.Y;
                    var t=Math.Clamp(((x-a.X)*dx+(row.Y-a.Y)*dy)/(dx*dx+dy*dy),0,1);
                    distance=Math.Min(distance,Math.Sqrt(Math.Pow(x-a.X-t*dx,2)+Math.Pow(row.Y-a.Y-t*dy,2)));
                }
                var ink=Math.Clamp(1.25-distance,0,1);
                var skinIndex=(row.Y*96+row.Skin+1)*4;var inkIndex=(row.Y*96+row.Edge)*4;
                double oldAlpha=source[i+3]/255d;double alpha=cover+oldAlpha*(1-cover);
                var proposed=new double[4];proposed[3]=alpha*255;
                for(int c=0;c<3;c++)
                {
                    var colour=source[skinIndex+c]*(1-ink)+source[inkIndex+c]*ink;
                    proposed[c]=alpha==0?0:(colour*cover+source[i+c]*oldAlpha*(1-cover))/alpha;
                }
                // Smooth onset from the accepted raster avoids a redraw pop at
                // the first nonzero gesture. Full strength uses the round curve.
                BlendPremultiplied(accepted,i,proposed,mix,result);
            }
        }
        return result;
    }
    internal static double Edge(double y,double amount)
    {
        double initial;
        int row=Math.Clamp((int)Math.Floor(y)-53,0,10);
        var a=CheekPullRenderer.Rows[row];var b=CheekPullRenderer.Rows[row+1];
        initial=a.Edge+(b.Edge-a.Edge)*(y-a.Y);
        bool upper=y<=57.5;double lo=0,hi=1;
        for(int i=0;i<24;i++){
            var t=(lo+hi)*.5;var cy=upper?Bezier(53,53,53,57.5,t):Bezier(57.5,61,63,64,t);
            if(cy<y)lo=t;else hi=t;
        }
        var u=(lo+hi)*.5;
        var target=upper?Bezier(13,7,2,2,u):Bezier(2,2,10,19,u);
        return initial+(target-initial)*amount;
    }
    static double Bezier(double a,double b,double c,double d,double t){var s=1-t;return a*s*s*s+3*b*s*s*t+3*c*s*t*t+d*t*t*t;}
    static double Smooth(double t){t=Math.Clamp(t,0,1);return t*t*(3-2*t);}
    static double Bump(double v,double start,double fullStart,double fullEnd,double end)=>Smooth((v-start)/(fullStart-start))*Smooth((end-v)/(end-fullEnd));
    static void BlendPremultiplied(byte[] original,int i,double[] target,double blend,byte[] output)
    {
        var a=original[i+3]/255d;var b=target[3]/255d;var alpha=a*(1-blend)+b*blend;
        for(int c=0;c<3;c++)output[i+c]=(byte)Math.Clamp(Math.Round(alpha==0?0:(original[i+c]*a*(1-blend)+target[c]*b*blend)/alpha),0,255);
        output[i+3]=(byte)Math.Clamp(Math.Round(alpha*255),0,255);
    }
    static void SamplePremultiplied(byte[] pixels,double x,double y,byte[] output,int destination)
    {
        int left=(int)Math.Floor(x),top=(int)Math.Floor(y);double a=0;var rgb=new double[3];
        for(int yy=top;yy<=top+1;yy++)for(int xx=left;xx<=left+1;xx++)
        {
            if(xx<0||xx>=96||yy<0||yy>=96)continue;
            var weight=(1-Math.Abs(x-xx))*(1-Math.Abs(y-yy));var i=(yy*96+xx)*4;var alpha=pixels[i+3]/255d;
            a+=alpha*weight;for(int c=0;c<3;c++)rgb[c]+=pixels[i+c]*alpha*weight;
        }
        // Stabilize half-LSB ties against coordinate subtraction noise before
        // the usual nearest-even byte quantization; no perceptual blur filter.
        for(int c=0;c<3;c++)output[destination+c]=(byte)Math.Clamp(Math.Round(Math.Round(a==0?0:rgb[c]/a,10)),0,255);
        output[destination+3]=(byte)Math.Clamp(Math.Round(a*255),0,255);
    }
}
