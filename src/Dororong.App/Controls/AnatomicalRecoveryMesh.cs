using Dororong.Core.Geometry;

namespace Dororong.App.Controls;

// Explicit correspondence: front paw, middle paw, rear paw and their torso
// junctions. Each strip has a root and a contour row; triangles retain the
// original texture and internal strokes. No silhouette is reconstructed.
internal sealed class AnatomicalRecoveryMesh
{
    private static readonly double[] RestX = [0,17,23,28,32,43,49,56,59,66,72,80,96];
    private static readonly double[][] PoseX =
    [
        RestX, RestX, [0,17,24,29,33,43,49,56,59,67,73,80,96],
        [0,18,25,31,35,43,50,56,60,68,74,81,96],
        [0,19,26,32,35,40,47,56,60,68,75,81,96],
        [0,21,29,35,38,45,51,57,61,69,76,82,96],
        [0,24,31,37,40,48,55,59,62,70,76,82,96],
        [0,26,33,39,42,50,56,60,63,70,77,83,96],
        [0,28,35,41,44,51,57,61,64,71,77,83,96]
    ];
    private static readonly double[][] ContourY =
    [
        [74,74,81,75,76,86,77,74,77,83,69,72,72],
        [74,72,81,75,76,86,77,74,73,83,78,72,72],
        [74,68,80,74,75,84,75,75,77,85,80,72,72],
        [72,65,77,73,70,80,74,77,81,86,81,72,72],
        [70,61,72,68,66,72,62,75,82,86,81,72,72],
        [68,58,68,64,62,69,57,75,81,86,81,72,72],
        [66,56,65,59,58,65,55,75,79,86,80,72,72],
        [65,56,65,58,57,65,54,75,79,86,80,72,72],
        [65,56,65,59,57,66,54,75,79,86,80,72,72]
    ];
    private readonly PointD[] from, to;
    private const int Columns = 13;

    internal AnatomicalRecoveryMesh(int source, int target, PointD sourceHead, PointD targetHead,
        PremultipliedFrame sourceFrame, PremultipliedFrame targetFrame)
    {
        if(source is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(source));
        from = Vertices(source,sourceHead,targetHead,sourceFrame);
        to = Vertices(target,targetHead,targetHead,targetFrame);
    }

    private static PointD[] Vertices(int key, PointD head, PointD restHead, PremultipliedFrame frame)
    {
        var result = new PointD[Columns*4];
        for(var i=0;i<Columns;i++)
        {
            var neck=head.Y+20;
            var edge=ContourY[key][i];
            // Refine the authored feature against its own local alpha edge,
            // never another paw or the farthest silhouette in the whole image.
            if(i>0 && i<Columns-2)
            {
                var x=(int)PoseX[key][i]; var best=double.PositiveInfinity;
                var authoredEdge=edge;
                for(var y=Math.Max(1,(int)authoredEdge-3);y<=Math.Min(94,(int)authoredEdge+3);y++)
                {
                    var alpha=frame.Pixels[(y*96+x)*4+3];
                    var next=frame.Pixels[((y+1)*96+x)*4+3];
                    if(alpha<128 || next>=128) continue;
                    var score=Math.Abs(y-authoredEdge);
                    if(score>=best) continue;
                    best=score;edge=y;
                }
            }
            edge=Math.Max(neck+4,edge);
            result[i]=new(PoseX[key][i],neck);
            result[Columns+i]=new(PoseX[key][i],Math.Max(neck+2,edge-9));
            result[Columns*2+i]=new(PoseX[key][i],edge);
            result[Columns*3+i]=new(PoseX[key][i],96);
        }
        return result;
    }

    internal (PointD From, PointD To)?[] Sample(double progress)
    {
        var result=new (PointD,PointD)?[96*96];
        var positions=from.Select((p,i)=>new PointD(p.X+(to[i].X-p.X)*progress,p.Y+(to[i].Y-p.Y)*progress)).ToArray();
        for(var row=0;row<3;row++) for(var col=0;col<Columns-1;col++)
        {
            var a=row*Columns+col;var b=a+1;var c=a+Columns;var d=c+1;
            Draw(a,b,d);Draw(a,d,c);
        }
        return result;

        void Draw(int ia,int ib,int ic)
        {
            var a=positions[ia];var b=positions[ib];var c=positions[ic];
            var determinant=(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
            if(determinant<=.0001) throw new InvalidOperationException("Anatomical recovery triangle folded.");
            var left=Math.Max(0,(int)Math.Floor(Math.Min(a.X,Math.Min(b.X,c.X))));
            var right=Math.Min(95,(int)Math.Ceiling(Math.Max(a.X,Math.Max(b.X,c.X))));
            var top=Math.Max(0,(int)Math.Floor(Math.Min(a.Y,Math.Min(b.Y,c.Y))));
            var bottom=Math.Min(95,(int)Math.Ceiling(Math.Max(a.Y,Math.Max(b.Y,c.Y))));
            for(var y=top;y<=bottom;y++) for(var x=left;x<=right;x++)
            {
                var u=((x-a.X)*(c.Y-a.Y)-(y-a.Y)*(c.X-a.X))/determinant;
                var v=((b.X-a.X)*(y-a.Y)-(b.Y-a.Y)*(x-a.X))/determinant;
                if(u < -1e-8 || v < -1e-8 || u+v > 1+1e-8) continue;
                PointD Interpolate(PointD[] p)=>new(p[ia].X+(p[ib].X-p[ia].X)*u+(p[ic].X-p[ia].X)*v,
                    p[ia].Y+(p[ib].Y-p[ia].Y)*u+(p[ic].Y-p[ia].Y)*v);
                result[y*96+x]=(Interpolate(from),Interpolate(to));
            }
        }
    }
}
