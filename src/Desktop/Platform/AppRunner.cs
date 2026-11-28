using System.Windows;
using Serilog;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>Runs the WPF application for a tray app with no main window.</summary>
public static class AppRunner
{
    /// <summary>
    /// Rejects a call from a non-STA thread, creates the WPF application (it ends only when told to) with its theme
    /// following Windows' light or dark setting, builds the lifetime and starts it on the UI thread, then stops it once
    /// when it ends the app or Windows ends the session, keeping the dispatcher running through cleanup.
    /// The first end request starts <paramref name="shutdownGrace"/> before dispatching to the UI. After it, the process
    /// is asked to exit with code 1, then forcibly ended three seconds later if still alive, even after Run returns.
    /// Logs startup, exit, and unhandled errors. Shutdown errors never show a dialog. Before shutdown, an error on the
    /// UI thread shows one message box and leaves the app running; an error during startup or on another thread shows
    /// a box and ends with a non-zero code. An unobserved task error is logged.
    /// </summary>
    /// <returns>The process's exit code.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="createLifetime"/> or <paramref name="logger"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="shutdownGrace"/> is not a positive finite timer duration with room for the backstop.</exception>
    public static int Run(Func<IAppLifetime> createLifetime, TimeSpan shutdownGrace, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(createLifetime);
        ArgumentNullException.ThrowIfNull(logger);
        ShutdownDeadline.EnsureUsable(shutdownGrace, nameof(shutdownGrace));
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException("The app must run on an STA thread.");
        }

        return new Session(createLifetime, shutdownGrace, logger).Run();
    }

    /// <summary>One run of the app: the application, the lifetime, and the once-only asynchronous shutdown.</summary>
    private sealed class Session(Func<IAppLifetime> createLifetime, TimeSpan shutdownGrace, ILogger logger) : IProcessExit
    {
        private const int ErrorExitCode = 1;
        private readonly IProcessExit _process = new ProcessExit();
        private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private ShutdownDeadline _deadline = null!;
        private Application? _application;
        private IAppLifetime? _started;
        private Task? _shutdown;
        private volatile int _exitCode;
        private bool _shutdownStarted;
        private bool _running;
        private bool _boxOpen;
        private int _ended = -1;

        public int Run()
        {
            _deadline = new ShutdownDeadline(shutdownGrace, new ShutdownTimer(), this, logger);
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
                lifetime.Start(EndApp);
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
                ReportFatal(error, "that ended the message pump");
                BeginShutdown();
            }

            LogEnd();
            return _exitCode;
        }

        // UI thread only. Once started, the app survives the error; otherwise the error ends it.
        private void OnUiThreadError(Exception error)
        {
            if (!_running || _deadline.HasBegun)
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
            try
            {
                ReportFatal(error, where);
            }
            finally
            {
                EndApp();
            }
        }

        private void EndApp()
        {
            _deadline.Begin();
            _application!.Dispatcher.InvokeAsync(BeginShutdown);
        }

        private void ReportFatal(Exception error, string where)
        {
            _exitCode = ErrorExitCode;
            logger.Fatal(error, "Unhandled error {Where}: {Reason}", where, error.Message);
            if (!_deadline.HasBegun)
            {
                ErrorBox.Show(error);
            }
        }

        // Keep the runtime's fatal-error thread here while the UI cleans up; the independent deadline bounds it.
        private void FailOnOtherThread(Exception error)
        {
            try
            {
                try
                {
                    ReportFatal(error, "on another thread");
                }
                finally
                {
                    EndApp();
                }
                if (!_application!.Dispatcher.CheckAccess())
                {
                    _stopped.Task.Wait();
                }
            }
            finally
            {
                ExitProcess(ErrorExitCode);
            }
        }

        private void ExitProcess(int exitCode)
        {
            _exitCode = exitCode;
            try
            {
                LogEnd();
            }
            finally
            {
                _process.RequestExit(exitCode);
            }
        }

        void IProcessExit.RequestExit(int exitCode) => ExitProcess(exitCode);

        void IProcessExit.Terminate(int exitCode) => _process.Terminate(exitCode);

        // A forced exit after Main returned corrects an earlier successful end entry.
        private void LogEnd()
        {
            var code = _exitCode;
            if (Interlocked.Exchange(ref _ended, code) != code)
            {
                logger.Information("Ended {App} with exit code {ExitCode}", AppName.Value, code);
            }
        }

        // UI thread only. Every request joins the one shutdown.
        private void BeginShutdown()
        {
            _deadline.Begin();
            _running = false;
            if (_shutdownStarted)
            {
                return;
            }
            _shutdownStarted = true;
            _shutdown = ShutDownAsync();
        }

        private async Task ShutDownAsync()
        {
            logger.Information("Stopping {App}", AppName.Value);
            try
            {
                if (_started is { } lifetime)
                {
                    await lifetime.StopAsync(CancellationToken.None);
                }
            }
            catch (Exception)
            {
                _exitCode = ErrorExitCode;
                logger.Fatal("Shutdown cleanup failed; exiting with code {ExitCode}", ErrorExitCode);
            }

            try
            {
                _application!.Shutdown();
            }
            catch (Exception error)
            {
                ReportFatal(error, "while WPF shuts down");
            }
            finally
            {
                _stopped.TrySetResult();
            }
        }
    }
}
