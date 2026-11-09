namespace Memoria.ToDo.Shared.Tasks;

/// <summary>The JSON of the kept draft: <c>version</c>, <c>id</c>, <c>title</c>, <c>description</c>, and <c>tags</c>.</summary>
internal static class DraftFileFormat
{
    public static byte[] Write(TaskDraft draft) => JsonFields.Write(writer =>
    {
        writer.WriteNumber("version", JsonFields.CurrentVersion);
        writer.WriteString("id", draft.TaskId);
        writer.WriteString("title", draft.Title);
        writer.WriteString("description", draft.Description);
        JsonFields.WriteStrings(writer, "tags", draft.Tags);
    });

    /// <summary>Throws <see cref="InvalidDataException"/> when the file isn't a draft this app can read.</summary>
    public static TaskDraft Read(byte[] bytes)
    {
        using var document = JsonFields.Parse(bytes);
        var root = document.RootElement;
        JsonFields.RequireSupportedVersion(root);
        var id = JsonFields.String(root, "id");
        TaskId.ThrowIfInvalidData(id);
        return new TaskDraft(id, JsonFields.String(root, "title"), JsonFields.String(root, "description"),
            JsonFields.Strings(root, "tags"));
    }
}
