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

    public ConfigureShow(IStartWithWindows startWithWindows)
    {
        ArgumentNullException.ThrowIfNull(startWithWindows);
        _window = new SingleWindow(() => NewWindow(startWithWindows));
    }

    public void Show() => _window.Show();

    public bool ConnectShortcuts(IShortcutAction addNewTask, IShortcutAction manageTasks)
    {
        ArgumentNullException.ThrowIfNull(addNewTask);
        ArgumentNullException.ThrowIfNull(manageTasks);
        var addConnected = _addNewTask.Connect(addNewTask);
        var manageConnected = _manageTasks.Connect(manageTasks);
        return addConnected && manageConnected;
    }

    private Window NewWindow(IStartWithWindows startWithWindows)
    {
        var shortcuts = new ShortcutSection(_addNewTask, _manageTasks);
        var window = new Window
        {
            Title = "Configure",
            Width = 600,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            Content = new StackPanel
            {
                Margin = new Thickness(24),
                Children =
                {
                    shortcuts,
                    ThemedText.Heading("Startup", new Thickness(0, 16, 0, 8)),
                    new StartWithWindowsRow(startWithWindows),
                    ThemedText.Footnote("Sync and the deletion delay aren't built yet.", new Thickness(0, 16, 0, 0)),
                },
            },
        };
        window.Closed += (_, _) => shortcuts.WindowClosed();
        return window;
    }
}
