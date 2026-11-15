using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// What closing View Tasks does with an edit in place: whether it asks, and what each answer means. The window asks
/// <see cref="CloseDecision"/> before it closes; the decision uses the prompt through <see cref="IClosePrompt"/> and the
/// edit through <see cref="IClosingEdit"/>, both fakes here. How the prompt looks and how the editor shows a failed
/// save are in the UX tests. The class and its two ports are a proposal the tests pin down, for the coder to confirm:
/// <c>CloseDecision(IClosePrompt)</c>, <c>bool MayClose(IClosingEdit? edit)</c>, <c>CloseAnswer { Save, Discard, Cancel }</c>.
/// </summary>
[Unit]
public class CloseDecisionTests
{
    private readonly FakePrompt _prompt = new();
    private readonly FakeEdit _edit = new();
    private readonly CloseDecision _decision;

    public CloseDecisionTests()
    {
        _decision = new CloseDecision(_prompt);
    }

    [Fact]
    public void Closes_without_asking_when_no_edit_is_open()
    {
        Assert.True(_decision.MayClose(null));

        Assert.Equal(0, _prompt.Asked);
    }

    [Fact]
    public void Closes_without_asking_or_discarding_when_the_edit_has_no_changes()
    {
        _edit.HasChanges = false;

        Assert.True(_decision.MayClose(_edit));

        Assert.Equal(0, _prompt.Asked);
        Assert.Equal(0, _edit.Saves);
        Assert.Equal(0, _edit.Discards);
    }

    [Fact]
    public void Asks_once_when_the_edit_has_changes()
    {
        _prompt.Answer = CloseAnswer.Cancel;

        _decision.MayClose(_edit);

        Assert.Equal(1, _prompt.Asked);
    }

    [Fact]
    public void Save_saves_the_edit_and_lets_the_window_close()
    {
        _prompt.Answer = CloseAnswer.Save;

        Assert.True(_decision.MayClose(_edit));

        Assert.Equal(1, _edit.Saves);
        Assert.Equal(0, _edit.Discards);
    }

    [Fact]
    public void Save_that_does_not_save_keeps_the_window_open_and_the_edit_as_it_is()
    {
        _prompt.Answer = CloseAnswer.Save;
        _edit.SaveSucceeds = false;

        Assert.False(_decision.MayClose(_edit));

        Assert.Equal(1, _edit.Saves);
        Assert.Equal(0, _edit.Discards);
    }

    [Fact]
    public void Discard_deletes_the_edits_images_without_saving_and_lets_the_window_close()
    {
        _prompt.Answer = CloseAnswer.Discard;

        Assert.True(_decision.MayClose(_edit));

        Assert.Equal(1, _edit.Discards);
        Assert.Equal(0, _edit.Saves);
    }

    [Fact]
    public void Cancel_keeps_the_window_open_with_the_edit_untouched_and_the_cursor_back_in_the_editor()
    {
        _prompt.Answer = CloseAnswer.Cancel;

        Assert.False(_decision.MayClose(_edit));

        Assert.Equal(0, _edit.Saves);
        Assert.Equal(0, _edit.Discards);
        Assert.Equal(1, _edit.Returns);
    }

    [Fact]
    public void Closing_again_while_the_prompt_shows_closes_nothing_and_asks_nothing_more()
    {
        _prompt.Answer = CloseAnswer.Cancel;
        var again = new List<bool>();
        _prompt.WhileShowing = () => again.Add(_decision.MayClose(_edit));

        _decision.MayClose(_edit);

        Assert.Equal([false], again);
        Assert.Equal(1, _prompt.Asked);
        Assert.Equal(0, _edit.Saves);
        Assert.Equal(0, _edit.Discards);
    }

    [Fact]
    public void Asks_again_on_a_later_close_once_the_prompt_was_answered()
    {
        _prompt.Answer = CloseAnswer.Cancel;
        _decision.MayClose(_edit);

        _decision.MayClose(_edit);

        Assert.Equal(2, _prompt.Asked);
    }

    [Fact]
    public void A_prompt_that_fails_leaves_the_edit_alone_and_does_not_block_the_next_close()
    {
        _prompt.Failure = new InvalidOperationException("No dialog");

        Assert.Throws<InvalidOperationException>(() => _decision.MayClose(_edit));
        _prompt.Failure = null;
        _prompt.Answer = CloseAnswer.Discard;

        Assert.True(_decision.MayClose(_edit));
        Assert.Equal(0, _edit.Saves);
        Assert.Equal(1, _edit.Discards);
    }

    private sealed class FakePrompt : IClosePrompt
    {
        public CloseAnswer Answer { get; set; } = CloseAnswer.Cancel;

        public Action? WhileShowing { get; set; }

        public Exception? Failure { get; set; }

        public int Asked { get; private set; }

        public CloseAnswer Ask()
        {
            Asked++;
            WhileShowing?.Invoke();
            return Failure is null ? Answer : throw Failure;
        }
    }

    private sealed class FakeEdit : IClosingEdit
    {
        public bool HasChanges { get; set; } = true;

        public bool SaveSucceeds { get; set; } = true;

        public int Saves { get; private set; }

        public int Discards { get; private set; }

        public int Returns { get; private set; }

        public bool Save()
        {
            Saves++;
            return SaveSucceeds;
        }

        public void Discard() => Discards++;

        public void ReturnToEditor() => Returns++;
    }
}
