using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
class Program
{
    [STAThread] static void Main(string[] args)
    {
        _ = new Application();
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
        {
            dc.DrawRectangle(args.Length > 1 ? Brushes.Black : Brushes.White,null,new Rect(0,0,1500,600));
            for(var side=0;side<2;side++)for(var step=0;step<5;step++)
            {
                var paw=step switch {0=>new PerchPawSnapshot(side==1,default),1=>new(side==1,new(0,18)),2=>new(side==1,new(-14,5)),3=>new(side==1,default,20),_=>new(side==1,default,-20)};
                dc.DrawImage(PerchPawRenderer.Render(PerchExpressionFrames.Open,paw),new Rect(step*300,side*300,300,300));
            }
        }
        var target=new RenderTargetBitmap(1500,600,96,96,PixelFormats.Pbgra32);target.Render(visual);
        var path=Path.GetFullPath(args[0]);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(target));using var stream=File.Create(path);png.Save(stream);
    }
}
