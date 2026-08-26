using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dororong.Core.Behavior;
using Dororong.Core.Geometry;

namespace Dororong.App.Controls;

public sealed class BodyPressEventArgs(PointD localPosition) : EventArgs
{
    public PointD LocalPosition { get; } = localPosition;
}

public partial class DororongPresenter : UserControl
{
    private const double OpenEyeHeight = 16;
    private const double LeftPupilX = 37;
    private const double RightPupilX = 69;

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
                BodyScaleTransform.ScaleY = 1 + (0.025 * cycle);
                if (p is >= 0.66 and <= 0.72)
                {
                    SetEyeScaleY(0.12);
                }

                break;

            case PetState.Walk:
                BodyTranslateTransform.Y = -4 * Math.Abs(cycle);
                BodyScaleTransform.ScaleX = snapshot.Facing == FacingDirection.Left ? -1 : 1;
                break;

            case PetState.Curious:
                BodyRotateTransform.Angle = snapshot.Facing == FacingDirection.Right ? 7 : -7;
                var pupilOffset = snapshot.Facing == FacingDirection.Right ? 2 : -2;
                Canvas.SetLeft(LeftPupil, LeftPupilX + pupilOffset);
                Canvas.SetLeft(RightPupil, RightPupilX + pupilOffset);
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
                BodyRotateTransform.Angle = Math.Clamp(snapshot.GrabOffset?.X - 54 ?? 0, -8, 8);
                break;

            case PetState.Sleep:
                BodyScaleTransform.ScaleX = 1 + (0.025 * cycle);
                BodyScaleTransform.ScaleY = 0.82;
                BodyTranslateTransform.Y = 8;
                SetSleepingEyes(snapshot.IsDirectInteractionPending);
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

        LeftEye.Height = OpenEyeHeight;
        RightEye.Height = OpenEyeHeight;
        LeftEyeScaleTransform.ScaleX = 1;
        RightEyeScaleTransform.ScaleX = 1;
        SetEyeScaleY(1);

        Canvas.SetLeft(LeftPupil, LeftPupilX);
        Canvas.SetLeft(RightPupil, RightPupilX);
        LeftPupil.Visibility = Visibility.Visible;
        RightPupil.Visibility = Visibility.Visible;
    }

    private void SetEyeScaleY(double scaleY)
    {
        LeftEyeScaleTransform.ScaleY = scaleY;
        RightEyeScaleTransform.ScaleY = scaleY;
    }

    private void SetSleepingEyes(bool isOpening)
    {
        LeftEye.Height = isOpening ? OpenEyeHeight / 2 : 1;
        RightEye.Height = isOpening ? OpenEyeHeight / 2 : 1;
        LeftPupil.Visibility = isOpening ? Visibility.Visible : Visibility.Collapsed;
        RightPupil.Visibility = isOpening ? Visibility.Visible : Visibility.Collapsed;
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
