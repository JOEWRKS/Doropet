using Dororong.App.Controls;

internal static class FacialCheekRenderer
{
    // Authored observed feature footprints, not an anatomical segmentation claim.
    internal static readonly (int Y,int Left,int Right)[] Eye =
    [(45,20,23),(46,19,23),(47,19,24),(48,18,24),(49,18,25),(50,17,26),
     (51,17,27),(52,17,26),(53,18,26),(54,19,26),(55,20,25),(56,21,23)];
    internal static readonly (int Y,int Left,int Right)[] Mouth =
    [(57,25,33),(58,25,33),(59,25,33),(60,25,33),(61,25,33)];

    internal static byte[] Render(byte[] source,double outwardDips,double releaseMilliseconds=0,bool cancelled=false)
    {
        if(source.Length!=96*96*4||!double.IsFinite(outwardDips)||!double.IsFinite(releaseMilliseconds))throw new ArgumentException("Finite gesture and96x96 BGRA source required");
        if(cancelled||outwardDips<=0||releaseMilliseconds>=220)
            return CheekPullRenderer.Render(source,outwardDips,0,releaseMilliseconds,cancelled);
        var t=Math.Clamp(releaseMilliseconds/220,0,1);
        var amount=Math.Clamp(outwardDips,0,20)*(1-t*t*(3-2*t));
        if(amount==0)return (byte[])source.Clone();
        var clean=(byte[])source.Clone();
        foreach(var row in Eye)for(var x=row.Left;x<=row.Right;x++)
        {
            // Revealed feature footprint only: copy observed nearby colours.
            // Top rows reveal bangs; lower rows reveal cheek/central skin.
            var donor=row.Y<=47 ? (X:x,Y:row.Y-3) : x<22 ? (X:18,Y:57) : (X:29,Y:57);
            Copy(source,donor.X,donor.Y,clean,x,row.Y);
        }
        foreach(var row in Mouth)for(var x=row.Left;x<=row.Right;x++)Copy(source,34,row.Y,clean,x,row.Y);
        // Retain the approved contour/alpha envelope and its existing local
        // side-hair occlusion. This study expands interior support, not hair cover.
        var result=CheekPullRenderer.Render(clean,amount);
        foreach(var row in CheekPullRenderer.Rows)
        {
            var shift=(int)Math.Round(amount*.5*row.Weight);
            if(shift==0)continue;
            var left=row.Skin-shift;
            const int root=35;
            for(var x=left+1;x<root;x++)
            {
                // Smooth falloff from the old moved cheek edge to the fixed
                // center-side endpoint. One contour-adjacent texel stays intact.
                var u=(x-left)/(double)(root-1-left);
                var sx=row.Skin+u*(root-1-row.Skin);
                var sourceX=Math.Clamp((int)Math.Round(sx),row.Skin,root-1);
                Copy(clean,sourceX,row.Y,result,x,row.Y);
            }
        }
        var fraction=amount/20;
        // Rigid subpixel translation, no feature scaling. At maximum, every
        // selected feature texel is copied exactly at an integer translation.
        // Clear the fixed bangs vertically before the slight lateral follow.
        // A diagonal subpixel path would temporarily blend eye ink onto four
        // observed bang-edge texels even though its final endpoint is clear.
        var vertical=Math.Clamp(fraction*2,0,1);vertical=vertical*vertical*(3-2*vertical);
        var lateral=Math.Clamp(fraction*2-1,0,1);lateral=lateral*lateral*(3-2*lateral);
        CompositeFeature(source,result,Eye,-lateral,2*vertical);
        CompositeFeature(source,result,Mouth,-lateral,2*vertical);
        // Interior sampling must not overwrite the accepted diagonal connectors.
        // Preserve the complete geometric boundary collar, not a colour threshold.
        var approved=CheekPullRenderer.Render(source,amount);
        var outline=CheekPullRenderer.Rows.Select(r=>(X:r.Edge-(int)Math.Round(amount*.5*r.Weight),Y:r.Y)).ToArray();
        for(var y=53;y<=64;y++)for(var x=0;x<35;x++)
        {
            var distance=double.PositiveInfinity;
            for(var j=1;j<outline.Length;j++)
            {
                var a=outline[j-1];var b=outline[j];var dx=b.X-a.X;var dy=b.Y-a.Y;
                var u=Math.Clamp(((x-a.X)*dx+(y-a.Y)*dy)/(double)(dx*dx+dy*dy),0,1);
                distance=Math.Min(distance,Math.Sqrt(Math.Pow(x-a.X-u*dx,2)+Math.Pow(y-a.Y-u*dy,2)));
            }
            if(distance<=1.5)Copy(approved,x,y,result,x,y);
        }
        return result;
    }

    static void CompositeFeature(byte[] source,byte[] result,(int Y,int Left,int Right)[] rows,double dx,double dy)
    {
        var coverage=new double[96*96];var colour=new double[96*96*4];
        foreach(var row in rows)for(var x=row.Left;x<=row.Right;x++)
        {
            var tx=x+dx;var ty=row.Y+dy;var left=(int)Math.Floor(tx);var top=(int)Math.Floor(ty);
            for(var yy=top;yy<=top+1;yy++)for(var xx=left;xx<=left+1;xx++)
            {
                var weight=(1-Math.Abs(tx-xx))*(1-Math.Abs(ty-yy));
                if(weight<=0||xx<0||xx>=96||yy<0||yy>=96)continue;
                var i=yy*96+xx;coverage[i]+=weight;
                for(var c=0;c<4;c++)colour[i*4+c]+=source[(row.Y*96+x)*4+c]*weight;
            }
        }
        for(var i=0;i<coverage.Length;i++)if(coverage[i]>0)
        {
            var a=Math.Clamp(coverage[i],0,1);
            for(var c=0;c<4;c++)result[i*4+c]=(byte)Math.Clamp(Math.Round(colour[i*4+c]+result[i*4+c]*(1-a)),0,255);
        }
    }
    static void Copy(byte[] source,int sx,int sy,byte[] target,int x,int y)=>Array.Copy(source,(sy*96+sx)*4,target,(y*96+x)*4,4);
}
