using System.Windows.Controls;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Core;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>The Shortcuts card of Configure's Application section, built in memory with no window.</summary>
[Unit]
public class ShortcutSectionTests
{
    private static ShortcutSection Section()
    {
        var addNewTask = TestShortcutSetting.AddNewTask().Connected().Setting;
        var manageTasks = TestShortcutSetting.ManageTasks().Connected().Setting;
        var contacts = new ShortcutSetting("Contacts", TestShortcut.ContactsDefault, () => null, _ => { }, Logger.None);
        var configure = new ShortcutSetting("Configure", TestShortcut.ConfigureDefault, () => null, _ => { }, Logger.None);
        contacts.Connect(new FakeShortcutAction());
        configure.Connect(new FakeShortcutAction());
        return new ShortcutSection(
            addNewTask, manageTasks, contacts, configure,
            new CloseWindowSetting(() => null, _ => { }, (_, _) => { }, Logger.None),
            new SaveSetting(() => null, _ => { }, (_, _) => { }, Logger.None));
    }

    [WpfFact]
    public void Shows_six_rows_in_order_with_their_current_shortcuts()
    {
        var section = Section();

        var shown = OpenWindows.Descendants(section).OfType<ShortcutField>().Select(field => field.Text).ToArray();

        Assert.Equal(["Win+Alt+N", "Win+Alt+V", "Win+Alt+O", "Win+Alt+C", "Shift+Escape", "Ctrl+Enter"], shown);
    }

    [WpfFact]
    public void Names_the_rows_in_order_and_says_how_to_change_a_shortcut()
    {
        var section = Section();

        var texts = OpenWindows.Descendants(section).OfType<TextBlock>().Select(OpenWindows.TextOf).ToArray();

        Assert.Contains("Click a box, then press the new shortcut. Esc keeps the current one:", texts);
        string[] labels = ["Add new task", "Task View", "Contacts", "Configure", "Close window", "Save"];
        Assert.Equal(labels, texts.Where(labels.Contains).ToArray());
    }

    [WpfFact]
    public void Shows_no_message_before_anything_is_picked()
    {
        var section = Section();

        var messages = OpenWindows.Descendants(section).OfType<TextBlock>()
            .Where(block => block.Visibility == System.Windows.Visibility.Visible)
            .Select(OpenWindows.TextOf)
            .Where(text => text.Contains("already uses", StringComparison.Ordinal) || text.Contains("Use ", StringComparison.Ordinal));

        Assert.Empty(messages);
    }
}
