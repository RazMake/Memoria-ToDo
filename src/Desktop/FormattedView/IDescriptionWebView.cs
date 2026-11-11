using System.Windows;

namespace Memoria.ToDo.Desktop.FormattedView;

/// <summary>The web view a description shows in. Its members are used, and its events raised, on the UI thread.</summary>
public interface IDescriptionWebView : IDisposable
{
    /// <summary>The WPF element that shows the page.</summary>
    FrameworkElement Element { get; }

    /// <summary>The address the clone's files load from: absolute, http or https, and ending in "/".</summary>
    Uri CloneAddress { get; }

    /// <summary>
    /// Shows the page in place of the one shown, with scripts off. Before the browser has started,
    /// the last page waits, and shows once it has.
    /// </summary>
    void Show(string page);

    /// <summary>Raised with the address of a link the user clicked. The web view opens nothing itself.</summary>
    event EventHandler<string>? LinkClicked;

    /// <summary>
    /// Raised once, with the reason, when the browser can't start, or can't load or measure the shown page.
    /// It can be raised while <see cref="Show"/> runs. A failure to measure a page that a newer page has replaced is ignored.
    /// </summary>
    event EventHandler<string>? Failed;
}
