using System.IO;
using System.IO.Compression;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.App.Interaction;
using Dororong.Core.Behavior;

namespace Dororong.App.Tests.Controls;

public sealed class SuppliedBodyDragFramesTests
{
    [Theory]
    [InlineData(74, 37)] [InlineData(75, 37)]
    [InlineData(74, 38)] [InlineData(75, 38)] [InlineData(76, 38)]
    [InlineData(74, 39)] [InlineData(75, 39)] [InlineData(75, 40)]
    public void Hanging_ribbon_middle_loop_retains_authored_white_on_black(int x, int y) => RunOnSta(() =>
    {
        var presenter = new DororongPresenter();
        var canonical = PremultipliedFrame.From(new BitmapImage(new Uri(
            "pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png")));
        // The user now requests the ordinary ribbon, including its white RGB,
        // rather than the JPEG's 254/255 colour noise in these same texels.
        var expected = canonical.Pixels.AsSpan(((y+6) * 96 + x-8) * 4, 4).ToArray();
        Assert.Equal(255, expected[3]);
        foreach (var phase in new[] { DirectInteractionPhase.BodyDragEntry, DirectInteractionPhase.BodyDragHold })
        {
            var actual = Render(presenter, phase, 1);
            Assert.Equal(expected, actual.Pixels.AsSpan(((y - 10) * 96 + x + 3) * 4, 4).ToArray());
            // The previously open corner (78,31) now contains the restored
            // canonical stroke. Beyond the repaired loop is still background.
            Assert.Equal(0, actual.Pixels[(31 * 96 + 81) * 4 + 3]);
        }
    });

    // Independently measured from the eight supplied100px source files.
    [Theory]
    [InlineData(1, -13, "7A326475A80D34A49B5123300C8752874A2B0FDD83130F914115E0A378AC6F11")]
    [InlineData(2, -9, "03702322627917D0C100E65F7DB33415DAF0D10F47E623D8C625E5E4603792F0")]
    [InlineData(3, -9, "BCA1E8133AF2121BE3D50F174A35279E3345553D960EB5881645A3609C5FF761")]
    [InlineData(4, -10, "95969CD393586272D3637DF7D761A69A98A485135B5E6DC40C3AB557440F5D96")]
    [InlineData(5, -10, "F21CE8CF8C45E1F9F8E1A8A46BBF9A3CFD3FAC8DB12F02B015E5A8FA41B9738A")]
    [InlineData(6, -10, "BDBB0418F996514BEA5EFC588C71EF366541C12889D5A013A2BE2925A7ED080C")]
    [InlineData(7, -10, "787054C28417D7D6544554F7875018DA6FEBFC48CDFF7794A7173C36FB5EB53A")]
    [InlineData(8, -10, "DAF9726E9DBA7A2274EF421828826BD56CF556B9DC6801B2428D6D74BBCF2E56")]
    public void Actual_presenter_keeps_supplied_interior_outside_repaired_head_and_ribbon(
        int number, int offsetY, string sha256)
    {
        RunOnSta(() =>
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "DororongDesktopPet.sln"))) root = root.Parent;
            Assert.NotNull(root);
            var path = Path.Combine(root!.FullName, $"src/Dororong.App/Assets/user-body-drag/{number:D2}.png");
            Assert.Equal(sha256, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
            var raw = PremultipliedFrame.From(new BitmapImage(new Uri(path)));
            var presenter = new DororongPresenter();
            using var embedded = System.Windows.Application.GetResourceStream(new Uri(
                $"pack://application:,,,/Dororong.App;component/Assets/user-body-drag/{number:D2}.png")).Stream;
            Assert.Equal(sha256, Convert.ToHexString(SHA256.HashData(embedded)));
            var actual = Render(presenter, DirectInteractionPhase.BodyDragEntry, (number - 1) / 7.0);
            var approved = ReadApprovedFrame((number-1)*16);
            AssertOutsideHeadRepair(approved, actual.Pixels, (number-1)/7d);
            // Adding the missing ribbon outline intentionally changes total
            // support. Exact outside-patch pixel preservation remains below.
            Assert.True(Enumerable.Range(0, 96 * 96).Count(i => actual.Pixels[i * 4 + 3] is > 0 and < 255) > 30,
                "Supplied outline still has a binary cutout instead of a translucent exterior edge.");
            for (var y = 0; y < 100; y++)
            for (var x = 0; x < 100; x++)
            {
                var input = (y * 100 + x) * 4;
                var targetY = y + offsetY;
                if (InHeadRepair(x+3,targetY,(number-1)/7d)) continue;
                if (raw.Pixels.AsSpan(input, 3).ToArray().Min() >= 240)
                {
                    if (targetY is >= 0 and < 96 && x + 3 < 96)
                    {
                        var whiteOutput = (targetY * 96 + x + 3) * 4;
                        if (actual.Pixels[whiteOutput + 3] != 0)
                            Assert.Equal(raw.Pixels.AsSpan(input, 4).ToArray(), actual.Pixels.AsSpan(whiteOutput, 4).ToArray());
                    }
                    continue;
                }
                Assert.InRange(targetY, 0, 95);
                Assert.InRange(x + 3, 0, 95);
                var output = (targetY * 96 + x + 3) * 4;
                var alpha = actual.Pixels[output + 3];
                if (alpha < 255)
                {
                    // A formerly exterior texel can now be inside the restored
                    // ribbon support; it must still equal the frozen old pixel.
                    Assert.True(IsExteriorBoundary(actual, x + 3, targetY) ||
                        actual.Pixels.AsSpan(output,4).SequenceEqual(approved.AsSpan(output,4)), "Interior pixel was modified.");
                    for (var channel = 0; channel < 3; channel++)
                    {
                        Assert.InRange(actual.Pixels[output + channel], 0, alpha);
                        // Independent white-composite invariant: preserve original line appearance on white.
                        Assert.Equal(raw.Pixels[input + channel], actual.Pixels[output + channel] + 255 - alpha);
                    }
                }
                else Assert.Equal(raw.Pixels.AsSpan(input, 4).ToArray(), actual.Pixels.AsSpan(output, 4).ToArray());
            }
            var sole = Enumerable.Range(0, 96 * 96).Where(i => actual.Pixels[i * 4 + 3] != 0).Max(i => i / 96);
            Assert.Equal(86, sole);
            var reverse = Render(presenter, DirectInteractionPhase.BodyDragSettle, 1, 1 - (number - 1) / 7.0);
            Assert.Equal(actual.Pixels, reverse.Pixels);
            if (number == 8) Assert.Equal(actual.Pixels, Render(presenter, DirectInteractionPhase.BodyDragHold, 1).Pixels);
        });
    }

    [Theory]
    [InlineData(63, 63, 63, 159, 159, 159, 32, 32, 32)]
    [InlineData(45, 85, 165, 150, 170, 210, 23, 43, 83)]
    public void White_matted_boundary_is_unmatted_without_eroding_shape_or_changing_interior(
        byte coreB, byte coreG, byte coreR, byte edgeB, byte edgeG, byte edgeR,
        byte expectedB, byte expectedG, byte expectedR)
    {
        RunOnSta(() =>
        {
            // One half-coverage edge on a closed outline; literal expected colors are hand-derived.
            var pixels = Enumerable.Repeat((byte)255, 100 * 100 * 4).ToArray();
            for (var y = 40; y <= 70; y++)
            for (var x = 20; x <= 40; x++)
                if (x < 22 || x > 38 || y < 42 || y > 68)
                    Set(x, y, coreB, coreG, coreR);
            Set(20, 55, edgeB, edgeG, edgeR);
            var frozenInput = (byte[])pixels.Clone();
            var bitmap = BitmapSource.Create(100, 100, 96, 96, PixelFormats.Pbgra32, null, pixels, 400);
            bitmap.Freeze();
            var source = new PremultipliedFrame(bitmap, pixels, 400);
            var reference = PremultipliedFrame.From(new BitmapImage(new Uri(
                "pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png")));
            var result = PremultipliedFrame.From(SuppliedBodyDragFrames.Prepare(source, reference));
            Assert.Equal(new byte[] { expectedB, expectedG, expectedR, 128 }, Pixel(23, 71));
            Assert.Equal(new byte[] { coreB, coreG, coreR, 255 }, Pixel(24, 71));
            Assert.Equal(new byte[] { 255, 255, 255, 255 }, Pixel(33, 71));
            Assert.Equal(new byte[] { 0, 0, 0, 0 }, Pixel(22, 71));
            Assert.Equal(21 * 31, Enumerable.Range(0, 96 * 96).Count(i => result.Pixels[i * 4 + 3] > 0));
            Assert.Equal(frozenInput, pixels);

            void Set(int x, int y, byte b, byte g, byte r)
            {
                var offset = (y * 100 + x) * 4;
                pixels[offset] = b; pixels[offset + 1] = g; pixels[offset + 2] = r;
            }
            byte[] Pixel(int x, int y) => result.Pixels.AsSpan((y * 96 + x) * 4, 4).ToArray();
        });
    }

    private static bool IsExteriorBoundary(PremultipliedFrame frame, int x, int y)
    {
        for (var dy = -1; dy <= 1; dy++)
        for (var dx = -1; dx <= 1; dx++)
        {
            var nx = x + dx; var ny = y + dy;
            if (nx < 0 || nx >= 96 || ny < 0 || ny >= 96 || frame.Pixels[(ny * 96 + nx) * 4 + 3] == 0) return true;
        }
        return false;
    }

    // Approved browser rasters, independently captured before desktop integration.
    // Catches accidentally retaining the old eight-key selector in the presenter.
    [Theory]
    [InlineData(9, "A5CCEE6826BC037E655FF9736853291C38F133287294402F8020D4056E82BE60")]
    [InlineData(25, "4FE4E90D27D162C1863191A9C6C72D347779477F179BCB16BB0D1A8AC04874C4")]
    [InlineData(35, "4128CE30A961C7B64F148FDC38FBF803328E617BCF380694D58B89EE8DBCC978")]
    [InlineData(41, "B7DAD527BA563800C5AFA557E96554091D9C616F5950FA22E14B83CFBA2052BF")]
    [InlineData(49, "5F6E1D9D028B7475EAFF43C196D5F0651601A42C30257BB3D72DBF508EF304E9")]
    [InlineData(57, "13EA0DD6CDDE64DC5CE260BA26CF5D6F151048B7E0F7AD1E69A8450C71EDD926")]
    public void Intermediate_strengths_render_the_selected_layered_preview(int frame, string sha)
    {
        RunOnSta(() =>
        {
            var presenter = new DororongPresenter();
            var strength = (frame - 1) / 112d;
            var approved = ReadApprovedFrame(frame-1);
            Assert.Equal(sha, Convert.ToHexString(SHA256.HashData(approved)));
            foreach (var phase in new[] { DirectInteractionPhase.BodyDragEntry, DirectInteractionPhase.BodyDragSettle })
            {
                var pixels = Render(presenter, phase, strength, 1 - strength).Pixels;
                AssertOutsideHeadRepair(approved,pixels,strength);
            }
        });
    }

    [Fact]
    public void Captured_intermediate_release_retraces_layered_pose_instead_of_old_mesh() => RunOnSta(() =>
    {
        var (presenter, pet, direct) = HeadTiltSamplingTests.Setup(false);
        direct = direct with { Phase = DirectInteractionPhase.BodyDragEntry, Strength = 40 / 112d };
        presenter.Render(pet, direct, TimeSpan.Zero);
        presenter.Render(pet with { State = PetState.Idle }, direct with
        {
            Phase = DirectInteractionPhase.BodyDragSettle, RequiresCapture = false,
            IsPartialDragSettle = true, ReleaseProgress = 2 / 7d
        }, TimeSpan.Zero);
        var pixels = PremultipliedFrame.From((BitmapSource)((Image)presenter.FindName("DororongImage")).Source).Pixels;
        // From pose41 to pose25: 2.5 authored intervals - (2/7)*3.5 = 1.5.
        var approved=ReadApprovedFrame(24);
        Assert.Equal("4FE4E90D27D162C1863191A9C6C72D347779477F179BCB16BB0D1A8AC04874C4",
            Convert.ToHexString(SHA256.HashData(approved)));
        AssertOutsideHeadRepair(approved,pixels,24/112d);
        Assert.Equal(PremultipliedFrame.From(LayeredPullFrames.Sample(24/112d)).Pixels,pixels);
    });

    // Keep the independently frozen preview hashes above. Only the requested
    // lower-head correction is exempt from exact per-pixel preview parity.
    private static byte[] ReadApprovedFrame(int index)
    {
        using var stream=typeof(DororongPresenter).Assembly.GetManifestResourceStream("Dororong.App.Assets.layered-pull.pbgra.gz")!;
        using var gzip=new GZipStream(stream,CompressionMode.Decompress);
        var bytes=new byte[96*96*4];
        for(var i=0;i<=index;i++)gzip.ReadExactly(bytes);
        return bytes;
    }

    private static void AssertOutsideHeadRepair(byte[] expected,byte[] actual,double progress)
    {
        for(var y=0;y<96;y++)for(var x=0;x<96;x++)
            if(!InHeadRepair(x,y,progress))
                Assert.Equal(expected.AsSpan((y*96+x)*4,4).ToArray(),actual.AsSpan((y*96+x)*4,4).ToArray());
    }

    private static bool InHeadRepair(int x,int y,double progress)
    {
        // Independent measured registration bounds; do not call repair logic
        // here, or an accidentally widened production mask could exempt itself.
        (double X,double Y)[] shifts=[(0,-1),(0,-3),(1,-7),(2,-11),(4,-13),(6,-15),(8,-15),(11,-16)];
        var position=progress*7;var index=Math.Min(6,(int)position);var t=position-index;
        var dx=shifts[index].X+(shifts[index+1].X-shifts[index].X)*t;
        var dy=shifts[index].Y+(shifts[index+1].Y-shifts[index].Y)*t;
        return x>=Math.Floor(19+dx)&&x<=Math.Ceiling(53+dx)&&y>=Math.Floor(57+dy)&&y<=Math.Ceiling(72+dy) ||
            x>=Math.Floor(60+dx)&&x<=Math.Ceiling(70+dx)&&y>=Math.Floor(36+dy)&&y<=Math.Ceiling(61+dy);
    }

    private static PremultipliedFrame Render(DororongPresenter presenter, DirectInteractionPhase phase, double strength, double release = 0)
    {
        presenter.Render(new(PetState.Dragged, new(100, 100), FacingDirection.Right, 0, false, null),
            new(DirectInteractionTarget.Body, phase, default, default, strength, release, phase != DirectInteractionPhase.BodyDragSettle));
        return PremultipliedFrame.From((BitmapSource)((Image)presenter.FindName("DororongImage")).Source);
    }

    private static void RunOnSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception e) { error = e; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
