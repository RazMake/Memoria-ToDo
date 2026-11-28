using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Android.Platform.Tests;

[Integration]
public sealed class AndroidProcessTests
{
    [Fact]
    public void runs_xunit_inside_the_Android_process()
    {
        Assert.True(OperatingSystem.IsAndroid());
        Assert.NotNull(TestContext.Current.Test);
        Assert.Equal("com.razmake.memoriatodo.tests", global::Android.App.Application.Context.PackageName);
    }
}
