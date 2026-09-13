using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;

class Program
{
    [STAThread] static void Main(string[] args)
    {
        _=new Application();var p=new DororongPresenter();
        var standing=new PetSnapshot(PetState.Idle,new(100,100),FacingDirection.Left,0,false,null);
        var dt=TimeSpan.FromMilliseconds(16);
        if(args.Length>1 && args[1]=="walk-exit")
        {
            object? Field(string name)=>typeof(DororongPresenter).GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(p);
            standing=standing with {State=PetState.Walk};
            for(var i=0;i<50;i++)
            {
                p.UpdateHunting(standing,DirectInteractionSnapshot.None,new(true,new(340,100)),dt,false);
                p.RenderDesktop(standing,DirectInteractionSnapshot.None,dt);
            }
            var image=(System.Windows.Controls.Image)p.FindName("DororongImage");
            var still=new byte[96*96*4];new HuntRenderer().Render(0,default).CopyPixels(still,384,0);
            var contact=new DrawingVisual();using var ink=contact.RenderOpen();
            ink.DrawRectangle(Brushes.White,null,new Rect(0,0,1680,280));var col=0;
            for(var i=1;i<=60;i++)
            {
                var hold=p.UpdateHunting(standing,DirectInteractionSnapshot.None,PointerSample.Unavailable,dt,false);
                if(!hold)standing=standing with {Position=standing.Position+new Dororong.Core.Geometry.PointD(.4,0)};
                p.RenderDesktop(standing,DirectInteractionSnapshot.None,dt);
                var pixels=new byte[96*96*4];new FormatConvertedBitmap((BitmapSource)image.Source,PixelFormats.Pbgra32,null,0).CopyPixels(pixels,384,0);
                var legsDelta=0;for(var y=75;y<90;y++)for(var x=10;x<70;x++)for(var c=0;c<4;c++)legsDelta+=Math.Abs(pixels[(y*96+x)*4+c]-still[(y*96+x)*4+c]);
                if(i is 1 or 10 or 20 or 30 or 40 or 50 or 60)
                {
                    Console.WriteLine($"{i*16}ms hold={hold} dx={standing.Position.X-100:F1} blend={Field("_huntBlendProgress")} walkingSource={LocomotionFrames.Contains(image.Source)} legsVsStanding={legsDelta}");
                    ink.DrawImage(image.Source,new Rect(col*240,30,240,240));
                    ink.DrawText(new FormattedText($"{i*16}ms",CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),16,Brushes.Black,1),new Point(col*240+10,5));col++;
                }
            }
            ink.Close();
            if(args[0]!="unused")
            {
                var bitmap=new RenderTargetBitmap(1680,280,96,96,PixelFormats.Pbgra32);bitmap.Render(contact);
                var output=Path.GetFullPath(args[0]);Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=File.Create(output);encoder.Save(stream);
            }
            return;
        }
        void Step(PointerSample pointer)
        {
            p.UpdateHunting(standing,DirectInteractionSnapshot.None,pointer,dt,false);
            p.RenderDesktop(standing,DirectInteractionSnapshot.None,dt);
            p.Measure(new Size(144,144));p.Arrange(new Rect(0,0,144,144));p.UpdateLayout();
        }
        var entry=args.Length>1 && args[1]=="entry";
        var draw=new DrawingVisual();using(var dc=draw.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White,null,new Rect(0,0,1920,260));
            if(entry)Step(PointerSample.Unavailable);
            else for(var i=0;i<50;i++)Step(new(true,new(340,100)));
            var elapsed=0;var column=0;
            foreach(var at in entry?new[]{0,16,32,64,96,128,256,640}:new[]{0,16,80,160,320,640,720,800})
            {
                while(elapsed<at){Step(entry?new(true,new(340,100)):PointerSample.Unavailable);elapsed+=16;}
                var frame=new RenderTargetBitmap(144,144,96,96,PixelFormats.Pbgra32);frame.Render(p);
                dc.DrawImage(frame,new Rect(column*240,25,240,240));
                var label=new FormattedText(at==0?entry?"Ordinary":"Tracking":$"{(entry?"Entry":"Exit")} +{at}ms",CultureInfo.InvariantCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),16,Brushes.Black,1);
                dc.DrawText(label,new Point(column++*240+40,12));
            }
        }
        var target=new RenderTargetBitmap(1920,260,96,96,PixelFormats.Pbgra32);target.Render(draw);
        var path=Path.GetFullPath(args[0]);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(target));using var file=File.Create(path);png.Save(file);
    }
}
