using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using Serilog;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>Runs the WPF application for a tray app with no main window.</summary>
public static class AppRunner
{
    /// <summary>
    /// Rejects a call from a non-STA thread, creates the WPF application (it ends only when told to) with its theme
    /// following Windows' light or dark setting, builds the lifetime and starts it on the UI thread, then stops it once
    /// when it ends the app or Windows ends the session, keeping the dispatcher running until the asynchronous stop
    /// finishes. Logs the start with the app's version and the end with the exit code. Logs every unhandled error and
    /// shows it in a message box: once the app has started, an error on the UI thread leaves the app running (the box
    /// says so, and one box shows at a time); an error while it starts or stops, or on another thread, ends it with a
    /// non-zero code; an error in a task nobody waited for is only logged.
    /// </summary>
    /// <returns>The process's exit code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is null.</exception>
    public static int Run(Func<IAppLifetime> createLifetime, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException("The app must run on an STA thread.");
        }

        return new Session(createLifetime, logger).Run();
    }

    /// <summary>One run of the app: the application, the lifetime, and the once-only asynchronous shutdown.</summary>
    private sealed class Session(Func<IAppLifetime> createLifetime, ILogger logger)
    {
        private const int ErrorExitCode = 1;
        private static readonly TimeSpan StopWait = TimeSpan.FromSeconds(5);

        private Application? _application;
        private IAppLifetime? _started;
        private Task? _shutdown;
        private int _exitCode;
        private bool _running;
        private bool _boxOpen;
        private int _ended;

        public int Run()
        {
            logger.Information("Starting {App} {Version}", AppName.Value, AppVersion.Value);
            var application = _application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
#pragma warning disable WPF0001 // Application.ThemeMode is experimental; allowed only on this line.
            application.ThemeMode = ThemeMode.System;
#pragma warning restore WPF0001
            ButtonCursor.Register();
            application.DispatcherUnhandledException += (_, e) =>
            {
                e.Handled = true;
                OnUiThreadError(e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += (_, e) => FailOnOtherThread((Exception)e.ExceptionObject);
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                e.SetObserved();
                logger.Error(e.Exception, "A task failed and nobody waited for it: {Reason}", e.Exception.Message);
            };
            application.SessionEnding += (_, _) => BeginShutdown();

            try
            {
                var lifetime = createLifetime();
                _started = lifetime;
                lifetime.Start(() => application.Dispatcher.InvokeAsync(BeginShutdown));
                _running = true;
            }
            catch (Exception error)
            {
                Fail(error, "while the app starts or stops");
            }

            try
            {
                application.Run();
            }
            catch (Exception error)
            {
                // The pump is gone, so nothing can await the stop: report, and run the stop's synchronous part
                // (icon and shortcut release) on this still-owning UI thread.
                _exitCode = ErrorExitCode;
                logger.Fatal(error, "Unhandled error {Where}: {Reason}", "that ended the message pump", error.Message);
                ErrorBox.Show(error);
                BeginShutdown();
            }

            LogEnd();
            return _exitCode;
        }

        // UI thread only. Once started, the app survives the error; otherwise the error ends it.
        private void OnUiThreadError(Exception error)
        {
            if (!_running)
            {
                Fail(error, "while the app starts or stops");
                return;
            }

            logger.Error(error, "Unhandled error {Where}, the app keeps running: {Reason}", "on the UI thread", error.Message);
            if (_boxOpen)
            {
                return;
            }

            _boxOpen = true;
            try
            {
                ErrorBox.ShowAndKeepRunning(error);
            }
            finally
            {
                _boxOpen = false;
            }
        }

        // Shutdown always starts from the pump, so it never runs before application.Run, and the dispatcher stays alive
        // until the lifetime's stop has finished.
        private void Fail(Exception error, string where)
        {
            _exitCode = ErrorExitCode;
            logger.Fatal(error, "Unhandled error {Where}: {Reason}", where, error.Message);
            ErrorBox.Show(error);
            _application!.Dispatcher.InvokeAsync(BeginShutdown);
        }

        // The process is ending: start stopping the lifetime on the UI thread (a tray icon would linger otherwise), then
        // exit here, because the runtime would otherwise end it with Windows' error report.
        private void FailOnOtherThread(Exception error)
        {
            _exitCode = ErrorExitCode;
            logger.Fatal(error, "Unhandled error {Where}: {Reason}", "on another thread", error.Message);
            ErrorBox.Show(error);
            try
            {
                // Wait for the asynchronous stop too, so exiting doesn't cut it off. The invoke and the wait share StopWait.
                var started = Stopwatch.StartNew();
                var shutdown = _application!.Dispatcher.Invoke(
                    () =>
                    {
                        BeginShutdown();
                        return _shutdown!;
                    },
                    DispatcherPriority.Send,
                    CancellationToken.None,
                    StopWait);
                shutdown.Wait(TimeSpan.FromTicks(Math.Max(0, (StopWait - started.Elapsed).Ticks)));
            }
            finally
            {
                LogEnd();
                Environment.Exit(ErrorExitCode);
            }
        }

        // The end is logged once, by whichever of the normal return and the exit after an error on another thread comes first.
        private void LogEnd()
        {
            if (Interlocked.Exchange(ref _ended, 1) == 0)
            {
                logger.Information("Ended {App} with exit code {ExitCode}", AppName.Value, _exitCode);
            }
        }

        // UI thread only. Every request joins the one shutdown.
        private void BeginShutdown()
        {
            _running = false;
            _shutdown ??= ShutDownAsync();
        }

        private async Task ShutDownAsync()
        {
            try
            {
                if (_started is { } lifetime)
                {
                    await lifetime.StopAsync(CancellationToken.None);
                }
            }
            catch (Exception error)
            {
                _exitCode = ErrorExitCode;
                logger.Fatal(error, "Unhandled error {Where}: {Reason}", "while the app stops", error.Message);
                ErrorBox.Show(error);
            }

            _application!.Shutdown();
        }
    }
}
