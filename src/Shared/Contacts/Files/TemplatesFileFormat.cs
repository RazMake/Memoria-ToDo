using System.Text.Json;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Shared.Contacts;

/// <summary>Reads the mention templates file.</summary>
internal static class TemplatesFileFormat
{
    public static readonly IReadOnlyList<string> Default = ["{fullName}"];

    /// <summary>
    /// The templates in file order; the default for a file of zero bytes. Throws <see cref="InvalidDataException"/>
    /// when the file can't be read, needs a newer app, or lists no template or an invalid one.
    /// </summary>
    public static IReadOnlyList<string> Read(byte[] bytes)
    {
        if (bytes.Length == 0)
            return Default;

        using var document = JsonFile.Parse(bytes);
        JsonFile.RequireVersion(document.RootElement, 1);
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

        return templates.Count > 0 ? templates : throw new InvalidDataException("'templates' lists no template.");
    }
}
