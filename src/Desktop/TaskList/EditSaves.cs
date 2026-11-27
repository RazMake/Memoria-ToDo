using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Tasks;
using Serilog;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>What saving a Title edit or a Description edit hands to Tasks: the edited field and the tag control's whole list in one save.</summary>
internal static class EditSaves
{
    /// <summary>
    /// The save for the Title editor: <c>title => </c> Tasks' one save of an edit of <paramref name="started"/>, with the new
    /// title, the control's <paramref name="tags"/>, and the description unchanged. Tasks replaces the tags only when they
    /// differ from the started ones, in tags or order. A save Tasks kept before a notice failed returns normally, so the editor
    /// closes as on success, after the failure is logged and its shared message passed to <paramref name="report"/>. Other failures propagate for the editor to report.
    /// </summary>
    public static Action<string> Title(ITasks tasks, TaskItem started, IReadOnlyList<string> tags, Action<string> report, ILogger logger) =>
        title => Save(() => tasks.SaveEdit(started, new TaskDraft(started.Id, title, started.Description, tags)), report, logger);

    /// <summary>
    /// The save for the Description editor: Tasks' one save of an edit of <paramref name="started"/>, with the new description,
    /// the control's <paramref name="tags"/>, and the title unchanged. Tasks replaces the tags only when they differ from the
    /// started ones, in tags or order. Notice and other failures are handled as <see cref="Title"/> says.
    /// </summary>
    public static Action<string> Description(ITasks tasks, TaskItem started, IReadOnlyList<string> tags, Action<string> report, ILogger logger) =>
        description => Save(() => tasks.SaveEdit(started, new TaskDraft(started.Id, started.Title, description, tags)), report, logger);

    // Known failures aren't named here: the editor words and logs them. Only a notice failure is handled, which isn't a failed save.
    private static void Save(Action save, Action<string> report, ILogger logger) =>
        StoreFailures.Run(save, _ => null, report, logger);

    /// <summary>Whether the control's <paramref name="tags"/> differ from those of <paramref name="started"/> in tags or order, once both are normalized.</summary>
    public static bool TagsChanged(TaskItem started, IReadOnlyList<string> tags) =>
        !started.Tags.Select(TagName.Normalize).SequenceEqual(tags.Select(TagName.Normalize));
}
