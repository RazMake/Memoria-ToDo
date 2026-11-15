using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>What saving a Title edit or a Description edit hands to Tasks: only that one field is new.</summary>
internal static class EditSaves
{
    /// <summary>
    /// The save for the Title editor: <c>title => </c> Tasks' save of an edit of <paramref name="started"/>, with the new
    /// title and its description and tags unchanged. Failures propagate for the editor to report.
    /// </summary>
    public static Action<string> Title(ITasks tasks, TaskItem started) =>
        title => tasks.SaveEdit(started, new TaskDraft(started.Id, title, started.Description, started.Tags));

    /// <summary>
    /// The save for the Description editor: Tasks' save of an edit of <paramref name="started"/>, with the new description
    /// and its title and tags unchanged. Failures propagate for the editor to report.
    /// </summary>
    public static Action<string> Description(ITasks tasks, TaskItem started) =>
        description => tasks.SaveEdit(started, new TaskDraft(started.Id, started.Title, description, started.Tags));
}
