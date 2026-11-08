using System.Windows.Input;
using Memoria.ToDo.Desktop.Configure;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Shell;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The RegisterHotKey adapter, through <see cref="IGlobalShortcut"/>. Each test uses its own rarely used key
/// (F20 to F24 with Ctrl+Alt+Shift), because a registered shortcut is shared by the whole Windows session. The
/// press is sent as real key presses. The adapter, <c>HotKeyShortcut</c>, is created on the test's UI thread.
/// </summary>
[Integration]
public class HotKeyShortcutTests
{
    private const ModifierKeys Modifiers = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift;
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    private static Shortcut Of(Key key) => TestShortcut.Of(Modifiers, key);

    [WpfFact]
    public void A_free_shortcut_registers()
    {
        IGlobalShortcut port = new HotKeyShortcut();
        var shortcut = Of(Key.F20);
        try
        {
            Assert.True(port.Register(shortcut, () => { }));
        }
        finally
        {
            port.Unregister(shortcut);
        }
    }

    [WpfFact]
    public void A_shortcut_held_by_another_registration_is_taken()
    {
        IGlobalShortcut first = new HotKeyShortcut();
        IGlobalShortcut second = new HotKeyShortcut();
        var shortcut = Of(Key.F21);
        Assert.True(first.Register(shortcut, () => { }));
        try
        {
            Assert.False(second.Register(shortcut, () => { }));
        }
        finally
        {
            first.Unregister(shortcut);
        }
    }

    [WpfFact]
    public void Unregister_releases_the_shortcut_for_another_registration()
    {
        IGlobalShortcut first = new HotKeyShortcut();
        IGlobalShortcut second = new HotKeyShortcut();
        var shortcut = Of(Key.F22);
        Assert.True(first.Register(shortcut, () => { }));

        first.Unregister(shortcut);

        try
        {
            Assert.True(second.Register(shortcut, () => { }));
        }
        finally
        {
            second.Unregister(shortcut);
        }
    }

    [WpfFact]
    public async Task A_press_runs_the_action_on_the_UI_thread()
    {
        IGlobalShortcut port = new HotKeyShortcut();
        var shortcut = Of(Key.F23);
        var uiThread = Environment.CurrentManagedThreadId;
        var pressed = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(port.Register(shortcut, () => pressed.TrySetResult(Environment.CurrentManagedThreadId)));
        try
        {
            KeyboardInput.Chord(KeyboardInput.Control, KeyboardInput.Alt, KeyboardInput.Shift, 0x86); // 0x86 is F23.

            var threadOfAction = await pressed.Task.WaitAsync(Limit, TestContext.Current.CancellationToken);

            Assert.Equal(uiThread, threadOfAction);
        }
        finally
        {
            port.Unregister(shortcut);
        }
    }

    [WpfFact]
    public async Task A_failed_release_leaves_the_shortcut_running_its_action()
    {
        IGlobalShortcut port = new HotKeyShortcut();
        var shortcut = Of(Key.F24);
        var pressed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.True(port.Register(shortcut, () => pressed.TrySetResult()));
        try
        {
            // Windows refuses to release a hot key from a thread other than its window's, so this release really fails.
            await Assert.ThrowsAnyAsync<Exception>(() => Task.Run(() => port.Unregister(shortcut), TestContext.Current.CancellationToken));

            KeyboardInput.Chord(KeyboardInput.Control, KeyboardInput.Alt, KeyboardInput.Shift, 0x87); // 0x87 is F24.

            await pressed.Task.WaitAsync(Limit, TestContext.Current.CancellationToken);
        }
        finally
        {
            port.Unregister(shortcut);
        }
    }
}
