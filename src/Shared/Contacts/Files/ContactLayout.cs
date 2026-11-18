namespace Memoria.ToDo.Shared.Contacts;

/// <summary>Where Contacts keeps its files, relative to the clone with "/" and from a folder on disk.</summary>
internal static class ContactLayout
{
    public const string FolderName = "autocomplete/contacts";
    public const string DataTypesFolderName = FolderName + "/dataTypes";
    public const string TemplatesFileName = "mentionTemplates.json";
    public const string TemplatesRelativePath = DataTypesFolderName + "/" + TemplatesFileName;
    public const string GroupExtension = ".json";

    public static string Folder(string clone) => Path.Combine(clone, "autocomplete", "contacts");

    public static string GroupFile(string clone, string group) => Path.Combine(Folder(clone), group + GroupExtension);

    public static string GroupRelativePath(string group) => $"{FolderName}/{group}{GroupExtension}";

    public static string File(string clone, string relativePath) => Path.Combine(clone, relativePath.Replace('/', Path.DirectorySeparatorChar));
}
