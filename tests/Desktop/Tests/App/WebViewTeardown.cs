using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// How a test host ends a real <c>WpfDescriptionWebView</c>: the web views first, then the windows, then a bounded wait for the
/// browser's resource-release signal. A window closed before its WebView2 control is disposed can leave the browser's own window, titled "about:blank",
/// on the user's screen. WebView2's <c>CoreWebView2Environment</c> has nothing to dispose; the control's disposal releases it.
/// </summary>
internal static class WebViewTeardown
{
    public static readonly TimeSpan ExitLimit = TimeSpan.FromSeconds(20);

    /// <summary>Watches the signal that the browser has released its resources, including its user data folder.</summary>
    public static Task WatchExit(CoreWebView2Environment environment)
    {
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnExited(object? sender, CoreWebView2BrowserProcessExitedEventArgs args)
        {
            environment.BrowserProcessExited -= OnExited;
            exited.TrySetResult();
        }

        environment.BrowserProcessExited += OnExited;
        return exited.Task;
    }

    /// <summary>
    /// Disposes each web view and its control, then calls <paramref name="closeWindows"/>, then waits for WebView2 to release
    /// its resources without blocking the UI thread. A browser still running after <see cref="ExitLimit"/> is killed with its
    /// process tree, and the call then throws so the caller leaves its folders in place. Each step runs even when an earlier one throws.
    /// </summary>
    public static void Run(IEnumerable<WpfDescriptionWebView> views, Action closeWindows, Process? browser, Task? resourcesReleased, string dataFolder)
    {
        if (browser is not null)
        {
            ArgumentNullException.ThrowIfNull(resourcesReleased);
        }

        var failures = new List<Exception>();
        foreach (var view in views)
        {
            Attempt(failures, view.Dispose);
            Attempt(failures, ((WebView2CompositionControl)view.Element).Dispose);
        }

        Attempt(failures, closeWindows);
        if (browser is not null)
        {
            try
            {
                Attempt(failures, () => EndBrowser(browser, resourcesReleased!, dataFolder));
            }
            finally
            {
                browser.Dispose();
            }
        }

        if (failures.Count == 1)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(failures[0]);
        }

        if (failures.Count > 1)
        {
            throw new AggregateException(failures);
        }
    }

    private static void Attempt(List<Exception> failures, Action step)
    {
        try
        {
            step();
        }
        catch (Exception failure)
        {
            failures.Add(failure);
        }
    }

    private static void EndBrowser(Process browser, Task resourcesReleased, string dataFolder)
    {
        var exited = false;
        try
        {
            exited = WaitForExitPumping(resourcesReleased);
        }
        finally
        {
            if (!exited)
            {
                // The backstop: no window of a browser that outlived the wait stays on the user's screen.
                try
                {
                    browser.Kill(entireProcessTree: true);
                    browser.WaitForExit(ExitLimit);
                }
                catch (InvalidOperationException)
                {
                    // It exited on its own just now.
                }
            }
        }

        if (!exited)
        {
            throw new TimeoutException($"The browser using {dataFolder} did not exit, so it was ended and its folder was left in place.");
        }
    }

    private static bool WaitForExitPumping(Task resourcesReleased)
    {
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        var frame = new System.Windows.Threading.DispatcherFrame();
        var exit = resourcesReleased.WaitAsync(ExitLimit);
        _ = exit.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
        if (!exit.IsCompleted)
        {
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }

        try
        {
            exit.GetAwaiter().GetResult();
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}
