namespace Memoria.ToDo.Shared.Tasks;

/// <summary>Where Tasks keeps things. The clone and this device's draft folder share the <c>images/&lt;task-id&gt;/</c> layout.</summary>
internal static class RepositoryLayout
{
    public static string TaskFile(string clone, string taskId) => Path.Combine(TasksFolder(clone), taskId + ".json");

    public static string TasksFolder(string clone) => Path.Combine(clone, "tasks");

    public static string ImagesFolder(string root, string taskId) => Path.Combine(root, "images", taskId);

    /// <summary>The address a description uses for an image, from the clone's root.</summary>
    public static string ImageLink(string taskId, string fileName) => $"images/{taskId}/{fileName}";

    public static string DraftFile(string deviceFolder) => Path.Combine(deviceFolder, "draft.json");
}
