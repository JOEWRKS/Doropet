using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;

class Program
{
    [STAThread] static void Main(string[] args)
    {
        _ = new Application();
        if(args.Length>1&&args[1]=="idle-audit"){IdleAudit.Run(args[0]);return;}
        var canonical = new BitmapImage(new Uri("pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png"));
        var raw=PremultipliedFrame.From(canonical).Pixels;var cleaned=PremultipliedFrame.From(UprightRumpSource.Standing(false)).Pixels;
        Console.WriteLine("Cleaned texels: "+string.Join(";",Enumerable.Range(0,96*96).Where(i=>!raw.AsSpan(i*4,4).SequenceEqual(cleaned.AsSpan(i*4,4))).Select(i=>$"{i%96},{i/96}")));
        if(args.Length>1 && args[1]=="transition")
        {
            var render=new HuntRenderer();var proof=new DrawingVisual();RenderOptions.SetBitmapScalingMode(proof,BitmapScalingMode.NearestNeighbor);
            using(var dc=proof.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White,null,new Rect(0,0,2304,960));
                for(var row=0;row<3;row++)for(var col=0;col<8;col++)
                {
                    var frame=new[]{0,16,18,20,22,26,150,156}[col];var roll=(row-1)*Math.PI/9;
                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(20,24,30)),null,new Rect(col*288,row*320+232,288,88));
                    dc.DrawImage(render.Render(frame,new(0,0,roll)),new Rect(col*288,row*320+28,288,288));
                    dc.DrawText(new FormattedText($"frame{frame} / roll{roll:F2}",CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),14,Brushes.Black,1),new Point(col*288+8,row*320+4));
                }
            }
            var pathOut=Path.GetFullPath(args[0]);Directory.CreateDirectory(Path.GetDirectoryName(pathOut)!);
            var shot=new RenderTargetBitmap(2304,960,96,96,PixelFormats.Pbgra32);shot.Render(proof);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(shot));using var stream=File.Create(pathOut);encoder.Save(stream);return;
        }
        if(args.Length>1 && args[1]=="breathing")
        {
            foreach(var (name,source) in new[]{("canonical",(BitmapSource)canonical),("locomotion",LocomotionFrames.Walk(0,0))})
            {
                var data=PremultipliedFrame.From(source).Pixels;
                foreach(var (x,y) in new[]{(74,70),(73,71),(74,71)})
                    Console.WriteLine($"{name} fringe {x},{y}: {string.Join(',',data.AsSpan((y*96+x)*4,4).ToArray())}");
            }
            var sheet=new DrawingVisual();using(var dc=sheet.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White,null,new Rect(0,0,1280,440));
                for(var row=0;row<2;row++)
                {
                    var p=new DororongPresenter();
                    var pose=new Dororong.Core.Behavior.PetSnapshot(Dororong.Core.Behavior.PetState.Idle,new(100,100),Dororong.Core.Behavior.FacingDirection.Left,0,false,null);
                    if(row==1)
                    {
                        for(var t=0;t<20;t++)p.RenderDesktop(pose with{State=Dororong.Core.Behavior.PetState.Walk},Dororong.App.Interaction.DirectInteractionSnapshot.None,TimeSpan.FromMilliseconds(16));
                        for(var t=0;t<20;t++)p.RenderDesktop(pose,Dororong.App.Interaction.DirectInteractionSnapshot.None,TimeSpan.FromMilliseconds(16));
                    }
                    for(var n=0;n<8;n++)
                    {
                        p.RenderDesktop(pose with{Phase=n/8d},Dororong.App.Interaction.DirectInteractionSnapshot.None,TimeSpan.Zero);
                        p.Measure(new Size(144,144));p.Arrange(new Rect(0,0,144,144));p.UpdateLayout();
                        var shot=new RenderTargetBitmap(144,144,96,96,PixelFormats.Pbgra32);shot.Render(p);
                        dc.DrawImage(new CroppedBitmap(shot,new Int32Rect(80,60,32,44)),new Rect(n*160,row*220+20,160,220));
                        dc.DrawText(new FormattedText($"{row}:phase{n/8d:F3}",CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),12,Brushes.Black,1),new Point(n*160,row*220));
                    }
                }
            }
            var output=Path.GetFullPath(args[0]);Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            var rendered=new RenderTargetBitmap(1280,460,96,96,PixelFormats.Pbgra32);rendered.Render(sheet);
            var pngOut=new PngBitmapEncoder();pngOut.Frames.Add(BitmapFrame.Create(rendered));using var stream=File.Create(output);pngOut.Save(stream);return;
        }
        var renderer = new HuntRenderer();
        var frames = new (string Name, BitmapSource Image)[] {
            ("Canonical", canonical), ("Locomotion rest", LocomotionFrames.Walk(0,0)),
            ("Walk phase 0", LocomotionFrames.Walk(1,0)), ("Walk phase 8", LocomotionFrames.Walk(1,2.5)),
            ("Tracking neutral", renderer.Render(0,default)), ("Post-pounce neutral", renderer.Render(156,default)),
            ("Tracking up",renderer.Render(0,new(0,0,-.22))), ("Tracking down",renderer.Render(0,new(0,0,.22))) };
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White,null,new Rect(0,0,2304,340));
            for(var n=0;n<frames.Length;n++)
            {
                var (name,frame)=frames[n];
                var pixels=new byte[96*96*4];new FormatConvertedBitmap(frame,PixelFormats.Pbgra32,null,0).CopyPixels(pixels,384,0);
                var points=Enumerable.Range(0,96*96).Where(i=>pixels[i*4+3]>=128).ToArray();
                Console.WriteLine($"{name}: bbox {points.Min(i=>i%96)},{points.Min(i=>i/96)} - {points.Max(i=>i%96)},{points.Max(i=>i/96)}; area {points.Length}");
                if(n is 0 or 4) Console.WriteLine(string.Join("; ",Enumerable.Range(48,27).Select(y=>$"{y}:{Enumerable.Range(65,20).Where(x=>pixels[(y*96+x)*4+3]>=128).DefaultIfEmpty(-1).Max()}")));
                dc.DrawText(new FormattedText(name,CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),16,Brushes.Black,1),new Point(n*288+10,10));
                dc.DrawImage(frame,new Rect(n*288,40,288,288));
                dc.DrawLine(new Pen(Brushes.LightGray,1),new Point(n*288,40+87*3),new Point((n+1)*288,40+87*3));
            }
        }
        var target=new RenderTargetBitmap(2304,340,96,96,PixelFormats.Pbgra32);target.Render(visual);
        var path=Path.GetFullPath(args[0]);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(target));using var file=File.Create(path);png.Save(file);
    }
}
