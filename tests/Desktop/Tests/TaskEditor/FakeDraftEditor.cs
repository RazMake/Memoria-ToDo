using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>An editor for <see cref="AddDraft"/> tests: the values it holds, and a save that fails as the test says.</summary>
internal sealed class FakeDraftEditor : IDraftEditor
{
    private TaskDraft _value = new("", "", "", []);

    public TaskDraft Value
    {
        get => _value;
        set
        {
            _value = value;
            TypedTags = ""; // Setting the draft clears the typed tag text, as the editor does.
        }
    }

    public string TypedTags { get; set; } = "";

    /// <summary>Every message shown, in order; "" clears the message.</summary>
    public List<string> Messages { get; } = [];

    public int TitleFocused { get; private set; }

    public void ShowMessage(string text) => Messages.Add(text);

    public void FocusTitle() => TitleFocused++;

    public bool Save(Action<TaskDraft> save)
    {
        if (string.IsNullOrWhiteSpace(Value.Title))
        {
            return false;
        }

        try
        {
            save(Value);
            return true;
        }
        catch (System.IO.IOException)
        {
            return false; // A known failure: the editor keeps the text.
        }
    }
}
