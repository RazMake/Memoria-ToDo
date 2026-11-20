using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The Up key with one of the editor's popup lists open (the color, snippet, or task list, each a <see cref="ChoiceList"/> that
/// <see cref="PopupLists"/> manages): the list handles it, so the caret doesn't move, and with no list open Up keeps its usual meaning.
/// </summary>
[Unit]
public class PopupListsUpKeyTests
{
    private readonly ChoiceList _color = new();
    private readonly ChoiceList _snippet = new();
    private readonly ChoiceList _task = new();
    private readonly PopupLists _popups;
    private readonly KeyMap _keys = new();

    public PopupListsUpKeyTests()
    {
        _popups = new PopupLists(_color, _snippet, _task);
        _popups.AddKeysTo(_keys);
    }

    private ChoiceList Open(string which)
    {
        var list = which switch { "color" => _color, "snippet" => _snippet, _ => _task };
        _popups.Show(list, ["a", "b", "c"]);
        return list;
    }

    private bool Up() => _keys.TryRun(ModifierKeys.None, Key.Up);

    private bool Down() => _keys.TryRun(ModifierKeys.None, Key.Down);

    [Theory]
    [InlineData("color")]
    [InlineData("snippet")]
    [InlineData("task")]
    public void Up_moves_to_the_previous_item_and_is_handled_so_the_caret_stays(string which)
    {
        var list = Open(which);
        Down();
        Down();

        Assert.True(Up());

        Assert.Equal(0, list.SelectedIndex);
    }

    [Theory]
    [InlineData("color")]
    [InlineData("snippet")]
    [InlineData("task")]
    public void Up_stops_at_the_first_item_and_is_still_handled(string which)
    {
        var list = Open(which);
        Down();

        Assert.True(Up());
        Assert.True(Up());

        Assert.Equal(0, list.SelectedIndex);
    }

    [Theory]
    [InlineData("color")]
    [InlineData("snippet")]
    [InlineData("task")]
    public void Up_with_nothing_moved_to_yet_moves_nowhere_and_is_handled(string which)
    {
        var list = Open(which);

        Assert.True(Up());

        Assert.Equal(-1, list.SelectedIndex);
        Assert.True(list.IsOpen);
    }

    [Fact]
    public void Up_with_no_list_open_is_not_handled_so_it_moves_the_caret()
    {
        Assert.False(Up());
    }
}
