using System.Text.Json;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>A parsed templates file: the templates in file order, and the top-level fields other than "version" and "templates".</summary>
internal sealed record TemplatesFile(IReadOnlyList<string> Templates, ObjectFields Others)
{
    /// <summary>The file with the template at <paramref name="index"/> moved to the top, the others keeping their order.</summary>
    public TemplatesFile WithFirst(int index) =>
        this with { Templates = [Templates[index], .. Templates.Where((_, at) => at != index)] };
}

/// <summary>Reads and writes the mention templates file: <c>{ "version": 1, "templates": [ ... ] }</c>, plus any other top-level fields.</summary>
internal static class TemplatesFileFormat
{
    private const int Version = 1;

    public static readonly IReadOnlyList<string> Default = ["{fullName}"];

    /// <summary>
    /// The templates in file order; the default for a file of zero bytes. Throws <see cref="InvalidDataException"/>
    /// when the file can't be read, needs a newer app, or lists no template or an invalid one.
    /// </summary>
    public static IReadOnlyList<string> Read(byte[] bytes) => Parse(bytes)?.Templates ?? Default;

    /// <summary>The file's content; null for a file of zero bytes. Throws as <see cref="Read"/> does.</summary>
    public static TemplatesFile? Parse(byte[] bytes)
    {
        if (bytes.Length == 0)
            return null;

        using var document = JsonFile.Parse(bytes);
        JsonFile.RequireVersion(document.RootElement, Version);
        if (!document.RootElement.TryGetProperty("templates", out var list) || list.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("'templates' must be a list.");

        var templates = new List<string>();
        foreach (var item in list.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("A template must be text.");
            var template = item.GetString()!;
            if (MentionTemplate.Parse(template, out var problem) is null)
                throw new InvalidDataException($"The template '{template}' is invalid: {problem}");
            templates.Add(template);
        }

        if (templates.Count == 0)
            throw new InvalidDataException("'templates' lists no template.");
        var others = ObjectFields.Read(document.RootElement).Pairs.Where(pair => pair.Key != "version" && pair.Key != "templates");
        return new TemplatesFile(templates, ObjectFields.Of(others));
    }

    /// <summary>The file's bytes: this app's version, the templates, then the other fields in their order.</summary>
    public static byte[] Write(TemplatesFile file) =>
        JsonFile.Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", Version);
            writer.WriteStartArray("templates");
            foreach (var template in file.Templates)
                writer.WriteStringValue(template);
            writer.WriteEndArray();
            foreach (var (name, value) in file.Others.Pairs)
            {
                writer.WritePropertyName(name);
                value.WriteTo(writer);
            }

            writer.WriteEndObject();
        });

    /// <summary>
    /// How <paramref name="side"/> changed from <paramref name="basis"/>, from least to most: the same bytes; only the layout (the same
    /// templates in the same order); only the order (each template as many times, in any order); or an edit, which includes the other
    /// fields differing and a file of zero bytes against a non-empty one.
    /// </summary>
    public static TemplatesChange Change(ParsedTemplates basis, ParsedTemplates side)
    {
        if (basis.Bytes.AsSpan().SequenceEqual(side.Bytes))
            return TemplatesChange.None;
        if (basis.Content is not { } a || side.Content is not { } b || !a.Others.SameAs(b.Others))
            return TemplatesChange.Edit;
        if (a.Templates.SequenceEqual(b.Templates))
            return TemplatesChange.Layout;
        return a.Templates.Order(StringComparer.Ordinal).SequenceEqual(b.Templates.Order(StringComparer.Ordinal))
            ? TemplatesChange.Order
            : TemplatesChange.Edit;
    }
}

/// <summary>How one side of a templates merge differs from the common version, from least to most.</summary>
internal enum TemplatesChange { None, Layout, Order, Edit }

/// <summary>A templates file's bytes with their content, parsed once; the content is null for a file of zero bytes.</summary>
internal sealed record ParsedTemplates(byte[] Bytes, TemplatesFile? Content)
{
    /// <summary>Throws <see cref="InvalidDataException"/> as <see cref="TemplatesFileFormat.Read"/> does.</summary>
    public static ParsedTemplates Of(byte[] bytes) => new(bytes, TemplatesFileFormat.Parse(bytes));
}
