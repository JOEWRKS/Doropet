using System.Windows.Media.Imaging;
using Dororong.App.Controls;

namespace Dororong.App.Tests.Controls;

public class HangingHeadContourTests
{
    [Theory]
    [InlineData(false,0,0,-1)] [InlineData(true,0,0,-1)]
    [InlineData(false,1,0,-3)] [InlineData(true,1,0,-3)]
    [InlineData(false,2,1,-7)] [InlineData(true,2,1,-7)]
    [InlineData(false,3,2,-11)] [InlineData(true,3,2,-11)]
    [InlineData(false,4,4,-13)] [InlineData(true,4,4,-13)]
    [InlineData(false,5,6,-15)] [InlineData(true,5,6,-15)]
    [InlineData(false,6,8,-15)] [InlineData(true,6,8,-15)]
    [InlineData(false,7,11,-16)] [InlineData(true,7,11,-16)]
    public void Hanging_key_and_dense_bank_share_the_filled_authored_hair_tip(bool dense,int key,int dx,int dy) => CheekProductTests.Sta(() =>
    {
        _ = new DororongPresenter();
        var canonical = new BitmapImage(new Uri("pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png"));
        var bank = new SuppliedBodyDragFrames(PremultipliedFrame.From(canonical));
        var frame = dense ? LayeredPullFrames.Sample(key/7d) : bank.Sample(key/7d);
        var actual = PerchExpressionTests.Pixels(frame);
        var reference = PerchExpressionTests.Pixels(canonical);
        // Independently measured translations for the eight original keys. These texels are
        // inside the ordinary hair tip, not white chest or transparent background.
        foreach (var (x,y) in new[] { (42,68), (42,69) })
        {
            var donor = (y * 96 + x) * 4;
            Assert.Equal(255, reference[donor + 3]);
            Assert.Equal(reference.AsSpan(donor,4).ToArray(),
                actual.AsSpan(((y+dy)*96+x+dx)*4,4).ToArray());
        }
    });

    [Fact]
    public void Repaired_hair_does_not_pop_at_original_key_boundaries() => CheekProductTests.Sta(() =>
    {
        _ = new DororongPresenter();
        var canonical = new BitmapImage(new Uri("pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png"));
        var bank = new SuppliedBodyDragFrames(PremultipliedFrame.From(canonical));
        (int X,int Y)[] positions=[(0,-1),(0,-3),(1,-7),(2,-11),(4,-13),(6,-15),(8,-15),(11,-16)];
        for(var key=0;key<8;key++)
        {
            var middle=PerchExpressionTests.Pixels(bank.Sample(key/7d));
            foreach(var side in new[]{-1,1})
            {
                var near=PerchExpressionTests.Pixels(bank.Sample(Math.Clamp(key/7d+side*1e-6,0,1)));
                foreach(var (x,y) in new[]{(30,66),(42,68),(42,69)})
                {
                    var i=((y+positions[key].Y)*96+x+positions[key].X)*4;
                    for(var c=0;c<4;c++)Assert.InRange(Math.Abs(middle[i+c]-near[i+c]),0,1);
                }
            }
        }
    });
}
