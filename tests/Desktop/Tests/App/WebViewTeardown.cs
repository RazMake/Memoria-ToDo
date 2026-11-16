using System.Diagnostics;
using Microsoft.Web.WebView2.Wpf;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// How a test host ends a real <c>WpfDescriptionWebView</c>: the web views first, then the windows, then a bounded wait for the
/// browser. A window closed before its WebView2 control is disposed can leave the browser's own window, titled "about:blank",
/// on the user's screen. WebView2's <c>CoreWebView2Environment</c> has nothing to dispose; the control's disposal releases it.
/// </summary>
internal static class WebViewTeardown
{
    public static readonly TimeSpan ExitLimit = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Disposes each web view and its control, then calls <paramref name="closeWindows"/>, then waits for the browser to exit
    /// without blocking the UI thread, which WebView2's teardown needs. A browser still running after <see cref="ExitLimit"/>
    /// is killed with its process tree, and the call then throws so the caller leaves its folders in place.
    /// Each step runs even when an earlier one throws.
    /// </summary>
    public static void Run(IEnumerable<WpfDescriptionWebView> views, Action closeWindows, Process? browser, string dataFolder)
    {
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
                Attempt(failures, () => EndBrowser(browser, dataFolder));
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

    private static void EndBrowser(Process browser, string dataFolder)
    {
        var exited = false;
        try
        {
            exited = WaitForExitPumping(browser);
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

    private static bool WaitForExitPumping(Process browser)
    {
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        var frame = new System.Windows.Threading.DispatcherFrame();
        var exit = Task.Run(() => browser.WaitForExit(ExitLimit));
        _ = exit.ContinueWith(_ => dispatcher.BeginInvoke(() => frame.Continue = false), TaskScheduler.Default);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
        return exit.Result;
    }
}
