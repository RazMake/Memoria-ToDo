using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>What <see cref="ViewTasksWindow.Create"/> requires: every part, the platform's Close window keys included.</summary>
[Unit]
public class ViewTasksWindowCreateTests
{
    [Fact]
    public void Rejects_missing_Close_window_keys()
    {
        var services = new ListServices();

        var failure = Assert.Throws<ArgumentNullException>(() => ViewTasksWindow.Create(
            services.Tasks, Palette.Colors, services.Theme, services.Time, services.Clipboard, new FakeImageFileChooser(),
            services.Files, () => new FakeDescriptionWebView(), services.Browser, services.Sync, null!));

        Assert.Equal("closeWindowKeys", failure.ParamName);
    }
}
