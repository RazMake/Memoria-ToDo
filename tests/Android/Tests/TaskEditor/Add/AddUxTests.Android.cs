using Google.Android.Material.AppBar;
using Memoria.ToDo.Android.DeviceHost;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Android.TaskEditor.Tests.Add;

[UX("Add"), Collection(NativeScreenCollection.Name)]
public sealed class AddUxTests
{
    [Fact]
    public async Task shows_the_add_toolbar_in_the_native_screen()
    {
        await using var screen = await NativeScreen<AddActivity>.ShowAsync();
        await screen.CheckAsync(activity =>
        {
            var toolbar = Assert.IsType<MaterialToolbar>(activity.FindViewById(Resource.Id.taskeditor_toolbar));
            Assert.Equal("Add new task", toolbar.Title);
            Assert.True(toolbar.Width > 0);
            Assert.True(toolbar.Height > 0);
        });
    }
}
