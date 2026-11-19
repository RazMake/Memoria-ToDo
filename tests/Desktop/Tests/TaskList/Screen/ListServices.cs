using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The ports a Task View window test builds its window on: <see cref="ListTasks"/> plus the shared fakes a window needs.
/// Shared test setup, not a fake of a port.
/// </summary>
internal sealed class ListServices : ListTasks
{
    private readonly List<FakeDescriptionWebView> webViews = [];

    public FakeTheme Theme { get; } = new();

    public FakeDefaultBrowser Browser { get; } = new();

    public FakeClipboard Clipboard { get; } = new();

    public FakeSyncService Sync { get; } = new();

    public FakeUpdateStatus Updates { get; } = new();

    /// <summary>The Close window keys Task View is attached to; a test sets them as Configure does.</summary>
    public CloseWindowKeys CloseWindowKeys { get; } = new();

    /// <summary>The Save keys Edit Description and Edit Title save on, set to Ctrl+Enter as the app starts with.</summary>
    public SaveKeys SaveKeys { get; } = CtrlEnter();

    /// <summary>The mouse Task View drags with: the real one, as Task View used before it was passed in.</summary>
    public IDragPointer Pointer { get; } = new MousePointer();

    private static SaveKeys CtrlEnter()
    {
        var keys = new SaveKeys();
        keys.Set(System.Windows.Input.ModifierKeys.Control, System.Windows.Input.Key.Enter);
        return keys;
    }

    /// <summary>The web views Task View made, oldest first.</summary>
    public IReadOnlyList<FakeDescriptionWebView> WebViews => webViews;

    /// <summary>Opens Task View on these services. It shows only after <c>Show</c>. Each web view it asks for is kept in <see cref="WebViews"/>.</summary>
    public IViewTasksWindow CreateWindow() => ViewTasksWindow.Create(
        Tasks, TagColors, TagOrder, Palette.Colors, Theme, Time, Clipboard, new FakeImageFileChooser(), Files,
        () =>
        {
            var view = new FakeDescriptionWebView();
            webViews.Add(view);
            return view;
        },
        Browser, Sync, CloseWindowKeys, Updates, SaveKeys, Pointer);
}
