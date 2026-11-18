namespace Memoria.ToDo.Shared.Contacts;

/// <summary>One entry of a data type file. Text holds every text property but id, by name.</summary>
public sealed record DataTypeEntry(string Id, string Shown, IReadOnlyDictionary<string, string> Text);

/// <summary>A key field's data type. Loaded is false when its file is missing or skipped; Entries is then empty.</summary>
public sealed record DataType(string KeyField, string RelativePath, bool Loaded, IReadOnlyList<DataTypeEntry> Entries);
