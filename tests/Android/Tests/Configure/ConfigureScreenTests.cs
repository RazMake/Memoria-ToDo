using Memoria.ToDo.Android.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Android.Configure.Tests;

[Unit]
public sealed class ConfigureScreen_creating_and_showing
{
    private const string ConfigureActivity = "memoria.todo.configure.ConfigureActivity";

    [Fact]
    public void rejects_a_null_opener()
    {
        var logger = new InMemoryLogSink().CreateLogger();

        Assert.Throws<ArgumentNullException>(() => ConfigureScreen.Create(null!, logger));
    }

    [Fact]
    public void rejects_a_null_logger()
    {
        Assert.Throws<ArgumentNullException>(() => ConfigureScreen.Create(new RecordingScreenOpener(), null!));
    }

    [Fact]
    public void opens_the_configure_activity_once_per_show()
    {
        var opener = new RecordingScreenOpener();
        var screen = ConfigureScreen.Create(opener, new InMemoryLogSink().CreateLogger());

        screen.Show();
        Assert.Equal([ConfigureActivity], opener.Opened);

        screen.Show();
        Assert.Equal([ConfigureActivity, ConfigureActivity], opener.Opened);
    }

    [Fact]
    public void writes_nothing_at_information_or_above()
    {
        var sink = new InMemoryLogSink();

        ConfigureScreen.Create(new RecordingScreenOpener(), sink.CreateLogger()).Show();

        sink.Events.AssertNothingAtInformationOrAbove();
    }
}
