using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CheekPullProof;

internal static class Program
{
    private const int Size = 96;
    private const string SourceSha = "699348D1973709F228D843341AC5312AA7F449D57B9BFC76576256231E259A78";

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Usage: CheekPullProof <repository> <new-proof-directory>");
        var repo = Path.GetFullPath(args[0]);
        var output = Path.GetFullPath(args[1]);
        var allowed = Path.GetFullPath(Path.Combine(repo, "artifacts/repro/head-distance-cheek-20260906-attempt-1/cheek-proof-"));
        if (!output.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) || Directory.Exists(output))
            throw new ArgumentException("Output must be a new cheek-proof-* directory in the authorized attempt folder.");
        Directory.CreateDirectory(output);
        var path = Path.Combine(repo, "src/Dororong.App/Assets/dororong-canonical.png");
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        if (hash != SourceSha) throw new InvalidOperationException("Canonical source identity mismatch.");
        var decoder = BitmapDecoder.Create(new Uri(path), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var bitmap = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
        if (bitmap.PixelWidth != Size || bitmap.PixelHeight != Size) throw new InvalidOperationException("Source size mismatch.");
        var source = new byte[Size * Size * 4];
        bitmap.CopyPixels(source, Size * 4, 0);
        var cases = new (string Name, double Outward, double Vertical, double Release, bool Cancel)[]
        {
            ("rest",0,0,0,false), ("small-outward4dip",4,0,0,false),
            ("half-outward10dip",10,0,0,false), ("max-outward20dip",20,0,0,false),
            ("inward-minus10dip",-10,0,0,false), ("vertical20dip",20,20,0,false),
            ("release055ms",20,0,55,false), ("release110ms",20,0,110,false),
            ("release165ms",20,0,165,false), ("release220ms",20,0,220,false),
            ("cancel",20,10,0,true)
        };
        var frames = cases.ToDictionary(c => c.Name,
            c => LocalCheekRenderer.Render(source, c.Outward, c.Vertical, c.Release, c.Cancel));
        foreach (var pair in frames)
        {
            Save(Path.Combine(output, pair.Key + "-native96.png"), pair.Value, Size, Size);
            Save(Path.Combine(output, pair.Key + "-nearest8x.png"), Enlarge(pair.Value, Size, Size, 8), Size * 8, Size * 8);
        }

        var checks = new List<Check>();
        void Check(string name, bool passed, string observed) => checks.Add(new(name, passed, observed));
        var rest = frames["rest"]; var max = frames["max-outward20dip"];
        Check("neutral-exact-restore", source.SequenceEqual(rest), $"changed={Changed(source, rest)}");
        Check("release220-exact-restore", source.SequenceEqual(frames["release220ms"]), $"changed={Changed(source, frames["release220ms"])}");
        Check("cancel-exact-restore", source.SequenceEqual(frames["cancel"]), $"changed={Changed(source, frames["cancel"])}");
        Check("outward-transports-original-skin-texels", Changed(source, max) >= 8 &&
            Pixel(max, 16, 58).SequenceEqual(Pixel(source, 19, 58)),
            $"changed={Changed(source, max)}; destination(16,58)={Hex(max,16,58)}; required source(19,58)={Hex(source,19,58)}");
        Check("small-half-max-have-distinct-raster-states", Changed(source, frames["small-outward4dip"]) > 0 &&
            Changed(frames["small-outward4dip"], frames["half-outward10dip"]) > 0 && Changed(frames["half-outward10dip"], max) > 0,
            $"smallChanges={Changed(source,frames["small-outward4dip"])}; halfToMax={Changed(frames["half-outward10dip"],max)}");
        Check("inward-compresses-skin", Changed(source,frames["inward-minus10dip"]) > 0 &&
            !frames["inward-minus10dip"].SequenceEqual(max), $"changed={Changed(source,frames["inward-minus10dip"])}");
        Check("release-intermediate-differs", !frames["release055ms"].SequenceEqual(max) &&
            !frames["release110ms"].SequenceEqual(source), $"055toMax={Changed(frames["release055ms"],max)}; 110toRest={Changed(frames["release110ms"],source)}");
        Check("clamp-at20dip", max.SequenceEqual(LocalCheekRenderer.Render(source,200)), "Compared output at 20 and 200 outward DIPs.");
        Check("vertical-input-is-bounded-and-damped", frames["vertical20dip"].SequenceEqual(LocalCheekRenderer.Render(source,20,200)) &&
            !frames["vertical20dip"].SequenceEqual(max), $"verticalChanges={Changed(max,frames["vertical20dip"])}");
        // Independent source-coordinate fixtures: these do not use the renderer's selected mask.
        var fixtures = new Dictionary<string,(int X1,int Y1,int X2,int Y2)[]>
        {
            ["eyes"] = [(18,46,26,54),(20,55,24,55),(21,56,23,56),(35,48,43,58)],
            ["mouth"] = [(25,57,33,60)],
            ["hair"] = [(8,48,12,60),(13,60,14,60),(13,61,15,61),(12,62,17,62),(12,63,20,63),(11,64,19,69),(10,26,55,45)],
            ["ornaments"] = [(52,28,68,45)],
            ["opposite-cheek"] = [(35,60,41,64)],
            ["body"] = [(0,70,95,95)],
            ["lower-face-outline"] = [(20,64,24,64),(20,65,40,65)]
        };
        foreach (var fixture in fixtures)
        {
            var changes = frames.Values.Sum(frame => fixture.Value.Sum(rect => RectChanges(source,frame,rect)));
            Check("protected-"+fixture.Key, changes==0, $"changed={changes}; rectangles={JsonSerializer.Serialize(fixture.Value.Select(r=>new[]{r.X1,r.Y1,r.X2,r.Y2}))}");
        }
        var outsideChanges = frames.Values.Sum(frame => Enumerable.Range(0,Size*Size).Count(i =>
            !LocalCheekRenderer.Selected(i%Size,i/Size) && !Pixel(source,i%Size,i/Size).SequenceEqual(Pixel(frame,i%Size,i/Size))));
        Check("entire-mask-complement-exact", outsideChanges==0,$"changed={outsideChanges}");
        var sourceTexels = LocalCheekRenderer.Rows.SelectMany(row => Enumerable.Range(row.Left,row.Right-row.Left+1).Select(x=>Hex(source,x,row.Y))).ToHashSet();
        Check("every-edited-pixel-is-an-original-selected-texel", frames.Values.All(frame=>Enumerable.Range(0,Size*Size).All(i=>
            Pixel(source,i%Size,i/Size).SequenceEqual(Pixel(frame,i%Size,i/Size)) || sourceTexels.Contains(Hex(frame,i%Size,i/Size)))), "Exact BGRA membership; no recoloring, blending or generated fill.");
        var alphaChanges = frames.Values.Sum(frame=>Enumerable.Range(0,Size*Size).Count(i=>source[i*4+3]!=frame[i*4+3]));
        Check("no-alpha-holes-or-detached-alpha-islands", alphaChanges==0,$"alphaChanges={alphaChanges}; alpha topology is identical to source.");
        var mask = Enumerable.Range(0,Size*Size).Where(i=>LocalCheekRenderer.Selected(i%Size,i/Size)).ToHashSet();
        var transparentAdjacency = mask.Sum(i=>Neighbors(i).Count(n=>source[n*4+3]==0));
        var visibleExtension = Enumerable.Range(0,Size*Size).Count(i=>source[i*4+3]==0 && max[i*4+3]>0);
        Check("visible-connected-exterior-cheek-displacement", visibleExtension>=3,
            $"newExteriorPixels={visibleExtension}; selectedSkinTransparentAdjacencies={transparentAdjacency}; changed interior texels do not prove stretching.");
        ExportMask(output,source,mask);
        var sheet = new byte[Size*4 * Size*cases.Length];
        for(var k=0;k<cases.Length;k++) for(var y=0;y<Size;y++)
            Array.Copy(frames[cases[k].Name],y*Size*4,sheet,(y*Size*cases.Length+k*Size)*4,Size*4);
        Save(Path.Combine(output,"contact-sheet-native.png"),sheet,Size*cases.Length,Size);
        Save(Path.Combine(output,"contact-sheet-nearest4x.png"),Enlarge(sheet,Size*cases.Length,Size,4),Size*cases.Length*4,Size*4);
        File.WriteAllText(Path.Combine(output,"checks.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
        File.WriteAllText(Path.Combine(output,"cases.json"),JsonSerializer.Serialize(cases.Select(c=>new{c.Name,c.Outward,c.Vertical,c.Release,c.Cancel}),new JsonSerializerOptions{WriteIndented=true}));
        var log=string.Join(Environment.NewLine,checks.Select(c=>$"{(c.Passed?"PASS":"FAIL")} {c.Name}: {c.Observed}"));
        File.WriteAllText(Path.Combine(output,"self-check.log"),$"Source SHA256 {hash}\n{log}\n");
        Console.WriteLine(log);
        Console.WriteLine($"Artifact directory: {output}");
        return checks.All(c=>c.Passed)?0:2;
    }

    private static IEnumerable<int> Neighbors(int i)
    {
        if(i%Size>0)yield return i-1;if(i%Size<Size-1)yield return i+1;
        if(i>=Size)yield return i-Size;if(i<Size*(Size-1))yield return i+Size;
    }
    private static void ExportMask(string output,byte[] source,HashSet<int> mask)
    {
        var overlay=(byte[])source.Clone();var selected=new byte[source.Length];var locked=(byte[])source.Clone();
        var csv=new StringBuilder("x,y,bgraHex\n");
        foreach(var i in mask)
        {
            Array.Copy(source,i*4,selected,i*4,4);Array.Clear(locked,i*4,4);
            overlay[i*4]=255;overlay[i*4+1]=220;overlay[i*4+2]=0;overlay[i*4+3]=255;
            csv.AppendLine($"{i%Size},{i/Size},{Hex(source,i%Size,i/Size)}");
        }
        foreach(var (name,data) in new[]{("mask-overlay",overlay),("selected-source-texels",selected),("locked-source-texels",locked)})
        {
            Save(Path.Combine(output,name+"-native96.png"),data,Size,Size);
            Save(Path.Combine(output,name+"-nearest8x.png"),Enlarge(data,Size,Size,8),Size*8,Size*8);
        }
        File.WriteAllText(Path.Combine(output,"selected-source-texels.csv"),csv.ToString());
    }
    private static ReadOnlySpan<byte> Pixel(byte[] pixels,int x,int y)=>pixels.AsSpan((y*Size+x)*4,4);
    private static string Hex(byte[] pixels,int x,int y)=>Convert.ToHexString(Pixel(pixels,x,y));
    private static int Changed(byte[] a,byte[] b)=>Enumerable.Range(0,Size*Size).Count(i=>!Pixel(a,i%Size,i/Size).SequenceEqual(Pixel(b,i%Size,i/Size)));
    private static int RectChanges(byte[] a,byte[] b,(int X1,int Y1,int X2,int Y2) r)=>Enumerable.Range(r.Y1,r.Y2-r.Y1+1).Sum(y=>Enumerable.Range(r.X1,r.X2-r.X1+1).Count(x=>!Pixel(a,x,y).SequenceEqual(Pixel(b,x,y))));
    private static byte[] Enlarge(byte[] input,int width,int height,int factor)
    {
        var output=new byte[width*height*factor*factor*4];
        for(var y=0;y<height*factor;y++)for(var x=0;x<width*factor;x++)Array.Copy(input,((y/factor)*width+x/factor)*4,output,(y*width*factor+x)*4,4);
        return output;
    }
    private static void Save(string path,byte[] pixels,int width,int height)
    {
        var bitmap=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream=new FileStream(path,FileMode.CreateNew);encoder.Save(stream);
    }
    private sealed record Check(string Name,bool Passed,string Observed);
}
