using System.Windows;
using System.Windows.Interop;
using Dororong.App.Interop;

namespace Dororong.App;

public partial class MainWindow : Window
{
    private WindowActivationGuard? _activationGuard;

    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
    }

    protected override void OnClosed(EventArgs e)
    {
        SourceInitialized -= OnSourceInitialized;

        try
        {
            DetachActivationGuard();
        }
        finally
        {
            base.OnClosed(e);
        }
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        if (_activationGuard?.IsAttached == true)
        {
            return;
        }

        DetachActivationGuard();

        var handle = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(handle)
            ?? throw new InvalidOperationException("The WPF window source is unavailable.");

        WindowStyleManager.ApplyNoActivateToolWindow(handle);
        _activationGuard = new WindowActivationGuard(source);
    }

    private void DetachActivationGuard()
    {
        _activationGuard?.Dispose();
        _activationGuard = null;
    }
}
