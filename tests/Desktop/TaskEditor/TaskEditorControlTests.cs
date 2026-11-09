using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

[UX]
public class TaskEditorControlTests
{
    [WpfFact]
    public void Holds_the_draft_it_was_given()
    {
        using var host = new EditorHost();
        var draft = new TaskDraft(host.Services.Tasks.StartDraft().TaskId, "Title", "Text", ["a", "b"]);

        host.Editor.Value = draft;

        Assert.Equal(draft.Title, host.Editor.Value.Title);
        Assert.Equal(draft.Description, host.Editor.Value.Description);
        Assert.Equal(draft.Tags, host.Editor.Value.Tags);
    }

    [WpfFact]
    public void Keeps_the_task_id_of_the_draft_it_was_given()
    {
        using var host = new EditorHost();
        var draft = host.Services.Tasks.StartDraft();

        host.Editor.Value = draft with { Title = "Title" };

        Assert.Equal(draft.TaskId, host.Editor.Value.TaskId);
    }

    [WpfFact]
    public void Setting_a_draft_replaces_all_three_fields()
    {
        using var host = new EditorHost();
        var id = host.Services.Tasks.StartDraft().TaskId;
        host.Editor.Value = new TaskDraft(id, "Old", "Old text", ["old"]);

        host.Editor.Value = new TaskDraft(id, "New", "", []);

        Assert.Equal("New", host.Editor.Value.Title);
        Assert.Equal("", host.Editor.Value.Description);
        Assert.Empty(host.Editor.Value.Tags);
    }

    [WpfFact]
    public void Ctrl_Enter_asks_to_save_once_and_nothing_else()
    {
        using var host = new EditorHost();
        host.Press(Key.LeftCtrl, Key.Enter);

        Assert.Equal((1, 0, 0), host.Counts);
    }

    [WpfFact]
    public void Ctrl_Shift_Enter_asks_to_save_and_clear_once_and_nothing_else()
    {
        using var host = new EditorHost();
        host.Press(Key.LeftCtrl, Key.LeftShift, Key.Enter);

        Assert.Equal((0, 1, 0), host.Counts);
    }

    [WpfFact]
    public void Esc_asks_to_cancel_once_and_nothing_else()
    {
        using var host = new EditorHost();
        host.Press(Key.Escape);

        Assert.Equal((0, 0, 1), host.Counts);
    }

    [WpfFact]
    public void Saves_nothing_itself()
    {
        using var host = new EditorHost();
        host.Services.UseRepository();
        host.Editor.Value = host.Editor.Value with { Title = "Buy milk" };

        host.Press(Key.LeftCtrl, Key.Enter);

        Assert.Empty(host.Services.Tasks.All);
    }
}

/// <summary>Hosts one editor in a showing window and counts the requests it raises. Shared test setup.</summary>
internal sealed class EditorHost : IDisposable
{
    private readonly Window window;
    private int save;
    private int saveAndClear;
    private int cancel;

    public EditorHost()
    {
        Services = new EditorServices();
        Editor = Services.CreateEditor();
        Editor.SaveRequested += (_, _) => save++;
        Editor.SaveAndClearRequested += (_, _) => saveAndClear++;
        Editor.CancelRequested += (_, _) => cancel++;
        window = new Window { Content = Editor, Width = 480, Height = 300 };
        window.Show();
        EditorFields.Field(Editor, EditorFields.Title).Focus();
    }

    public EditorServices Services { get; }

    public TaskEditorControl Editor { get; }

    public (int Save, int SaveAndClear, int Cancel) Counts => (save, saveAndClear, cancel);

    public void Press(params Key[] keys) => Memoria.ToDo.Desktop.Fakes.SimulatedKey.Chord(
        Keyboard.FocusedElement as UIElement ?? Editor, keys);

    public void Dispose() => window.Close();
}
