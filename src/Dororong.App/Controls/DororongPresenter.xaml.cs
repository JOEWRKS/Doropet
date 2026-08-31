using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Controls;

public sealed class BodyPressEventArgs(PointD localPosition) : EventArgs
{
    public PointD LocalPosition { get; } = localPosition;
}

public partial class DororongPresenter : UserControl
{
    private static readonly BitmapImage CanonicalFrame = LoadFrame("dororong-canonical.png");
    private static readonly BitmapImage BlinkSquintFrame = LoadFrame("dororong-blink-squint.png");
    private static readonly BitmapImage ClosedEyesFrame = LoadFrame("dororong-closed-eyes.png");
    private static readonly BitmapImage SleepFrame = LoadFrame("dororong-sleep.png");
    private static readonly BitmapImage SleepCrouchClosedFrame = LoadFrame("dororong-sleep-crouch-closed.png");
    private static readonly BitmapImage SleepCrouchSquintFrame = LoadFrame("dororong-sleep-crouch-squint.png");
    private static readonly BitmapImage SleepTuckClosedFrame = LoadFrame("dororong-sleep-tuck-closed.png");
    private static readonly BitmapImage SleepTuckSquintFrame = LoadFrame("dororong-sleep-tuck-squint.png");
    private static readonly CrossfadeFrame CanonicalCrossfadeFrame = new(CanonicalFrame);
    private static readonly CrossfadeFrame BlinkSquintCrossfadeFrame = new(BlinkSquintFrame);
    private static readonly CrossfadeFrame ClosedEyesCrossfadeFrame = new(ClosedEyesFrame);
    private static readonly CrossfadeFrame SleepCrouchClosedCrossfadeFrame = new(SleepCrouchClosedFrame);
    private static readonly CrossfadeFrame SleepTuckClosedCrossfadeFrame = new(SleepTuckClosedFrame);
    private static readonly CrossfadeFrame SleepCrossfadeFrame = new(SleepFrame);

    private PetState? _lastRenderedState;
    private bool _sleepEntryComplete;
    private PetState? _wakeBridgeState;
    private bool _wakeBridgeComplete;

    public DororongPresenter()
    {
        InitializeComponent();
    }

    public event EventHandler<BodyPressEventArgs>? BodyPrimaryPressed;

    public event EventHandler? ExitRequested;

    public void Render(PetSnapshot snapshot)
    {
        var p = Math.Clamp(snapshot.Phase, 0, 1);
        var cycle = Math.Sin(p * Math.PI * 2);
        var bounce = Math.Sin(p * Math.PI);
        var wokeFromSettledSleep =
            _lastRenderedState == PetState.Sleep &&
            _sleepEntryComplete;

        if (snapshot.State == PetState.Sleep && _lastRenderedState != PetState.Sleep)
        {
            _sleepEntryComplete = false;
            _wakeBridgeState = null;
            _wakeBridgeComplete = false;
        }
        else if (snapshot.State != PetState.Sleep && _lastRenderedState == PetState.Sleep)
        {
            _sleepEntryComplete = false;
            _wakeBridgeState = wokeFromSettledSleep && SupportsWakeBridge(snapshot.State)
                ? snapshot.State
                : null;
            _wakeBridgeComplete = !wokeFromSettledSleep;
        }
        else if (snapshot.State != PetState.Sleep && snapshot.State != _lastRenderedState)
        {
            _wakeBridgeState = null;
            _wakeBridgeComplete = false;
        }

        ResetPose();

        switch (snapshot.State)
        {
            case PetState.Idle:
                ApplyBreathing(p);
                if (p is >= 0.65 and < 0.69)
                {
                    DororongImage.Source = BlinkSquintFrame;
                }
                else if (p is >= 0.69 and < 0.73)
                {
                    DororongImage.Source = ClosedEyesFrame;
                }
                else if (p is >= 0.73 and < 0.77)
                {
                    DororongImage.Source = BlinkSquintFrame;
                }

                break;

            case PetState.Walk:
                BodyTranslateTransform.Y = -4 * Math.Abs(cycle);
                BodyScaleTransform.ScaleX = snapshot.Facing == FacingDirection.Left ? -1 : 1;
                break;

            case PetState.Curious:
                BodyRotateTransform.Angle = snapshot.Facing == FacingDirection.Right ? 7 : -7;
                break;

            case PetState.Startled:
                if (p < 0.5)
                {
                    BodyScaleTransform.ScaleX = 1 + (0.18 * bounce);
                    BodyScaleTransform.ScaleY = 1 - (0.14 * bounce);
                }

                break;

            case PetState.ClickReaction:
                BodyTranslateTransform.Y = -10 * bounce;
                break;

            case PetState.Dragged:
                BodyScaleTransform.ScaleY = 1.12;
                var bodyCenterX = Canvas.GetLeft(BodyGroup) + (BodyGroup.Width / 2);
                BodyRotateTransform.Angle = Math.Clamp(
                    (snapshot.GrabOffset?.X ?? bodyCenterX) - bodyCenterX,
                    -8,
                    8);
                break;

            case PetState.Sleep:
                ApplySleepPose(snapshot.Phase);
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(snapshot), snapshot.State, "Unknown pet state.");
        }

        ApplyWakeBridge(snapshot.State, p);
        _lastRenderedState = snapshot.State;
    }

    private static bool SupportsWakeBridge(PetState state) =>
        state is PetState.Curious or PetState.Startled or PetState.ClickReaction;

    private void ApplySleepPose(double phase)
    {
        if (_sleepEntryComplete)
        {
            ApplySettledSleep(phase);
            return;
        }

        if (phase < 0.0225)
        {
            ApplySleepCrossfade(CanonicalCrossfadeFrame, BlinkSquintCrossfadeFrame, phase, 0, 0.0225);
        }
        else if (phase < 0.045)
        {
            ApplySleepCrossfade(BlinkSquintCrossfadeFrame, ClosedEyesCrossfadeFrame, phase, 0.0225, 0.045);
        }
        else if (phase < 0.070)
        {
            ApplySleepCrossfade(ClosedEyesCrossfadeFrame, SleepCrouchClosedCrossfadeFrame, phase, 0.045, 0.070);
        }
        else if (phase < 0.100)
        {
            ApplySleepCrossfade(SleepCrouchClosedCrossfadeFrame, SleepTuckClosedCrossfadeFrame, phase, 0.070, 0.100);
        }
        else if (phase < 0.135)
        {
            ApplySleepCrossfade(SleepTuckClosedCrossfadeFrame, SleepCrossfadeFrame, phase, 0.100, 0.135);
        }
        else
        {
            _sleepEntryComplete = true;
            ApplySettledSleep(phase);
        }
    }

    private void ApplySleepCrossfade(
        CrossfadeFrame from,
        CrossfadeFrame to,
        double phase,
        double start,
        double end)
    {
        var progress = Math.Clamp((phase - start) / (end - start), 0, 1);
        var opacity = progress * progress * (3 - (2 * progress));
        if (opacity <= 0)
        {
            DororongImage.Source = from.Source;
            return;
        }

        if (opacity >= 1)
        {
            DororongImage.Source = to.Source;
            return;
        }

        var pixels = new byte[from.Pixels.Length];
        for (var index = 0; index < pixels.Length; index++)
        {
            pixels[index] = (byte)Math.Round(
                from.Pixels[index] + ((to.Pixels[index] - from.Pixels[index]) * opacity),
                MidpointRounding.AwayFromZero);
        }

        var blended = BitmapSource.Create(
            from.Source.PixelWidth,
            from.Source.PixelHeight,
            96,
            96,
            PixelFormats.Pbgra32,
            null,
            pixels,
            from.Stride);
        blended.Freeze();
        DororongImage.Source = blended;
    }

    private void ApplySettledSleep(double phase)
    {
        DororongImage.Source = SleepFrame;
        DororongImage.RenderTransformOrigin = new Point(0.435630, 0.854167);
        ApplyBreathing((phase - 0.135 + 1) % 1);
    }

    private void ApplyWakeBridge(PetState state, double phase)
    {
        if (_wakeBridgeState != state || _wakeBridgeComplete)
        {
            return;
        }

        var (firstLimit, secondLimit) = state switch
        {
            PetState.Curious => (0.05625, 0.1125),
            PetState.Startled => (0.06, 0.12),
            PetState.ClickReaction => (0.09, 0.18),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unsupported wake bridge state.")
        };

        if (phase < firstLimit)
        {
            DororongImage.Source = SleepTuckSquintFrame;
        }
        else if (phase < secondLimit)
        {
            DororongImage.Source = SleepCrouchSquintFrame;
        }
        else
        {
            _wakeBridgeComplete = true;
        }
    }

    private void ApplyBreathing(double phase)
    {
        var amount = GetBreathingAmount(phase);
        ImageBreathingScaleTransform.ScaleX = 1 + (0.024 * amount);
        ImageBreathingScaleTransform.ScaleY = 1 + (0.012 * amount);
    }

    private static double GetBreathingAmount(double phase)
    {
        if (phase < 0.38)
        {
            var inhaleProgress = phase / 0.38;
            return inhaleProgress * inhaleProgress * (3 - (2 * inhaleProgress));
        }

        if (phase <= 0.43)
        {
            return 1;
        }

        var exhaleProgress = (phase - 0.43) / 0.57;
        return 1 - (exhaleProgress * exhaleProgress * (3 - (2 * exhaleProgress)));
    }

    private void ResetPose()
    {
        BodyScaleTransform.ScaleX = 1;
        BodyScaleTransform.ScaleY = 1;
        BodyRotateTransform.Angle = 0;
        BodyTranslateTransform.X = 0;
        BodyTranslateTransform.Y = 0;
        ImageBreathingScaleTransform.ScaleX = 1;
        ImageBreathingScaleTransform.ScaleY = 1;
        DororongImage.RenderTransformOrigin = new Point(0.428987, 0.916667);
        DororongImage.Source = CanonicalFrame;
        DororongImage.Opacity = 1;
    }

    private sealed class CrossfadeFrame
    {
        public CrossfadeFrame(BitmapImage source)
        {
            Source = source;
            var converted = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
            Stride = converted.PixelWidth * 4;
            Pixels = new byte[Stride * converted.PixelHeight];
            converted.CopyPixels(Pixels, Stride, 0);
        }

        public BitmapImage Source { get; }

        public byte[] Pixels { get; }

        public int Stride { get; }
    }

    private static BitmapImage LoadFrame(string fileName)
    {
        var frame = new BitmapImage();
        frame.BeginInit();
        frame.UriSource = new Uri(
            $"pack://application:,,,/Dororong.App;component/Assets/{fileName}",
            UriKind.Absolute);
        frame.CacheOption = BitmapCacheOption.OnLoad;
        frame.EndInit();
        frame.Freeze();
        return frame;
    }

    private void OnBodyPrimaryPressed(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(this);
        BodyPrimaryPressed?.Invoke(
            this,
            new BodyPressEventArgs(new PointD(position.X, position.Y)));
        e.Handled = true;
    }

    private void OnExitClicked(object sender, RoutedEventArgs e)
    {
        ExitRequested?.Invoke(this, EventArgs.Empty);
    }
}
