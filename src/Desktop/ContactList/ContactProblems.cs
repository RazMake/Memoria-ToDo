using Memoria.ToDo.Shared.Contacts;

namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>The problems line: <see cref="Summary"/> is null when there is nothing to report; <see cref="Items"/> are the lines of its list.</summary>
internal sealed record ContactProblems(string? Summary, IReadOnlyList<string> Items)
{
    /// <summary>What Contacts found wrong with its files and names, in the window's words.</summary>
    public static ContactProblems Of(IContacts contacts)
    {
        var skipped = contacts.SkippedFiles;
        var shared = contacts.SharedNames;
        List<string> counts = [];
        if (skipped.Count > 0)
        {
            counts.Add($"{skipped.Count} contact {(skipped.Count == 1 ? "file" : "files")} skipped");
        }

        if (shared.Count > 0)
        {
            counts.Add($"{shared.Count} {(shared.Count == 1 ? "name" : "names")} shared by more than one contact");
        }

        return new(
            counts.Count == 0 ? null : string.Join(", ", counts),
            [.. skipped.Select(file => $"{file.RelativePath}: {file.Reason}"), .. shared.Select(name => $"Shared by more than one contact: {name}")]);
    }
}
