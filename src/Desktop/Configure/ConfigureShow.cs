using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.Updates;
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
    private readonly CloseWindowSetting _closeWindow;
    private readonly SaveSetting _save;
    private readonly CloseWindowKeys _closeWindowKeys;
    private readonly ITheme _theme;
    private readonly IUpdateStatus _updates;
    private Window? _open;
    private Action? _refreshOpen;
    private bool _disposed;

    public ConfigureShow(
        IStartWithWindows startWithWindows, ISharedTaskSettings settings, ISyncService sync, ISetupActions setupActions,
        CloseWindowKeys closeWindowKeys, ITheme theme, IUpdateStatus updates, SaveKeys saveKeys)
    {
        ArgumentNullException.ThrowIfNull(startWithWindows);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(sync);
        ArgumentNullException.ThrowIfNull(setupActions);
        ArgumentNullException.ThrowIfNull(closeWindowKeys);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(updates);
        ArgumentNullException.ThrowIfNull(saveKeys);
        _updates = updates;
        _theme = theme;
        _settings = settings;
        _closeWindowKeys = closeWindowKeys;
        _closeWindow = new CloseWindowSetting(() => settings.CloseWindowShortcut, settings.SetCloseWindowShortcut, closeWindowKeys.Set);
        _save = new SaveSetting(() => settings.SaveShortcut, settings.SetSaveShortcut, saveKeys.Set);
        _addNewTask = new("Add new task", Shortcut.AddNewTaskDefault,
            () => settings.AddNewTaskShortcut, settings.SetAddNewTaskShortcut);
        _manageTasks = new("Task View", Shortcut.ManageTasksDefault,
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
        _closeWindow.SettingsChanged();
        _save.SettingsChanged();
        _refreshOpen?.Invoke();
    });

    private Window NewWindow(IStartWithWindows startWithWindows, ISyncService sync, ISetupActions setupActions)
    {
        var shortcuts = new ShortcutSection(_addNewTask, _manageTasks, _closeWindow, _save);
        var delay = new DeletionDelayRow(_settings);
        var ui = new SyncUi(sync, setupActions, _notices.Post);
        var syncSettings = new SyncSettingsSection(ui, delay, _theme);
        var sections = new Accordion(
            _theme,
            ("Shortcuts", shortcuts),
            ("Startup", new StartWithWindowsRow(startWithWindows)),
            ("Sync settings", syncSettings))
        { Margin = new Thickness(24, 12, 24, 24) };
        var layout = new DockPanel();
        var card = new UpdateCard(_updates) { Margin = new Thickness(24, 24, 24, 0) };
        DockPanel.SetDock(card, Dock.Top);
        layout.Children.Add(card);
        layout.Children.Add(sections);
        var window = new Window
        {
            Title = "Configure",
            Width = 800,
            Height = Math.Min(850, SystemParameters.MaximumWindowTrackHeight),
            SizeToContent = SizeToContent.Manual,
            ResizeMode = ResizeMode.NoResize,
            Content = layout,
        };
        _closeWindowKeys.Attach(window);
        EscCloses.Attach(window);
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
