using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.FormattedView;

/// <summary>
/// Which page a description shows: Rendering's page for its Markdown in the current theme, passed to <c>showPage</c>,
/// and never the same page twice in a row.
/// </summary>
internal sealed class ShownDescription(ITheme theme, Func<Uri> cloneAddress, Action<string> showPage)
{
    private string? _markdown;
    private string? _shownMarkdown;
    private bool _shownDark;

    /// <summary>Shows the Markdown in the current theme, unless that is what is already shown.</summary>
    public void Set(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        _markdown = markdown;
        Show();
    }

    /// <summary>Shows the Markdown set, if any, in the theme as it is now.</summary>
    public void ThemeChanged() => Show();

    private void Show()
    {
        if (_markdown is null)
        {
            return;
        }

        var dark = theme.IsDark;
        if (_shownMarkdown == _markdown && _shownDark == dark)
        {
            return;
        }

        _shownMarkdown = _markdown;
        _shownDark = dark;
        showPage(DescriptionPage.Of(_markdown, dark, cloneAddress()));
    }
}
