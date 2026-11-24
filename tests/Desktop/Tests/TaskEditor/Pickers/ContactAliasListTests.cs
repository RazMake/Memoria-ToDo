using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests.Pickers;

/// <summary>
/// The "@" list of a plain WPF text box, as Task View's search box uses it. Each test follows one path: typing "@" and choosing a
/// contact, the keys the list takes from the box's owner, and the alias's color in each theme. The rows, keys, and closing rules
/// are in the unit tests.
/// </summary>
[UX("Pickers")]
[Collection(ScreenTests.Collection)]
public class ContactAliasListTests : IDisposable
{
    private readonly OpenWindows _windows = new();
    private readonly EditorServices _services = new();
    private readonly TextBox _box = new() { Width = 300 };
    private readonly Window _window;
    private readonly List<Key> _reachedTheWindow = [];

    public ContactAliasListTests()
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

    [WpfFact]
    public void Typing_an_at_sign_shows_the_contacts_and_Down_then_Enter_puts_the_alias_in_the_box()
    {
        EditorFields.TypeOnFocused("@");
        Assert.Equal(["Ana Pop (anap)", "Bob Stan (bob)"], Items());
        Assert.Equal("@", _box.Text);
        Assert.Null(TextFields.FindShowing(ShownList()!, EditorFields.MentionHeader));
        Assert.Null(TextFields.FindShowing(ShownList()!, EditorFields.MentionHint));

        EditorFields.TypeOnFocused("bo");
        Assert.Equal(["Bob Stan (bob)"], Items());

        _box.Clear();
        EditorFields.TypeOnFocused("find @an");
        Assert.True(Press(Key.Down));
        Assert.True(Press(Key.Enter));
        Assert.Equal("find @anap", _box.Text);
        Assert.Equal(_box.Text.Length, _box.CaretIndex);
        Assert.Empty(Items());
        Assert.Same(_box, Keyboard.FocusedElement);
    }

    [WpfFact]
    public void The_list_takes_Down_Up_Enter_and_Esc_from_the_owner_only_while_it_is_open_and_Tab_closes_it()
    {
        Press(Key.Down);
        Press(Key.Up);
        Press(Key.Escape);
        Assert.Equal([Key.Down, Key.Up, Key.Escape], _reachedTheWindow);
        _reachedTheWindow.Clear();

        EditorFields.TypeOnFocused("@");
        Press(Key.Down);
        Press(Key.Up);
        Press(Key.Escape);
        EditorFields.TypeOnFocused("@");
        Press(Key.Down);
        Press(Key.Enter);
        Assert.DoesNotContain(_reachedTheWindow, key => key is Key.Down or Key.Up or Key.Enter or Key.Escape);

        _box.Clear();
        EditorFields.TypeOnFocused("@an");
        Assert.True(Press(Key.Escape));
        Assert.Empty(Items());
        Assert.Equal("@an", _box.Text);

        _box.Clear();
        EditorFields.TypeOnFocused("@an");
        Assert.False(Press(Key.Tab));
        Assert.Empty(Items());
        Assert.Equal("@an", _box.Text);
    }

    [WpfFact]
    public void A_rows_alias_is_in_the_italic_color_of_the_theme_and_follows_a_switch_while_the_list_shows()
    {
        EditorFields.TypeOnFocused("@");
        var look = TextLooks.Of(false);
        var name = RowRuns().Single(run => run.Text == "Ana Pop");
        Assert.Equal(Hex(look.Italic), ColorOf(AliasRun().Foreground));
        Assert.NotEqual(Hex(look.Italic), ColorOf(name.Foreground));
        Assert.Equal(ColorOf(RowRuns().First(run => run.Text == " (").Foreground), ColorOf(name.Foreground));

        _services.Theme.Switch(true);

        Assert.Equal(Hex(TextLooks.Of(true).Italic), ColorOf(AliasRun().Foreground));
    }

    private static FrameworkElement? ShownList() => EditorFields.ShownPopupMentionList();

    private static IReadOnlyList<string> Items() => EditorFields.ShownPopupMentions();

    private bool Press(Key key) => SimulatedKey.Raise(_box, key, Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent);

    private static List<Run> RowRuns()
    {
        var list = ShownList()!;
        list.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        list.UpdateLayout();
        return [.. VisualTree.Visuals(list).OfType<TextBlock>().SelectMany(block => block.Inlines).OfType<Run>()];
    }

    private static Run AliasRun() => RowRuns().Single(run => run.Text == "anap");

    private static Color Hex(string color) => (Color)ColorConverter.ConvertFromString(color);

    private static Color ColorOf(Brush brush) => Assert.IsType<SolidColorBrush>(brush).Color;

    public void Dispose() => _windows.Dispose();
}
