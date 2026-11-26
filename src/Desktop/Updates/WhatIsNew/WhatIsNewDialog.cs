using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Desktop.Platform;
using Serilog;

namespace Memoria.ToDo.Desktop.Updates;

/// <summary>The What is new dialog: modal, titled "What is new", with each release's notes under its version, newest first.</summary>
internal sealed class WhatIsNewDialog(ITheme theme, Func<IDescriptionWebView> createWebView, IDefaultBrowser browser, CloseWindowKeys closeWindowKeys, IPointerPosition pointer, ILogger logger)
{
    /// <summary>
    /// Shows the notes in a new web view, which it releases when the dialog closes. Returns when the dialog has closed, and, when
    /// Update Now closed it, after <paramref name="updates"/> has started the update. The dialog follows <paramref name="updates"/> while open.
    /// </summary>
    public void Show(Window owner, NewerVersion newer, IUpdateStatus updates)
    {
        using var page = new FormattedDescription(createWebView(), theme, browser, pointer, logger) { Markdown = MarkdownOf(newer), Margin = new Thickness(12) };
        page.OpenFailed += (_, failure) => logger.Warning(failure, "A link in the release notes could not be opened");
        var updateNow = new Button { Content = "Update Now", Margin = new Thickness(0, 12, 0, 12) };
        var close = new Button { Content = "Close", IsCancel = true, Margin = new Thickness(12) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Children = { updateNow, close } };
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = page };
        var layout = new Grid { RowDefinitions = { new RowDefinition(), new RowDefinition { Height = GridLength.Auto } } };
        Grid.SetRow(buttons, 1);
        layout.Children.Add(scroll);
        layout.Children.Add(buttons);

        void ShowState()
        {
            var stage = updates.State.Stage;
            updateNow.Visibility = stage is UpdateStage.Available or UpdateStage.Updating ? Visibility.Visible : Visibility.Collapsed;
            updateNow.IsEnabled = stage == UpdateStage.Available;
        }

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
        var startUpdate = false;
        close.Click += (_, _) => window.Close();
        updateNow.Click += (_, _) =>
        {
            startUpdate = true;
            window.Close();
        };
        ShowState();
        updates.Changed += OnChanged;
        void OnChanged(object? sender, EventArgs e) => ShowState();
        closeWindowKeys.Attach(window);
        EscCloses.Attach(window);
        try
        {
            ModalDialog.Show(window, logger);
        }
        finally
        {
            updates.Changed -= OnChanged;
        }

        // The update asks Task View to close its windows, so it starts only once this dialog, which Task View may own, is gone.
        if (startUpdate)
        {
            logger.Information("Update Now was clicked in What is new");
            updates.UpdateNow();
        }
    }

    private static string MarkdownOf(NewerVersion newer)
    {
        var markdown = string.Join(
            "\n\n",
            newer.Notes.Select(release => $"## {release.Version}\n\n{(string.IsNullOrWhiteSpace(release.Markdown) ? "No notes for this version." : release.Markdown)}"));
        return newer.OlderNotesPage is { } older ? $"{markdown}\n\nOlder notes are on the [GitHub Releases page]({older.AbsoluteUri})." : markdown;
    }
}
