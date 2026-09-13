using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var output=Path.GetFullPath(args[0]);Directory.CreateDirectory(output);
        var renderer=new HuntRenderer();var sheet=new DrawingVisual();
        var previous=new byte[96*96*4];renderer.Render(15,new(0,.65,Math.PI/9),true).CopyPixels(previous,384,0);
        var next=new byte[96*96*4];renderer.Render(16,new(0,.65,Math.PI/9),true).CopyPixels(next,384,0);
        var stepDifference=0;
        for(var y=60;y<75;y++)for(var x=10;x<40;x++)for(var c=0;c<4;c++)stepDifference+=Math.Abs(next[(y*96+x)*4+c]-previous[(y*96+x)*4+c]);
        Console.WriteLine($"Crouch entry frame15->16 raised neck absolute channel delta={stepDifference}");
        if(args.Contains("--assert-entry"))
        {
            if(stepDifference/(30d*15*4)>=2)throw new InvalidOperationException($"First crouch step abruptly changes the neck: channel delta {stepDifference}.");
            Console.WriteLine("PASS: first crouch step keeps the raised neck continuous.");return;
        }
        using(var dc=sheet.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White,null,new Rect(0,0,1200,1440));
            var row=0;
            foreach(var frame in new[]{0,20,28,42,60,99})
            {
                var col=0;
                foreach(var degrees in new[]{-20,-10,0,10,20})
                {
                    var gaze=new HuntGazePose(0,.65,degrees*Math.PI/180);
                    var image=renderer.Render(frame,gaze,true);
                    var name=$"frame{frame}-roll{degrees}";
                    Save(image,Path.Combine(output,name+".png"));
                    if(args.Length>1)
                    {
                        var before=new BitmapImage(new Uri(Path.GetFullPath(Path.Combine(args[1],name+".png"))));
                        var oldPixels=new byte[96*96*4];var newPixels=new byte[96*96*4];
                        new FormatConvertedBitmap(before,PixelFormats.Pbgra32,null,0).CopyPixels(oldPixels,384,0);image.CopyPixels(newPixels,384,0);
                        var count=0;var alpha=0;var minX=96;var minY=96;var maxX=0;var maxY=0;
                        for(var i=0;i<newPixels.Length;i+=4)
                        {
                            if(newPixels[i+3]!=oldPixels[i+3])alpha++;
                            if(!Enumerable.Range(0,4).Any(c=>Math.Abs(newPixels[i+c]-oldPixels[i+c])>2))continue;
                            count++;var px=i/4%96;var py=i/4/96;minX=Math.Min(minX,px);minY=Math.Min(minY,py);maxX=Math.Max(maxX,px);maxY=Math.Max(maxY,py);
                        }
                        Console.WriteLine($"{name}: changed>{2}={count} alpha={alpha} bounds={minX},{minY}..{maxX},{maxY}");
                    }
                    dc.DrawImage(image,new Rect(col*240,row*240,240,240));
                    dc.DrawText(new FormattedText(name,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Arial"),14,Brushes.Black,1),new Point(col*240+5,row*240+220));
                    col++;
                }
                row++;
            }
        }
        var bitmap=new RenderTargetBitmap(1200,1440,96,96,PixelFormats.Pbgra32);bitmap.Render(sheet);Save(bitmap,Path.Combine(output,"contact.png"));
        foreach(var (frame, degrees) in new[]{(28,20),(42,0),(28,-20)})
        {
            var gaze=new HuntGazePose(0,.65,degrees*Math.PI/180);
            var final=renderer.Render(frame,gaze,true);
            var front=HuntRenderer.HeadTransform(frame,gaze).Transform(new Point(16,68));var chest=HuntPose.At(frame/60d).Map(17.2,73);
            Console.WriteLine($"frame{frame} roll{degrees}: front={front}, chest={chest}");
            var visual=new DrawingVisual();
            var joins=(BitmapSource)typeof(HuntRenderer).GetField("_maskedJoins",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(renderer)!;
            var head=new HuntEyes().Paint(0,.65,true);
            using(var dc=visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.LightGray,null,new Rect(0,0,1920,480));
                dc.DrawImage(final,new Rect(0,0,480,480));
                dc.PushTransform(new TranslateTransform(480,0));
                dc.DrawImage(joins,new Rect(-80,-80,640,640));dc.Pop();
                dc.PushTransform(new TranslateTransform(960,0));
                dc.DrawImage(HuntFrames.Instance.Body(frame),new Rect(-80,-80,640,640));dc.Pop();
                dc.PushTransform(new TranslateTransform(1440,0));
                var matrix=HuntRenderer.HeadTransform(frame,gaze);matrix.Scale(5,5);
                dc.PushTransform(new MatrixTransform(matrix));dc.DrawImage(head,new Rect(0,0,96,96));dc.Pop();dc.Pop();
            }
            var layers=new RenderTargetBitmap(1920,480,96,96,PixelFormats.Pbgra32);layers.Render(visual);Save(layers,Path.Combine(output,$"layers-frame{frame}-roll{degrees}.png"));
            var data=new byte[96*96*4];final.CopyPixels(data,384,0);
            using(var dc=visual.RenderOpen())
            {dc.PushTransform(new MatrixTransform(HuntRenderer.HeadTransform(frame,gaze)));dc.DrawImage(head,new Rect(0,0,96,96));dc.Pop();}
            var headNative=new RenderTargetBitmap(96,96,96,96,PixelFormats.Pbgra32);headNative.Render(visual);
            var hp=new byte[96*96*4];headNative.CopyPixels(hp,384,0);
            for(var y=65;y<85;y++)for(var x=8;x<55;x++)
            {
                var i=(y*96+x)*4;var alpha=data[i+3];
                if(alpha<128||data[i+2]*255d/alpha<180)continue;
                if(new[]{i-4,i+4,i-384,i+384}.Any(j=>data[j+3]<64))
                    Console.WriteLine($"frame{frame} roll{degrees}: pale boundary {x},{y} BGRa={string.Join(',',data.Skip(i).Take(4))} head={string.Join(',',hp.Skip(i).Take(4))}");
            }
        }
    }
    static void Save(BitmapSource source,string path)
    {var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));using var file=File.Create(path);encoder.Save(file);}
}
