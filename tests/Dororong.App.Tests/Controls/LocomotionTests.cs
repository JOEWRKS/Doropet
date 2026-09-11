using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.Core.Behavior;

namespace Dororong.App.Tests.Controls;

public sealed class LocomotionTests
{
    [Fact]
    public void Presenter_uses_motion_frames_and_preserves_native_cheek_capture() => EdgePerchPresentationTests.Sta(() =>
    {
        var presenter=new DororongPresenter();
        var snapshot=new PetSnapshot(PetState.Idle,new(100,100),FacingDirection.Left,0,false,null);
        presenter.SetSittingRequested(true);
        presenter.Render(snapshot,Dororong.App.Interaction.DirectInteractionSnapshot.None,TimeSpan.FromMilliseconds(1650));
        presenter.Measure(new System.Windows.Size(144,144));presenter.Arrange(new System.Windows.Rect(0,0,144,144));presenter.UpdateLayout();
        var image=(Dororong.App.Controls.AlphaHitTestImage)presenter.FindName("DororongImage");
        Assert.Same(LocomotionFrames.Sit(1),image.Source);
        Assert.True(presenter.TryCreateCheekPullCapture(new(16,58),out _));
        Assert.True(presenter.TryCreateBodyPullCapture(new(23,77),out _));
        presenter.SetSittingRequested(false);
        presenter.Render(snapshot with {State=PetState.Walk},Dororong.App.Interaction.DirectInteractionSnapshot.None,TimeSpan.FromMilliseconds(650));
        presenter.Render(snapshot with {State=PetState.Walk,Position=new(102.5,100)},Dororong.App.Interaction.DirectInteractionSnapshot.None,TimeSpan.FromMilliseconds(180));
        Assert.True(presenter.TryCreateCheekPullCapture(new(16,58),out _));
        Assert.True(presenter.TryCreateBodyPullCapture(new(23,77),out _));
        presenter.Render(snapshot with { IsDirectInteractionPending=true },Dororong.App.Interaction.DirectInteractionSnapshot.None,TimeSpan.FromMilliseconds(16));
        Assert.False(LocomotionFrames.Contains(image.Source));
    });

    [Fact]
    public void Platform_owner_suppresses_sit_until_support_is_restored() => EdgePerchPresentationTests.Sta(() =>
    {
        var p=new DororongPresenter();var snapshot=new PetSnapshot(PetState.Idle,new(100,100),FacingDirection.Right,0,false,null);
        p.SetSittingRequested(true);
        p.Render(snapshot,Dororong.App.Interaction.DirectInteractionSnapshot.None,TimeSpan.FromMilliseconds(1650));
        p.SetLocomotionBlocked(true);
        p.Render(snapshot,Dororong.App.Interaction.DirectInteractionSnapshot.None,TimeSpan.FromMilliseconds(50));
        var image=(System.Windows.Controls.Image)p.FindName("DororongImage");
        Assert.False(LocomotionFrames.Contains(image.Source));
        p.SetLocomotionBlocked(false);
        p.Render(snapshot,Dororong.App.Interaction.DirectInteractionSnapshot.None,TimeSpan.Zero);
        Assert.Same(LocomotionFrames.Sit(1),image.Source);
    });

    [Fact]
    public void Export_actual_presenter_motion_proof() => EdgePerchPresentationTests.Sta(() =>
    {
        var destination=Path.Combine(EdgePerchPresentationTests.ProjectRoot(),"artifacts/repro/locomotion-product");
        Directory.CreateDirectory(destination);
        var p=new DororongPresenter();
        var snapshot=new PetSnapshot(PetState.Idle,new(100,100),FacingDirection.Right,0,false,null);
        void Capture(string name,int milliseconds)
        {
            p.Render(snapshot,Dororong.App.Interaction.DirectInteractionSnapshot.None,TimeSpan.FromMilliseconds(milliseconds));
            p.Measure(new System.Windows.Size(144,144));p.Arrange(new System.Windows.Rect(0,0,144,144));p.UpdateLayout();
            var bitmap=new RenderTargetBitmap(144,144,96,96,System.Windows.Media.PixelFormats.Pbgra32);bitmap.Render(p);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var file=File.Create(Path.Combine(destination,"wpf-"+name+".png"));encoder.Save(file);
        }
        Capture("standing",0);p.SetSittingRequested(true);Capture("half-sit",325);Capture("sit",325);
        snapshot=snapshot with {Phase=.7};Capture("sit-blink",1590);
        p.SetSittingRequested(false);
        snapshot=snapshot with {State=PetState.Walk,Phase=0};Capture("half-rise",325);Capture("risen",325);
        snapshot=snapshot with {Position=new(102.5,100)};Capture("walk",180);
        snapshot=snapshot with {Facing=FacingDirection.Left,Position=new(105,100)};Capture("walk-left",100);
    });
    [Fact]
    public void Approved_bank_is_embedded() => Assert.Contains("Dororong.App.Assets.locomotion.pbgra.gz",
        typeof(DororongPresenter).Assembly.GetManifestResourceNames());

    [Fact]
    public void Initial_rest_preserves_canonical_but_completed_rise_keeps_cleaned_standing() => EdgePerchPresentationTests.Sta(() =>
    {
        var p=new DororongPresenter();
        var snapshot=new PetSnapshot(PetState.Idle,new(100,100),FacingDirection.Right,0,false,null);
        var image=(System.Windows.Controls.Image)p.FindName("DororongImage");
        p.Render(snapshot,Dororong.App.Interaction.DirectInteractionSnapshot.None,TimeSpan.Zero);
        Assert.False(LocomotionFrames.Contains(image.Source));
        p.SetSittingRequested(true);
        p.Render(snapshot,Dororong.App.Interaction.DirectInteractionSnapshot.None,TimeSpan.FromMilliseconds(1650));
        p.SetSittingRequested(false);
        p.Render(snapshot with {State=PetState.Walk},Dororong.App.Interaction.DirectInteractionSnapshot.None,TimeSpan.FromMilliseconds(650));
        // At2300ms the shared eye clock is closed; changing locomotion state
        // must not reset it or bring back the old canonical body fringe.
        Assert.Same(LocomotionFrames.Sit(0, true),image.Source);
        p.Render(snapshot,Dororong.App.Interaction.DirectInteractionSnapshot.None,TimeSpan.Zero);
        Assert.Same(LocomotionFrames.Sit(0, true),image.Source);
    });

    [Fact]
    public void Initial_walk_waits_for_a_nonzero_sample_then_captures_the_actual_moving_frame() => EdgePerchPresentationTests.Sta(() =>
    {
        var p=new DororongPresenter();
        var snapshot=new PetSnapshot(PetState.Walk,new(100,100),FacingDirection.Right,0,false,null);
        var image=(System.Windows.Controls.Image)p.FindName("DororongImage");
        p.Render(snapshot,Dororong.App.Interaction.DirectInteractionSnapshot.None,TimeSpan.FromMilliseconds(16));
        Assert.False(LocomotionFrames.Contains(image.Source));
        p.Render(snapshot with {Position=new(101,100)},Dororong.App.Interaction.DirectInteractionSnapshot.None,TimeSpan.FromMilliseconds(16));
        Assert.Same(LocomotionFrames.Walk(.125,1),image.Source);
        Assert.NotSame(LocomotionFrames.Sit(0),image.Source);
        p.Measure(new System.Windows.Size(144,144));p.Arrange(new System.Windows.Rect(0,0,144,144));p.UpdateLayout();
        Assert.True(p.TryCreateCheekPullCapture(new(16,58),out var capture));
        var converted=new FormatConvertedBitmap((BitmapSource)image.Source,System.Windows.Media.PixelFormats.Bgra32,null,0);
        var expected=new byte[96*96*4];converted.CopyPixels(expected,384,0);
        Assert.Equal(expected,capture!.Render(0,0,0));
        Assert.True(p.TryCreateBodyPullCapture(new(23,77),out _));
    });

    [Fact]
    public void Command_sits_immediately_and_reverses_before_walking()
    {
        var p = new LocomotionPresentation();
        p.Advance(999, false, 0, false); Assert.Equal(0, p.Sit);
        p.Advance(325, false, 0, false, true); Assert.Equal(.5, p.Sit, 6);
        p.Advance(100, true, 0, false); Assert.InRange(p.Sit, 0.1, .5);
        Assert.True(p.HoldWalking);
        p.Advance(650, true, 0, false); Assert.Equal(0, p.Sit);
        Assert.False(p.HoldWalking);
        p.Advance(180, true, 2, false); Assert.Equal(1, p.Walk);
        var distance = p.Distance;
        p.Advance(180, false, 0, false); Assert.Equal(distance, p.Distance); Assert.Equal(0, p.Walk);
        p.Advance(650, false, 0, false, true); Assert.Equal(1, p.Sit);
        p.Advance(16, false, 0, true); Assert.Equal(0, p.Sit); Assert.False(p.HoldWalking);
    }

    [Fact]
    public void Frames_are_frozen_native96_and_exact_exported_bytes()
    {
        var root = EdgePerchPresentationTests.ProjectRoot();
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"tools/PreviewLocomotion/product-manifest.json")));
        foreach (var row in manifest.RootElement.GetProperty("frames").EnumerateArray())
        {
            var frame = LocomotionFrames.All[row.GetProperty("index").GetInt32()];
            Assert.True(frame.IsFrozen); Assert.Equal(96, frame.PixelWidth); Assert.Equal(96, frame.PixelHeight);
            var bytes = new byte[96*96*4]; frame.CopyPixels(bytes,96*4,0);
            Assert.Equal(row.GetProperty("sha256").GetString(), Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant());
        }
        Assert.Same(LocomotionFrames.Sit(0),LocomotionFrames.Walk(0,0));
        Assert.Same(LocomotionFrames.Sit(1),LocomotionFrames.Sit(2));
        Assert.NotSame(LocomotionFrames.Walk(1,0),LocomotionFrames.Walk(1,2.5));
        var first=new byte[96*96*4];var second=new byte[first.Length];
        LocomotionFrames.Walk(1,0).CopyPixels(first,384,0);LocomotionFrames.Walk(1,2.5).CopyPixels(second,384,0);
        Assert.True(Enumerable.Range(70*384,18*384).Count(i=>first[i]!=second[i])>100);
        Assert.Throws<ArgumentOutOfRangeException>(()=>LocomotionFrames.Sit(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(()=>LocomotionFrames.Walk(1,double.PositiveInfinity));
    }
}
