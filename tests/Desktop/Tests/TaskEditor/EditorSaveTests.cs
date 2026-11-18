using System.IO;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

[Unit]
public class EditorSaveTests
{
    private readonly List<string> _events = [];
    private TaskDraft _draft = new("id", "Buy milk", "", []);
    private bool _tagsAdded = true;
    private readonly EditorSave _save;

    public EditorSaveTests() =>
        _save = new EditorSave(
            addTypedTags: () =>
            {
                _events.Add("tags");
                if (_tagsAdded)
                {
                    _draft = _draft with { Tags = ["typed"] };
                }

                return _tagsAdded;
            },
            value: () => _draft,
            markTitleMissing: () => _events.Add("mark"),
            focusTitle: () => _events.Add("focus"),
            showMessage: text => _events.Add("message:" + text));

    [Fact]
    public void Adds_the_typed_tags_then_saves_the_value_and_clears_the_message()
    {
        TaskDraft? saved = null;

        var result = _save.Run(draft =>
        {
            saved = draft;
            _events.Add("save");
        });

        Assert.True(result);
        Assert.Equal(_draft, saved);
        Assert.Equal(["tags", "save", "message:"], _events);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_title_is_marked_and_focused_and_nothing_is_saved(string title)
    {
        _draft = _draft with { Title = title };
        var called = false;

        var result = _save.Run(_ => called = true);

        Assert.False(result);
        Assert.False(called);
        Assert.Equal(["tags", "mark", "focus"], _events);
    }

    [Fact]
    public void The_saved_value_includes_the_tags_typed_but_not_yet_added()
    {
        TaskDraft? saved = null;

        _save.Run(draft => saved = draft);

        Assert.Equal(["typed"], saved!.Tags);
    }

    [Fact]
    public void A_missing_repository_folder_shows_what_to_do_and_keeps_the_text()
    {
        var result = _save.Run(_ => throw new NoRepositoryFolderException());

        Assert.False(result);
        Assert.Equal(["tags", "message:" + SaveFailures.MessageOf(new NoRepositoryFolderException())], _events);
    }

    [Fact]
    public void A_failure_Tasks_is_known_to_throw_shows_its_message_and_returns_false()
    {
        var result = _save.Run(_ => throw new IOException("disk full"));

        Assert.False(result);
        Assert.Contains("message:The task wasn't saved: disk full", _events);
    }

    [Fact]
    public void A_failure_nobody_knows_propagates()
    {
        Assert.Throws<InvalidOperationException>(() => _save.Run(_ => throw new InvalidOperationException()));
    }

    [Fact]
    public void Saves_nothing_when_the_typed_tag_cant_be_added()
    {
        _tagsAdded = false;
        var called = false;

        var result = _save.Run(_ => called = true);

        Assert.False(result);
        Assert.False(called);
    }

    [Fact]
    public void A_typed_tag_that_cant_be_added_leaves_the_Title_and_the_message_alone()
    {
        _tagsAdded = false;
        _draft = _draft with { Title = "" };

        _save.Run(_ => { });

        Assert.Equal(["tags"], _events);
    }

    [Fact]
    public void A_missing_message_action_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => new EditorSave(() => true, () => _draft, () => { }, () => { }, null!));
    }

    [Fact]
    public void A_missing_save_action_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => _save.Run(null!));
    }
}
