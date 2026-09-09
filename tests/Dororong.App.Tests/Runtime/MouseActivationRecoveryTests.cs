using Dororong.App.Interop;

namespace Dororong.App.Tests.Runtime;

public sealed class MouseActivationRecoveryTests
{
    [Fact]
    public void Unavailable_left_press_requests_explicit_activation_and_still_reaches_the_presenter()
    {
        // The callback is the native activation boundary. A WM_MOUSEACTIVATE
        // response alone did not activate the live WS_EX_NOACTIVATE window.
        var requests = 0;
        var handled = WindowActivationGuard.TryHandleMessage(0x201, out var result,
            false, () => requests++);
        Assert.Equal(1, requests);
        Assert.False(handled);
        Assert.Equal(IntPtr.Zero, result);
    }

    [Theory]
    [InlineData(0x201, true)]
    [InlineData(0x204, false)]
    [InlineData(0x200, false)]
    [InlineData(0x202, false)]
    [InlineData(0x21, false)]
    public void Available_press_hover_release_and_right_click_do_not_request_activation(int message, bool buttonDown)
    {
        var requests = 0;
        var handled = WindowActivationGuard.TryHandleMessage(message, out var result,
            buttonDown, () => requests++);
        Assert.Equal(0, requests);
        Assert.Equal(message == 0x21, handled);
        Assert.Equal(new IntPtr(message == 0x21 ? 3 : 0), result);
    }
}
