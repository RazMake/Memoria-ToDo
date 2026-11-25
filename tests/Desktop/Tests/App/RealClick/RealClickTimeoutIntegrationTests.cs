using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Platform.Tests;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

[Integration]
[Collection(ScreenTests.Collection)]
public class RealClickTimeoutIntegrationTests
{
    [WpfFact]
    public async Task A_release_timeout_keeps_the_original_timeout_and_reports_the_clicks_observations()
    {
        var target = new Border { Width = 300, Height = 100, Background = Brushes.White };
        var window = new Window { Width = 400, Height = 240, Content = target };
        try
        {
            window.Show();
            AfterFirstDraw.Wait(window.Dispatcher);

            var failure = await Assert.ThrowsAsync<TimeoutException>(() =>
                RealClick.OnAsync(window, target, new Point(150, 50), TimeSpan.Zero));

            Assert.IsType<TimeoutException>(failure.InnerException);
            Assert.Contains("Border 300x100", failure.Message);
            Assert.Contains("after press:", failure.Message);
            Assert.Contains("at timeout:", failure.Message);
            Assert.Contains("pending input:", failure.Message);
            Assert.Contains("Press: count 1", failure.Message);
            Assert.Contains("Release: count 1", failure.Message);
            Assert.Contains("NativeDown:", failure.Message);
            Assert.Contains("NativeUp:", failure.Message);
        }
        finally
        {
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            window.Close();
        }
    }
}
