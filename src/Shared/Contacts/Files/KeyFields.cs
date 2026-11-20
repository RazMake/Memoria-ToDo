namespace Memoria.ToDo.Shared.Contacts;

/// <summary>A contact field whose value is the ID of an entry in a data type file.</summary>
internal sealed record KeyField(string Field, string FileName)
{
    /// <summary>The first part of a template placeholder that reaches the entry: "careerPath" for "careerPathKey".</summary>
    public string Prefix => Field[..^"Key".Length];

    public string RelativePath => $"{ContactLayout.DataTypesFolderName}/{FileName}";

    /// <summary>The property that holds the file's entries: the file name without ".json", such as "pronouns".</summary>
    public string ListName => Path.GetFileNameWithoutExtension(FileName);

    public string? KeyOf(Contact contact) => ContactFields.Named(Field)!.TextOf(contact);
}

/// <summary>The key fields, in the order the form shows them, each with its data type file.</summary>
internal static class KeyFields
{
    public static readonly IReadOnlyList<KeyField> All =
    [
        new("pronounsKey", "pronouns.json"),
        new("careerPathKey", "careerPaths.json"),
    ];

    /// <summary>The key field whose data type is in the file, by file name.</summary>
    public static KeyField? ForFile(string fileName) => All.FirstOrDefault(field => field.FileName == fileName);
}
