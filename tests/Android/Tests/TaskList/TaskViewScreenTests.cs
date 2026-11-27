using Memoria.ToDo.Android.Fakes;
using Memoria.ToDo.Android.TaskEditor;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Android.TaskList.Tests;

/// <summary>Create sets the static <see cref="TaskViewServices"/>, so every test that calls it runs in this one collection.</summary>
internal static class TaskViewServicesCollection
{
    public const string Name = "TaskViewServices";
}

/// <summary>Counts how often Add was shown.</summary>
internal sealed class CountingAddScreen : IAddScreen
{
    public int Shown { get; private set; }

    public void Show() => Shown++;
}

[Unit]
[Collection(TaskViewServicesCollection.Name)]
public sealed class TaskViewScreen_creating_and_showing
{
    private const string TaskViewActivity = "memoria.todo.tasklist.TaskViewActivity";

    private static readonly Serilog.ILogger Logger = new InMemoryLogSink().CreateLogger();

    [Fact]
    public void rejects_a_null_add_screen()
    {
        Assert.Throws<ArgumentNullException>(() => TaskViewScreen.Create(null!, () => { }, new RecordingScreenOpener(), Logger));
    }

    [Fact]
    public void rejects_a_null_settings_action()
    {
        Assert.Throws<ArgumentNullException>(() => TaskViewScreen.Create(new CountingAddScreen(), null!, new RecordingScreenOpener(), Logger));
    }

    [Fact]
    public void rejects_a_null_opener()
    {
        Assert.Throws<ArgumentNullException>(() => TaskViewScreen.Create(new CountingAddScreen(), () => { }, null!, Logger));
    }

    [Fact]
    public void rejects_a_null_logger()
    {
        Assert.Throws<ArgumentNullException>(() => TaskViewScreen.Create(new CountingAddScreen(), () => { }, new RecordingScreenOpener(), null!));
    }

    [Fact]
    public void opens_the_task_view_activity_once_per_show()
    {
        var opener = new RecordingScreenOpener();
        var screen = TaskViewScreen.Create(new CountingAddScreen(), () => { }, opener, Logger);

        screen.Show();

        Assert.Equal([TaskViewActivity], opener.Opened);
    }
}

/// <summary>Taps go through the static <see cref="TaskViewServices"/>, so these tests share one class and never run side by side.</summary>
[Unit]
[Collection(TaskViewServicesCollection.Name)]
public sealed class TaskViewScreen_handling_taps
{
    [Fact]
    public void the_floating_button_shows_add_and_does_not_open_settings()
    {
        var add = new CountingAddScreen();
        var settingsOpened = 0;
        TaskViewScreen.Create(add, () => settingsOpened++, new RecordingScreenOpener(), new InMemoryLogSink().CreateLogger());

        TaskViewServices.Current.Get().AddTapped();

        Assert.Equal(1, add.Shown);
        Assert.Equal(0, settingsOpened);
    }

    [Fact]
    public void the_settings_item_opens_settings_and_does_not_show_add()
    {
        var add = new CountingAddScreen();
        var settingsOpened = 0;
        TaskViewScreen.Create(add, () => settingsOpened++, new RecordingScreenOpener(), new InMemoryLogSink().CreateLogger());

        TaskViewServices.Current.Get().SettingsTapped();

        Assert.Equal(1, settingsOpened);
        Assert.Equal(0, add.Shown);
    }

    [Fact]
    public void a_later_create_replaces_what_an_earlier_one_set()
    {
        var earlier = new CountingAddScreen();
        var later = new CountingAddScreen();
        TaskViewScreen.Create(earlier, () => { }, new RecordingScreenOpener(), new InMemoryLogSink().CreateLogger());
        TaskViewScreen.Create(later, () => { }, new RecordingScreenOpener(), new InMemoryLogSink().CreateLogger());

        TaskViewServices.Current.Get().AddTapped();

        Assert.Equal(0, earlier.Shown);
        Assert.Equal(1, later.Shown);
    }

    [Fact]
    public void creating_showing_and_tapping_write_nothing_at_information_or_above()
    {
        var sink = new InMemoryLogSink();
        var screen = TaskViewScreen.Create(new CountingAddScreen(), () => { }, new RecordingScreenOpener(), sink.CreateLogger());

        screen.Show();
        TaskViewServices.Current.Get().AddTapped();
        TaskViewServices.Current.Get().SettingsTapped();

        sink.Events.AssertNothingAtInformationOrAbove();
    }
}
