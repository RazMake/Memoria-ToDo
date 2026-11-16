using System.Threading.Channels;
using System.Windows.Input;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// While a user has clicked the real WebView2 adapter's page, the keys they press still reach the hosting window, because the
/// page never holds the keyboard focus. The tests click the page with the real mouse and press Esc, the function keys, and every
/// key pressed with Ctrl or Alt held, which the window sees as key presses. Keys are real key presses sent to Windows, so these
/// tests need the desktop's input, like the hot key tests.
/// </summary>
[Integration]
[Collection(ScreenTests.Collection)]
public class WpfDescriptionWebViewKeyHandOffTests
{
    [WpfFact]
    public async Task Hands_Ctrl_Tab_to_the_window()
    {
        using var harness = new DescriptionWebViewHarness();
        var keys = await FocusedPage(harness);

        KeyboardInput.Chord(KeyboardInput.Control, KeyboardInput.Tab);

        Assert.Equal((Key.Tab, ModifierKeys.Control), await Next(keys));
    }

    [WpfFact]
    public async Task Hands_Ctrl_Enter_to_the_window()
    {
        using var harness = new DescriptionWebViewHarness();
        var keys = await FocusedPage(harness);

        KeyboardInput.Chord(KeyboardInput.Control, KeyboardInput.Enter);

        Assert.Equal((Key.Enter, ModifierKeys.Control), await Next(keys));
    }

    [WpfFact]
    public async Task Hands_a_key_with_Alt_held_to_the_window()
    {
        using var harness = new DescriptionWebViewHarness();
        var keys = await FocusedPage(harness);

        KeyboardInput.Chord(KeyboardInput.Alt, KeyboardInput.Enter);

        Assert.Equal((Key.Enter, ModifierKeys.Alt), await Next(keys));
    }

    [WpfFact]
    public async Task Hands_a_function_key_to_the_window()
    {
        using var harness = new DescriptionWebViewHarness();
        var keys = await FocusedPage(harness);

        KeyboardInput.Chord(KeyboardInput.F5);

        Assert.Equal((Key.F5, ModifierKeys.None), await Next(keys));
    }

    [WpfFact]
    public async Task Hands_Esc_to_the_window()
    {
        using var harness = new DescriptionWebViewHarness();
        var keys = await FocusedPage(harness);

        KeyboardInput.Chord(KeyboardInput.Escape);

        Assert.Equal((Key.Escape, ModifierKeys.None), await Next(keys));
    }

    [WpfFact]
    public async Task Neither_clicking_the_page_nor_pressing_Tab_gives_it_the_keyboard_focus()
    {
        using var harness = new DescriptionWebViewHarness();
        await FocusedPage(harness);

        // A page that took the focus would hold it in the browser, outside WPF, so WPF would report no focused element.
        Assert.False(harness.Control.IsFocused);
        Assert.False(harness.Control.IsKeyboardFocusWithin);
        Assert.Same(harness.Window, Keyboard.FocusedElement);
    }

    private static Task<(Key, ModifierKeys)> Next(Channel<(Key, ModifierKeys)> keys) =>
        keys.Reader.ReadAsync().AsTask().WaitAsync(DescriptionWebViewHarness.Limit);

    // Shows a page, clicks it with the real mouse, as a user does to focus it, then moves the focus on as Tab does, and returns the key presses the hosting window
    // sees, as (key, modifiers). Keys are marked handled, as a window that used them would, so none goes on to reload or leave
    // the page.
    private static async Task<Channel<(Key, ModifierKeys)>> FocusedPage(DescriptionWebViewHarness harness)
    {
        var keys = Channel.CreateUnbounded<(Key, ModifierKeys)>();
        harness.Window.PreviewKeyDown += (_, e) =>
        {
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key is not (Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.F24))
            {
                keys.Writer.TryWrite((key, e.KeyboardDevice.Modifiers));
                e.Handled = true;
            }
        };
        await harness.Started;
        harness.Port.Show(DescriptionPage.Of("some text", dark: false, harness.Port.CloneAddress));
        await harness.NextLoadedPageTitle();
        await ClickPage(harness);
        // What Tab does: moves the focus to the next focusable element, which would be the page if it were focusable.
        harness.Window.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        return keys;
    }

    // Presses and releases the real left button on a blank part of the page, and returns once the window has seen the release.
    private static async Task ClickPage(DescriptionWebViewHarness harness)
    {
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Window.PreviewMouseLeftButtonUp += (_, _) => released.TrySetResult();
        KeyboardInput.BringToFront(new System.Windows.Interop.WindowInteropHelper(harness.Window).Handle);
        try
        {
            MouseInput.Press(harness.Control.PointToScreen(new System.Windows.Point(harness.Control.ActualWidth - 6, 4)));
        }
        finally
        {
            MouseInput.Release();
        }

        await released.Task.WaitAsync(DescriptionWebViewHarness.Limit);
    }
}
