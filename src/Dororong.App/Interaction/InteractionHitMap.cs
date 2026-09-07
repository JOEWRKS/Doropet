using Dororong.Core.Geometry;

namespace Dororong.App.Interaction;

// Input ownership only. Do not reuse these enlarged targets as deformation masks.
internal static class InteractionHitMap
{
    // Authored from the user's red outline on the unchanged96px canonical grid.
    // Includes the selected eye and mouth-side cheek, but stops before the body.
    private static readonly PointD[] SelectedCheek =
    [
        new(9,48),new(12,46),new(23,44),new(27,45),new(32,49),new(35,53),
        new(35,59),new(32,64),new(28,66),new(20,66),new(13,64),new(9,62),
        new(7,57),new(7,53)
    ];

    internal static bool InSource(PointD p) => double.IsFinite(p.X) && double.IsFinite(p.Y) &&
        p.X >= 0 && p.Y >= 0 && p.X < 96 && p.Y < 96;

    internal static bool IsSelectedCheek(PointD p) => InSource(p) &&
        BodyRegionMap.Inside(SelectedCheek,(int)p.X+.5,(int)p.Y+.5);

    internal static bool IsUpperHead(PointD p) => InSource(p) &&
        p.X >= 6 && p.X < 64 && p.Y < 48 && !IsSelectedCheek(p);

    internal static BodyRegion PickBody(PointD point,ReadOnlySpan<byte> pixels)
    {
        var exact=BodyRegionMap.Pick(point,pixels);
        if(exact!=BodyRegion.None)return exact;
        if(!InSource(point)||pixels.Length!=96*96*4||point.Y<66)return BodyRegion.None;
        var x=(int)point.X;var y=(int)point.Y;var i=(y*96+x)*4;
        if(pixels[i+3]==0||BodyRegionMap.IsExcludedConnector(x,y))return BodyRegion.None;
        // A few visible white proximal-body texels are inside the renderer's
        // conservative foreground lock. Admit only nearby neutral-white material;
        // pink hair, grey seams, face, and the excluded connector remain excluded.
        var low=Math.Min(pixels[i],Math.Min(pixels[i+1],pixels[i+2]));
        var high=Math.Max(pixels[i],Math.Max(pixels[i+1],pixels[i+2]));
        if(low<225||high-low>20)return BodyRegion.None;
        var best=17;var owner=BodyRegion.None;
        for(var dy=-4;dy<=4;dy++)for(var dx=-4;dx<=4;dx++)
        {
            var distance=dx*dx+dy*dy;
            if(distance>=best)continue;
            var candidate=BodyRegionMap.Pick(new(x+dx,y+dy),pixels);
            if(candidate==BodyRegion.None)continue;
            best=distance;owner=candidate;
        }
        return owner;
    }
}
