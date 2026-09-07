using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

internal static class Program
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length is < 1 or > 2) throw new ArgumentException("Supply a new evidence output directory and optionally a prior proof directory for edge comparison.");
        var destination = Path.GetFullPath(args[0]);
        if (Directory.Exists(destination)) throw new IOException("Evidence output must be a new directory.");
        Directory.CreateDirectory(destination);
        SaveFootRegistration(destination);
        if (args.Length == 2) SaveEdgeComparison(destination, Path.GetFullPath(args[1]));
        var rows = new List<FrameRow>();

        var partial = new Sequence(destination, "partial", rows);
        partial.Step("press", 0, true, true);
        for (var x = 2; x <= 42; x += 2) partial.Step("extend-partial", x);
        for (var i = 0; i < 64; i++) partial.Step("stationary-partial-1024ms", 42);
        partial.Step("reverse-partial", 23);
        partial.Step("partial-release-start", 23, false);
        for (var i = 0; i < 14; i++) partial.Step("partial-settle", 23, false);

        var full = new Sequence(destination, "full", rows);
        full.Step("press", 0, true, true);
        for (var x = 2; x <= 78; x += 2) full.Step("extend", x);
        full.Step("below-threshold-79", 79);
        full.Step("full-extension-80-follows80", 80);
        full.Step("above-threshold-81", 81);
        for (var i = 0; i < 12; i++) full.Step("stationary-full", 81);
        for (var x = 84; x <= 120; x += 4) full.Step("carry", x);
        full.Step("carry-reversal-latched", 60);
        full.Step("full-release-start", 60, false);
        for (var i = 0; i < 14; i++) full.Step("full-settle", 60, false);

        var overshoot = new Sequence(destination, "overshoot", rows);
        overshoot.Step("press", 0, true, true);
        overshoot.Step("first-sample-100-follows100", 100);
        overshoot.Step("release", 100, false);
        for (var i = 0; i < 14; i++) overshoot.Step("settle", 100, false);

        var metadata = new
        {
            method = "Deterministic OFFSCREEN production DirectInteractionController + PetBrain + DororongPresenter. No app/window launched; no physical input/capture/desktop composition claim.",
            normalSpeed = true,
            frameDurationMs = 16,
            framesPerSecond = 62.5,
            sourcePixels = "96x96 PNG from actual Presenter. Active head drag selects one of the user's8 original100px images without scaling/interpolation; transparent canvas reframed only. Pending/idle remain existing canonical.",
            footRegistration = "Original edge-connected RGB>=240 cutout establishes fixedX+3 and sole registrationY86 BEFORE matte correction. Only the exterior1px ring can receive estimated white-matte removal/partial alpha; support and all interior pixels unchanged. Retained edge RGB reconstructs source exactly over white. Raw supplied100x100 PNGs embedded unchanged. Comparison: supplied raw100px above, actual Presenter96px below, both1:1 pixels.",
            presenterPixels = "144x144 RenderTargetBitmap at 96 DPI; actual offscreen WPF presenter",
            pointerUnits = "DIPs in global desktop space, Euclidean distance from press",
            deadzone = "OS test fixture rectangle +/-4 DIPs per axis",
            fullExtensionDips = 80,
            movement = "Window immediately follows the original grab offset after OS drag deadzone. Presenter compensates selected-key head displacement using the captured point and rounded pink-hair landmark; no source pixel changes. Source foot registration remains; displayed feet may move to retain the head anchor. Work-area clamp is authoritative.",
            release = "180ms settle. Partial reverses supplied entry from last Strength; full uses the SAME supplied8 images in reverse, never legacy13.",
            frames = rows
        };
        var json = JsonSerializer.Serialize(metadata, JsonOptions);
        File.WriteAllText(Path.Combine(destination, "playback.json"), json);
        File.WriteAllText(Path.Combine(destination, "playback.html"), Html.Replace("FRAME_DATA", JsonSerializer.Serialize(rows)));
        Console.WriteLine($"HEAD PULL OFFSCREEN PROOF: {rows.Count} ordered native96 + presenter144 PNG pairs, playback.json and normal-speed playback.html at {destination}");
    }

    private sealed class Sequence
    {
        private readonly string _destination;
        private readonly string _name;
        private readonly List<FrameRow> _rows;
        private readonly DororongPresenter _presenter = new();
        private readonly PetBrain _brain = new(BehaviorTuning.Default with
        {
            IdleMin = TimeSpan.FromMinutes(10), IdleMax = TimeSpan.FromMinutes(10), SleepDelay = TimeSpan.FromMinutes(10)
        }, new SeededRandomSource(17), new(100, 100));
        private readonly object _controller;
        private readonly MethodInfo _advance;
        private readonly MethodInfo _render;
        private readonly PropertyInfo _current;
        private int _frame;
        private string _lastPhase = "None";

        internal Sequence(string destination, string name, List<FrameRow> rows)
        {
            _destination = destination; _name = name; _rows = rows;
            var controllerType = typeof(DororongPresenter).Assembly.GetType("Dororong.App.Interaction.DirectInteractionController", true)!;
            _controller = Activator.CreateInstance(controllerType, true)!;
            _advance = controllerType.GetMethod("Advance", Members)!;
            _current = controllerType.GetProperty("Current", Members)!;
            _render = typeof(DororongPresenter).GetMethods(Members | BindingFlags.DeclaredOnly).Single(m => m.Name == "Render" && m.GetParameters().Length == 2);
            _presenter.Measure(new Size(144, 144));
            _presenter.Arrange(new Rect(0, 0, 144, 144));
            _render.Invoke(_presenter, [_brain.Current, _current.GetValue(_controller)!]);
            _presenter.UpdateLayout();
            controllerType.GetMethod("BeginDistanceBody", Members)!.Invoke(_controller, [new PointD(160, 150), new SizeD(4, 4)]);
        }

        internal void Step(string label, double distance, bool down = true, bool first = false)
        {
            var pointer = new PointerSample(true, new(160 + distance, 150));
            var before = _brain.Current;
            var core = _brain.Update(new PetInput(TimeSpan.FromMilliseconds(16), new(0, 0, 1000, 800), new(144, 144),
                pointer, down, _lastPhase == "BodyDragSettle", first ? new PointD(160, 150) : null, new(4, 4),
                DistanceDrivenBodyDrag: true));
            _advance.Invoke(_controller, [TimeSpan.FromMilliseconds(16), pointer, down, before.State, core.State]);
            var direct = _current.GetValue(_controller)!;
            _render.Invoke(_presenter, [core, direct]);
            _presenter.UpdateLayout();
            var image = (Image)_presenter.FindName("DororongImage");
            var source = (BitmapSource)image.Source;
            var prefix = $"{_rows.Count:D4}-{_name}-{label}";
            var native = prefix + "-native96.png";
            var rendered = prefix + "-presenter144.png";
            Save(Path.Combine(_destination, native), source);
            var bitmap = new RenderTargetBitmap(144, 144, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(_presenter);
            Save(Path.Combine(_destination, rendered), bitmap);
            _lastPhase = Value(direct, "Phase")!.ToString()!;
            _rows.Add(new(_rows.Count, _name, _frame++ * 16, label, distance, _lastPhase,
                (double)Value(direct, "Strength")!, (double)Value(direct, "ReleaseProgress")!,
                (bool)Value(direct, "IsPartialDragSettle")!, (bool)Value(direct, "RequiresCapture")!,
                core.Position.X, core.Position.Y, native, rendered));
        }
    }

    private static object? Value(object instance, string name) => instance.GetType().GetProperty(name, Members)!.GetValue(instance);

    private static void SaveFootRegistration(string destination)
    {
        string[] names = ["01", "02", "03", "04", "05", "06", "07", "08"];
        var presenter = new DororongPresenter();
        var assembly = typeof(DororongPresenter).Assembly;
        var directType = assembly.GetType("Dororong.App.Interaction.DirectInteractionSnapshot", true)!;
        var targetType = assembly.GetType("Dororong.App.Interaction.DirectInteractionTarget", true)!;
        var phaseType = assembly.GetType("Dororong.App.Interaction.DirectInteractionPhase", true)!;
        var render = typeof(DororongPresenter).GetMethods(Members | BindingFlags.DeclaredOnly).Single(m => m.Name == "Render" && m.GetParameters().Length == 2);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(17, 19, 27)), null, new Rect(0, 0, 832, 272));
            for (var i = 0; i < names.Length; i++)
            {
                var raw = new BitmapImage(new Uri($"pack://application:,,,/Dororong.App;component/Assets/user-body-drag/{names[i]}.png"));
                var direct = Activator.CreateInstance(directType, Members, null,
                    [Enum.Parse(targetType, "Body"), Enum.Parse(phaseType, "BodyDragEntry"), new PointD(0, 0), new PointD(0, 0), i / 7.0, 0.0, true], null)!;
                render.Invoke(presenter, [new PetSnapshot(PetState.Dragged, new(100, 100), FacingDirection.Right, 0, false, null), direct]);
                var aligned = (BitmapSource)((Image)presenter.FindName("DororongImage")).Source;
                Save(Path.Combine(destination, $"registration-key-{i + 1:D2}-original.png"), raw);
                Save(Path.Combine(destination, $"registration-key-{i + 1:D2}-presenter.png"), aligned);
                for (var row = 0; row < 2; row++)
                {
                    var top = row * 136;
                    drawing.DrawText(new FormattedText($"{(row == 0 ? "Supplied" : "Runtime")} {i + 1}",
                        System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"), 11, Brushes.White, 1), new Point(i * 104 + 4, top));
                    var guide = top + 16 + (row == 0 ? 99.5 : 86.5);
                    drawing.DrawLine(new Pen(Brushes.DarkGoldenrod, 1), new Point(i * 104, guide), new Point((i + 1) * 104, guide));
                    var size = row == 0 ? 100 : 96;
                    drawing.DrawImage(row == 0 ? raw : aligned, new Rect(i * 104, top + 16, size, size));
                }
            }
        }
        var native = new RenderTargetBitmap(832, 272, 96, 96, PixelFormats.Pbgra32);
        native.Render(visual);
        Save(Path.Combine(destination, "foot-registration-comparison-native.png"), native);
        var enlargedVisual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(enlargedVisual, BitmapScalingMode.NearestNeighbor);
        using (var drawing = enlargedVisual.RenderOpen()) drawing.DrawImage(native, new Rect(0, 0, 2496, 816));
        var enlarged = new RenderTargetBitmap(2496, 816, 96, 96, PixelFormats.Pbgra32);
        enlarged.Render(enlargedVisual);
        Save(Path.Combine(destination, "foot-registration-comparison-nearest3x.png"), enlarged);
    }

    private static void Save(string path, BitmapSource bitmap)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(path); encoder.Save(output);
    }

    private static void SaveEdgeComparison(string destination, string prior)
    {
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        Color[] backgrounds = [Color.FromRgb(17, 19, 27), Colors.White, Color.FromRgb(44, 100, 110)];
        using (var drawing = visual.RenderOpen())
        for (var row = 0; row < 6; row++)
        {
            var background = backgrounds[row / 2];
            var textColor = row / 2 == 1 ? Brushes.Black : Brushes.White;
            drawing.DrawRectangle(new SolidColorBrush(background), null, new Rect(0, row * 116, 832, 116));
            for (var frame = 1; frame <= 8; frame++)
            {
                var file = Path.Combine(row % 2 == 0 ? prior : destination, $"registration-key-{frame:D2}-presenter.png");
                var source = new BitmapImage(new Uri(file));
                if (source.PixelWidth != 96 || source.PixelHeight != 96) throw new InvalidDataException("Edge comparison requires actual native96 rasters.");
                var x = (frame - 1) * 104;
                drawing.DrawText(new FormattedText($"{(row % 2 == 0 ? "Before" : "After")} {frame}",
                    System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 11, textColor, 1), new Point(x + 4, row * 116));
                drawing.DrawImage(source, new Rect(x, row * 116 + 16, 96, 96));
            }
        }
        var native = new RenderTargetBitmap(832, 696, 96, 96, PixelFormats.Pbgra32);
        native.Render(visual);
        Save(Path.Combine(destination, "edge-background-comparison-native.png"), native);
        var enlarged = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(enlarged, BitmapScalingMode.NearestNeighbor);
        using (var drawing = enlarged.RenderOpen()) drawing.DrawImage(native, new Rect(0, 0, 1664, 1392));
        var bitmap = new RenderTargetBitmap(1664, 1392, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(enlarged);
        Save(Path.Combine(destination, "edge-background-comparison-nearest2x.png"), bitmap);
    }

    private sealed record FrameRow(int Index, string Sequence, int TimeMs, string Label, double PointerDistanceDips,
        string Phase, double Strength, double ReleaseProgress, bool PartialSettle, bool RequiresCapture,
        double WindowX, double WindowY, string NativePng, string PresenterPng);

    private const string Html = """
<!doctype html><meta charset="utf-8"><title>Head pull — offscreen production proof</title>
<style>body{font:16px system-ui;background:#eee;color:#222;max-width:960px;margin:32px auto}canvas{image-rendering:pixelated;background:repeating-conic-gradient(#ddd 0% 25%,#fff 0% 50%) 0/16px 16px}button,select{font:inherit;margin:8px}pre{white-space:pre-wrap}</style>
<h1>Head pull — offscreen production proof</h1><p>Actual production controller, brain and presenter. No app/window launched. 16 ms per frame at normal speed. Left: 96px native source enlarged 3×. Right: 144px offscreen presenter in a desktop-position canvas.</p>
<select id="sequence"><option>partial</option><option>full</option><option>overshoot</option></select><button id="toggle">Pause</button><button id="restart">Restart</button>
<div><canvas id="native" width="288" height="288"></canvas> <canvas id="desktop" width="560" height="320"></canvas></div><pre id="status"></pre>
<script>const frames=FRAME_DATA;const pictures=new Map;let playing=true,epoch=0,selected=[];const sequence=document.getElementById('sequence'),toggle=document.getElementById('toggle'),restart=document.getElementById('restart'),statusLabel=document.getElementById('status'),nativeCanvas=document.getElementById('native'),desk=document.getElementById('desktop'),nc=nativeCanvas.getContext('2d'),dc=desk.getContext('2d');nc.imageSmoothingEnabled=false;dc.imageSmoothingEnabled=false;let paused=0;
function choose(){selected=frames.filter(f=>f.Sequence===sequence.value);epoch=performance.now();paused=0;}sequence.onchange=choose;restart.onclick=choose;toggle.onclick=()=>{playing=!playing;if(playing)epoch=performance.now()-paused;else paused=performance.now()-epoch;toggle.textContent=playing?'Pause':'Play';};
Promise.all(frames.flatMap(f=>[f.NativePng,f.PresenterPng]).map(src=>new Promise(resolve=>{let img=new Image;img.onload=()=>{pictures.set(src,img);resolve()};img.src=src;}))).then(()=>{choose();requestAnimationFrame(draw)});
function draw(t){const i=Math.floor((playing?t-epoch:paused)/16)%selected.length,f=selected[i];nc.clearRect(0,0,288,288);nc.drawImage(pictures.get(f.NativePng),0,0,288,288);dc.clearRect(0,0,560,320);dc.drawImage(pictures.get(f.PresenterPng),f.WindowX,f.WindowY);statusLabel.textContent=JSON.stringify(f,null,2);requestAnimationFrame(draw);}
</script>
""";
}
