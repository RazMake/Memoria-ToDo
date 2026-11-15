using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>What View Tasks refuses when it is created, before it makes a window. No window is made, so these run with no screen.</summary>
[Unit]
public class ViewTasksWindowCreateTests
{
    [Fact]
    public void Create_rejects_missing_tag_colors_by_name()
    {
        var services = new ListServices();

        var failure = Assert.Throws<ArgumentNullException>(() => ViewTasksWindow.Create(
            services.Tasks, null!, Palette.Colors, services.Theme, services.Time, services.Clipboard,
            new FakeImageFileChooser(), services.Files, () => new FakeDescriptionWebView(),
            services.Browser, services.Sync));

        Assert.Equal("tagColors", failure.ParamName);
    }
}
