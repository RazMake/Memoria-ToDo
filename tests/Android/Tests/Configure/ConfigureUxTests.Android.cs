using Google.Android.Material.AppBar;
using Memoria.ToDo.Android.DeviceHost;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Android.Configure.Tests;

[UX("Configure"), Collection(NativeScreenCollection.Name)]
public sealed class ConfigureUxTests
{
    [Fact]
    public async Task shows_the_configure_toolbar_in_the_native_screen()
    {
        await using var screen = await NativeScreen<ConfigureActivity>.ShowAsync();
        await screen.CheckAsync(activity =>
        {
            var toolbar = Assert.IsType<MaterialToolbar>(activity.FindViewById(Resource.Id.configure_toolbar));
            Assert.Equal("Configure", toolbar.Title);
            Assert.True(toolbar.Width > 0);
            Assert.True(toolbar.Height > 0);
        });
    }
}
