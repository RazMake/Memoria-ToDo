using System.Windows.Input;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// The platform's Close window keys in Task View, on the common path: pressed from any control, they close the window
/// as its close button does, so an open edit is cleaned up the same way. The keys here are Shift+F9, which no list key
/// uses. How the keys are matched is tested with the platform's CloseWindowKeys.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListCloseKeysTests
{
    [WpfFact]
    public void The_keys_close_the_window_from_a_row()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        services.CloseWindowKeys.Set(ModifierKeys.Shift, Key.F9);
        using var screen = new TaskListScreen(services);

        screen.PressOn("Buy milk", Key.LeftShift, Key.F9);

        Assert.False(screen.View.IsOpen);
    }

    [WpfFact]
    public void The_keys_close_the_window_from_the_search_box()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        services.CloseWindowKeys.Set(ModifierKeys.Shift, Key.F9);
        using var screen = new TaskListScreen(services);
        screen.Search("milk");

        screen.Press(Key.LeftShift, Key.F9);

        Assert.False(screen.View.IsOpen);
    }

    [WpfFact]
    public void The_keys_close_the_window_while_a_task_is_being_edited_and_delete_the_images_the_edit_pasted()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        services.CloseWindowKeys.Set(ModifierKeys.Shift, Key.F9);
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        var before = services.AllFiles;
        services.Clipboard.Image = FakeImageEncoder.Image(ImageFormat.Png, 100, 80);
        screen.PasteImageIntoDescription();
        var images = services.AllFiles.Except(before).ToList();
        Assert.NotEmpty(images);
        screen.TypeTitle("Buy milk", "Buy oat milk");

        screen.Press(Key.LeftShift, Key.F9);

        Assert.False(screen.View.IsOpen);
        Assert.All(images, image => Assert.False(services.Files.FileExists(image)));
        Assert.Equal("Buy milk", services.Tasks.All.Single().Title);
    }

    [WpfFact]
    public void The_window_opens_again_after_the_keys_closed_it()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        services.CloseWindowKeys.Set(ModifierKeys.Shift, Key.F9);
        using var screen = new TaskListScreen(services);
        screen.Press(Key.LeftShift, Key.F9);

        screen.View.Show();

        Assert.True(screen.View.IsOpen);
        Assert.Equal(["Buy milk"], screen.RowTitles);
    }
}
