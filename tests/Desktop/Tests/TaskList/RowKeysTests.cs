using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>The keys that act on the selected row, and what each does.</summary>
[Unit]
public class RowKeysTests
{
    [Theory]
    [InlineData(ModifierKeys.None, Key.Enter, "Toggle")]
    [InlineData(ModifierKeys.None, Key.F2, "Edit")]
    [InlineData(ModifierKeys.Control, Key.D, "Resolve")]
    [InlineData(ModifierKeys.None, Key.Delete, "Delete")]
    [InlineData(ModifierKeys.Alt, Key.Up, "MoveUp")]
    [InlineData(ModifierKeys.Alt, Key.Down, "MoveDown")]
    public void Acts_on_the_row_for_each_of_its_keys(ModifierKeys modifiers, Key key, string expected)
    {
        Assert.Equal(expected, RowKeys.ActionFor(modifiers, key)?.ToString());
    }

    [Theory]
    [InlineData(ModifierKeys.Control, Key.Enter)]
    [InlineData(ModifierKeys.Shift, Key.Delete)]
    [InlineData(ModifierKeys.None, Key.Up)]
    [InlineData(ModifierKeys.None, Key.D)]
    [InlineData(ModifierKeys.Alt | ModifierKeys.Control, Key.Down)]
    [InlineData(ModifierKeys.None, Key.F5)]
    public void Leaves_every_other_key_alone(ModifierKeys modifiers, Key key)
    {
        Assert.Null(RowKeys.ActionFor(modifiers, key));
    }
}
