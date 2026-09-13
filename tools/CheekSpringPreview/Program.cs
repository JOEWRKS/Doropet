using Dororong.App.Controls;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class Program
{
    const int Size=160, Pad=32;
    [STAThread]
    static int Main(string[] args)
    {
        var repo=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
        var bitmap=new FormatConvertedBitmap(new BitmapImage(new Uri(Path.Combine(repo,"src/Dororong.App/Assets/dororong-canonical.png"))),PixelFormats.Bgra32,null,0);
        var source=new byte[96*96*4];bitmap.CopyPixels(source,384,0);
        byte[] Render(double pull,bool larger,double gain=1.1)
        {
            var image=Expand(OutlineCheekRenderer.Render(source,pull,Math.Max(0,pull),Math.Max(0,pull)),pull,larger,true,gain);
            if(larger)CheekContour.Refine(image,source,pull,Size,Pad,gain);
            return image;
        }
        var oldMax=Render(20,false);var newMax=Render(20,true);
        if(args.Contains("--verify"))
        {
            void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
            double Left(byte[] pixels,int y){for(var x=0;x<Size;x++)if(pixels[(y*Size+x)*4+3]>=128)return x;return Size;}
            // A tip ten pixels outside the fixed x12 root reached x-2 in v2
            // (14px long). Half of the added14px remains:21px reaches x-9.
            var tipFixture=new byte[96*96*4];tipFixture[(58*96+2)*4+3]=255;
            Check(Expand(tipFixture,20,true)[((Pad+58)*Size+Pad-9)*4+3]>=250,
                "maximum reach retains half of the v3 added length with fixed root");
            Check(Left(newMax,Pad+58)>=Pad-10&&Left(newMax,Pad+58)<=Pad-8,
                "refined outline matches the shortened fill tip");
            Check(Left(newMax,Pad+58)<=Left(oldMax,Pad+58)-3,"maximum cheek extends at least3more source pixels");
            Check(Render(0,true).SequenceEqual(Render(0,false)),"rest is byte-identical");
            var rigFixture=new byte[Size*Size*4];
            rigFixture[((Pad+40)*Size+Pad+20)*4+3]=255;
            rigFixture[((Pad+76)*Size+Pad+40)*4+3]=255;
            rigFixture[((Pad+90)*Size+Pad+40)*4+3]=255;
            var rigHeld=CheekReaction.Render(rigFixture,20,Size,Pad);
            Check(rigHeld[((Pad+41)*Size+Pad+18)*4+3]==255,
                "held head follows pull by two pixels and lowers one pixel without scaling");
            Check(rigHeld[((Pad+76)*Size+Pad+39)*4+3]>100&&rigHeld[((Pad+77)*Size+Pad+39)*4+3]>100,
                "mid torso compresses downward while upper body leans toward the pull");
            var rigRecoil=CheekReaction.Render(rigFixture,-5,Size,Pad);
            double headMass=0,headX=0;
            for(var y=Pad+35;y<Pad+46;y++)for(var x=Pad+14;x<Pad+28;x++)
            {var a=rigRecoil[(y*Size+x)*4+3];headMass+=a;headX+=(x-Pad)*a;}
            Check(Math.Abs(headX/headMass-21.5)<.02,"first recoil pushes head opposite the pull");
            Check(CheekReaction.Render(newMax,0,Size,Pad).SequenceEqual(newMax),"reaction zero restores exact input");
            var reactedMax=CheekReaction.Render(newMax,20,Size,Pad);
            for(var y=43;y<=61;y++)for(var x=14;x<46;x++)for(var c=0;c<4;c++)
                CheckSilent(reactedMax[((y+Pad)*Size+x+Pad)*4+c]==newMax[((y-1+Pad)*Size+x+2+Pad)*4+c],
                    "eyes and cheek head region translate rigidly with no new eye expression");
            for(double p=-10;p<=20;p+=.25)
            {
                var rigInput=Render(p,true);var reacted=CheekReaction.Render(rigInput,p,Size,Pad);
                for(var y=88+Pad;y<Size;y++)for(var x=0;x<Size;x++)for(var c=0;c<4;c++)
                    CheckSilent(reacted[(y*Size+x)*4+c]==rigInput[(y*Size+x)*4+c],"grounded foot rows move during reaction");
                for(var i=0;i<Size;i++)CheckSilent(reacted[i*4+3]==0&&reacted[((Size-1)*Size+i)*4+3]==0&&reacted[(i*Size)*4+3]==0&&reacted[(i*Size+Size-1)*4+3]==0,"reaction clips padding");
            }
            Console.WriteLine("PASS121 reaction poses: grounded feet and clear padding; rigid open-eye head");
            // At maximum reach the tip is near x-9. Its dark ink must remain
            // a narrow contour, not the several-pixel horizontal raster smear.
            var tipRow=Enumerable.Range(Pad-15,16).Select(x=>newMax[(((Pad+58)*Size+x)*4)..(((Pad+58)*Size+x+1)*4)]).ToArray();
            var darkTip=tipRow.Count(c=>c[3]>100&&c[2]<170);
            Check(darkTip>=1&&darkTip<=2,
                "extended tip retains one to two dark outline pixels across its normal");
            Check(newMax[((Pad+58)*Size+Pad-5)*4+2]>210&&newMax[((Pad+58)*Size+Pad-5)*4+3]>240,
                "tip interior is opaque skin instead of stretched dark ink");
            for(double p=0;p<=20;p+=.25)
            {
                var enlarged=Render(p,true);var approved=Render(p,false);
                var raster=Expand(OutlineCheekRenderer.Render(source,p,p,p),p,true);
                for(var x=0;x<12;x++)for(var c=0;c<4;c++)
                    CheckSilent(enlarged[((64+Pad)*Size+x+Pad)*4+c]==raster[((64+Pad)*Size+x+Pad)*4+c],
                        "lower hair attachment row is preserved by the contour pass");
                // Horizontal extension ends at x12; the cheek root retains its
                // original rows instead of swelling vertically into a vase.
                for(var y=47;y<71;y++)for(var x=12;x<16;x++)for(var c=0;c<4;c++)
                    CheckSilent(enlarged[((y+Pad)*Size+x+Pad)*4+c]==approved[((y+Pad)*Size+x+Pad)*4+c],"cheek root keeps original shape without vertical inflation");
                for(var y=43;y<61;y++)for(var x=16;x<30;x++)for(var c=0;c<4;c++)
                    CheckSilent(enlarged[((y+Pad)*Size+x+Pad)*4+c]==approved[((y+Pad)*Size+x+Pad)*4+c],"whole selected eye must remain unchanged by enlargement");
            }
            Console.WriteLine("PASS selected-eye preservation through81positive poses");
            var rebound=Render(-6,true);var inward=Render(-6,false);var moved=false;
            for(var y=49;y<56;y++)for(var x=22;x<27;x++)for(var c=0;c<4;c++)
                moved|=rebound[((y+Pad)*Size+x+Pad)*4+c]!=inward[((y+Pad)*Size+x+Pad)*4+c];
            Check(moved,"selected eye participates in negative spring follow");
            var compressed=Expand(OutlineCheekRenderer.Render(source,-6,0,0),-6,true,false);
            // At(13,62), the side-hair field moves right0.09408/up0.04116 .
            // Its inverse sample must consume the compressed cheek, not the original.
            var weights=new[]{(X:12,Y:62,W:.09408*.95884),(X:13,Y:62,W:.90592*.95884),
                (X:12,Y:63,W:.09408*.04116),(X:13,Y:63,W:.90592*.04116)};
            var alpha=weights.Sum(q=>compressed[((q.Y+Pad)*Size+q.X+Pad)*4+3]/255d*q.W);
            for(var c=0;c<4;c++)
            {
                var expected=c==3?alpha*255:weights.Sum(q=>compressed[((q.Y+Pad)*Size+q.X+Pad)*4+c]*compressed[((q.Y+Pad)*Size+q.X+Pad)*4+3]/255d*q.W)/alpha;
                CheckSilent(rebound[((62+Pad)*Size+13+Pad)*4+c]==(byte)Math.Round(expected),"negative follow must sample already compressed cheek");
            }
            Console.WriteLine("PASS negative hair/cheek composition at overlapping contour");
            for(double p=-10;p<=20;p+=.25)
            {
                var image=Render(p,true);
                for(var i=0;i<Size;i++)CheckSilent(image[i*4+3]==0&&image[((Size-1)*Size+i)*4+3]==0&&image[(i*Size)*4+3]==0&&image[(i*Size+Size-1)*4+3]==0,"clipped boundary");
                for(var y=0;y<96;y++)for(var x=0;x<96;x++)if(y>=72||x>=55)
                    for(var c=0;c<4;c++)CheckSilent(image[((y+Pad)*Size+x+Pad)*4+c]==source[(y*96+x)*4+c],"body/ribbon modified");
            }
            Console.WriteLine("PASS121signed poses: padding clear; body and ribbon exact");return 0;
        }
        var output=Path.GetFullPath(args.Single());Directory.CreateDirectory(output);
        for(var mode=0;mode<2;mode++)
        {
            var atlas=new byte[Size*Size*121*4];
            for(var i=0;i<121;i++)
            {
                var pull=-10+i*.25;
                var frame=Render(pull,true);
                if(mode==1)frame=CheekReaction.Render(frame,pull,Size,Pad);
                Array.Copy(frame,0,atlas,i*Size*Size*4,Size*Size*4);
            }
            Save(Path.Combine(output,mode==0?"old.png":"new.png"),atlas,Size,Size*121);
        }
        Save(Path.Combine(output,"comparison.png"),Pair(newMax,CheekReaction.Render(newMax,20,Size,Pad)),Size*2,Size);
        var recoil=Render(-5.4,true);
        Save(Path.Combine(output,"recoil.png"),Pair(recoil,CheekReaction.Render(recoil,-5.4,Size,Pad)),Size*2,Size);
        Console.WriteLine("Exported canonical-based old/new121pose banks to "+output);return 0;
    }
    static void CheckSilent(bool ok,string message){if(!ok)throw new Exception(message);}
    static byte[] Expand(byte[] input,double pull,bool larger,bool secondary=true,double outwardGain=1.1)
    {
        var result=new byte[Size*Size*4];
        for(var y=0;y<96;y++)Array.Copy(input,y*384,result,((y+Pad)*Size+Pad)*4,384);
        if(!larger||pull==0)return result;
        var amount=Math.Clamp(pull/20,-.5,1);
        for(var y=47;y<71;y++)for(var x=-Pad+1;x<12;x++)
        {
            var vertical=Smooth((y-47)/6d)*Smooth((71-y)/7d);
            // Retain half the added v3 reach (1.4 -> 2.1), with
            // unchanged vertical profile and unchanged inward recoil shape.
            var gain=pull>0?outwardGain:.4;
            var sx=12+(x-12)/(1+gain*amount*vertical);
            Sample(input,sx,y,result,((y+Pad)*Size+x+Pad)*4);
        }
        // Negative spring lobe gently reverses the existing secondary follow.
        // Positive poses use the actual product's eye/hair response unchanged.
        if(pull<0&&secondary)
        {
            var cheekStage=(byte[])result.Clone();
            for(var y=23;y<71;y++)for(var x=2;x<54;x++)
            {
                var near=Bump(x,14,17,28,32)*Bump(y,42,45,56,57);
                var far=Bump(x,33,34,43,47)*Bump(y,43,47,58,63);
                var top=Bump(x,5,14,42,50)*Bump(y,23,29,36,47);
                var left=Bump(x,3,7,10,16)*Bump(y,30,41,59,69);
                var right=Bump(x,44,48,50,54)*Bump(y,44,48,60,70);
                var hair=Math.Max(top,Math.Max(left,right));if(x>=50&&y<44)hair=0;
                var dx=amount*(-near-.6*far-.8*hair);
                var dy=amount*(2*near+.65*far+.35*hair);
                if(dx==0&&dy==0)continue;
                Sample(cheekStage,x+Pad-dx,y+Pad-dy,result,((y+Pad)*Size+x+Pad)*4,Size);
            }
        }
        return result;
    }
    static double Smooth(double t){t=Math.Clamp(t,0,1);return t*t*(3-2*t);}
    static double Bump(double x,double a,double b,double c,double d)=>Smooth((x-a)/(b-a))*Smooth((d-x)/(d-c));
    static void Sample(byte[] pixels,double x,double y,byte[] output,int at,int size=96)
    {
        var left=(int)Math.Floor(x);var top=(int)Math.Floor(y);double alpha=0,b=0,g=0,r=0;
        for(var yy=top;yy<=top+1;yy++)for(var xx=left;xx<=left+1;xx++)
        {
            if(xx<0||xx>=size||yy<0||yy>=size)continue;
            var i=(yy*size+xx)*4;var a=pixels[i+3]/255d*(1-Math.Abs(x-xx))*(1-Math.Abs(y-yy));
            alpha+=a;b+=pixels[i]*a;g+=pixels[i+1]*a;r+=pixels[i+2]*a;
        }
        output[at]=(byte)Math.Clamp(Math.Round(alpha>0?b/alpha:0),0,255);
        output[at+1]=(byte)Math.Clamp(Math.Round(alpha>0?g/alpha:0),0,255);
        output[at+2]=(byte)Math.Clamp(Math.Round(alpha>0?r/alpha:0),0,255);
        output[at+3]=(byte)Math.Clamp(Math.Round(alpha*255),0,255);
    }
    static byte[] Pair(byte[] a,byte[] b){var result=new byte[Size*Size*8];for(var y=0;y<Size;y++){Array.Copy(a,y*Size*4,result,y*Size*8,Size*4);Array.Copy(b,y*Size*4,result,y*Size*8+Size*4,Size*4);}return result;}
    static void Save(string path,byte[] pixels,int width,int height){var b=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4);var e=new PngBitmapEncoder();e.Frames.Add(BitmapFrame.Create(b));using var f=File.Create(path);e.Save(f);}
}
