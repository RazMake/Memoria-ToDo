using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

[Unit]
public class WpfDescriptionWebViewFocusTests
{
    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Logs_unrestorable_focus_without_turning_it_into_a_page_failure(bool hasFocusableAncestor)
    {
        var files = new FakeFileSystem();
        var rules = TestTaskStore.Create(files, new FakeImageEncoder(), new FakeTimeProvider(), @"C:\web-view-focus-tests\device")
            .Synchronization.Files;
        var log = new InMemoryLogSink();
        using var view = new WpfDescriptionWebView(
            null, @"C:\web-view-focus-tests\edit-images", @"C:\web-view-focus-tests\browser-data", files, rules, log.CreateLogger());
        var failures = new List<string>();
        view.Failed += (_, failure) => failures.Add(failure);
        if (hasFocusableAncestor)
        {
            // A detached host cannot take keyboard focus; no browser or window is started.
            _ = new Border { Focusable = true, Child = view.Element };
        }

        view.Element.RaiseEvent(new RoutedEventArgs(UIElement.GotFocusEvent, view.Element));

        Assert.Single(log.Events, entry => entry.Level == LogEventLevel.Error);
        Assert.DoesNotContain(log.Events, entry => entry.Level == LogEventLevel.Information);
        Assert.Empty(failures);
    }
}
