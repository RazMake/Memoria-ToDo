using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// What leaving an edit in place does: whether it asks, and what each answer means. A window asks
/// <see cref="LeaveDecision"/> before it leaves a changed edit; the decision uses the prompt through <see cref="ILeavePrompt"/>
/// and the edit through <see cref="ILeavingEdit"/>, both fakes here. How the prompt looks and how the editor shows a failed
/// save are in each window's UX tests.
/// </summary>
[Unit]
public class LeaveDecisionTests
{
    private readonly FakePrompt _prompt = new();
    private readonly FakeEdit _edit = new();
    private readonly LeaveDecision _decision;

    public LeaveDecisionTests()
    {
        _decision = new LeaveDecision(_prompt);
    }

    [Fact]
    public void Leaves_without_asking_when_no_edit_is_open()
    {
        Assert.True(_decision.MayLeave(null));

        Assert.Equal(0, _prompt.Asked);
    }

    [Fact]
    public void Leaves_without_asking_or_discarding_when_the_edit_has_no_changes()
    {
        _edit.HasChanges = false;

        Assert.True(_decision.MayLeave(_edit));

        Assert.Equal(0, _prompt.Asked);
        Assert.Equal(0, _edit.Saves);
        Assert.Equal(0, _edit.Discards);
    }

    [Fact]
    public void Asks_once_when_the_edit_has_changes()
    {
        _prompt.Answer = LeaveAnswer.Cancel;

        _decision.MayLeave(_edit);

        Assert.Equal(1, _prompt.Asked);
    }

    [Fact]
    public void Save_saves_the_edit_and_lets_the_window_leave()
    {
        _prompt.Answer = LeaveAnswer.Save;

        Assert.True(_decision.MayLeave(_edit));

        Assert.Equal(1, _edit.Saves);
        Assert.Equal(0, _edit.Discards);
    }

    [Fact]
    public void Save_that_does_not_save_keeps_the_window_in_the_edit_as_it_is()
    {
        _prompt.Answer = LeaveAnswer.Save;
        _edit.SaveSucceeds = false;

        Assert.False(_decision.MayLeave(_edit));

        Assert.Equal(1, _edit.Saves);
        Assert.Equal(0, _edit.Discards);
    }

    [Fact]
    public void Discard_drops_the_edit_without_saving_and_lets_the_window_leave()
    {
        _prompt.Answer = LeaveAnswer.Discard;

        Assert.True(_decision.MayLeave(_edit));

        Assert.Equal(1, _edit.Discards);
        Assert.Equal(0, _edit.Saves);
    }

    [Fact]
    public void Cancel_keeps_the_window_in_the_edit_untouched_and_the_cursor_back_in_it()
    {
        _prompt.Answer = LeaveAnswer.Cancel;

        Assert.False(_decision.MayLeave(_edit));

        Assert.Equal(0, _edit.Saves);
        Assert.Equal(0, _edit.Discards);
        Assert.Equal(1, _edit.Returns);
    }

    [Fact]
    public void Leaving_again_while_the_prompt_shows_leaves_nothing_and_asks_nothing_more()
    {
        _prompt.Answer = LeaveAnswer.Cancel;
        var again = new List<bool>();
        _prompt.WhileShowing = () => again.Add(_decision.MayLeave(_edit));

        _decision.MayLeave(_edit);

        Assert.Equal([false], again);
        Assert.Equal(1, _prompt.Asked);
        Assert.Equal(0, _edit.Saves);
        Assert.Equal(0, _edit.Discards);
    }

    [Fact]
    public void Asks_again_on_a_later_leave_once_the_prompt_was_answered()
    {
        _prompt.Answer = LeaveAnswer.Cancel;
        _decision.MayLeave(_edit);

        _decision.MayLeave(_edit);

        Assert.Equal(2, _prompt.Asked);
    }

    [Fact]
    public void A_prompt_that_fails_leaves_the_edit_alone_and_does_not_block_the_next_leave()
    {
        _prompt.Failure = new InvalidOperationException("No dialog");

        Assert.Throws<InvalidOperationException>(() => _decision.MayLeave(_edit));
        _prompt.Failure = null;
        _prompt.Answer = LeaveAnswer.Discard;

        Assert.True(_decision.MayLeave(_edit));
        Assert.Equal(0, _edit.Saves);
        Assert.Equal(1, _edit.Discards);
    }

    private sealed class FakePrompt : ILeavePrompt
    {
        public LeaveAnswer Answer { get; set; } = LeaveAnswer.Cancel;

        public Action? WhileShowing { get; set; }

        public Exception? Failure { get; set; }

        public int Asked { get; private set; }

        public LeaveAnswer Ask()
        {
            Asked++;
            WhileShowing?.Invoke();
            return Failure is null ? Answer : throw Failure;
        }
    }

    private sealed class FakeEdit : ILeavingEdit
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
