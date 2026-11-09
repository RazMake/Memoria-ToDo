namespace Memoria.ToDo.Shared.Editing;

/// <summary>What a snippet needs the app to pass in.</summary>
public enum SnippetNeeds
{
    None,
    Time,
    ClipboardText,
    ImageMarkdown,
    TaskToReference,
}

/// <summary>A built-in snippet. Apps list <see cref="Snippets.All"/> and never switch on a kind.</summary>
public interface ISnippet
{
    string Name { get; }

    bool WorksInTitle { get; }

    SnippetNeeds Needs { get; }

    /// <exception cref="ArgumentException">The input this snippet <see cref="Needs"/> is missing.</exception>
    TextAndSelection Insert(TextAndSelection field, SnippetInput input);
}
