using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The editor the Add window's draft lives in, as <see cref="AddDraft"/> uses it.</summary>
internal interface IDraftEditor
{
    /// <summary>What the fields hold. Setting it replaces them and clears <see cref="TypedTags"/>.</summary>
    TaskDraft Value { get; set; }

    /// <summary>The Tags text typed and not yet added as a tag.</summary>
    string TypedTags { get; set; }

    /// <summary>Shows the text under the Tags line; an empty text clears it.</summary>
    void ShowMessage(string text);

    void FocusTitle();

    /// <summary>Saves the value with <paramref name="save"/>. Returns false, keeping the text, when it wasn't saved.</summary>
    bool Save(Action<TaskDraft> save);
}
