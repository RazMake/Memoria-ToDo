using System.Windows;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests.TagControl;

/// <summary>
/// Shows a <see cref="TagEditor"/> holding the tags in a window over a real Tasks (with a repository) and the fake tag
/// colors, records what it raises, and drives it as a user does, a drag included: the fake pointer is given in the editor's own
/// coordinates, and mouse events go to the element that holds the mouse, as WPF routes them. Dispose it to close the window.
/// Shared test setup.
/// </summary>
internal sealed class TagEditorWindow : IDisposable
{
    private readonly Window window;
    private Window? other;
    private UIElement? held;

    public TagEditorWindow(params string[] tags)
    {
        Services = new EditorServices();
        Services.UseRepository();
        Editor = Services.CreateTagEditor();
        Editor.TagsChanged += (_, changed) => Changes.Add((changed, Editor.TaskTags, [.. Services.TagColors.Kept]));
        Editor.TaskTags = tags;
        window = new Window { Content = Editor, Width = 480, Height = 400 };
        window.Show();
    }

    public EditorServices Services { get; }

    public TagEditor Editor { get; }

    /// <summary>Each TagsChanged: the list it carried, the list the editor held and the colors kept when it was raised.</summary>
    public List<(IReadOnlyList<string> Tags, IReadOnlyList<string> TagsThen, IReadOnlyList<(string Tag, string Color)> KeptThen)> Changes { get; } = [];

    /// <summary>The lists TagsChanged carried, in order.</summary>
    public IReadOnlyList<IReadOnlyList<string>> Raised => [.. Changes.Select(change => change.Tags)];

    public void Remove() => LoadSync.Remove(window, Editor);

    public void Put() => LoadSync.Put(window, Editor);

    /// <summary>The tags the pills show, in the order they show.</summary>
    public IReadOnlyList<string> Pills => TagControls.PillTags(Editor);

    /// <summary>The words the add pill shows: "No tag" or "+".</summary>
    public string AddText => TagControls.AddPillText(Editor);

    /// <summary>Clicks the tag's pill, away from its x.</summary>
    public void ClickPill(string tag) => OpenWindows.Click(TagControls.TagPill(Editor, tag));

    /// <summary>Clicks the add pill.</summary>
    public void ClickAdd() => OpenWindows.Click(TagControls.AddPill(Editor));

    /// <summary>Clicks the x of the tag's pill.</summary>
    public void ClickClear(string tag) => OpenWindows.Click(TagControls.ClearButton(Editor, tag));

    /// <summary>Clicks the add pill and then Custom, so Custom adds its tag at the end.</summary>
    public void OpenCustom()
    {
        ClickAdd();
        OpenWindows.Click(TagControls.CustomItem(Editor));
    }

    /// <summary>Clicks the tag's pill and then Custom, so Custom replaces that tag.</summary>
    public void OpenCustomFrom(string tag)
    {
        ClickPill(tag);
        OpenWindows.Click(TagControls.CustomItem(Editor));
    }

    /// <summary>Sets what the user has typed in the tag box.</summary>
    public void Type(string text) => EditorFields.Type(Editor, EditorFields.Tags, text);

    /// <summary>The text in the tag box.</summary>
    public string Typed => EditorFields.TextOf(Editor, EditorFields.Tags);

    /// <summary>Gives the keyboard focus to the element with the automation id.</summary>
    public void Focus(string id)
    {
        var element = EditorFields.Field(Editor, id);
        element.Focus();
        Keyboard.Focus(element);
    }

    /// <summary>Gives the keyboard focus to the element.</summary>
    public static void Focus(UIElement element)
    {
        element.Focus();
        Keyboard.Focus(element);
    }

    /// <summary>Presses the keys on the element that has the keyboard focus.</summary>
    public void Press(params Key[] keys) => EditorFields.PressOnFocused(keys);

    /// <summary>The texts the user can see beside the controls, such as the message under the colors.</summary>
    public IReadOnlyList<string> Texts => EditorFields.VisibleTexts(Editor);

    /// <summary>The middle of the tag's pill now, in the editor's coordinates.</summary>
    public Point CenterOf(string tag) => TagControls.CenterIn(Editor, TagControls.TagPill(Editor, tag));

    /// <summary>Presses the left button on the tag's pill, with the pointer at its middle.</summary>
    public void PressOn(string tag)
    {
        var pill = TagControls.TagPill(Editor, tag);
        Services.Pointer.Position = CenterOf(tag);
        held = pill;
        pill.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
            Source = pill,
        });
    }

    /// <summary>Moves the pointer, with the button held, to the point in the editor's coordinates.</summary>
    public void MoveTo(Point point) => SimulatedPointer.Move(MouseHolder, Services.Pointer, point);

    /// <summary>Moves the pointer, with the button held, to the middle of the tag's pill as the pills lie now.</summary>
    public void MoveOnto(string tag) => MoveTo(CenterOf(tag));

    /// <summary>Releases the left button.</summary>
    public void Release()
    {
        var target = MouseHolder;
        foreach (var routed in new[] { Mouse.PreviewMouseUpEvent, Mouse.MouseUpEvent })
        {
            target.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = routed,
                Source = target,
            });
        }

        held = null;
    }

    /// <summary>Takes the mouse away from whatever holds it, as another window taking it does.</summary>
    public void LoseMouse()
    {
        Mouse.Capture(null);
        held = null;
    }

    /// <summary>Whether an element of the editor holds the mouse, as a drag in progress does.</summary>
    public bool EditorHoldsMouse => Mouse.Captured is DependencyObject captured && (captured == Editor || Editor.IsAncestorOf(captured));

    // WPF sends mouse events to the element that holds the mouse, and to the one under the pointer when none does.
    private UIElement MouseHolder => Mouse.Captured as UIElement ?? held ?? Editor;

    /// <summary>Makes another window the active one, as clicking another application does, so the editor's window deactivates.</summary>
    public void Deactivate()
    {
        other = new Window { Width = 100, Height = 100 };
        other.Show();
        other.Activate();
        window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        if (window.IsActive)
        {
            throw new InvalidOperationException("The editor's window is still active.");
        }
    }

    public void Dispose()
    {
        other?.Close();
        window.Close();
    }
}
