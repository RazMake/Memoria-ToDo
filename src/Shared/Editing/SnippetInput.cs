namespace Memoria.ToDo.Shared.Editing;

/// <summary>What the app passes in for a snippet: only the parts the snippet <see cref="SnippetNeeds"/>.</summary>
public sealed record SnippetInput
{
    public DateTimeOffset? Now { get; init; }

    public string? ClipboardText { get; init; }

    public string? ImageMarkdown { get; init; }

    public TaskToReference? Task { get; init; }
}

/// <summary>The task a reference snippet links to.</summary>
public sealed record TaskToReference(string Address, string Title);
