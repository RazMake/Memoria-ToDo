using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Fakes;
using DescriptionControl = Memoria.ToDo.Desktop.FormattedView.FormattedDescription;

namespace Memoria.ToDo.Desktop.FormattedView.Tests.FormattedDescription;

/// <summary>
/// A description over the shared fakes, with every event it raises recorded. By default it sits in a showing window; pass
/// <c>inWindow: false</c> for a description that is laid out but never shown, which needs no desktop. Dispose it to close the window.
/// </summary>
internal sealed class DescriptionScreen : IDisposable
{
    /// <summary>A mention of Ana, shown as her first name.</summary>
    public const string AnaMention = "[Ana](contact:a1 \"{firstName}\")";

    /// <summary>A mention of Bo Li, shown as the full name.</summary>
    public const string BoMention = "[Bo Li](contact:b2 \"{fullName}\")";

    /// <summary>Where the fake web view finds a mention, in its own coordinates.</summary>
    public static readonly Rect MentionBox = new(40, 12, 30, 16);

    private readonly Window? window;

    public DescriptionScreen(bool dark = false, bool browserStarted = true, bool inWindow = true)
    {
        Theme = new FakeTheme { IsDark = dark };
        Browser = new FakeDefaultBrowser();
        WebView = new FakeDescriptionWebView(browserStarted);
        Description = new DescriptionControl(WebView, Theme, Browser, new FakeDragPointer(), Serilog.Core.Logger.None);
        Description.OtherLinkClicked += (_, address) => OtherLinks.Add(address);
        Description.OpenFailed += (_, failure) => OpenFailures.Add(failure);
        Description.ImageResized += (_, resize) => Resizes.Add(resize);
        if (inWindow)
        {
            window = new Window { Content = Description, Width = 480, Height = 240 };
            window.Show();
        }
        else
        {
            Description.Measure(new Size(480, 240));
            Description.Arrange(new Rect(0, 0, 480, 240));
        }
    }

    public FakeTheme Theme { get; }

    public FakeDefaultBrowser Browser { get; }

    public FakeDescriptionWebView WebView { get; }

    public DescriptionControl Description { get; }

    public List<string> OtherLinks { get; } = [];

    public List<Exception> OpenFailures { get; } = [];

    public List<ImageResize> Resizes { get; } = [];

    /// <summary>A card of a fixed size that tells which mention made it by its tag.</summary>
    public static Border CardOf(string name) => new() { Width = 60, Height = 24, Tag = name };

    /// <summary>A box in the web view's coordinates, in the description's.</summary>
    public Rect InDescription(Rect inWebView) => WebView.Element.TransformToAncestor(Description).TransformBounds(inWebView);

    /// <summary>A point in the description's coordinates, in the web view's.</summary>
    public Point InWebView(Point inDescription) => Description.TransformToDescendant(WebView.Element).Transform(inDescription);

    /// <summary>The texts the description displays, including a text box's text.</summary>
    public IReadOnlyList<string> Texts =>
    [
        .. OpenWindows.Descendants(Description).Prepend(Description).Select(element => element switch
        {
            TextBlock block => OpenWindows.TextOf(block),
            TextBox box => box.Text,
            _ => null,
        }).OfType<string>(),
    ];

    /// <summary>Closes the window and returns once the description has raised Unloaded, so it is off screen as far as WPF is concerned.</summary>
    public void Close() => LoadSync.Close(window!, Description);

    public void Dispose() => window?.Close();
}
