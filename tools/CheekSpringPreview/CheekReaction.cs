internal static class CheekReaction
{
    internal static byte[] Render(byte[] input,double pull,int size,int pad)
    {
        if(!double.IsFinite(pull)||input.Length!=size*size*4)throw new ArgumentException("Finite pull and square BGRA input required");
        var output=(byte[])input.Clone();
        var p=Math.Clamp(pull/20,-.5,1);
        if(p==0)return output;
        // Head/cheek translate as one rigid region. Only the torso below y64
        // flexes; its24px height compresses by1px at full pull (~4.2%).
        // Negative spring lobes lean away from the pull, with grounded feet.
        var headX=p>=0?-2*p:-6*p;
        for(var y=1;y<Math.Min(size-1,pad+88);y++)
        {
            var destinationY=y-pad;
            var sourceY=(double)destinationY;
            // Invert a continuous deformation; no independent cut-out layers
            // or strip boundaries that could leave gaps at the head/body seam.
            for(var i=0;i<8;i++)sourceY=destinationY-p*Weight(sourceY);
            var shiftX=headX*Weight(sourceY);
            for(var x=1;x<size-1;x++)Sample(input,x-shiftX,sourceY+pad,output,(y*size+x)*4,size);
        }
        return output;
    }
    static double Weight(double y)
    {
        var t=Math.Clamp((y-64)/24,0,1);
        return 1-t*t*(3-2*t);
    }
    static void Sample(byte[] input,double x,double y,byte[] output,int at,int size)
    {
        var left=(int)Math.Floor(x);var top=(int)Math.Floor(y);
        double alpha=0,b=0,g=0,r=0;
        for(var yy=top;yy<=top+1;yy++)for(var xx=left;xx<=left+1;xx++)
        {
            if(xx<0||xx>=size||yy<0||yy>=size)continue;
            var i=(yy*size+xx)*4;
            var a=input[i+3]/255d*(1-Math.Abs(x-xx))*(1-Math.Abs(y-yy));
            alpha+=a;b+=input[i]*a;g+=input[i+1]*a;r+=input[i+2]*a;
        }
        output[at]=(byte)Math.Clamp(Math.Round(alpha==0?0:b/alpha),0,255);
        output[at+1]=(byte)Math.Clamp(Math.Round(alpha==0?0:g/alpha),0,255);
        output[at+2]=(byte)Math.Clamp(Math.Round(alpha==0?0:r/alpha),0,255);
        output[at+3]=(byte)Math.Clamp(Math.Round(alpha*255),0,255);
    }
}
