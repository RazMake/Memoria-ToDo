namespace Memoria.ToDo.Shared.Contacts;

/// <summary>One piece of a mention template. Text is null when the contact lacks a value the piece needs.</summary>
internal abstract record TemplateSegment
{
    public abstract string? Text(Contact contact, IReadOnlyList<DataType> dataTypes);
}

/// <summary>Text written as it is in the template.</summary>
internal sealed record LiteralSegment(string Value) : TemplateSegment
{
    public override string? Text(Contact contact, IReadOnlyList<DataType> dataTypes) => Value;
}

/// <summary>A known field of the contact, such as <c>{title}</c>.</summary>
internal sealed record FieldSegment(ContactField Field) : TemplateSegment
{
    public override string? Text(Contact contact, IReadOnlyList<DataType> dataTypes) =>
        Field.TemplateTextOf(contact) is { Length: > 0 } value ? MarkdownValue.Escape(value) : null;
}

/// <summary>A property of the entry a key field names, such as <c>{careerPath.short}</c>.</summary>
internal sealed record EntrySegment(KeyField Key, string Property) : TemplateSegment
{
    public override string? Text(Contact contact, IReadOnlyList<DataType> dataTypes)
    {
        var key = Key.KeyOf(contact);
        var entry = key is null
            ? null
            : dataTypes.FirstOrDefault(type => type.KeyField == Key.Field)?.Entries
                .FirstOrDefault(candidate => candidate.Id.Equals(key, StringComparison.OrdinalIgnoreCase));
        return entry is not null && entry.Text.TryGetValue(Property, out var value) && value.Length > 0 ? MarkdownValue.Escape(value) : null;
    }
}
