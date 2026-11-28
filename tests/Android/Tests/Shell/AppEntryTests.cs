using Memoria.ToDo.Android.TaskList;
using Memoria.ToDo.Testing;
using Serilog.Events;
using Xunit;

namespace Memoria.ToDo.Android.Shell.Tests;

/// <summary>Counts how often Task View was shown.</summary>
internal sealed class CountingTaskViewScreen : ITaskViewScreen
{
    public int Shown { get; private set; }

    public void Show() => Shown++;
}

[Unit]
public sealed class AppEntry_connecting
{
    [Fact]
    public void rejects_a_null_task_view()
    {
        Assert.Throws<ArgumentNullException>(() => AppEntry.Connect(null!, new InMemoryLogSink().CreateLogger()));
    }

    [Fact]
    public void rejects_a_null_logger()
    {
        Assert.Throws<ArgumentNullException>(() => AppEntry.Connect(new CountingTaskViewScreen(), null!));
    }
}

/// <summary>Opening goes through the static <see cref="EntryServices"/>, so these tests share one class and never run side by side.</summary>
[Unit]
public sealed class AppEntry_opening_the_app
{
    [Fact]
    public void shows_task_view_exactly_once()
    {
        var taskView = new CountingTaskViewScreen();
        AppEntry.Connect(taskView, new InMemoryLogSink().CreateLogger());

        EntryServices.Current.Get().Open();

        Assert.Equal(1, taskView.Shown);
    }

    [Fact]
    public void logs_one_information_entry_naming_the_way_in()
    {
        var sink = new InMemoryLogSink();
        AppEntry.Connect(new CountingTaskViewScreen(), sink.CreateLogger());

        EntryServices.Current.Get().Open();

        Assert.Single(sink.Events, entry => entry.Level == LogEventLevel.Information);
        sink.Events.SingleAt(LogEventLevel.Information, "Opening the app");
    }

    [Fact]
    public void a_second_connect_replaces_the_first()
    {
        var first = new CountingTaskViewScreen();
        var second = new CountingTaskViewScreen();
        AppEntry.Connect(first, new InMemoryLogSink().CreateLogger());
        AppEntry.Connect(second, new InMemoryLogSink().CreateLogger());

        EntryServices.Current.Get().Open();

        Assert.Equal(0, first.Shown);
        Assert.Equal(1, second.Shown);
    }
}
