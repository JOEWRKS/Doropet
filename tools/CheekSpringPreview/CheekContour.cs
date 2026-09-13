using Dororong.App.Controls;

// Preview-only contour reconstruction. The accepted cheek curve is transformed
// geometrically; neither ink width nor antialiasing is stretched with its pixels.
internal static class CheekContour
{
    internal static void Refine(byte[] image,byte[] source,double pull,int size,int pad,double outwardGain=1.1)
    {
        if(pull<=0)return;
        var p=Math.Clamp(pull/20,0,1);
        var strength=Smooth(pull/4);
        var curve=new (double X,double Y)[177];
        for(var i=0;i<curve.Length;i++)
        {
            var y=53+i/16d;var x=PolishedCheekRenderer.Edge(y,p);
            var q=(X:x,Y:y);
            // Match the accepted side-hair displacement before exterior extension.
            for(var j=0;j<8;j++)
            {
                var h=Smooth((q.X-3)/4)*Smooth((16-q.X)/6)*
                    Smooth((q.Y-30)/11)*Smooth((69-q.Y)/10);
                q=(x-.8*p*h,y+.35*p*h);
            }
            var vertical=Smooth((q.Y-47)/6)*Smooth((71-q.Y)/7);
            if(q.X<12)q.X=12+(q.X-12)*(1+outwardGain*p*vertical);
            curve[i]=q;
        }
        var ink=(54*96+12)*4; // Original dark outline, RGBA95/61/72/255.
        var skin=(57*96+18)*4; // Original cheek fill, RGBA247/217/212/255.
        // Row64 belongs to the retained lower hair attachment, as in RoundTip.
        for(var y=53;y<64;y++)for(var x=1-pad;x<12;x++)
        {
            var blend=strength*Smooth((12-x)/4);
            if(blend==0)continue;
            double distance=double.PositiveInfinity,edgeX=double.NaN;
            for(var j=1;j<curve.Length;j++)
            {
                var a=curve[j-1];var b=curve[j];var dx=b.X-a.X;var dy=b.Y-a.Y;
                var u=Math.Clamp(((x-a.X)*dx+(y-a.Y)*dy)/(dx*dx+dy*dy),0,1);
                distance=Math.Min(distance,Math.Sqrt(Math.Pow(x-a.X-u*dx,2)+Math.Pow(y-a.Y-u*dy,2)));
                if(y>=a.Y&&y<=b.Y)edgeX=a.X+(y-a.Y)/dy*dx;
            }
            // Do not fabricate a closing edge beyond the original open contour.
            if(double.IsNaN(edgeX))continue;
            var signed=x>=edgeX?distance:-distance;
            var alpha=Math.Clamp(signed+.5,0,1);
            // Constant screen-space 1px ink core with a subpixel coverage fringe.
            var inkMix=Math.Clamp(1.4-signed,0,1);
            var at=((y+pad)*size+x+pad)*4;
            var oldAlpha=image[at+3]/255d;
            var finalAlpha=oldAlpha*(1-blend)+alpha*blend;
            for(var c=0;c<3;c++)
            {
                var colour=source[skin+c]*(1-inkMix)+source[ink+c]*inkMix;
                image[at+c]=(byte)Math.Clamp(Math.Round(finalAlpha==0?0:
                    (image[at+c]*oldAlpha*(1-blend)+colour*alpha*blend)/finalAlpha),0,255);
            }
            image[at+3]=(byte)Math.Round(finalAlpha*255);
        }
    }
    static double Smooth(double x){x=Math.Clamp(x,0,1);return x*x*(3-2*x);}
}
