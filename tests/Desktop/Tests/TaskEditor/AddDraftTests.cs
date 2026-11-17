using System.IO;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>How the Add window keeps, restores, clears, and saves its draft, with a plain editor standing in for the fields.</summary>
[Unit]
public class AddDraftTests
{
    private const string UnreadableMessage = "The kept draft couldn't be loaded, so Add opened empty.";

    private readonly EditorServices _services = new();
    private readonly FakeDraftEditor _editor = new();
    private readonly AddDraft _draft;

    public AddDraftTests()
    {
        _services.UseRepository();
        _draft = new AddDraft(_services.Tasks, _editor);
    }

    private void Type(string title, string description = "") =>
        _editor.Value = _editor.Value with { Title = title, Description = description };

    [Fact]
    public void Restoring_with_no_kept_draft_opens_a_fresh_empty_one()
    {
        _draft.Restore();

        Assert.Equal("", _editor.Value.Title);
        Assert.NotEqual("", _editor.Value.TaskId);
        Assert.Equal([""], _editor.Messages);
    }

    [Fact]
    public void Restoring_brings_back_the_kept_draft()
    {
        _draft.Restore();
        _editor.Value = _editor.Value with { Title = "Buy milk", Description = "2 liters", Tags = ["home"] };
        var id = _editor.Value.TaskId;
        _draft.Keep();
        _editor.Value = _services.Tasks.StartDraft();

        _draft.Restore();

        Assert.Equal("Buy milk", _editor.Value.Title);
        Assert.Equal("2 liters", _editor.Value.Description);
        Assert.Equal(["home"], _editor.Value.Tags);
        Assert.Equal(id, _editor.Value.TaskId);
    }

    [Fact]
    public void Restoring_a_draft_kept_with_several_older_tags_brings_back_its_first_tag_only()
    {
        _services.Tasks.KeepDraft(_services.Tasks.StartDraft() with { Title = "Buy milk", Tags = ["first", "second"] });

        _draft.Restore();

        Assert.Equal("Buy milk", _editor.Value.Title);
        Assert.Equal(["first"], _editor.Value.Tags);
    }

    [Fact]
    public void Saving_a_restored_draft_that_was_kept_with_several_older_tags_adds_the_task_with_its_first_tag()
    {
        _services.Tasks.KeepDraft(_services.Tasks.StartDraft() with { Title = "Buy milk", Tags = ["first", "second"] });
        _draft.Restore();

        Assert.True(_draft.Save(keepOpen: false));

        Assert.Equal(["first"], Assert.Single(_services.Tasks.All).Tags);
    }

    [Fact]
    public void Restoring_keeps_the_tag_text_typed_and_not_yet_added()
    {
        _draft.Restore();
        _editor.TypedTags = "ho";

        _draft.Restore();

        Assert.Equal("ho", _editor.TypedTags);
    }

    [Fact]
    public void Restoring_a_draft_that_cannot_be_read_opens_empty_and_says_so()
    {
        _services.KeepUnreadableDraft();

        _draft.Restore();

        Assert.Equal("", _editor.Value.Title);
        Assert.Equal([UnreadableMessage], _editor.Messages);
    }

    [Fact]
    public void Restoring_again_after_an_unreadable_draft_was_replaced_clears_the_message()
    {
        _services.KeepUnreadableDraft();
        _draft.Restore();
        Type("Mine");
        _draft.Keep();

        _draft.Restore();

        Assert.Equal("Mine", _editor.Value.Title);
        Assert.Equal("", _editor.Messages[^1]);
    }

    [Fact]
    public void Keeping_untouched_fields_leaves_an_unreadable_draft_on_disk()
    {
        _services.KeepUnreadableDraft();
        _draft.Restore();

        _draft.Keep();

        Assert.Equal("not a draft"u8.ToArray(), _services.Files.ReadFile(_services.DraftFile));
    }

    [Fact]
    public void Keeping_fields_the_user_typed_in_replaces_an_unreadable_draft()
    {
        _services.KeepUnreadableDraft();
        _draft.Restore();
        Type("Mine");

        _draft.Keep();

        Assert.Equal("Mine", _services.Tasks.LoadDraft()?.Title);
    }

    [Fact]
    public void A_tag_alone_counts_as_typing_so_the_unreadable_draft_is_replaced()
    {
        _services.KeepUnreadableDraft();
        _draft.Restore();
        _editor.Value = _editor.Value with { Tags = ["home"] };

        _draft.Keep();

        Assert.Equal(["home"], _services.Tasks.LoadDraft()?.Tags);
    }

    [Fact]
    public void Keeping_a_draft_that_cannot_be_written_fails_loudly()
    {
        _draft.Restore();
        Type("Mine");
        _services.Files.FailWritesUnder(_services.Device);

        Assert.Throws<IOException>(_draft.Keep);
    }

    [Fact]
    public void Clearing_discards_the_kept_draft_starts_a_new_one_and_focuses_the_title()
    {
        _draft.Restore();
        Type("Mine");
        _draft.Keep();
        var oldId = _editor.Value.TaskId;

        _draft.Clear();

        Assert.Null(_services.Tasks.LoadDraft());
        Assert.Equal("", _editor.Value.Title);
        Assert.NotEqual(oldId, _editor.Value.TaskId);
        Assert.Equal(1, _editor.TitleFocused);
    }

    [Fact]
    public void Saving_adds_the_task_starts_a_new_draft_and_asks_the_window_to_hide()
    {
        _draft.Restore();
        Type("Buy milk", "2 liters");
        var id = _editor.Value.TaskId;
        _editor.Messages.Clear();

        var hide = _draft.Save(keepOpen: false);

        Assert.True(hide);
        var task = Assert.Single(_services.Tasks.All);
        Assert.Equal((id, "Buy milk", "2 liters"), (task.Id, task.Title, task.Description));
        Assert.Equal("", _editor.Value.Title);
        Assert.NotEqual(id, _editor.Value.TaskId);
        Assert.Equal([""], _editor.Messages);
    }

    [Fact]
    public void Saving_and_keeping_open_adds_the_task_focuses_the_title_and_does_not_ask_to_hide()
    {
        _draft.Restore();
        Type("Buy milk");

        var hide = _draft.Save(keepOpen: true);

        Assert.False(hide);
        Assert.Single(_services.Tasks.All);
        Assert.Equal("", _editor.Value.Title);
        Assert.Equal(1, _editor.TitleFocused);
    }

    [Fact]
    public void Saving_with_no_title_adds_nothing_and_keeps_the_fields()
    {
        _draft.Restore();
        Type("", "Only a description");

        var hide = _draft.Save(keepOpen: false);

        Assert.False(hide);
        Assert.Empty(_services.Tasks.All);
        Assert.Equal("Only a description", _editor.Value.Description);
    }

    [Fact]
    public void A_save_that_fails_adds_nothing_keeps_the_fields_and_does_not_ask_to_hide()
    {
        _draft.Restore();
        Type("Buy milk");
        _services.Files.FailWritesUnder(_services.TaskFolder);

        var hide = _draft.Save(keepOpen: false);

        Assert.False(hide);
        Assert.Equal("Buy milk", _editor.Value.Title);
        Assert.Equal(0, _editor.TitleFocused);
    }
}
