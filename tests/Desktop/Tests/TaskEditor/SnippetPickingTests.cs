using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>Typing "/" in the Description opens the snippet list, which narrows as the user types. Whether a "/" opens it, and which snippets match, are Editing's rules.</summary>
[Unit]
public class SnippetPickingTests
{
    private static readonly string[] AllNames =
    [
        "Date stamp", "Date and time stamp", "Note", "Tip", "Warning", "Link", "Image", "Code block", "Task reference", "Table",
        "Horizontal line",
    ];

    private readonly ChoiceList _list = new();
    private readonly PopupLists _popups;
    private readonly FakeTextField _field = new();
    private readonly List<(string Snippet, int? ReplaceFrom)> _delegated = [];

    public SnippetPickingTests()
    {
        _popups = new PopupLists(_list);
        var inserter = new SnippetInserter(new FakeTimeProvider(), new FakeClipboard(),
            (_, snippet, replaceFrom) => _delegated.Add((snippet.Name, replaceFrom)),
            (_, snippet, replaceFrom) => _delegated.Add((snippet.Name, replaceFrom)));
        _ = new SnippetPicking(_field, _list, _popups, inserter);
    }

    [Fact]
    public void Typing_a_slash_lists_every_snippet_in_order()
    {
        _field.Type("/");

        Assert.Equal(AllNames, _list.Items);
    }

    [Fact]
    public void A_slash_after_a_space_opens_the_list_too()
    {
        _field.Type("a /");

        Assert.True(_list.IsOpen);
    }

    [Theory]
    [InlineData("/ta", new[] { "Task reference", "Table" })]
    [InlineData("/TIP", new[] { "Tip" })]
    [InlineData("/lin", new[] { "Link", "Horizontal line" })]
    [InlineData("/date", new[] { "Date stamp", "Date and time stamp" })]
    public void The_list_narrows_to_the_snippets_matching_what_is_typed_ignoring_case(string typed, string[] expected)
    {
        _field.Type(typed);

        Assert.Equal(expected, _list.Items);
    }

    [Fact]
    public void The_list_closes_when_no_snippet_matches()
    {
        _field.Type("/zzz");

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void The_list_closes_when_the_slash_is_erased()
    {
        _field.Type("/t");

        _field.Backspace();
        _field.Backspace();

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void The_list_closes_when_the_cursor_moves_before_the_slash()
    {
        _field.Type("ab /t");

        _field.Cursor(1);

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void The_list_closes_when_the_cursor_moves_to_just_before_the_slash()
    {
        _field.Type("ab /t");

        _field.Cursor(3);

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void Typing_a_slash_again_after_the_list_closed_opens_it_again()
    {
        _field.Type("/zzz");
        Assert.False(_list.IsOpen);

        _field.Type(" /");

        Assert.True(_list.IsOpen);
    }

    [Fact]
    public void Text_typed_without_a_slash_opens_no_list()
    {
        _field.Type("tip");

        Assert.False(_list.IsOpen);
    }

    [Fact]
    public void Choosing_a_snippet_replaces_the_slash_and_what_was_typed_and_closes_the_list()
    {
        _field.Type("a /tip");

        _list.Choose(0);

        Assert.Equal("a \n> [!TIP]\n> ", _field.Text);
        Assert.False(_list.IsOpen);
        Assert.Equal(1, _field.FocusCount);
    }

    [Fact]
    public void Down_then_Enter_chooses_the_first_snippet()
    {
        _field.Type("/not");
        _popups.MoveDown();

        _popups.ChooseMovedOrClose();

        Assert.StartsWith("> [!NOTE]", _field.Text);
    }

    [Fact]
    public void Choosing_Image_hands_the_slash_position_to_the_image_insertion()
    {
        _field.Type("a /im");

        _list.Choose(0);

        Assert.Equal([("Image", (int?)2)], _delegated);
    }

    [Fact]
    public void Choosing_Task_reference_hands_the_slash_position_to_the_task_picker()
    {
        _field.Type("/task");

        _list.Choose(0);

        Assert.Equal([("Task reference", (int?)0)], _delegated);
    }

    [Fact]
    public void One_undo_restores_the_slash_text()
    {
        _field.Type("a /ti");
        _list.Choose(0);

        _field.Undo();

        Assert.Equal("a /ti", _field.Text);
    }
}
