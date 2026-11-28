using Google.Android.Material.AppBar;
using Google.Android.Material.FloatingActionButton;
using Memoria.ToDo.Android.DeviceHost;
using Memoria.ToDo.Android.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Android.TaskList.Tests.TaskView;

[UX("TaskView"), Collection(NativeScreenCollection.Name)]
public sealed class TaskViewUxTests
{
    [Fact]
    public async Task routes_the_native_add_button_and_settings_item_to_their_services()
    {
        var add = new CountingAddScreen();
        var settings = 0;
        await MainThread.InvokeOnMainThreadAsync(() =>
            TaskViewScreen.Create(add, () => settings++, new RecordingScreenOpener(), new InMemoryLogSink().CreateLogger()));
        await using var screen = await NativeScreen<TaskViewActivity>.ShowAsync();
        await screen.CheckAsync(activity =>
        {
            var button = Assert.IsType<FloatingActionButton>(activity.FindViewById(Resource.Id.tasklist_add_button));
            Assert.True(button.Width > 0);
            Assert.True(button.PerformClick());
            Assert.Equal(1, add.Shown);
            Assert.Equal(0, settings);
            var toolbar = Assert.IsType<MaterialToolbar>(activity.FindViewById(Resource.Id.tasklist_toolbar));
            Assert.True(toolbar.Menu!.PerformIdentifierAction(Resource.Id.tasklist_settings, 0));
            Assert.Equal(1, settings);
            Assert.Equal(1, add.Shown);
        });
    }
}
