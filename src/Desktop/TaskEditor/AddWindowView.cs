using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The Add window. Esc, the close button, and a click on another window hide it and keep its text as a draft, so the
/// next Show reopens it at once with that draft. Application shutdown still closes it: WPF ignores a cancelled
/// Closing then, so the draft is kept by the same hide.
/// </summary>
internal sealed class AddWindowView : Window, IAddWindow
{
    private const string DraftUnreadableMessage = "The kept draft couldn't be loaded, so Add opened empty.";

    private readonly ITasks _tasks;
    private readonly TaskEditorControl _editor;
    private bool _unreadableDraftOnDisk;

    public AddWindowView(ITasks tasks, TaskEditorControl editor)
    {
        _tasks = tasks;
        _editor = editor;
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
        clear.Click += (_, _) => Clear();
        var save = new Button
        {
            Content = "Save",
            ToolTip = "Save the task (Ctrl+Enter)",
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(16, 0, 16, 16),
        };
        save.SetResourceReference(StyleProperty, "AccentButtonStyle");
        save.Click += (_, _) => editor.RequestSave();
        editor.ShareKeysWith(clear);
        editor.ShareKeysWith(save);

        var panel = new StackPanel();
        panel.Children.Add(clear);
        panel.Children.Add(editor);
        panel.Children.Add(save);
        Content = panel;

        editor.SaveRequested += (_, _) => Save(keepOpen: false);
        editor.SaveAndClearRequested += (_, _) => Save(keepOpen: true);
        editor.CancelRequested += (_, _) => HideAndKeepDraft();
        Deactivated += (_, _) =>
        {
            // The file dialog takes the focus, and that isn't clicking elsewhere.
            if (!editor.IsChoosingImageFile)
            {
                HideAndKeepDraft();
            }
        };
    }

    void IAddWindow.Show()
    {
        if (!IsVisible)
        {
            RestoreDraft();
        }

        WindowPlacement.ShowOnPointerScreen(this);
        _editor.FocusTitle();
    }

    bool IAddWindow.IsOpen => IsVisible;

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.Escape)
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

    // Does nothing once hidden, so the hide's own Deactivated, and a close of the hidden window at exit, keep nothing twice.
    private void HideAndKeepDraft()
    {
        if (!IsVisible)
        {
            return;
        }

        Hide();
        var draft = _editor.Value;
        if (!_unreadableDraftOnDisk || !IsUntouched(draft))
        {
            _tasks.KeepDraft(draft);
            _unreadableDraftOnDisk = false;
        }
    }

    private static bool IsUntouched(TaskDraft draft) =>
        draft.Title.Length == 0 && draft.Description.Length == 0 && draft.Tags.Count == 0;

    private void RestoreDraft()
    {
        // The window stays alive while hidden, so Tags text typed and not yet added is still in its line; setting the draft clears it.
        var typedTags = _editor.TypedTags;
        try
        {
            _editor.Value = _tasks.LoadDraft() ?? _tasks.StartDraft();
            _unreadableDraftOnDisk = false;
            ShowMessage("");
        }
        catch (InvalidDataException)
        {
            _editor.Value = _tasks.StartDraft();
            _unreadableDraftOnDisk = true;
            ShowMessage(DraftUnreadableMessage);
        }

        _editor.TypedTags = typedTags;
    }

    private void StartNewDraft()
    {
        _editor.Value = _tasks.StartDraft();
        _unreadableDraftOnDisk = false;
        ShowMessage("");
    }

    private void Clear()
    {
        _tasks.DiscardDraft();
        StartNewDraft();
        _editor.FocusTitle();
    }

    private void Save(bool keepOpen)
    {
        if (!_editor.Save(draft => _tasks.Add(draft)))
        {
            return;
        }

        StartNewDraft();
        if (keepOpen)
        {
            _editor.FocusTitle();
        }
        else
        {
            HideAndKeepDraft();
        }
    }

    private void ShowMessage(string text) => _editor.ShowMessage(text);
}
