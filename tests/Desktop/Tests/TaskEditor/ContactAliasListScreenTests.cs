using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The "@" list of a plain WPF text box, as Task View's search box uses it, on its common paths: typing "@" shows the contacts in a popup
/// with no header, and Down then Enter replaces the "@" and what was typed with "@" and the alias. While the list is open it takes Down, Up,
/// Enter, and Esc before the box's owner; Tab closes it and goes on. The rows, keys, and closing rules are in the unit tests.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class ContactAliasListScreenTests : IDisposable
{
    private readonly OpenWindows _windows = new();
    private readonly EditorServices _services = new();
    private readonly TextBox _box = new() { Width = 300 };
    private readonly Window _window;
    private readonly List<Key> _reachedTheWindow = [];

    public ContactAliasListScreenTests()
    {
        _services.UseContacts(
            ["{fullName}"],
            ContactFiles.Person("ana", "Ana Pop", ("alias", "anap")),
            ContactFiles.Person("bob", "Bob Stan"));
        ContactAliasList.Attach(_box, _services.Contacts, _services.Theme);
        _window = new Window { Content = new StackPanel { Children = { _box } }, Width = 400, Height = 300 };
        _window.PreviewKeyDown += (_, e) => _reachedTheWindow.Add(e.Key);
        _window.Show();
        _box.Focus();
        Keyboard.Focus(_box);
    }

    private static FrameworkElement? ShownList() => EditorFields.ShownPopupMentionList();

    private static IReadOnlyList<string> Items() => EditorFields.ShownPopupMentions();

    private bool Press(Key key) => SimulatedKey.Raise(_box, key, Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent);

    [WpfFact]
    public void Typing_an_at_sign_shows_the_contacts_in_a_popup_with_no_header_and_leaves_the_at_sign_in_the_box()
    {
        EditorFields.TypeOnFocused("@");

        Assert.Equal(["Ana Pop (anap)", "Bob Stan (bob)"], Items());
        Assert.Equal("@", _box.Text);
        Assert.Null(TextFields.FindShowing(ShownList()!, EditorFields.MentionHeader));
        Assert.Null(TextFields.FindShowing(ShownList()!, EditorFields.MentionHint));
    }

    [WpfFact]
    public void Typing_after_the_at_sign_narrows_the_list()
    {
        EditorFields.TypeOnFocused("@bo");

        Assert.Equal(["Bob Stan (bob)"], Items());
    }

    [WpfFact]
    public void Down_then_Enter_replaces_the_at_sign_and_what_was_typed_with_the_at_sign_and_the_alias_and_the_box_keeps_the_focus()
    {
        EditorFields.TypeOnFocused("find @an");

        Assert.True(Press(Key.Down));
        Assert.True(Press(Key.Enter));

        Assert.Equal("find @anap", _box.Text);
        Assert.Equal(_box.Text.Length, _box.CaretIndex);
        Assert.Empty(Items());
        Assert.Same(_box, Keyboard.FocusedElement);
    }

    [WpfFact]
    public void While_the_list_is_open_Down_Up_Enter_and_Esc_do_not_reach_the_owner_of_the_box()
    {
        EditorFields.TypeOnFocused("@");
        Press(Key.Down);
        Press(Key.Up);
        Press(Key.Escape);

        EditorFields.TypeOnFocused("@");
        Press(Key.Down);
        Press(Key.Enter);

        Assert.DoesNotContain(_reachedTheWindow, key => key is Key.Down or Key.Up or Key.Enter or Key.Escape);
    }

    [WpfFact]
    public void With_the_list_closed_Down_Up_Enter_and_Esc_reach_the_owner_of_the_box()
    {
        Press(Key.Down);
        Press(Key.Up);
        Press(Key.Escape);

        Assert.Equal([Key.Down, Key.Up, Key.Escape], _reachedTheWindow);
    }

    [WpfFact]
    public void Esc_closes_the_list_and_keeps_the_typed_text()
    {
        EditorFields.TypeOnFocused("@an");

        Assert.True(Press(Key.Escape));

        Assert.Empty(Items());
        Assert.Equal("@an", _box.Text);
    }

    [WpfFact]
    public void Tab_closes_the_list_and_is_left_for_the_box_to_move_the_focus()
    {
        EditorFields.TypeOnFocused("@an");

        var taken = Press(Key.Tab);

        Assert.False(taken);
        Assert.Empty(Items());
        Assert.Equal("@an", _box.Text);
    }

    private static List<System.Windows.Documents.Run> RowRuns()
    {
        var list = ShownList()!;
        list.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        list.UpdateLayout();
        return [.. VisualTree.Visuals(list).OfType<TextBlock>().SelectMany(block => block.Inlines).OfType<System.Windows.Documents.Run>()];
    }

    private static System.Windows.Documents.Run AliasRun() => RowRuns().Single(run => run.Text == "anap");

    private static System.Windows.Media.Color Hex(string color) =>
        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color);

    private static System.Windows.Media.Color ColorOf(System.Windows.Media.Brush brush) =>
        Assert.IsType<System.Windows.Media.SolidColorBrush>(brush).Color;

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_rows_alias_is_in_the_italic_color_of_the_current_theme_and_its_name_keeps_the_text_color(bool dark)
    {
        _services.Theme.IsDark = dark;
        EditorFields.TypeOnFocused("@");

        var runs = RowRuns();
        var name = runs.Single(run => run.Text == "Ana Pop");

        Assert.Equal(Hex(Memoria.ToDo.Shared.Rendering.TextLooks.Of(dark).Italic), ColorOf(AliasRun().Foreground));
        Assert.NotEqual(Hex(Memoria.ToDo.Shared.Rendering.TextLooks.Of(dark).Italic), ColorOf(name.Foreground));
        Assert.Equal(ColorOf(runs.First(run => run.Text == " (").Foreground), ColorOf(name.Foreground));
    }

    [WpfFact]
    public void A_rows_alias_changes_to_the_other_themes_italic_color_when_Windows_switches_while_the_list_shows()
    {
        EditorFields.TypeOnFocused("@");

        _services.Theme.Switch(true);

        Assert.Equal(Hex(Memoria.ToDo.Shared.Rendering.TextLooks.Of(true).Italic), ColorOf(AliasRun().Foreground));
    }

    public void Dispose() => _windows.Dispose();
}
