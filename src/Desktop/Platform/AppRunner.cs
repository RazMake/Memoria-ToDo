using System.Windows;
using System.Windows.Threading;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>Runs the WPF application for a tray app with no main window.</summary>
public static class AppRunner
{
    /// <summary>
    /// Rejects a call from a non-STA thread, creates the WPF application (it ends only when told to) with its theme
    /// following Windows' light or dark setting, builds the lifetime and starts it on the UI thread, then stops it once when it ends the app or
    /// Windows ends the session. Shows an unhandled error in a message box and ends with a non-zero code.
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

    /// <summary>One run of the app: the application, the lifetime, and the once-only stop.</summary>
    private sealed class Session(Func<IAppLifetime> createLifetime)
    {
        private const int ErrorExitCode = 1;
        private static readonly TimeSpan StopWait = TimeSpan.FromSeconds(5);

        private Application? _application;
        private IAppLifetime? _started;
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

            try
            {
                var lifetime = createLifetime();
                _started = lifetime;
                lifetime.Start(() => application.Dispatcher.InvokeAsync(() => application.Shutdown()));
                application.Run();
            }
            catch (Exception error)
            {
                Fail(error);
            }
            finally
            {
                Stop();
            }

            return _exitCode;
        }

        private void Fail(Exception error)
        {
            _exitCode = ErrorExitCode;
            ErrorBox.Show(error);
            _application!.Shutdown();
        }

        // The process is ending: stop the lifetime on the UI thread (a tray icon would linger otherwise), then exit
        // here, because the runtime would otherwise end it with Windows' error report.
        private void FailOnOtherThread(Exception error)
        {
            ErrorBox.Show(error);
            try
            {
                _application!.Dispatcher.Invoke(Stop, DispatcherPriority.Send, CancellationToken.None, StopWait);
            }
            finally
            {
                Environment.Exit(ErrorExitCode);
            }
        }

        private void Stop() => Interlocked.Exchange(ref _started, null)?.Stop();
    }
}
