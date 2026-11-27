using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Desktop.Updates;
using Memoria.ToDo.Shared.Tasks;
using Serilog;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The Add window. Esc and the close button hide it and keep its text as a draft, so the next Show reopens it at once
/// with that draft. A click on another window leaves it open behind that window, as every window of the app does.
/// Application shutdown still closes it: WPF ignores a cancelled Closing then, so the draft is kept by the same hide.
/// </summary>
internal sealed class AddWindowView : Window, IAddWindow
{
    private readonly TaskEditorControl _editor;
    private readonly AddDraft _draft;
    private readonly ILogger _logger;
    private bool _keepingOpen;

    public AddWindowView(ITasks tasks, TaskEditorControl editor, IUpdateStatus updates, SaveKeys saveKeys, IMessageBox messageBox, ILogger logger)
    {
        _editor = editor;
        _logger = logger;
        _draft = new AddDraft(tasks, editor, new HideFirstBox(this, messageBox), logger);
        Title = "Add new Task";
        Width = 480;
        MinHeight = 240;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;

        var clear = new Button
        {
            Content = "Clear",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(16, 8, 16, 0),
            Padding = new Thickness(8, 2, 8, 2),
            FontSize = 11,
        };
        clear.Click += (_, _) => _draft.Clear();
        var save = SaveButton.Create(saveKeys, editor.RequestSave);
        save.Margin = new Thickness(16, 0, 16, 16);
        editor.ShareKeysWith(clear);
        editor.ShareKeysWith(save);

        var panel = new StackPanel();
        panel.Children.Add(clear);
        panel.Children.Add(editor);
        panel.Children.Add(save);
        panel.Children.Add(new UpdateStatusBar(updates, null));
        Content = panel;

        editor.SaveRequested += (_, _) => Save(keepOpen: false);
        editor.SaveAndClearRequested += (_, _) => Save(keepOpen: true);
        editor.CancelRequested += (_, _) => HideAndKeepDraft();
    }

    void IAddWindow.Show()
    {
        if (!IsVisible)
        {
            _draft.Restore();
        }

        WindowPlacement.ShowOnPointerScreen(this, _logger);
        _editor.FocusTitle();
    }

    bool IAddWindow.IsOpen => IsVisible;

    void IAddWindow.Close() => HideAndKeepDraft();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // The editor's keys see Esc first, as the Save shortcut or to close a list, and handle it. Of the Esc presses that reach
        // the window, only a plain one closes.
        if (e.Handled)
        {
            return;
        }

        if (e.Key == Key.Escape && e.KeyboardDevice.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            if (!_editor.CloseOpenList())
            {
                HideAndKeepDraft();
            }
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        e.Cancel = true;
        HideAndKeepDraft();
    }

    // Does nothing once hidden, so a close of the hidden window at exit keeps nothing twice.
    private void HideAndKeepDraft()
    {
        if (!IsVisible)
        {
            return;
        }

        Hide();
        _draft.Keep();
    }

    private void Save(bool keepOpen)
    {
        _keepingOpen = keepOpen;
        if (_draft.Save(keepOpen))
        {
            HideAndKeepDraft();
        }
    }

    // The message box blocks, so a save that doesn't keep Add open hides it first, and the box shows over nothing of Add's.
    private sealed class HideFirstBox(AddWindowView window, IMessageBox inner) : IMessageBox
    {
        public void Show(string text)
        {
            if (!window._keepingOpen)
            {
                window.HideAndKeepDraft();
            }

            inner.Show(text);
        }
    }
}
