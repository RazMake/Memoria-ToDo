namespace Memoria.ToDo.Shared.Tasks;

/// <summary>Where Tasks keeps things. The clone and this device's draft folder share the <c>images/&lt;task-id&gt;/</c> layout.</summary>
internal static class RepositoryLayout
{
    public const string TasksFolderName = "tasks";
    public const string ImagesFolderName = "images";
    public const string SettingsFileName = "settings.json";

    public static string TaskFile(string clone, string taskId) => Path.Combine(TasksFolder(clone), taskId + ".json");

    public static string TasksFolder(string clone) => Path.Combine(clone, TasksFolderName);

    public static string SettingsFile(string clone) => Path.Combine(clone, SettingsFileName);

    public static string ImagesFolder(string root, string taskId) => Path.Combine(root, ImagesFolderName, taskId);

    /// <summary>The address a description uses for an image, from the clone's root.</summary>
    public static string ImageLink(string taskId, string fileName) => $"{ImagesFolderName}/{taskId}/{fileName}";

    /// <summary>Where this device keeps the images of an open edit of an existing task, apart from the draft's images.</summary>
    public static string EditImagesFolder(string deviceFolder, string taskId) => ImagesFolder(Path.Combine(deviceFolder, "edits"), taskId);

    public static string DraftFile(string deviceFolder) => Path.Combine(deviceFolder, "draft.json");
}
