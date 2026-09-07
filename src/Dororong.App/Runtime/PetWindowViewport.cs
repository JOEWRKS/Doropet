using System.Windows;
using Dororong.App.Controls;
using Dororong.Core.Geometry;

namespace Dororong.App.Runtime;

// Transparent render gutter only. The brain, input and captures continue to use
// the original 144-DIP presenter position/size, not the larger native window.
internal sealed class PetWindowViewport
{
    private const double Padding = 96;
    private readonly Window _window;
    private readonly DororongPresenter _presenter;

    internal PetWindowViewport(Window window, DororongPresenter presenter)
    {
        _window = window;
        _presenter = presenter;
        presenter.HorizontalAlignment = HorizontalAlignment.Center;
        presenter.VerticalAlignment = VerticalAlignment.Center;
        window.Width = presenter.Width + 2 * Padding;
        window.Height = presenter.Height + 2 * Padding;
    }

    internal SizeD GetPetSize() => new(_presenter.Width, _presenter.Height);
    internal PointD GetPosition() => new(_window.Left + Padding, _window.Top + Padding);
    internal void SetPosition(PointD position)
    {
        _window.Left = position.X - Padding;
        _window.Top = position.Y - Padding;
    }
}
