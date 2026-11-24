namespace Memoria.ToDo.Desktop.Platform;

/// <summary>Holds the one hover card that shows. Showing a second card closes the first. Use it on the UI thread.</summary>
internal sealed class HoverCardSlot
{
    private Action? _closeShown;

    /// <summary>Records that a card shows, after closing the card that showed before.</summary>
    /// <param name="close">Closes the new card; the slot calls it when another card replaces this one.</param>
    public void Show(Action close)
    {
        var previous = _closeShown;
        _closeShown = close;
        previous?.Invoke();
    }

    /// <summary>Records that the card closed by itself. Does nothing when another card has replaced it.</summary>
    public void Closed(Action close)
    {
        if (_closeShown == close)
        {
            _closeShown = null;
        }
    }
}
