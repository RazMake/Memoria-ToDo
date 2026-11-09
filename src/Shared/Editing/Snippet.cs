namespace Memoria.ToDo.Shared.Editing;

/// <summary>Where a snippet's text goes relative to the line it is inserted in.</summary>
internal enum SnippetPlacement
{
    /// <summary>In place, in the middle of a line.</summary>
    Inline,

    /// <summary>On a line of its own.</summary>
    Block,

    /// <summary>On a line of its own, after a blank line, where Markdown would otherwise read the line above differently.</summary>
    BlockAfterBlankLine,
}

/// <summary>The text a snippet inserts, and where the cursor goes in it.</summary>
internal readonly record struct SnippetContent(string Text, int CursorOffset)
{
    public SnippetContent(string text) : this(text, text.Length)
    {
    }
}

/// <summary>Builds a snippet's text from the selected text and the app's input.</summary>
internal delegate SnippetContent SnippetContentBuilder(ReadOnlySpan<char> selected, SnippetInput input);

/// <summary>A built-in snippet: its facts, and the one function that builds its text.</summary>
internal sealed class Snippet(
    string name,
    SnippetNeeds needs,
    SnippetPlacement placement,
    SnippetContentBuilder build,
    bool worksInTitle = false) : ISnippet
{
    /// <summary>Tells whether the app passed in what each kind of need asks for.</summary>
    private static readonly Dictionary<SnippetNeeds, Func<SnippetInput, bool>> IsProvided = new()
    {
        [SnippetNeeds.None] = _ => true,
        [SnippetNeeds.Time] = input => input.Now is not null,
        [SnippetNeeds.ClipboardText] = input => input.ClipboardText is not null,
        [SnippetNeeds.ImageMarkdown] = input => input.ImageMarkdown is not null,
        [SnippetNeeds.TaskToReference] = input => input.Task is not null,
    };

    public string Name { get; } = name;

    public bool WorksInTitle { get; } = worksInTitle;

    public SnippetNeeds Needs { get; } = needs;

    public TextAndSelection Insert(TextAndSelection field, SnippetInput input)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(input);
        if (!IsProvided[Needs](input))
        {
            throw new ArgumentException($"The '{Name}' snippet needs {Needs}.", nameof(input));
        }

        var (text, start, length) = (field.Text, field.SelectionStart, field.SelectionLength);
        var content = build(text.AsSpan(start, length), input);
        var before = text.AsSpan(0, start);
        var after = text.AsSpan(start + length);

        var lead = LineBreaksBefore(before);
        var trail = placement != SnippetPlacement.Inline && after.Length > 0 && after[0] != '\n' ? 1 : 0;
        var middle = string.Concat(new string('\n', lead), content.Text, new string('\n', trail));
        return new TextAndSelection(string.Concat(before, middle, after), start + lead + content.CursorOffset, 0);
    }

    /// <summary>The line breaks to add so the snippet starts where its placement needs it.</summary>
    private int LineBreaksBefore(ReadOnlySpan<char> before)
    {
        if (placement == SnippetPlacement.Inline || before.IsEmpty)
        {
            return 0;
        }

        var wanted = placement == SnippetPlacement.BlockAfterBlankLine ? 2 : 1;
        var present = before.Length - before.TrimEnd('\n').Length;
        return Math.Max(0, wanted - present);
    }
}
