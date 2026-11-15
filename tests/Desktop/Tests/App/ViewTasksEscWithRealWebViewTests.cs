using Memoria.ToDo.Testing;
using Microsoft.Web.WebView2.Wpf;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// Esc reaches Task View from the real description web view. The web view passes keys to the window as accelerator keys,
/// and the page never holds the keyboard focus, so the test clicks the page with the real mouse and sends a real Esc key
/// press to Windows. Needs the WebView2 Runtime and the desktop's input, like the hot key tests.
/// </summary>
[Integration]
[Collection(ScreenTests.Collection)]
public class ViewTasksEscWithRealWebViewTests
{
    [WpfFact]
    public async Task Esc_after_clicking_the_description_closes_Task_View()
    {
        using var view = new RealViewTasks();
        view.Add("First", "alphadescription");
        var core = await view.ShowAsync();
        view.Expand("First");
        await view.WaitForPageText(core, "alphadescription");
        var window = view.Window;
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.Closed += (_, _) => closed.TrySetResult();
        var web = (WebView2CompositionControl)view.WebViews.Single().Element;
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.PreviewMouseLeftButtonUp += (_, _) => released.TrySetResult();
        KeyboardInput.BringToFront(new System.Windows.Interop.WindowInteropHelper(window).Handle);
        try
        {
            MouseInput.Press(web.PointToScreen(new System.Windows.Point(web.ActualWidth - 6, 4)));
        }
        finally
        {
            MouseInput.Release();
        }

        await released.Task.WaitAsync(RealViewTasks.Limit);

        KeyboardInput.Chord(KeyboardInput.Escape);

        await closed.Task.WaitAsync(RealViewTasks.Limit);
        Assert.False(view.View.IsOpen);
    }
}
