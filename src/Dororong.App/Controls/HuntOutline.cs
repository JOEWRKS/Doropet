namespace Dororong.App.Controls;

internal static class HuntOutline
{
    private static double Smooth(double v){v=Math.Clamp(v,0,1);return v*v*(3-2*v);}
    // Pbgra32 variant of the approved visible-boundary ink pass. Alpha is never
    // changed, so rendering cannot grow the hit target or foot contact region.
    internal static void Finish(byte[] pixels,double amount,byte[]? headReference=null,byte[]? headCoverage=null)
    {
        const int ox=64,oy=178,w=80,h=30;
        int Index(int x,int y)=>((y+oy)*256+x+ox)*4;
        double[] Distances(byte[] source)
        {
            var result=new double[w*h];Array.Fill(result,99d);
            void Stamp(double px,double py)
            {
                for(var y=Math.Max(0,(int)Math.Floor(py)-3);y<=Math.Min(h-1,(int)Math.Ceiling(py)+3);y++)
                for(var x=Math.Max(0,(int)Math.Floor(px)-3);x<=Math.Min(w-1,(int)Math.Ceiling(px)+3);x++)
                {var d=Math.Sqrt(Math.Pow(x-px,2)+Math.Pow(y-py,2));var i=y*w+x;if(d<result[i])result[i]=d;}
            }
            for(var y=0;y<h-1;y++)for(var x=0;x<w-1;x++)
            {
                var a=source[Index(x,y)+3];
                for(var axis=0;axis<2;axis++)
                {var dx=axis==0?1:0;var dy=1-dx;var b=source[Index(x+dx,y+dy)+3];if((a>=128)==(b>=128))continue;var t=(127.5-a)/(b-a);Stamp(x+dx*t,y+dy*t);}
            }
            return result;
        }
        var distances=Distances(pixels);
        var headDistances=headReference is null?null:Distances(headReference);
        var visibility=Smooth(amount/.2);
        for(var y=0;y<h;y++)for(var x=0;x<w;x++)
        {
            var i=Index(x,y);var a=pixels[i+3];if(a==0)continue;
            var nx=(ox+x+.5)/2-16;var ny=(oy+y+.5)/2-16;
            var region=Math.Max(Smooth((nx-17)/3)*Smooth((32-nx)/3),Smooth((nx-38)/4)*Smooth((55-nx)/4));
            var weight=visibility*region*Smooth((ny-73)/1.5)*Smooth((84-ny)/1.5);
            var r=pixels[i+2]*255d/a;var g=pixels[i+1]*255d/a;var b=pixels[i]*255d/a;
            if(r-g>65||Math.Abs(g-b)>35)continue;
            var coverage=Math.Clamp((2.4-distances[y*w+x])/1.6,0,1)*weight;
            if(headDistances is not null&&headCoverage is not null)
            {
                var head=headCoverage[i+3]/255d;
                var stable=Math.Clamp((2.4-headDistances[y*w+x])/1.6,0,1)*weight;
                coverage=coverage*(1-head)+stable*head;
            }
            for(var c=0;c<3;c++)pixels[i+c]=(byte)Math.Min(pixels[i+c],Math.Round((255-(255-(c==0?61:c==1?52:80))*coverage)*a/255));
        }
    }
}
