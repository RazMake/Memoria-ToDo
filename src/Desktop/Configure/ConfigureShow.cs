using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>Configure's Show part: its window, opened one at a time by the platform's <see cref="SingleWindow"/>.</summary>
internal sealed class ConfigureShow : IConfigureWindow
{
    private readonly SingleWindow _window;
    private readonly ShortcutSetting _addNewTask = new("Add new task", Shortcut.AddNewTaskDefault);
    private readonly ShortcutSetting _manageTasks = new("Manage tasks", Shortcut.ManageTasksDefault);

    public ConfigureShow(IStartWithWindows startWithWindows, IRepositoryFolder repositoryFolder)
    {
        ArgumentNullException.ThrowIfNull(startWithWindows);
        ArgumentNullException.ThrowIfNull(repositoryFolder);
        _window = new SingleWindow(() => NewWindow(startWithWindows, repositoryFolder));
    }

    public void Show() => _window.Show();

    public bool IsOpen => _window.IsOpen;

    public bool ConnectShortcuts(IShortcutAction addNewTask, IShortcutAction manageTasks)
    {
        ArgumentNullException.ThrowIfNull(addNewTask);
        ArgumentNullException.ThrowIfNull(manageTasks);
        var addConnected = _addNewTask.Connect(addNewTask);
        var manageConnected = _manageTasks.Connect(manageTasks);
        return addConnected && manageConnected;
    }

    private Window NewWindow(IStartWithWindows startWithWindows, IRepositoryFolder repositoryFolder)
    {
        var shortcuts = new ShortcutSection(_addNewTask, _manageTasks);
        var sections = new Accordion { Margin = new Thickness(24) };
        sections.Add("Shortcuts", shortcuts);
        sections.Add("Startup", new StartWithWindowsRow(startWithWindows));
        sections.Add("Sync settings", new RepositorySection(repositoryFolder));
        var window = new Window
        {
            Title = "Configure",
            Width = 600,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            Content = sections,
        };
        window.Closed += (_, _) => shortcuts.WindowClosed();
        return window;
    }
}
