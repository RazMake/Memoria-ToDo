using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Editing;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// Which key inserts which snippet, and the input the app owes it: the local time, the clipboard's text, the task chosen,
/// the image. What each snippet writes is Editing's, tested there.
/// </summary>
[Unit]
public class SnippetKeysTests
{
    private const ModifierKeys Ctrl = ModifierKeys.Control;
    private const ModifierKeys CtrlShift = ModifierKeys.Control | ModifierKeys.Shift;

    private readonly FakeTimeProvider _time = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly List<(string Snippet, int ReplaceFrom)> _askedForTask = [];
    private readonly List<(string Snippet, int? ReplaceFrom)> _askedForImage = [];

    private (FakeTextField Field, KeyMap Keys) Keys(bool isTitle, string text = "", int start = 0, int length = 0)
    {
        var field = new FakeTextField(text, start, length);
        var inserter = new SnippetInserter(_time, _clipboard,
            (_, snippet, replaceFrom) => _askedForTask.Add((snippet.Name, replaceFrom)),
            (_, snippet, replaceFrom) => _askedForImage.Add((snippet.Name, replaceFrom)));
        var keys = new KeyMap();
        SnippetKeys.AddTo(keys, field, isTitle, inserter);
        return (field, keys);
    }

    [Fact]
    public void Ctrl_semicolon_inserts_the_local_date_at_the_time_of_the_key()
    {
        _time.Advance(TimeSpan.FromDays(3));
        var (field, keys) = Keys(false, "on ", 3);

        Assert.True(keys.TryRun(Ctrl, Key.OemSemicolon));

        Assert.Equal("on " + _time.GetLocalNow().ToString("yyyy-MM-dd"), field.Text);
    }

    [Fact]
    public void Ctrl_Shift_semicolon_inserts_the_local_date_and_time()
    {
        _time.Advance(TimeSpan.FromHours(10.5));
        var (field, keys) = Keys(false);

        keys.TryRun(CtrlShift, Key.OemSemicolon);

        Assert.Equal(_time.GetLocalNow().ToString("yyyy-MM-dd HH:mm"), field.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_stamp_keys_work_in_the_Title(bool withShift)
    {
        var (field, keys) = Keys(true, "Call ", 5);

        Assert.True(keys.TryRun(withShift ? CtrlShift : Ctrl, Key.OemSemicolon));

        Assert.StartsWith("Call 20", field.Text);
        Assert.Equal(1, field.FocusCount);
    }

    [Theory]
    [InlineData(Key.N, "> [!NOTE]\n> ")]
    [InlineData(Key.T, "> [!TIP]\n> ")]
    [InlineData(Key.W, "> [!WARNING]\n> ")]
    public void The_block_keys_insert_their_block_with_the_cursor_after_it(Key key, string expected)
    {
        var (field, keys) = Keys(false);

        Assert.True(keys.TryRun(CtrlShift, key));

        Assert.Equal(expected, field.Text);
        Assert.Equal(expected.Length, field.CaretOffset);
    }

    [Fact]
    public void A_block_key_after_text_starts_the_block_on_its_own_line()
    {
        var (field, keys) = Keys(false, "hello", 5);

        keys.TryRun(CtrlShift, Key.N);

        Assert.Equal("hello\n> [!NOTE]\n> ", field.Text);
    }

    [Theory]
    [InlineData(Key.E, "Code block")]
    [InlineData(Key.G, "Table")]
    [InlineData(Key.OemMinus, "Horizontal line")]
    public void The_code_table_and_line_keys_insert_their_snippet_in_the_Description(Key key, string snippet)
    {
        var (field, keys) = Keys(false, "hello", 5);
        var expected = Snippets.All.Single(s => s.Name == snippet).Insert(new TextAndSelection("hello", 5, 0), new SnippetInput());

        Assert.True(keys.TryRun(CtrlShift, key));

        Assert.Equal(expected.Text, field.Text);
        Assert.Equal(expected.SelectionStart, field.CaretOffset);
    }

    [Theory]
    [InlineData(Key.N)]
    [InlineData(Key.T)]
    [InlineData(Key.W)]
    [InlineData(Key.E)]
    [InlineData(Key.G)]
    [InlineData(Key.OemMinus)]
    public void A_snippet_that_does_not_work_in_the_Title_is_not_handled_there(Key key)
    {
        var (field, keys) = Keys(true, "ab", 1);

        Assert.False(keys.TryRun(CtrlShift, key));

        Assert.Equal("ab", field.Text);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ctrl_K_makes_the_selection_the_text_of_a_link_to_the_URL_on_the_clipboard(bool isTitle)
    {
        _clipboard.Content = "https://example.com/docs";
        var (field, keys) = Keys(isTitle, "see docs now", 4, 4);

        Assert.True(keys.TryRun(Ctrl, Key.K));

        Assert.Equal("see [docs](https://example.com/docs) now", field.Text);
    }

    [Theory]
    [InlineData("just some words")]
    [InlineData("")]
    [InlineData(null)]
    public void Ctrl_K_without_a_web_address_on_the_clipboard_leaves_the_address_empty(string? clipboard)
    {
        _clipboard.Content = clipboard;
        var (field, keys) = Keys(false, "docs", 0, 4);

        keys.TryRun(Ctrl, Key.K);

        Assert.Equal("[docs]()", field.Text);
    }

    [Fact]
    public void Ctrl_K_reads_the_clipboard_when_the_key_is_pressed()
    {
        _clipboard.Content = "old";
        var (field, keys) = Keys(false, "docs", 0, 4);
        _clipboard.Content = "https://example.com/new";

        keys.TryRun(Ctrl, Key.K);

        Assert.Equal("[docs](https://example.com/new)", field.Text);
    }

    [Fact]
    public void Ctrl_Shift_R_asks_for_a_task_to_replace_from_the_selection_start()
    {
        var (field, keys) = Keys(false, "see ", 4);

        Assert.True(keys.TryRun(CtrlShift, Key.R));

        Assert.Equal([("Task reference", 4)], _askedForTask);
        Assert.Equal("see ", field.Text);
    }

    [Fact]
    public void Ctrl_Shift_I_asks_for_an_image_replacing_nothing_before_the_selection()
    {
        var (_, keys) = Keys(false);

        Assert.True(keys.TryRun(CtrlShift, Key.I));

        Assert.Equal([("Image", (int?)null)], _askedForImage);
    }

    [Fact]
    public void An_unrelated_chord_is_not_handled()
    {
        var (_, keys) = Keys(false);

        Assert.False(keys.TryRun(Ctrl, Key.Q));
        Assert.False(keys.TryRun(Ctrl | ModifierKeys.Alt, Key.K));
    }

    [Fact]
    public void One_undo_reverts_a_snippet()
    {
        var (field, keys) = Keys(false, "hello", 5);
        keys.TryRun(CtrlShift, Key.N);

        field.Undo();

        Assert.Equal("hello", field.Text);
    }
}
