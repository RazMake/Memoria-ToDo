using Memoria.ToDo.Android.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Android.TaskEditor.Tests;

[Unit]
public sealed class AddScreen_creating_and_showing
{
    private const string AddActivity = "memoria.todo.taskeditor.AddActivity";

    [Fact]
    public void rejects_a_null_opener()
    {
        var logger = new InMemoryLogSink().CreateLogger();

        Assert.Throws<ArgumentNullException>(() => AddScreen.Create(null!, logger));
    }

    [Fact]
    public void rejects_a_null_logger()
    {
        Assert.Throws<ArgumentNullException>(() => AddScreen.Create(new RecordingScreenOpener(), null!));
    }

    [Fact]
    public void opens_the_add_activity_once_per_show()
    {
        var opener = new RecordingScreenOpener();
        var screen = AddScreen.Create(opener, new InMemoryLogSink().CreateLogger());

        screen.Show();
        Assert.Equal([AddActivity], opener.Opened);

        screen.Show();
        Assert.Equal([AddActivity, AddActivity], opener.Opened);
    }

    [Fact]
    public void writes_nothing_at_information_or_above()
    {
        var sink = new InMemoryLogSink();

        AddScreen.Create(new RecordingScreenOpener(), sink.CreateLogger()).Show();

        sink.Events.AssertNothingAtInformationOrAbove();
    }
}
