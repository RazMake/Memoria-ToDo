using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// Two modifier keys pressed and released with no other key say the shortcut may be taken. The rules are
/// unit-tested on ShortcutKeyInput and ShortcutSetting; this checks that real key events reach them.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class ConfigureSwallowedKeyTests
{
    [WpfFact]
    public void Two_modifiers_pressed_and_released_with_no_other_key_say_the_shortcut_may_be_taken()
    {
        using var screen = new ConfigureScreen();
        screen.Open();

        screen.Chord(screen.AddField, Key.LeftCtrl, Key.LeftShift);

        Assert.Equal(
            "If you pressed a key with Ctrl+Shift, Windows or another app uses that shortcut. Press a different one.",
            screen.AddMessage);
        Assert.Equal("", screen.ManageMessage);
    }
}
