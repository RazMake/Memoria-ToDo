using System.IO;
using System.Runtime.InteropServices;
using Memoria.ToDo.Shared.Editing;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// Gets an image from the clipboard, the file dialog, or a dropped file, keeps it as a draft image through Tasks, and
/// inserts the Markdown link Tasks returns. A failure inserts nothing and is reported as a message; a later success
/// reports an empty message, which clears it.
/// </summary>
internal sealed class ImageInsertion
{
    private const string ClipboardAltText = "image";
    private const string FailedPrefix = "The image wasn't added: ";
    private const string TooLargeMessage = FailedPrefix + "it can't be shrunk to 1 MB.";

    private readonly ITasks _tasks;
    private readonly IClipboard _clipboard;
    private readonly IImageFileChooser _chooser;
    private readonly IFileSystem _files;
    private readonly Func<string> _taskId;
    private readonly Action<string> _report;

    /// <param name="taskId">Gives the ID of the task being edited, read when an image is kept.</param>
    /// <param name="report">Gets the message to show, or an empty one to clear it.</param>
    public ImageInsertion(ITasks tasks, IClipboard clipboard, IImageFileChooser chooser, IFileSystem files,
        Func<string> taskId, Action<string> report)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(chooser);
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(taskId);
        ArgumentNullException.ThrowIfNull(report);
        _tasks = tasks;
        _clipboard = clipboard;
        _chooser = chooser;
        _files = files;
        _taskId = taskId;
        _report = report;
    }

    /// <summary>Whether the file dialog is open, so a window that deactivates because of it isn't one the user clicked away from.</summary>
    public bool IsChoosingFile { get; private set; }

    /// <summary>Inserts the clipboard's image, or else the file the user chooses; nothing when the user cancels. <paramref name="replaceFrom"/> is as for <see cref="SnippetInsertion.Apply"/>.</summary>
    public void InsertClipboardOrChosen(StyledTextEditor field, ISnippet snippet, int? replaceFrom)
    {
        if (!TryInsertClipboard(field, snippet, replaceFrom))
        {
            Insert(field, snippet, replaceFrom, KeepChosen);
        }
    }

    /// <summary>Inserts the clipboard's image in place of the selection. Returns false, doing nothing, when the clipboard holds none.</summary>
    public bool PasteClipboardImage(StyledTextEditor field) => TryInsertClipboard(field, Snippets.Image, null);

    /// <summary>Inserts the image file in place of the selection.</summary>
    public void InsertFile(StyledTextEditor field, string path) =>
        Insert(field, Snippets.Image, null, () => Keep(path));

    private bool TryInsertClipboard(StyledTextEditor field, ISnippet snippet, int? replaceFrom)
    {
        // A clipboard that fails reports the failure and counts as handled, so no file dialog follows.
        if (!Guarded(field, _clipboard.ImagePng, out var png))
        {
            return true;
        }

        if (png is null)
        {
            return false;
        }

        Insert(field, snippet, replaceFrom, () => _tasks.SaveDraftImage(_taskId(), png, ClipboardAltText));
        return true;
    }

    // The link of the file the user chooses, or null when the user cancels.
    private string? KeepChosen()
    {
        IsChoosingFile = true;
        string? path;
        try
        {
            path = _chooser.Choose();
        }
        finally
        {
            IsChoosingFile = false;
        }

        return path is null ? null : Keep(path);
    }

    private string Keep(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        return _tasks.SaveDraftImage(_taskId(), _files.ReadFile(path), name.Length == 0 ? ClipboardAltText : name);
    }

    // Leaves the text and the cursor as they were when there is no link, whether the user cancelled or it failed.
    private void Insert(StyledTextEditor field, ISnippet snippet, int? replaceFrom, Func<string?> keepImage)
    {
        if (!Guarded(field, keepImage, out var link))
        {
            return;
        }

        if (link is null)
        {
            KeyboardFocus.MoveTo(field);
            return;
        }

        SnippetInsertion.Apply(field, snippet, replaceFrom, new SnippetInput { ImageMarkdown = link });
        _report("");
    }

    // Runs the step that gets or keeps an image. A failure is reported, leaves the text as it was, and returns false.
    private bool Guarded<T>(StyledTextEditor field, Func<T> step, out T? result)
    {
        try
        {
            result = step();
            return true;
        }
        catch (ImageTooLargeException)
        {
            Fail(field, TooLargeMessage);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or ExternalException)
        {
            Fail(field, FailedPrefix + e.Message);
        }

        result = default;
        return false;
    }

    private void Fail(StyledTextEditor field, string message)
    {
        _report(message);
        KeyboardFocus.MoveTo(field);
    }
}
