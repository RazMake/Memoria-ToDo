using Memoria.ToDo.Shared.Editing;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Inserts a snippet, first getting from the app what the snippet needs, as Editing's <see cref="SnippetNeeds"/> say.</summary>
internal sealed class SnippetInserter
{
    private delegate void Way(StyledTextEditor field, ISnippet snippet, int? replaceFrom);

    private readonly Dictionary<SnippetNeeds, Way> _byNeed;

    /// <param name="time">Gives the local time a stamp shows, read when the snippet is inserted.</param>
    /// <param name="clipboard">Gives the text of a link, read when the snippet is inserted.</param>
    /// <param name="askForTask">Lets the user choose a task, then inserts the snippet. It is given where the replaced text starts.</param>
    /// <param name="insertImage">Gets an image, keeps it, then inserts the snippet. It is given where the replaced text starts, if any.</param>
    public SnippetInserter(TimeProvider time, IClipboard clipboard, Action<StyledTextEditor, ISnippet, int> askForTask,
        Action<StyledTextEditor, ISnippet, int?> insertImage)
    {
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(clipboard);
        ArgumentNullException.ThrowIfNull(askForTask);
        ArgumentNullException.ThrowIfNull(insertImage);
        _byNeed = new()
        {
            [SnippetNeeds.None] = Now(() => new SnippetInput()),
            [SnippetNeeds.Time] = Now(() => new SnippetInput { Now = time.GetLocalNow() }),

            // Nothing on the clipboard is no web address, so the link gets an empty address.
            [SnippetNeeds.ClipboardText] = Now(() => new SnippetInput { ClipboardText = clipboard.Text() ?? "" }),
            [SnippetNeeds.TaskToReference] = (field, snippet, replaceFrom) =>
                askForTask(field, snippet, replaceFrom ?? field.SelectionStart),
            [SnippetNeeds.ImageMarkdown] = (field, snippet, replaceFrom) => insertImage(field, snippet, replaceFrom),
        };
    }

    /// <summary>Inserts the snippet. <paramref name="replaceFrom"/> is as for <see cref="SnippetInsertion.Apply"/>.</summary>
    /// <exception cref="NotSupportedException">The snippet needs something the app can't give yet.</exception>
    public void Insert(StyledTextEditor field, ISnippet snippet, int? replaceFrom)
    {
        if (!_byNeed.TryGetValue(snippet.Needs, out var way))
        {
            throw new NotSupportedException($"The '{snippet.Name}' snippet needs {snippet.Needs}, which isn't built yet.");
        }

        way(field, snippet, replaceFrom);
    }

    private static Way Now(Func<SnippetInput> input) =>
        (field, snippet, replaceFrom) => SnippetInsertion.Apply(field, snippet, replaceFrom, input());
}
