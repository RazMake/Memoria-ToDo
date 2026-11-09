namespace Memoria.ToDo.Shared.Tasks;

/// <summary>The JSON of a task file: nine fields in a fixed order.</summary>
internal static class TaskFileFormat
{
    public static byte[] Write(TaskItem task) => JsonFields.Write(writer =>
    {
        writer.WriteString("id", task.Id);
        writer.WriteString("title", task.Title);
        writer.WriteString("description", task.Description);
        writer.WriteString("rank", task.Rank);
        JsonFields.WriteStrings(writer, "tags", task.Tags);
        JsonFields.WriteTime(writer, "created", task.Created);
        JsonFields.WriteTime(writer, "updated", task.Updated);
        if (task.Resolved is { } resolved)
            JsonFields.WriteTime(writer, "resolved", resolved);
        else
            writer.WriteNull("resolved");
        writer.WriteNumber("version", JsonFields.CurrentVersion);
    });

    /// <summary>Throws <see cref="InvalidDataException"/> when the file isn't a task this app can read.</summary>
    public static TaskItem Read(byte[] bytes)
    {
        using var document = JsonFields.Parse(bytes);
        var root = document.RootElement;
        JsonFields.RequireSupportedVersion(root);
        var id = JsonFields.String(root, "id");
        TaskId.ThrowIfInvalidData(id);
        return new TaskItem(id, JsonFields.String(root, "title"), JsonFields.String(root, "description"),
            JsonFields.String(root, "rank"), JsonFields.Strings(root, "tags"), JsonFields.Time(root, "created"),
            JsonFields.Time(root, "updated"), JsonFields.OptionalTime(root, "resolved"));
    }
}
