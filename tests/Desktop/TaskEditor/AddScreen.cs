using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The Add window as a UX test uses it: built through <see cref="AddWindow.Create"/> over the shared fakes and a real
/// Tasks module, and driven as a user would. Shared test setup, not a fake of a port. Dispose it to close the window.
/// </summary>
internal sealed class AddScreen : IDisposable
{
    private readonly OpenWindows windows = new();
    private readonly IAddWindow add;

    public AddScreen(bool withRepository = true)
    {
        Services = new EditorServices();
        if (withRepository)
        {
            Services.UseRepository();
        }

        add = Services.CreateAddWindow();
    }

    public EditorServices Services { get; }

    public IAddWindow Add => add;

    public Window Window => Assert.Single(windows.Visible().OfType<IAddWindow>().Cast<Window>());

    public bool IsShowing => windows.Visible().Count > 0;

    /// <summary>The editor inside the window.</summary>
    public TaskEditorControl Editor => OpenWindows.Descendants(Window).OfType<TaskEditorControl>().Single();

    /// <summary>The saved tasks, top of the list first.</summary>
    public IReadOnlyList<TaskItem> Saved => Services.Tasks.All;

    /// <summary>Opens the window, as the tray's Add item does.</summary>
    public void Show() => add.Show();

    /// <summary>Fills the three fields, as typing does.</summary>
    public void Fill(string title, string description = "", params string[] tags) =>
        Editor.Value = Editor.Value with { Title = title, Description = description, Tags = tags };

    /// <summary>The field's name that holds the keyboard focus.</summary>
    public string? FocusedField => EditorFields.FocusedFieldName();

    /// <summary>Gives the keyboard focus to the field.</summary>
    public void Focus(string field)
    {
        var element = EditorFields.Field(Window, field);
        element.Focus();
        Keyboard.Focus(element);
    }

    /// <summary>Presses a chord on the focused element: Ctrl+Enter is <c>Press(Key.LeftCtrl, Key.Enter)</c>.</summary>
    public void Press(params Key[] keys) => EditorFields.PressOnFocused(keys);

    /// <summary>Presses Ctrl, or Ctrl+Shift, with the key.</summary>
    public void PressCtrl(bool withShift, Key key) =>
        Press(withShift ? [Key.LeftCtrl, Key.LeftShift, key] : [Key.LeftCtrl, key]);

    /// <summary>The fake clock's time in this PC's time zone, which is what stamps show.</summary>
    public DateTimeOffset LocalNow => TimeZoneInfo.ConvertTime(Services.Time.GetUtcNow(), TimeZoneInfo.Local);

    /// <summary>The draft Tasks keeps on this device, or null when it keeps none.</summary>
    public TaskDraft? KeptDraft => Services.Tasks.LoadDraft();

    /// <summary>Gives the window the theme the platform gives the app's windows, so the theme's resources resolve; a bare test window has none.</summary>
    public void UseTheme(bool dark)
    {
#pragma warning disable WPF0001 // Window.ThemeMode is experimental; allowed only on this line.
        Window.ThemeMode = dark ? ThemeMode.Dark : ThemeMode.Light;
#pragma warning restore WPF0001
        Window.UpdateLayout();
    }

    /// <summary>Presses Esc in the window.</summary>
    public void PressEscape() => OpenWindows.PressEscape(Window);

    /// <summary>Clicks the window's close button.</summary>
    public void CloseWindow() => Window.Close();

    /// <summary>Clicks the Clear button.</summary>
    public void Clear() => OpenWindows.Click(Window, "Clear");

    /// <summary>The Clear button.</summary>
    public System.Windows.Controls.Button ClearButton => ButtonLabelled("Clear");

    /// <summary>The Save button.</summary>
    public System.Windows.Controls.Button SaveButton => ButtonLabelled("Save");

    /// <summary>Clicks the Save button.</summary>
    public void ClickSave() => OpenWindows.Click(SaveButton);

    private System.Windows.Controls.Button ButtonLabelled(string text) =>
        OpenWindows.Descendants(Window).OfType<System.Windows.Controls.Button>().Single(b => b.Content as string == text);

    public void Save() => Press(Key.LeftCtrl, Key.Enter);

    /// <summary>Types the text into the Tags line, which gets the keyboard focus, as the user does.</summary>
    public void TypeTag(string text)
    {
        Focus(EditorFields.Tags);
        EditorFields.Type(Window, EditorFields.Tags, text);
    }

    /// <summary>What the Tags line holds, typed and not yet added.</summary>
    public string TypedTag => EditorFields.TextOf(Window, EditorFields.Tags);

    /// <summary>The tags the task shows, in order.</summary>
    public IReadOnlyList<string> ShownTags => EditorFields.ShownTags(Window);

    /// <summary>The suggestions on offer; empty while the list is closed.</summary>
    public IReadOnlyList<string> Suggestions => EditorFields.ShownSuggestions(Window);

    /// <summary>The tags of the chips below the Tags line, in order.</summary>
    public IReadOnlyList<string> ChipTags => EditorFields.ChipTags(Window);

    /// <summary>Puts the text in the field, selects <paramref name="length"/> characters from <paramref name="start"/>, and gives the field the keyboard focus.</summary>
    public void Edit(string field, string text, int start, int length = 0)
    {
        EditorFields.Type(Window, field, text);
        Focus(field);
        EditorFields.Select(Window, field, start, length);
    }

    /// <summary>What the field's text holds now.</summary>
    public string TextOf(string field) => EditorFields.TextOf(Window, field);

    /// <summary>Where the field's selection starts and how long it is; length 0 is the cursor.</summary>
    public (int Start, int Length) SelectionOf(string field) => EditorFields.SelectionOf(Window, field);

    /// <summary>Undoes the field's last change once, as Ctrl+Z does.</summary>
    public void Undo(string field) => Assert.True(EditorFields.Undo(Window, field), $"The {field} has nothing to undo.");

    /// <summary>The colors the open color list offers, in order; empty while it is closed.</summary>
    public IReadOnlyList<string> ShownColors => EditorFields.ShownColors(Window);

    /// <summary>Clicks the color in the open color list.</summary>
    public void ClickColor(string color) => OpenWindows.Click(EditorFields.ColorButton(Window, color));

    /// <summary>Types the text at the cursor of the focused field, one character at a time, as the user does.</summary>
    public void Type(string text) => EditorFields.TypeOnFocused(text);

    /// <summary>The snippets the open snippet list offers, in order; empty while it is closed.</summary>
    public IReadOnlyList<string> ShownSnippets => EditorFields.ShownSnippets(Window);

    /// <summary>Clicks the snippet in the open snippet list.</summary>
    public void ClickSnippet(string snippet) => OpenWindows.Click(EditorFields.ItemButton(Window, EditorFields.SnippetList, snippet));

    /// <summary>The titles the open task list offers, in order; empty while it is closed.</summary>
    public IReadOnlyList<string> ShownTaskReferences => EditorFields.ShownTaskReferences(Window);

    /// <summary>Clicks the task, by its title as the list shows it, in the open task list.</summary>
    public void ClickTaskReference(string title) => OpenWindows.Click(EditorFields.ItemButton(Window, EditorFields.TaskReferenceList, title));

    public void SaveAndClear() => Press(Key.LeftCtrl, Key.LeftShift, Key.Enter);

    /// <summary>The texts the window displays beside its controls.</summary>
    public IReadOnlyList<string> Texts => OpenWindows.TextsIn(Window);

    /// <summary>Whether a displayed text contains the words, ignoring case.</summary>
    public bool ShowsText(string words) =>
        Texts.Any(text => text.Contains(words, StringComparison.OrdinalIgnoreCase));

    /// <summary>Whether the Title field is marked as empty: it carries a status text for assistive tools and the eye.</summary>
    public bool TitleIsMarked =>
        !string.IsNullOrEmpty(AutomationProperties.GetItemStatus(EditorFields.Field(Window, EditorFields.Title)));

    /// <summary>The brush the Title's border is painted with now.</summary>
    public System.Windows.Media.Brush? TitleBorderBrush => EditorFields.Field(Window, EditorFields.Title) is System.Windows.Controls.Control title
        ? title.BorderBrush
        : throw new InvalidOperationException("The Title is not a control.");

    /// <summary>The Description field's height on screen now, after layout.</summary>
    public double DescriptionHeight
    {
        get
        {
            var description = EditorFields.Field(Window, EditorFields.Description);
            Window.UpdateLayout();
            return description.ActualHeight;
        }
    }

    public void Dispose() => windows.Dispose();
}
