using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        var output=Path.GetFullPath(args.Single());Directory.CreateDirectory(output);
        var renderer=new HuntRenderer();
        foreach(var frame in new[]{0,15,28,42,60,99,123,140,156})foreach(var degrees in new[]{-20,0,20})
        {
            var gaze=new HuntGazePose(0,degrees<0?.65:degrees>0?-.65:0,degrees*Math.PI/180);
            var name=$"frame{frame}-roll{degrees}";
            var plain=renderer.Render(frame,gaze);Save(plain,Path.Combine(output,name+".png"));
            var before=renderer.Render(frame,gaze,false,20,20,20);
            var input=PremultipliedFrame.From(before);
            var transform=HuntRenderer.HeadTransform(frame,gaze);
            var pin=transform.Transform(new Point(12,57));
            var maskSource=new HuntEyes().Paint(gaze.EyeX,gaze.EyeY,false,20,20,20);
            var drawing=new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(drawing,BitmapScalingMode.Linear);
            var doubled=transform;doubled.Scale(2,2);doubled.Translate(32,32);
            using(var dc=drawing.RenderOpen())
            {dc.PushTransform(new MatrixTransform(doubled));dc.DrawImage(maskSource,new Rect(0,0,96,96));dc.Pop();}
            var largeMask=new RenderTargetBitmap(256,256,96,96,PixelFormats.Pbgra32);largeMask.Render(drawing);
            using(var dc=drawing.RenderOpen())dc.DrawImage(largeMask,new Rect(-16,-16,128,128));
            var mask=new RenderTargetBitmap(96,96,96,96,PixelFormats.Pbgra32);mask.Render(drawing);
            var maskPixels=new byte[96*96*4];mask.CopyPixels(maskPixels,384,0);
            if(frame==0&&degrees is -20 or 0)
            {
                BitmapSource Native(BitmapSource layer)
                {
                    var visual=new DrawingVisual();using(var dc=visual.RenderOpen())dc.DrawImage(layer,new Rect(-16,-16,128,128));
                    var target=new RenderTargetBitmap(96,96,96,96,PixelFormats.Pbgra32);target.Render(visual);return target;
                }
                var joins=(BitmapSource)typeof(HuntRenderer).GetField("_maskedJoins",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(renderer)!;
                foreach(var (layerName,layer) in new[]{("head",(BitmapSource)mask),("body",Native(HuntFrames.Instance.Body(frame))),("joins",Native(joins)),("final",plain)})
                {
                    var data=new byte[96*96*4];layer.CopyPixels(data,384,0);
                    Console.WriteLine($"{name} {layerName} alpha at neck(54,60)={data[(60*96+54)*4+3]}");
                    Save(layer,Path.Combine(output,name+"-layer-"+layerName+".png"));
                }
            }
            var maximum=0;
            for(var phase=0;phase<16;phase++)
            {
                // Reflection preserves the diagnosis mode against the old installed
                // assembly, whose renderer has no pose-aware readiness argument.
                var method=typeof(HuntRenderer).GetMethod("Render",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
                var after=method.GetParameters().Length==7
                    ? PremultipliedFrame.From((BitmapSource)method.Invoke(renderer,new object[]{frame,gaze,false,20d,20d,20d,phase/16d})!).Pixels
                    : ForelegFlutterFrame.Render(input,false,phase/16d,pin);
                var changed=0;
                for(var i=0;i<after.Length;i+=4)
                    if(maskPixels[i+3]==255&&input.Pixels[i+3]==255&&Enumerable.Range(0,4).Any(c=>Math.Abs(after[i+c]-input.Pixels[i+c])>16))changed++;
                maximum=Math.Max(maximum,changed);
                if(phase==4)
                {
                    Save(before,Path.Combine(output,name+"-cheek.png"));
                    Save(BitmapSource.Create(96,96,96,96,PixelFormats.Pbgra32,null,after,384),Path.Combine(output,name+"-flutter.png"));
                }
            }
            Console.WriteLine($"{name}: maximum changed opaque head pixels during flutter={maximum}");
        }
    }
    static void Save(BitmapSource source,string path)
    {var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));using var file=File.Create(path);encoder.Save(file);}
}
