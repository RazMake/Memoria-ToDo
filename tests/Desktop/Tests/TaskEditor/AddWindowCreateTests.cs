using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>What <see cref="AddWindow.Create"/> requires: every part, the platform's Close window keys included.</summary>
[Unit]
public class AddWindowCreateTests
{
    [Fact]
    public void Rejects_missing_Close_window_keys()
    {
        var services = new EditorServices();

        var failure = Assert.Throws<ArgumentNullException>(() => AddWindow.Create(
            services.Tasks, services.TagColors, Palette.Colors, services.Theme, services.Time, services.Clipboard, services.ImageFiles,
            services.Files, null!, services.Updates));

        Assert.Equal("closeWindowKeys", failure.ParamName);
    }

    [Fact]
    public void Rejects_a_missing_update_status()
    {
        var services = new EditorServices();

        var failure = Assert.Throws<ArgumentNullException>(() => AddWindow.Create(
            services.Tasks, services.TagColors, Palette.Colors, services.Theme, services.Time, services.Clipboard, services.ImageFiles,
            services.Files, services.CloseWindowKeys, null!));

        Assert.Equal("updates", failure.ParamName);
    }
}
