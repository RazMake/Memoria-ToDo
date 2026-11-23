using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Path 8, closing with a changed edit: the prompt asks Save, Discard, or Cancel; Cancel keeps the edit open, Save saves and
/// closes, and Discard closes without saving. What each answer means is in <see cref="ViewTasksWindowCloseTests"/>; the prompt is
/// answered by <see cref="ClosePrompt"/>.
/// </summary>
[UX("TaskList")]
[Collection(ScreenTests.Collection)]
public class TaskViewClosePromptPathTests
{
    private readonly ClosePrompt _prompt = new();

    private bool Close(TaskListScreen screen, Action<System.Windows.Window> answer) => _prompt.Run(() => screen.View.Close(), answer);

    [WpfFact]
    public void Closing_with_a_changed_edit_asks_and_Cancel_Save_and_Discard_each_do_what_they_say()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.LeftShift, Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");

        Assert.False(Close(screen, ClosePrompt.Click("Cancel")));
        Assert.True(screen.View.IsOpen);
        Assert.Equal("Buy oat milk", screen.EditorTitleText("Buy milk"));
        Assert.Equal("Buy milk", services.Stored("Buy milk").Title);

        Assert.True(Close(screen, ClosePrompt.Click("Save")));
        Assert.False(screen.View.IsOpen);
        Assert.Equal("Buy oat milk", services.Tasks.All.Single().Title);

        screen.View.Show();
        screen.PressOn("Buy oat milk", Key.LeftShift, Key.F2);
        screen.TypeTitle("Buy oat milk", "Buy almond milk");

        Assert.True(Close(screen, ClosePrompt.Click("Discard")));
        Assert.False(screen.View.IsOpen);
        Assert.Equal("Buy oat milk", services.Tasks.All.Single().Title);
        Assert.Equal(3, _prompt.Shown);
    }
}
