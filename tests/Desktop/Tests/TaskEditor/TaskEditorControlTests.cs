using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

[UX]
[Collection(ScreenTests.Collection)]
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
        Assert.Equal(draft.TaskId, host.Editor.Value.TaskId);
    }

    [WpfFact]
    public void HasChanges_follows_what_is_typed_and_resets_when_a_draft_is_set()
    {
        using var host = new EditorHost();
        var draft = host.Services.Tasks.StartDraft();
        host.Editor.Value = draft;
        Assert.False(host.Editor.HasChanges);

        host.Type(EditorFields.Title, "Buy milk");

        Assert.True(host.Editor.HasChanges);

        host.Editor.Value = draft with { Title = "Buy milk" };

        Assert.False(host.Editor.HasChanges);
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
    public void Esc_closes_an_open_suggestion_list_first_then_Custom_and_only_a_third_Esc_asks_to_cancel()
    {
        using var host = new EditorHost();
        host.Services.UseRepository();
        host.Services.UseTag("home");
        host.OpenCustom();
        host.Focus(EditorFields.Tags);
        host.Type(EditorFields.Tags, "ho");
        Assert.NotEmpty(EditorFields.ShownSuggestions(host.Editor));

        host.Press(Key.Escape);

        Assert.Empty(EditorFields.ShownSuggestions(host.Editor));
        Assert.Equal((0, 0, 0), host.Counts);

        host.Press(Key.Escape);

        Assert.False(TagControls.CustomIsOpen(host.Editor));
        Assert.Equal((0, 0, 0), host.Counts);

        host.Press(Key.Escape);

        Assert.Equal((0, 0, 1), host.Counts);
    }

    [WpfFact]
    public void Saves_nothing_itself()
    {
        using var host = new EditorHost();
        host.Services.UseRepository();
        host.Editor.Value = host.Services.Tasks.StartDraft() with { Title = "Buy milk" };

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

    /// <summary>The texts the editor displays beside its controls, such as its message line.</summary>
    public IReadOnlyList<string> Texts => EditorFields.VisibleTexts(window);

    /// <summary>Whether a displayed text contains the words, ignoring case.</summary>
    public bool ShowsText(string words) => Texts.Any(text => text.Contains(words, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether the Title field carries the status text that marks it as empty.</summary>
    public bool TitleIsMarked =>
        !string.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetItemStatus(EditorFields.Field(Editor, EditorFields.Title)));

    /// <summary>Gives the keyboard focus to the field.</summary>
    public void Focus(string field)
    {
        var element = EditorFields.Field(Editor, field);
        element.Focus();
        Keyboard.Focus(element);
    }

    /// <summary>Clicks + and then Custom, which opens the tag box.</summary>
    public void OpenCustom()
    {
        Memoria.ToDo.Desktop.Fakes.OpenWindows.Click(TagControls.Plus(Editor));
        Memoria.ToDo.Desktop.Fakes.OpenWindows.Click(TagControls.CustomItem(Editor));
    }

    /// <summary>The name of the field that holds the keyboard focus.</summary>
    public string? FocusedField => EditorFields.FocusedFieldName();

    /// <summary>Puts the text in the field, as typing does.</summary>
    public void Type(string field, string text) => EditorFields.Type(Editor, field, text);

    public void Press(params Key[] keys) => Memoria.ToDo.Desktop.Fakes.SimulatedKey.Chord(
        Keyboard.FocusedElement as UIElement ?? Editor, keys);

    public void Dispose() => window.Close();
}
