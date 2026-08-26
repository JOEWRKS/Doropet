using System.Windows;
using System.Windows.Interop;
using Dororong.App.Interop;

namespace Dororong.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        WindowStyleManager.ApplyNoActivateToolWindow(new WindowInteropHelper(this).Handle);
    }
}
