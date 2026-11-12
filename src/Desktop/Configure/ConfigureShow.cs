using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>Configure's Show part: its window, opened one at a time by the platform's <see cref="SingleWindow"/>.</summary>
internal sealed class ConfigureShow : IConfigureWindow
{
    private readonly SingleWindow _window;
    private readonly ISharedTaskSettings _settings;
    private readonly UiNotices _notices = new();
    private readonly ShortcutSetting _addNewTask;
    private readonly ShortcutSetting _manageTasks;
    private Window? _open;
    private Action? _refreshOpen;
    private bool _disposed;

    public ConfigureShow(
        IStartWithWindows startWithWindows, ISharedTaskSettings settings, ISyncService sync, ISetupActions setupActions)
    {
        ArgumentNullException.ThrowIfNull(startWithWindows);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(sync);
        ArgumentNullException.ThrowIfNull(setupActions);
        _settings = settings;
        _addNewTask = new("Add new task", Shortcut.AddNewTaskDefault,
            () => settings.AddNewTaskShortcut, settings.SetAddNewTaskShortcut);
        _manageTasks = new("Manage tasks", Shortcut.ManageTasksDefault,
            () => settings.ManageTasksShortcut, settings.SetManageTasksShortcut);
        _window = new SingleWindow(() => NewWindow(startWithWindows, sync, setupActions));
        settings.Changed += OnSettingsChanged;
    }

    public void Show()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _window.Show();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _settings.Changed -= OnSettingsChanged;
        _notices.Stop();
        _open?.Close();
    }

    public bool IsOpen => _window.IsOpen;

    public bool ConnectShortcuts(IShortcutAction addNewTask, IShortcutAction manageTasks)
    {
        ArgumentNullException.ThrowIfNull(addNewTask);
        ArgumentNullException.ThrowIfNull(manageTasks);
        var addConnected = _addNewTask.Connect(addNewTask);
        var manageConnected = _manageTasks.Connect(manageTasks);
        return addConnected && manageConnected;
    }

    private void OnSettingsChanged(object? sender, EventArgs e) => _notices.Post(() =>
    {
        _addNewTask.SettingsChanged();
        _manageTasks.SettingsChanged();
        _refreshOpen?.Invoke();
    });

    private Window NewWindow(IStartWithWindows startWithWindows, ISyncService sync, ISetupActions setupActions)
    {
        var shortcuts = new ShortcutSection(_addNewTask, _manageTasks);
        var delay = new DeletionDelayRow(_settings);
        var ui = new SyncUi(sync, setupActions, _notices);
        var syncSettings = new SyncSettingsSection(ui);
        syncSettings.Children.Insert(0, delay);
        var sections = new Accordion { Margin = new Thickness(24) };
        sections.Add("Shortcuts", shortcuts);
        sections.Add("Startup", new StartWithWindowsRow(startWithWindows));
        sections.Add("Sync settings", syncSettings);
        var window = new Window
        {
            Title = "Configure",
            Width = 600,
            SizeToContent = SizeToContent.Height,
            ResizeMode = ResizeMode.NoResize,
            Content = sections,
        };
        _open = window;
        _refreshOpen = () =>
        {
            shortcuts.Refresh();
            delay.Refresh();
        };
        window.Closed += (_, _) =>
        {
            shortcuts.WindowClosed();
            ui.Dispose();
            _open = null;
            _refreshOpen = null;
        };
        return window;
    }
}
