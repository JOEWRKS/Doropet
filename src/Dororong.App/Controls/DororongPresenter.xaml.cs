using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        ResetPose();

        switch (snapshot.State)
        {
            case PetState.Idle:
                BodyTranslateTransform.Y = Math.Round(-cycle);
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
                ImageBreathingScaleTransform.ScaleX = 1 + (0.012 * bounce);
                ImageBreathingScaleTransform.ScaleY = 1 + (0.012 * bounce);
                DororongImage.Source = ClosedEyesFrame;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(snapshot), snapshot.State, "Unknown pet state.");
        }
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
        DororongImage.Source = CanonicalFrame;
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
