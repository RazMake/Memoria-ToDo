using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>What saving a Title edit or a Description edit hands to Tasks: the edited field and the pill's tag in one save.</summary>
internal static class EditSaves
{
    /// <summary>
    /// The save for the Title editor: <c>title => </c> Tasks' one save of an edit of <paramref name="started"/>, with the new
    /// title, the pill's <paramref name="tag"/> or none when null, and the description unchanged. Tasks replaces the tag only
    /// when it differs from the started one. Failures propagate for the editor to report.
    /// </summary>
    public static Action<string> Title(ITasks tasks, TaskItem started, string? tag) =>
        title => tasks.SaveEdit(started, new TaskDraft(started.Id, title, started.Description, Tags(tag)));

    /// <summary>
    /// The save for the Description editor: Tasks' one save of an edit of <paramref name="started"/>, with the new description,
    /// the pill's <paramref name="tag"/> or none when null, and the title unchanged. Tasks replaces the tag only when it differs
    /// from the started one. Failures propagate for the editor to report.
    /// </summary>
    public static Action<string> Description(ITasks tasks, TaskItem started, string? tag) =>
        description => tasks.SaveEdit(started, new TaskDraft(started.Id, started.Title, description, Tags(tag)));

    /// <summary>Whether the pill's tag differs from the first tag of <paramref name="started"/>, ignoring case, where no tag is a value.</summary>
    public static bool TagChanged(TaskItem started, string? tag) =>
        !string.Equals(started.Tags.FirstOrDefault(), tag, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> Tags(string? tag) => tag is null ? [] : [tag];
}
