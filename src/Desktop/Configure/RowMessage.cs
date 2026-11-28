namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// The one message a shortcut row shows under its field. It remembers whether the message is about the stored text,
/// since only such a message is replaced or cleared when the stored text changes; a rejected pick's message stays.
/// </summary>
internal sealed class RowMessage
{
    private bool _aboutStoredText;

    /// <summary>What the row says, or null when there's nothing to say.</summary>
    public string? Text { get; private set; }

    /// <summary>Says something about a pick, or clears the message with null.</summary>
    public void Say(string? text)
    {
        Text = text;
        _aboutStoredText = false;
    }

    /// <summary>Says why the stored text is unusable; with no problem, clears only a message about the stored text.</summary>
    public void SayAboutStoredText(string? problem)
    {
        if (problem is not null)
        {
            Text = problem;
            _aboutStoredText = true;
        }
        else if (_aboutStoredText)
        {
            Say(null);
        }
    }
}
