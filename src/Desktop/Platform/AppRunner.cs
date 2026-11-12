using System.Windows;
using System.Windows.Threading;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>Runs the WPF application for a tray app with no main window.</summary>
public static class AppRunner
{
    /// <summary>
    /// Rejects a call from a non-STA thread, creates the WPF application (it ends only when told to) with its theme
    /// following Windows' light or dark setting, builds the lifetime and starts it on the UI thread, then stops it once
    /// when it ends the app or Windows ends the session, keeping the dispatcher running until the asynchronous stop
    /// finishes. Shows an unhandled error in a message box and ends with a non-zero code.
    /// </summary>
    /// <returns>The process's exit code.</returns>
    public static int Run(Func<IAppLifetime> createLifetime)
    {
        if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            throw new InvalidOperationException("The app must run on an STA thread.");
        }

        return new Session(createLifetime).Run();
    }

    /// <summary>One run of the app: the application, the lifetime, and the once-only asynchronous shutdown.</summary>
    private sealed class Session(Func<IAppLifetime> createLifetime)
    {
        private const int ErrorExitCode = 1;
        private static readonly TimeSpan StopWait = TimeSpan.FromSeconds(5);

        private Application? _application;
        private IAppLifetime? _started;
        private Task? _shutdown;
        private int _exitCode;

        public int Run()
        {
            var application = _application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
#pragma warning disable WPF0001 // Application.ThemeMode is experimental; allowed only on this line.
            application.ThemeMode = ThemeMode.System;
#pragma warning restore WPF0001
            application.DispatcherUnhandledException += (_, e) =>
            {
                e.Handled = true;
                Fail(e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += (_, e) => FailOnOtherThread((Exception)e.ExceptionObject);
            application.SessionEnding += (_, _) => BeginShutdown();

            try
            {
                var lifetime = createLifetime();
                _started = lifetime;
                lifetime.Start(() => application.Dispatcher.InvokeAsync(BeginShutdown));
            }
            catch (Exception error)
            {
                Fail(error);
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
                ErrorBox.Show(error);
                BeginShutdown();
            }

            return _exitCode;
        }

        // Shutdown always starts from the pump, so it never runs before application.Run, and the dispatcher stays alive
        // until the lifetime's stop has finished.
        private void Fail(Exception error)
        {
            _exitCode = ErrorExitCode;
            ErrorBox.Show(error);
            _application!.Dispatcher.InvokeAsync(BeginShutdown);
        }

        // The process is ending: start stopping the lifetime on the UI thread (a tray icon would linger otherwise), then
        // exit here, because the runtime would otherwise end it with Windows' error report.
        private void FailOnOtherThread(Exception error)
        {
            ErrorBox.Show(error);
            try
            {
                _application!.Dispatcher.Invoke(BeginShutdown, DispatcherPriority.Send, CancellationToken.None, StopWait);
            }
            finally
            {
                Environment.Exit(ErrorExitCode);
            }
        }

        // UI thread only. Every request joins the one shutdown.
        private void BeginShutdown() => _shutdown ??= ShutDownAsync();

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
                ErrorBox.Show(error);
            }

            _application!.Shutdown();
        }
    }
}
