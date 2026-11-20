using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.FormattedView;

/// <summary>A task's description as a reader sees it, in the web view it's given.</summary>
public sealed class FormattedDescription : ContentControl, IDisposable
{
    private readonly IDescriptionWebView _webView;
    private readonly LinkOpener _links;
    private readonly ShownDescription _shown;
    private readonly ThemeFollower _themeFollower;
    private string _markdown = "";
    private TextBox? _source;
    private bool _isPreview;
    private bool _disposed;

    /// <summary>Takes ownership of <paramref name="webView"/>: releasing the description releases it.</summary>
    public FormattedDescription(IDescriptionWebView webView, ITheme theme, IDefaultBrowser browser)
    {
        ArgumentNullException.ThrowIfNull(webView);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(browser);
        _webView = webView;
        _shown = new ShownDescription(theme, () => webView.CloneAddress, webView.Show);
        _links = new LinkOpener(
            browser,
            address => OtherLinkClicked?.Invoke(this, address),
            failure => OpenFailed?.Invoke(this, failure));
        Content = webView.Element;
        webView.LinkClicked += OnLinkClicked;
        webView.Failed += OnFailed;
        webView.ImageResized += OnImageResized;
        _themeFollower = new ThemeFollower(this, theme, OnThemeChanged);
        _ = new HoverCard(this, PartAt);
    }

    /// <summary>
    /// Makes the card shown by a mention while the pointer rests on it, from the mention's address and template.
    /// Returns null to show none. Unset, no mention shows a card.
    /// </summary>
    public Func<Mention, FrameworkElement?>? MentionCard { get; set; }

    /// <summary>The description's Markdown. Setting it shows it formatted.</summary>
    public string Markdown
    {
        get => _markdown;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _markdown = value;
            if (_disposed)
            {
                return;
            }

            if (_source is not null)
            {
                _source.Text = value;
                return;
            }

            _shown.Set(value);
        }
    }

    /// <summary>
    /// Whether the description is shown as a preview: false at first. In a preview the images can be resized, clicked links
    /// do nothing, and changing it shows the current Markdown again.
    /// </summary>
    public bool IsPreview
    {
        get => _isPreview;
        set
        {
            _isPreview = value;
            if (!_disposed && _source is null)
            {
                _shown.SetPreview(value);
            }
        }
    }

    /// <summary>Raised with the address of a clicked link that fails Rendering's browser check.</summary>
    public event EventHandler<string>? OtherLinkClicked;

    /// <summary>Raised when the default browser fails to open a web link.</summary>
    public event EventHandler<Exception>? OpenFailed;

    /// <summary>Raised when the user resizes an image in the description. The description's Markdown doesn't change.</summary>
    public event EventHandler<ImageResize>? ImageResized;

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
        _webView.ImageResized -= OnImageResized;
        _webView.Dispose();
    }

    private void OnLinkClicked(object? sender, string address)
    {
        if (!_isPreview)
        {
            _links.Click(address);
        }
    }

    private void OnImageResized(object? sender, ImageResize resize)
    {
        if (_isPreview)
        {
            ImageResized?.Invoke(this, resize);
        }
    }

    // The mention under the point, boxed, with the card MentionCard makes for it. The web view finds it by number in its page; the
    // number is the mention's place among Rendering's description mentions. An answer for a page that another has replaced shows nothing.
    internal async Task<HoverPart?> PartAt(Point inDescription)
    {
        if (MentionCard is not { } makeCard || _source is not null || _disposed)
        {
            return null;
        }

        var page = _shown.Pages;
        var found = await _webView.MentionAtAsync(TransformToDescendant(_webView.Element).Transform(inDescription));
        if (found is null || page != _shown.Pages || _source is not null || _disposed)
        {
            return null;
        }

        var mentions = Mentions.In(_markdown, title: false);
        if (found.Mention >= mentions.Count || makeCard(mentions[found.Mention]) is not { } card)
        {
            return null;
        }

        return new HoverPart(_webView.Element.TransformToAncestor(this).TransformBounds(found.Box), card);
    }

    // Shows the Markdown as source with the reason, in place of the web view, for good: the browser raises this once.
    private void OnFailed(object? sender, string reason)
    {
        _webView.ImageResized -= OnImageResized;
        _source = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Text = Markdown };
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = $"Descriptions can't show formatted: {reason}", TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(_source);
        Content = panel;
    }

    // The web view follows the theme; the source shown in its place has no theme.
    private void OnThemeChanged()
    {
        if (!_disposed && _source is null)
        {
            _shown.ThemeChanged();
        }
    }
}
