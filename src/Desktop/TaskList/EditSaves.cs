using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>What saving a Title edit or a Description edit hands to Tasks: the edited field and the tag control's whole list in one save.</summary>
internal static class EditSaves
{
    /// <summary>
    /// The save for the Title editor: <c>title => </c> Tasks' one save of an edit of <paramref name="started"/>, with the new
    /// title, the control's <paramref name="tags"/>, and the description unchanged. Tasks replaces the tags only when they
    /// differ from the started ones, in tags or order. Failures propagate for the editor to report.
    /// </summary>
    public static Action<string> Title(ITasks tasks, TaskItem started, IReadOnlyList<string> tags) =>
        title => tasks.SaveEdit(started, new TaskDraft(started.Id, title, started.Description, tags));

    /// <summary>
    /// The save for the Description editor: Tasks' one save of an edit of <paramref name="started"/>, with the new description,
    /// the control's <paramref name="tags"/>, and the title unchanged. Tasks replaces the tags only when they differ from the
    /// started ones, in tags or order. Failures propagate for the editor to report.
    /// </summary>
    public static Action<string> Description(ITasks tasks, TaskItem started, IReadOnlyList<string> tags) =>
        description => tasks.SaveEdit(started, new TaskDraft(started.Id, started.Title, description, tags));

    /// <summary>Whether the control's <paramref name="tags"/> differ from those of <paramref name="started"/> in tags or order, once both are normalized.</summary>
    public static bool TagsChanged(TaskItem started, IReadOnlyList<string> tags) =>
        !started.Tags.Select(TagName.Normalize).SequenceEqual(tags.Select(TagName.Normalize));
}
