namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>What the user chose in the prompt shown when Task View closes with a changed edit.</summary>
internal enum CloseAnswer
{
    Save,
    Discard,
    Cancel,
}

/// <summary>Asks the user what to do with a changed edit.</summary>
internal interface IClosePrompt
{
    /// <summary>Shows the prompt and waits for the answer. Closing the prompt any other way answers <see cref="CloseAnswer.Cancel"/>.</summary>
    CloseAnswer Ask();
}

/// <summary>The edit in place that Task View is closing over.</summary>
internal interface IClosingEdit
{
    bool HasChanges { get; }

    /// <summary>Saves the edit. False when it can't be saved; the editor then shows why.</summary>
    bool Save();

    /// <summary>Drops the edit and deletes its images.</summary>
    void Discard();

    /// <summary>Puts the cursor back in the editor.</summary>
    void ReturnToEditor();
}

/// <summary>
/// Whether Task View may close. An edit with no changes, or no edit, closes without asking; a changed one asks
/// through <see cref="IClosePrompt"/> and acts on the answer.
/// </summary>
internal sealed class CloseDecision(IClosePrompt prompt)
{
    private bool _asking;

    /// <summary>True when the window may close. False while the prompt already shows, nothing more is asked.</summary>
    public bool MayClose(IClosingEdit? edit)
    {
        if (_asking)
        {
            return false;
        }

        if (edit is not { HasChanges: true })
        {
            return true;
        }

        var answer = Ask();
        switch (answer)
        {
            case CloseAnswer.Save:
                return edit.Save();
            case CloseAnswer.Discard:
                edit.Discard();
                return true;
            default:
                edit.ReturnToEditor();
                return false;
        }
    }

    private CloseAnswer Ask()
    {
        _asking = true;
        try
        {
            return prompt.Ask();
        }
        finally
        {
            _asking = false;
        }
    }
}
