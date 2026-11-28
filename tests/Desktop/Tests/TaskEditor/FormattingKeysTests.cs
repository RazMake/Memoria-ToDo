using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>Which key runs which formatting command. What each command does to text is Editing's, tested there.</summary>
[Unit]
public class FormattingKeysTests
{
    private const ModifierKeys Ctrl = ModifierKeys.Control;
    private const ModifierKeys CtrlShift = ModifierKeys.Control | ModifierKeys.Shift;

    private readonly FakeTextField _field = new("a b c", 2, 1);
    private readonly KeyMap _keys = new();
    private int _colorsOpened;

    public FormattingKeysTests() => FormattingKeys.AddInline(_keys, _field, () => _colorsOpened++);

    [Theory]
    [InlineData(Key.B, false, "a **b** c")]
    [InlineData(Key.I, false, "a *b* c")]
    [InlineData(Key.X, true, "a ~~b~~ c")]
    [InlineData(Key.E, false, "a `b` c")]
    public void The_key_wraps_the_selection_in_its_markers(Key key, bool withShift, string expected)
    {
        var handled = _keys.TryRun(withShift ? CtrlShift : Ctrl, key);

        Assert.True(handled);
        Assert.Equal(expected, _field.Text);
    }

    [Fact]
    public void The_key_leaves_the_wrapped_text_selected()
    {
        _keys.TryRun(Ctrl, Key.B);

        Assert.Equal("b", _field.Text.Substring(_field.SelectionStart, _field.SelectionLength));
    }

    [Fact]
    public void One_undo_reverts_a_formatting_key()
    {
        _keys.TryRun(Ctrl, Key.B);

        _field.Undo();

        Assert.Equal("a b c", _field.Text);
    }

    [Fact]
    public void Ctrl_Shift_C_opens_the_colors_and_changes_no_text()
    {
        var handled = _keys.TryRun(CtrlShift, Key.C);

        Assert.True(handled);
        Assert.Equal(1, _colorsOpened);
        Assert.Equal("a b c", _field.Text);
    }

    [Theory]
    [InlineData(Key.B, false)]
    [InlineData(Key.I, false)]
    [InlineData(Key.X, true)]
    [InlineData(Key.E, false)]
    [InlineData(Key.C, true)]
    public void No_formatting_key_works_with_Ctrl_and_Alt_held(Key key, bool withShift)
    {
        var modifiers = ModifierKeys.Control | ModifierKeys.Alt | (withShift ? ModifierKeys.Shift : ModifierKeys.None);

        var handled = _keys.TryRun(modifiers, key);

        Assert.False(handled);
        Assert.Equal("a b c", _field.Text);
        Assert.Equal(0, _colorsOpened);
    }

    [Fact]
    public void Heading_keys_are_not_registered_unless_asked_for()
    {
        Assert.False(_keys.TryRun(Ctrl, Key.D1));
    }

    [Theory]
    [InlineData(Key.D1, "# Plan")]
    [InlineData(Key.D2, "## Plan")]
    [InlineData(Key.D3, "### Plan")]
    public void Ctrl_and_a_digit_sets_the_heading_level_of_the_line(Key digit, string expected)
    {
        var description = new FakeTextField("Plan", 2);
        var keys = new KeyMap();
        FormattingKeys.AddHeadings(keys, description);

        var handled = keys.TryRun(Ctrl, digit);

        Assert.True(handled);
        Assert.Equal(expected, description.Text);
    }

    [Fact]
    public void A_heading_key_changes_only_the_line_with_the_cursor()
    {
        var description = new FakeTextField("One\nTwo", 5);
        var keys = new KeyMap();
        FormattingKeys.AddHeadings(keys, description);

        keys.TryRun(Ctrl, Key.D1);

        Assert.Equal("One\n# Two", description.Text);
    }
}
