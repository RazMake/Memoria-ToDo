using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Desktop.Platform;
using Serilog;

namespace Memoria.ToDo.Desktop.Updates;

/// <summary>The What is new dialog: modal, titled "What is new", with each release's notes under its version, newest first.</summary>
internal sealed class WhatIsNewDialog(ITheme theme, Func<IDescriptionWebView> createWebView, IDefaultBrowser browser, CloseWindowKeys closeWindowKeys, IPointerPosition pointer, ILogger logger)
{
    /// <summary>Shows the notes in a new web view, which it releases when the dialog closes. Returns when the dialog has closed.</summary>
    public void Show(Window owner, NewerVersion newer)
    {
        using var page = new FormattedDescription(createWebView(), theme, browser, pointer, logger) { Markdown = MarkdownOf(newer), Margin = new Thickness(12) };
        page.OpenFailed += (_, failure) => logger.Warning(failure, "A link in the release notes could not be opened");
        var close = new Button { Content = "Close", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(12) };
        var layout = new Grid { RowDefinitions = { new RowDefinition(), new RowDefinition { Height = GridLength.Auto } } };
        Grid.SetRow(close, 1);
        layout.Children.Add(page);
        layout.Children.Add(close);

        var window = new Window
        {
            Title = "What is new",
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Width = 640,
            Height = 520,
            ShowInTaskbar = false,
            Content = layout,
        };
        close.Click += (_, _) => window.Close();
        closeWindowKeys.Attach(window);
        EscCloses.Attach(window);
        ModalDialog.Show(window, logger);
    }

    private static string MarkdownOf(NewerVersion newer)
    {
        var markdown = string.Join(
            "\n\n",
            newer.Notes.Select(release => $"## {release.Version}\n\n{(string.IsNullOrWhiteSpace(release.Markdown) ? "No notes for this version." : release.Markdown)}"));
        return newer.OlderNotesPage is { } older ? $"{markdown}\n\nOlder notes are on the [GitHub Releases page]({older.AbsoluteUri})." : markdown;
    }
}
