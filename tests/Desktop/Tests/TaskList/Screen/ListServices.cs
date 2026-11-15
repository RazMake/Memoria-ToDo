using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The ports a View Tasks window test builds its window on: <see cref="ListTasks"/> plus the shared fakes a window needs.
/// Shared test setup, not a fake of a port.
/// </summary>
internal sealed class ListServices : ListTasks
{
    private readonly List<FakeDescriptionWebView> webViews = [];

    public FakeTheme Theme { get; } = new();

    public FakeDefaultBrowser Browser { get; } = new();

    public FakeClipboard Clipboard { get; } = new();

    public FakeSyncService Sync { get; } = new();

    /// <summary>The Close window keys View Tasks is attached to; a test sets them as Configure does.</summary>
    public CloseWindowKeys CloseWindowKeys { get; } = new();

    /// <summary>The web views View Tasks made, oldest first.</summary>
    public IReadOnlyList<FakeDescriptionWebView> WebViews => webViews;

    /// <summary>Opens View Tasks on these services. It shows only after <c>Show</c>. Each web view it asks for is kept in <see cref="WebViews"/>.</summary>
    public IViewTasksWindow CreateWindow() => ViewTasksWindow.Create(
        Tasks, Palette.Colors, Theme, Time, Clipboard, new FakeImageFileChooser(), Files,
        () =>
        {
            var view = new FakeDescriptionWebView();
            webViews.Add(view);
            return view;
        },
        Browser, Sync, CloseWindowKeys);
}
