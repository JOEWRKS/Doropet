using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Rectangle = System.Windows.Shapes.Rectangle;
using Dororong.App.Controls;
using Dororong.Core.Behavior;
using Dororong.Core.Platforms;

namespace Dororong.App.Tests.Controls;

public sealed class EdgePerchPresentationTests
{
    [Theory]
    [InlineData(FacingDirection.Right, 39, 40)]
    [InlineData(FacingDirection.Right, 40, 70)]
    [InlineData(FacingDirection.Left, 39, 40)]
    [InlineData(FacingDirection.Left, 40, 70)]
    public void Perch_regrab_captures_the_visible_image09_anchor_before_the_layer_is_retired(
        FacingDirection facing, double sourceX, double sourceY) => Sta(() =>
    {
        var presenter = new DororongPresenter();
        var pet = new Dororong.Core.Behavior.PetSnapshot(
            Dororong.Core.Behavior.PetState.Walk, new(100, 100), facing, 0, false, null);
        presenter.Render(pet, Dororong.App.Interaction.DirectInteractionSnapshot.None);
        Layout(presenter);
        presenter.ApplyEdgePerch(EdgePerchPhase.Attached, facing, TimeSpan.Zero, false);
        Layout(presenter);
        var perch = presenter.EdgePerchImage;
        Assert.True(perch.TryGetOpaqueSourcePoint(new Point(sourceX, sourceY), out _));
        var local = perch.TranslatePoint(new Point(sourceX, sourceY), presenter);
        var expected = Assert.IsType<CapturedHeadAnchor>(HeadPullAnchoring.Capture(
            perch, presenter, new(local.X, local.Y), facing));
        var press = pet.Position + new Dororong.Core.Geometry.PointD(local.X, local.Y);
        var direct = new Dororong.App.Interaction.DirectInteractionSnapshot(
            Dororong.App.Interaction.DirectInteractionTarget.Body,
            Dororong.App.Interaction.DirectInteractionPhase.BodyDragHold,
            press, press, 1, 0, true);

        presenter.Render(pet with { State = Dororong.Core.Behavior.PetState.Dragged }, direct, TimeSpan.Zero);

        var field = typeof(DororongPresenter).GetField("_headAnchor", BindingFlags.Instance | BindingFlags.NonPublic);
        var actual = Assert.IsType<CapturedHeadAnchor>(field?.GetValue(presenter));
        Assert.Equal(expected, actual);
    });

    [Theory]
    [InlineData(FacingDirection.Right, 44, 74)]
    [InlineData(FacingDirection.Left, 62, 92)]
    public void Contact_comes_from_image09_anchors_through_the_actual_facing_transform(
        FacingDirection facing, double left, double right) => Sta(() =>
    {
        var (root, _, presentation) = Create();
        Layout(root);

        var contact = Assert.IsType<PerchContact>(presentation.Measure(facing, root));

        Assert.Equal(left, contact.Left, 6);
        Assert.Equal(right, contact.Right, 6);
        Assert.Equal(94, contact.GripY, 6);
        Assert.Equal(48, contact.VisibleTop, 6);
    });

    [Fact]
    public void Active_pose_uses_native_image09_alpha_for_visibility_and_hit_testing() => Sta(() =>
    {
        var (root, canonical, presentation) = Create();
        Layout(root);

        presentation.Apply(EdgePerchPhase.Attached, FacingDirection.Right, TimeSpan.Zero);
        Layout(root);

        Assert.Equal(Visibility.Hidden, canonical.Visibility);
        Assert.Equal(Visibility.Visible, presentation.Image.Visibility);
        Assert.Equal(100, presentation.Image.ActualWidth);
        Assert.Equal(100, presentation.Image.ActualHeight);
        Assert.True(presentation.Image.TryGetOpaqueSourcePoint(new Point(40, 40), out _));
        Assert.False(presentation.Image.TryGetOpaqueSourcePoint(new Point(90, 90), out _));
    });

    [Fact]
    public void Natural_exit_crossfades_for_a_bounded_interval_then_clears_only_perch_state() => Sta(() =>
    {
        var (root, canonical, presentation) = Create();
        Layout(root);
        presentation.Apply(EdgePerchPhase.Attached, FacingDirection.Left, TimeSpan.Zero);

        presentation.Apply(EdgePerchPhase.None, FacingDirection.Left, TimeSpan.FromMilliseconds(30));
        Assert.Equal(Visibility.Visible, canonical.Visibility);
        Assert.Equal(Visibility.Visible, presentation.Image.Visibility);
        Assert.Equal(1, presentation.Image.Opacity);
        Assert.Equal(1, canonical.Opacity);
        Assert.NotNull(presentation.Image.Clip);
        Assert.NotNull(canonical.Clip);

        presentation.Apply(EdgePerchPhase.None, FacingDirection.Left, TimeSpan.FromMilliseconds(90));
        Assert.Equal(Visibility.Visible, canonical.Visibility);
        Assert.Equal(Visibility.Collapsed, presentation.Image.Visibility);
        Assert.Equal(1, canonical.Opacity);
        Assert.Equal(1, presentation.Image.Opacity);
        Assert.Null(canonical.Clip);
        Assert.Same(Transform.Identity, presentation.Image.RenderTransform);
        Assert.DoesNotContain(presentation.Image, VisualDescendants(root));
    });

    [Fact]
    public void Real_render_keeps_the_registered_face_opaque_through_every_handoff_step() => Sta(() =>
    {
        foreach (var elapsed in new[] { 0, 30, 60, 90, 120 })
        {
            var (root, canonical, presentation) = Create();
            canonical.Source = new BitmapImage(new Uri(
                "pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png", UriKind.Absolute));
            Layout(root);
            presentation.Apply(EdgePerchPhase.Attached, FacingDirection.Right, TimeSpan.Zero);
            presentation.Apply(EdgePerchPhase.None, FacingDirection.Right, TimeSpan.FromMilliseconds(elapsed));
            Layout(root);
            var bitmap = new RenderTargetBitmap(144, 144, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
            var pixels = new byte[144 * 144 * 4]; bitmap.CopyPixels(pixels, 144 * 4, 0);
            Assert.True(pixels[(70 * 144 + 60) * 4 + 3] >= 200, $"Face alpha dropped at {elapsed}ms.");
        }
    });

    [Fact]
    public void Handoff_routes_each_revealed_region_to_its_actual_visible_source() => Sta(() =>
    {
        var presenter = new DororongPresenter();
        Layout(presenter);
        presenter.ApplyEdgePerch(EdgePerchPhase.Attached, FacingDirection.Right, TimeSpan.Zero, false);
        presenter.ApplyEdgePerch(EdgePerchPhase.None, FacingDirection.Right, TimeSpan.FromMilliseconds(60), false);
        Layout(presenter);
        var canonical = Assert.IsType<AlphaHitTestImage>(presenter.FindName("DororongImage"));
        var perch = presenter.EdgePerchImage;
        Assert.NotNull(canonical.Clip);
        Assert.NotNull(perch.Clip);
        Assert.True(canonical.TryGetOpaqueSourcePoint(new Point(31, 46), out _));
        Assert.True(perch.TryGetOpaqueSourcePoint(new Point(40, 70), out _));

        Assert.Same(canonical, presenter.ResolvePrimaryPressedImage(canonical));
        Assert.Same(perch, presenter.ResolvePrimaryPressedImage(perch));
    });

    [Fact]
    public void Regrab_restore_is_immediate_and_does_not_leave_mirror_or_hit_surface() => Sta(() =>
    {
        var (root, canonical, presentation) = Create();
        Layout(root);
        presentation.Apply(EdgePerchPhase.Entering, FacingDirection.Left, TimeSpan.Zero);

        presentation.Apply(EdgePerchPhase.None, FacingDirection.Left, TimeSpan.Zero, immediateRestore: true);

        Assert.Equal(Visibility.Visible, canonical.Visibility);
        Assert.Null(presentation.VisibleImage);
        Assert.Same(Transform.Identity, presentation.Image.RenderTransform);
        Assert.DoesNotContain(presentation.Image, VisualDescendants(root));
    });

    [Fact]
    public void Product_perch_surface_retains_the_existing_exit_context_menu() => Sta(() =>
    {
        var presenter = new DororongPresenter();
        var body = Assert.IsType<Canvas>(presenter.FindName("BodyGroup"));

        Assert.NotNull(body.ContextMenu);
        Assert.Same(body.ContextMenu, presenter.EdgePerchImage.ContextMenu);
    });

    [Fact]
    public void Dormant_perch_surface_is_detached_and_cannot_expand_the_character_hit_tree() => Sta(() =>
    {
        var presenter = new DororongPresenter();
        Layout(presenter);
        var perch = presenter.EdgePerchImage;

        Assert.Equal(Visibility.Collapsed, perch.Visibility);
        Assert.False(perch.IsVisible);
        var descendants = VisualDescendants(presenter).ToArray();
        Assert.DoesNotContain(perch, descendants);
        Assert.Single(descendants.OfType<AlphaHitTestImage>());
    });

    [Fact]
    public void Real_WPF_composition_writes_native_and_enlarged_entry_release_review_sheets() => Sta(() =>
    {
        const int tileWidth = 160; const int sheetHeight = 180;
        var sheet = new Canvas { Width = tileWidth * 9, Height = sheetHeight, Background = Brushes.White };
        AddProofTile(sheet, 0, "RIGHT entry", FacingDirection.Right, EdgePerchPhase.Entering, 12, 0);
        AddProofTile(sheet, 1, "RIGHT attached", FacingDirection.Right, EdgePerchPhase.Attached, 0, 0);
        AddProofTile(sheet, 2, "LEFT entry", FacingDirection.Left, EdgePerchPhase.Entering, 12, 0);
        AddProofTile(sheet, 3, "LEFT attached", FacingDirection.Left, EdgePerchPhase.Attached, 0, 0);
        AddProofTile(sheet, 4, "release 0ms", FacingDirection.Right, EdgePerchPhase.None, 0, 0);
        AddProofTile(sheet, 5, "release 30ms", FacingDirection.Right, EdgePerchPhase.None, 0, 30);
        AddProofTile(sheet, 6, "release 60ms", FacingDirection.Right, EdgePerchPhase.None, 0, 60);
        AddProofTile(sheet, 7, "release 90ms", FacingDirection.Right, EdgePerchPhase.None, 0, 90);
        AddProofTile(sheet, 8, "release 120ms", FacingDirection.Right, EdgePerchPhase.None, 0, 120);
        sheet.Measure(new Size(sheet.Width, sheet.Height));
        sheet.Arrange(new Rect(0, 0, sheet.Width, sheet.Height));
        sheet.UpdateLayout();

        var destination = Path.Combine(ProjectRoot(), "artifacts", "repro", "edge-perch-product-20260908", "render-proof");
        Directory.CreateDirectory(destination);
        var native = Path.Combine(destination, "image09-wpf-entry-release-native.png");
        var enlarged = Path.Combine(destination, "image09-wpf-entry-release-4x.png");
        Save(sheet, native, (int)sheet.Width, sheetHeight, 96);
        Save(sheet, enlarged, (int)sheet.Width * 4, sheetHeight * 4, 384);
        Assert.True(new FileInfo(native).Length > 1000);
        Assert.True(new FileInfo(enlarged).Length > 1000);
    });

    private static (Canvas Root, AlphaHitTestImage Canonical, EdgePerchPresentation Presentation) Create()
    {
        var root = new Canvas { Width = 144, Height = 144 };
        var canonical = new AlphaHitTestImage { Width = 96, Height = 96 };
        Canvas.SetLeft(canonical, 24); Canvas.SetTop(canonical, 24);
        root.Children.Add(canonical);
        var presentation = new EdgePerchPresentation(root, canonical, (_, _) => { });
        return (root, canonical, presentation);
    }

    private static void AddProofTile(Canvas sheet, int index, string label, FacingDirection facing,
        EdgePerchPhase phase, double entryOffset, double restoreMilliseconds)
    {
        var tile = new Canvas { Width = 160, Height = 180, Background = index % 2 == 0 ? Brushes.WhiteSmoke : Brushes.Gainsboro };
        Canvas.SetLeft(tile, index * 160); sheet.Children.Add(tile);
        var edge = new Rectangle { Width = 160, Height = 60, Fill = new SolidColorBrush(Color.FromRgb(184, 207, 225)) };
        Canvas.SetTop(edge, 120); tile.Children.Add(edge);
        var line = new Rectangle { Width = 160, Height = 2, Fill = Brushes.DimGray };
        Canvas.SetTop(line, 119); tile.Children.Add(line);
        var title = new TextBlock { Text = label, FontSize = 11, Foreground = Brushes.Black };
        Canvas.SetLeft(title, 4); Canvas.SetTop(title, 3); tile.Children.Add(title);

        var (presenter, canonical, presentation) = Create();
        canonical.Source = new BitmapImage(new Uri(
            "pack://application:,,,/Dororong.App;component/Assets/dororong-canonical.png", UriKind.Absolute));
        RenderOptions.SetBitmapScalingMode(canonical, BitmapScalingMode.NearestNeighbor);
        RenderOptions.SetBitmapScalingMode(presentation.Image, BitmapScalingMode.NearestNeighbor);
        Canvas.SetLeft(presenter, 8); Canvas.SetTop(presenter, 120 - 94 + entryOffset); tile.Children.Add(presenter);
        if (phase == EdgePerchPhase.None)
        {
            presentation.Apply(EdgePerchPhase.Attached, facing, TimeSpan.Zero);
            presentation.Apply(EdgePerchPhase.None, facing, TimeSpan.FromMilliseconds(restoreMilliseconds));
        }
        else presentation.Apply(phase, facing, TimeSpan.Zero);
    }

    private static void Save(Visual visual, string path, int width, int height, double dpi)
    {
        var bitmap = new RenderTargetBitmap(width, height, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }

    private static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(144, 144));
        element.Arrange(new Rect(0, 0, 144, 144));
        element.UpdateLayout();
    }

    private static IEnumerable<DependencyObject> VisualDescendants(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (var descendant in VisualDescendants(child)) yield return descendant;
        }
    }

    internal static string ProjectRoot()
    {
        var root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "src"))) root = Directory.GetParent(root)!.FullName;
        return root;
    }

    internal static void Sta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception exception) { error = exception; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
