using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.FormattedView;

/// <summary>A task's description as a reader sees it, in the web view it's given.</summary>
public sealed class FormattedDescription : ContentControl, IDisposable
{
    private readonly IDescriptionWebView _webView;
    private readonly ITheme _theme;
    private readonly LinkOpener _links;
    private readonly ThemeFollower _themeFollower;
    private string? _markdown;
    private string? _shownMarkdown;
    private bool _shownDark;
    private TextBox? _source;
    private bool _disposed;

    /// <summary>Takes ownership of <paramref name="webView"/>: releasing the description releases it.</summary>
    public FormattedDescription(IDescriptionWebView webView, ITheme theme, IDefaultBrowser browser)
    {
        ArgumentNullException.ThrowIfNull(webView);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(browser);
        _webView = webView;
        _theme = theme;
        _links = new LinkOpener(
            browser,
            address => OtherLinkClicked?.Invoke(this, address),
            failure => OpenFailed?.Invoke(this, failure));
        Content = webView.Element;
        webView.LinkClicked += OnLinkClicked;
        webView.Failed += OnFailed;
        _themeFollower = new ThemeFollower(this, theme, Show);
    }

    /// <summary>The description's Markdown. Setting it shows it formatted.</summary>
    public string Markdown
    {
        get => _markdown ?? "";
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _markdown = value;
            Show();
        }
    }

    /// <summary>Raised with the address of a clicked link that fails Rendering's browser check.</summary>
    public event EventHandler<string>? OtherLinkClicked;

    /// <summary>Raised when the default browser fails to open a web link.</summary>
    public event EventHandler<Exception>? OpenFailed;

    /// <summary>Releases the web view and its browser.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _themeFollower.Dispose();
        _webView.LinkClicked -= OnLinkClicked;
        _webView.Failed -= OnFailed;
        _webView.Dispose();
    }

    private void OnLinkClicked(object? sender, string address) => _links.Click(address);

    // Shows the Markdown as source with the reason, in place of the web view, for good: the browser raises this once.
    private void OnFailed(object? sender, string reason)
    {
        _source = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Text = Markdown };
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = $"Descriptions can't show formatted: {reason}", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(_source);
        Content = panel;
    }

    // Shows the Markdown in the current theme, unless that is what is already shown.
    private void Show()
    {
        if (_markdown is null || _disposed)
        {
            return;
        }

        if (_source is not null)
        {
            _source.Text = _markdown;
            return;
        }

        var dark = _theme.IsDark;
        if (_shownMarkdown == _markdown && _shownDark == dark)
        {
            return;
        }

        _shownMarkdown = _markdown;
        _shownDark = dark;
        _webView.Show(DescriptionPage.Of(_markdown, dark, _webView.CloneAddress));
    }
}
