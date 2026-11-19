namespace Memoria.ToDo.Desktop.Platform;

/// <summary>What the user chose when asked about a changed edit.</summary>
public enum LeaveAnswer
{
    Save,
    Discard,
    Cancel,
}

/// <summary>Asks the user what to do with a changed edit. Closing the prompt any other way answers Cancel.</summary>
public interface ILeavePrompt
{
    LeaveAnswer Ask();
}

/// <summary>The changed edit a window is leaving.</summary>
public interface ILeavingEdit
{
    bool HasChanges { get; }

    /// <summary>Saves the edit. False when it can't be saved; the window then shows why.</summary>
    bool Save();

    /// <summary>Drops the edit.</summary>
    void Discard();

    /// <summary>Puts the cursor back in the edit.</summary>
    void ReturnToEditor();
}

/// <summary>
/// Whether a window may leave an edit: no edit, or one with no changes, leaves without asking; a changed one asks and acts on the answer.
/// </summary>
public sealed class LeaveDecision
{
    private readonly ILeavePrompt _prompt;
    private bool _asking;

    public LeaveDecision(ILeavePrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        _prompt = prompt;
    }

    /// <summary>True when the edit may be left. False on Cancel, a failed save, or while the prompt already shows.</summary>
    public bool MayLeave(ILeavingEdit? edit)
    {
        if (_asking)
        {
            return false;
        }

        if (edit is not { HasChanges: true })
        {
            return true;
        }

        switch (Ask())
        {
            case LeaveAnswer.Save:
                return edit.Save();
            case LeaveAnswer.Discard:
                edit.Discard();
                return true;
            default:
                edit.ReturnToEditor();
                return false;
        }
    }

    private LeaveAnswer Ask()
    {
        _asking = true;
        try
        {
            return _prompt.Ask();
        }
        finally
        {
            _asking = false;
        }
    }
}
