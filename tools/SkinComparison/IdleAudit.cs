using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;

internal static class IdleAudit
{
    internal static void Run(string output)
    {
        Directory.CreateDirectory(output);
        var comparison=new DrawingVisual();RenderOptions.SetBitmapScalingMode(comparison,BitmapScalingMode.NearestNeighbor);
        using(var dc=comparison.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White,null,new Rect(0,0,640,310));
            var sources=new[]{UprightRumpSource.Standing(false),UprightRumpSource.Standing(true),LocomotionFrames.Walk(0,0),LocomotionFrames.Walk(0,0,true)};
            var names=new[]{"Fresh / open","Fresh / closed","After walk / open","After walk / closed"};
            for(var n=0;n<sources.Length;n++)
            {
                dc.DrawText(new FormattedText(names[n],CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),12,Brushes.Black,1),new Point(n*160+3,6));
                dc.DrawImage(new CroppedBitmap(sources[n],new Int32Rect(64,42,18,24)),new Rect(n*160+8,28,144,192));
                dc.DrawImage(sources[n],new Rect(n*160+32,216,96,96));
                var pixels=PremultipliedFrame.From(sources[n]).Pixels;
                Console.WriteLine($"{names[n]} pixel71,47 Pbgra={string.Join(',',pixels.AsSpan((47*96+71)*4,4).ToArray())}");
            }
        }
        var compareImage=new RenderTargetBitmap(640,310,96,96,PixelFormats.Pbgra32);compareImage.Render(comparison);Save(Path.Combine(output,"source-comparison.png"),compareImage);
        var sheet=new DrawingVisual();RenderOptions.SetBitmapScalingMode(sheet,BitmapScalingMode.NearestNeighbor);
        using(var dc=sheet.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White,null,new Rect(0,0,1536,768));
            for(var row=0;row<2;row++)
            {
                var p=new DororongPresenter();var pose=new PetSnapshot(PetState.Idle,new(100,100),FacingDirection.Left,0,false,null);
                var dt=TimeSpan.FromMilliseconds(16);
                if(row==1)
                {
                    for(var i=0;i<20;i++)p.RenderDesktop(pose with{State=PetState.Walk},DirectInteractionSnapshot.None,dt);
                    for(var i=0;i<20;i++)p.RenderDesktop(pose,DirectInteractionSnapshot.None,dt);
                }
                var column=0;var previous="";
                for(var t=0;t<5000;t+=16)
                {
                    p.RenderDesktop(pose with{Phase=(t%4000)/4000d},DirectInteractionSnapshot.None,dt);
                    var src=(BitmapSource)((Image)p.FindName("DororongImage")).Source;
                    var pixels=PremultipliedFrame.From(src).Pixels;
                    var signature=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels));
                    if(signature!=previous)
                    {
                        var samples=string.Join(";",new[]{(71,47),(72,47),(72,48),(73,48),(70,47)}.Select(pt=>$"{pt}:{pixels[(pt.Item2*96+pt.Item1)*4+3]}"));
                        Console.WriteLine($"mode{row} t{t+16} changed source; donor alpha {samples}");previous=signature;
                        Save(Path.Combine(output,$"mode{row}-source-{t+16:D4}.png"),src);
                    }
                    p.Measure(new Size(144,144));p.Arrange(new Rect(0,0,144,144));p.UpdateLayout();
                    var shot=new RenderTargetBitmap(144,144,96,96,PixelFormats.Pbgra32);shot.Render(p);
                    Save(Path.Combine(output,$"mode{row}-frame-{t+16:D4}.png"),shot);
                    if(new[]{16,352,1008,1360,2000,2160,2368,3216}.Contains(t+16))
                    {
                        var crop=new CroppedBitmap(shot,new Int32Rect(82,64,26,32));
                        dc.DrawImage(crop,new Rect(column*192,row*384+24,156,192));
                        dc.DrawImage(shot,new Rect(column*192,row*384+228,144,144));
                        dc.DrawText(new FormattedText($"mode{row} {t+16}ms",CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),13,Brushes.Black,1),new Point(column*192,row*384+3));column++;
                    }
                }
            }
        }
        var result=new RenderTargetBitmap(1536,768,96,96,PixelFormats.Pbgra32);result.Render(sheet);Save(Path.Combine(output,"sheet.png"),result);
    }
    private static void Save(string path,BitmapSource source){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(source));using var file=File.Create(path);encoder.Save(file);}
}
