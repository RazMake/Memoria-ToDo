using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Sync;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The Sync button and the line beside it: a compact icon button that turns green and off while any sync runs, and
/// "Syncing…" in place of the failure line. Measured in a bare container with no window or theme, so the numbers are the
/// control's own; the container holds the theme resource the green comes from.
/// </summary>
[Unit]
public class ListSyncTests
{
    private const string SuccessKey = "SystemFillColorSuccessBrush";

    [WpfFact]
    public void The_button_shows_the_sync_icon_from_the_Windows_icon_font()
    {
        using var host = new Host();

        var (glyph, font) = host.Icon;

        Assert.Equal("\uE895", glyph);
        Assert.StartsWith("Segoe Fluent Icons", font!.Source);
        Assert.Contains("Segoe MDL2 Assets", font.Source);
    }

    [WpfFact]
    public void The_button_has_the_tooltip_Sync_F5_and_the_name_Sync_for_assistive_tools()
    {
        using var host = new Host();

        Assert.Equal("Sync (F5)", host.ToolTipText);
        Assert.Equal("Sync", AutomationProperties.GetName(host.Button));
    }

    [WpfFact]
    public void The_button_is_on_and_shows_no_line_while_no_sync_runs()
    {
        using var host = new Host();

        Assert.True(host.Button.IsEnabled);
        Assert.Empty(host.Lines);
    }

    [WpfFact]
    public async Task While_a_sync_the_button_requested_runs_the_button_is_green_and_off_and_Syncing_shows_after_it()
    {
        using var host = new Host();
        var pass = host.Hold();

        var waiting = host.Sync.SyncAsync();

        Assert.False(host.Button.IsEnabled);
        Assert.Equal(Colors.Green, host.ButtonFill);
        Assert.Equal(["Syncing…"], host.Lines.Select(line => line.Text));
        var line = host.Lines.Single();
        Assert.True(line.Left >= host.ButtonRight - 0.5, "Syncing… isn't after the button.");
        Assert.True(line.Left - host.ButtonRight < 24, "Syncing… isn't right after the button.");
        Assert.NotEqual(HorizontalAlignment.Right, line.Block.HorizontalAlignment);
        Assert.NotEqual(HorizontalAlignment.Center, line.Block.HorizontalAlignment);

        pass.SetResult(new FakeSyncStatus());
        await waiting.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        Assert.True(host.Button.IsEnabled);
        Assert.NotEqual(Colors.Green, host.ButtonFill);
        Assert.Empty(host.Lines);
    }

    [WpfFact]
    public void While_a_sync_nobody_here_requested_runs_the_button_is_green_and_off_and_Syncing_shows()
    {
        using var host = new Host();

        host.ServiceStatus(SyncState.Working);

        Assert.False(host.Button.IsEnabled);
        Assert.Equal(Colors.Green, host.ButtonFill);
        Assert.Equal(["Syncing…"], host.Lines.Select(line => line.Text));

        host.ServiceStatus(SyncState.Idle);

        Assert.True(host.Button.IsEnabled);
        Assert.NotEqual(Colors.Green, host.ButtonFill);
        Assert.Empty(host.Lines);
    }

    [WpfFact]
    public void The_green_is_the_theme_success_color_and_follows_the_theme()
    {
        using var host = new Host();
        host.ServiceStatus(SyncState.Working);

        host.Container.Resources[SuccessKey] = new SolidColorBrush(Colors.Blue);

        Assert.Equal(Colors.Blue, host.ButtonFill);
    }

    [WpfFact]
    public async Task Syncing_takes_the_place_of_the_failure_line_and_the_failure_returns_when_the_sync_ends()
    {
        using var host = new Host();
        host.Service.SyncOperation = _ => Task.FromResult<ISyncStatus>(new FakeSyncStatus
        {
            State = SyncState.Error, Failure = SyncFailure.Connection, Details = "Requested sync failed"
        });
        await host.Sync.SyncAsync();
        Assert.Equal(["Requested sync failed"], host.Lines.Select(line => line.Text));

        host.ServiceStatus(SyncState.Working);

        Assert.Equal(["Syncing…"], host.Lines.Select(line => line.Text));

        host.ServiceStatus(SyncState.Idle);

        Assert.Equal(["Requested sync failed"], host.Lines.Select(line => line.Text));
        Assert.True(host.Button.IsEnabled);
    }

    [WpfFact]
    public async Task A_sync_that_ends_well_leaves_no_line()
    {
        using var host = new Host();
        var pass = host.Hold();
        var waiting = host.Sync.SyncAsync();

        pass.SetResult(new FakeSyncStatus());
        await waiting.WaitAsync(TestWait.ForSignal, TestContext.Current.CancellationToken);

        Assert.Empty(host.Lines);
    }

    private sealed class Host : IDisposable
    {
        public Host()
        {
            Service = new FakeSyncService();
            Sync = new ListSync(Service, Palette.Colors, new FakeTheme(), Serilog.Core.Logger.None);
            Container = new Grid();
            Container.Resources[SuccessKey] = new SolidColorBrush(Colors.Green);
            Container.Children.Add(Sync);
        }

        public FakeSyncService Service { get; }

        public ListSync Sync { get; }

        public Grid Container { get; }

        public Button Button => OpenWindows.Descendants(Sync).OfType<Button>().Single();

        public (string? Glyph, FontFamily? Font) Icon => Button.Content switch
        {
            string text => (text, Button.FontFamily),
            TextBlock block => (OpenWindows.TextOf(block), block.FontFamily),
            _ => (null, null),
        };

        public string? ToolTipText => Button.ToolTip is ToolTip { Content: var content } ? content?.ToString() : Button.ToolTip?.ToString();

        public Color ButtonFill => Button.Background is SolidColorBrush brush ? brush.Color : Colors.Transparent;

        public double ButtonRight
        {
            get
            {
                Layout();
                return Button.TranslatePoint(new Point(Button.ActualWidth, 0), Container).X;
            }
        }

        /// <summary>The texts shown outside the button, left to right, with where each starts.</summary>
        public IReadOnlyList<(string Text, double Left, TextBlock Block)> Lines
        {
            get
            {
                Layout();
                var inButton = OpenWindows.Descendants(Button).ToHashSet();
                return
                [
                    .. OpenWindows.Descendants(Sync).OfType<TextBlock>()
                        .Where(block => !inButton.Contains(block) && block.Visibility == Visibility.Visible)
                        .Select(block => (Text: OpenWindows.TextOf(block), Left: block.TranslatePoint(new Point(0, 0), Container).X, Block: block))
                        .Where(line => line.Text.Length > 0)
                        .OrderBy(line => line.Left),
                ];
            }
        }

        public TaskCompletionSource<ISyncStatus> Hold()
        {
            var pass = new TaskCompletionSource<ISyncStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
            Service.SyncOperation = _ => pass.Task;
            return pass;
        }

        /// <summary>The shared service reports the state, as when a sync starts on its own, and the window's dispatcher shows it.</summary>
        public void ServiceStatus(SyncState state)
        {
            Service.Publish(new FakeSyncStatus { State = state });
            Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
        }

        public void Dispose() => Sync.Dispose();

        private void Layout()
        {
            Container.Measure(new Size(500, 100));
            Container.Arrange(new Rect(0, 0, 500, 100));
        }
    }
}
