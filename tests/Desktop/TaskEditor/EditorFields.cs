using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// How tests find and drive the editor's three fields. A field is the element whose automation id is "Title",
/// "Description", or "Tags", so the tests don't depend on the text control the editor uses. Shared test setup.
/// </summary>
internal static class EditorFields
{
    public const string Title = "Title";
    public const string Description = "Description";
    public const string Tags = "Tags";

    private static readonly string[] Names = [Title, Description, Tags];

    /// <summary>The field with the name, found below <paramref name="root"/>.</summary>
    public static FrameworkElement Field(DependencyObject root, string name) => TextFields.Find(root, name);

    /// <summary>The element that lists the task's tags, in order, as the items of an items control.</summary>
    public const string TagList = "TagList";

    /// <summary>The element that lists the suggestions for the typed text, as the items of an items control.</summary>
    public const string Suggestions = "Suggestions";

    /// <summary>The tag chips control.</summary>
    public const string TagChips = "TagChips";

    /// <summary>Sets what the user has typed in the field, as typing does.</summary>
    public static void Type(DependencyObject root, string name, string text) => TextFields.Type(Field(root, name), text);

    /// <summary>What the field's text holds now.</summary>
    public static string TextOf(DependencyObject root, string name) => TextFields.TextOf(Field(root, name));

    /// <summary>The element that lists the palette's colors, as the items of an items control, while the color list is open.</summary>
    public const string ColorList = "ColorList";

    /// <summary>Selects <paramref name="length"/> characters of the text field from <paramref name="start"/>; length 0 puts the cursor there.</summary>
    public static void Select(DependencyObject root, string name, int start, int length = 0) =>
        Editor(root, name).Select(start, length);

    /// <summary>Where the selection starts and how long it is in the text field; length 0 is the cursor.</summary>
    public static (int Start, int Length) SelectionOf(DependencyObject root, string name)
    {
        var editor = Editor(root, name);
        return (editor.SelectionStart, editor.SelectionLength);
    }

    /// <summary>Undoes the text field's last change, as Ctrl+Z does. Returns whether there was one to undo.</summary>
    public static bool Undo(DependencyObject root, string name)
    {
        var editor = Editor(root, name);
        var could = editor.CanUndo;
        editor.Undo();
        return could;
    }

    /// <summary>The element that lists the snippets the typed "/" matches, as the items of an items control, while the snippet list is open. An item is the snippet's name.</summary>
    public const string SnippetList = "SnippetList";

    /// <summary>The element that lists the tasks a reference can point to, as the items of an items control, while the task list is open. An item is the task's title as a reader sees it.</summary>
    public const string TaskReferenceList = "TaskReferenceList";

    /// <summary>The colors on offer, in order: empty while the color list is closed.</summary>
    public static IReadOnlyList<string> ShownColors(DependencyObject root) => ShownItems(root, ColorList);

    /// <summary>The button of the color in the open color list. Its content is the color's name.</summary>
    public static System.Windows.Controls.Button ColorButton(DependencyObject root, string color) =>
        ItemButton(root, ColorList, color);

    /// <summary>The snippets on offer, in order: empty while the snippet list is closed.</summary>
    public static IReadOnlyList<string> ShownSnippets(DependencyObject root) => ShownItems(root, SnippetList);

    /// <summary>The titles on offer, in order: empty while the task list is closed.</summary>
    public static IReadOnlyList<string> ShownTaskReferences(DependencyObject root) => ShownItems(root, TaskReferenceList);

    /// <summary>The button of the item in the open list with the automation id. Its content is the item.</summary>
    public static System.Windows.Controls.Button ItemButton(DependencyObject root, string listId, string item)
    {
        var list = Field(root, listId);
        Layout(list);
        return VisualButtons(list).Single(button => (string)button.Content == item);
    }

    /// <summary>Types the text into the element that has the keyboard focus, one character at a time, as the keyboard does: the preview event, then the event itself unless a handler took it.</summary>
    public static void TypeOnFocused(string text)
    {
        var element = (UIElement)Keyboard.FocusedElement;
        foreach (var character in text)
        {
            var composition = new TextComposition(InputManager.Current, element, character.ToString());
            var preview = new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition)
            {
                RoutedEvent = TextCompositionManager.PreviewTextInputEvent,
            };
            element.RaiseEvent(preview);
            if (!preview.Handled)
            {
                element.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition)
                {
                    RoutedEvent = TextCompositionManager.TextInputEvent,
                });
            }
        }
    }

    // The items of the list with the automation id while it is shown; none while it is closed.
    private static IReadOnlyList<string> ShownItems(DependencyObject root, string listId) =>
        OpenWindows.Descendants(root).OfType<FrameworkElement>()
            .SingleOrDefault(element => AutomationProperties.GetAutomationId(element) == listId) is { IsVisible: true }
            ? ItemsOf(root, listId)
            : [];

    private static ICSharpCode.AvalonEdit.TextEditor Editor(DependencyObject root, string name) =>
        Field(root, name) as ICSharpCode.AvalonEdit.TextEditor
            ?? throw new InvalidOperationException($"The {name} field isn't a styled text editor.");

    /// <summary>The tags the task shows, in order.</summary>
    public static IReadOnlyList<string> ShownTags(DependencyObject root) => ItemsOf(root, TagList);

    /// <summary>The suggestions on offer: empty while the list is closed.</summary>
    public static IReadOnlyList<string> ShownSuggestions(DependencyObject root) =>
        Field(root, Suggestions).IsVisible ? ItemsOf(root, Suggestions) : [];

    /// <summary>The tag chips' tags, in order. Lays the window out first, so the chips exist.</summary>
    public static IReadOnlyList<string> ChipTags(DependencyObject root) =>
        [.. Chips(root).Select(chip => (string)chip.Content)];

    /// <summary>The chip buttons below <paramref name="root"/>, or <paramref name="root"/> itself when it is the chips control.</summary>
    public static IReadOnlyList<System.Windows.Controls.Button> Chips(DependencyObject root)
    {
        var chips = AutomationProperties.GetAutomationId(root) == TagChips ? root : Field(root, TagChips);
        Layout(chips);
        return [.. VisualButtons(chips)];
    }

    /// <summary>The chip button for the tag.</summary>
    public static System.Windows.Controls.Button Chip(DependencyObject root, string tag) =>
        Chips(root).Single(chip => (string)chip.Content == tag);

    /// <summary>The button that removes the tag from the task. Its automation id is "Remove " and the tag.</summary>
    public static System.Windows.Controls.Button RemoveButton(DependencyObject root, string tag)
    {
        var list = Field(root, TagList);
        Layout(list);
        return VisualButtons(list).Single(button => AutomationProperties.GetAutomationId(button) == "Remove " + tag);
    }

    /// <summary>The name of the field that holds the keyboard focus, or null when focus is elsewhere.</summary>
    public static string? FocusedFieldName()
    {
        for (var element = Keyboard.FocusedElement as DependencyObject; element is not null; element = ParentOf(element))
        {
            var id = AutomationProperties.GetAutomationId(element);
            if (Names.Contains(id))
            {
                return id;
            }
        }

        return null;
    }

    /// <summary>Presses the keys on the element that has the keyboard focus, in order, and releases them in reverse.</summary>
    public static void PressOnFocused(params Key[] keys) =>
        SimulatedKey.Chord((UIElement)Keyboard.FocusedElement, keys);

    private static IReadOnlyList<string> ItemsOf(DependencyObject root, string name)
    {
        var list = (System.Windows.Controls.ItemsControl)Field(root, name);
        return [.. list.Items.Cast<object>().Select(item => (string)item)];
    }

    private static void Layout(DependencyObject element)
    {
        if (element is UIElement ui)
        {
            ui.UpdateLayout();
        }
    }

    private static IEnumerable<System.Windows.Controls.Button> VisualButtons(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is System.Windows.Controls.Button button)
            {
                yield return button;
            }

            foreach (var inner in VisualButtons(child))
            {
                yield return inner;
            }
        }
    }

    private static DependencyObject? ParentOf(DependencyObject element) =>
        (element is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(element) : null)
            ?? LogicalTreeHelper.GetParent(element);
}
