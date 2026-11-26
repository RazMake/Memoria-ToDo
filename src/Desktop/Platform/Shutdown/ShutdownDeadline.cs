using System.Diagnostics;
using Serilog;

namespace Memoria.ToDo.Desktop.Platform;

internal sealed class ShutdownDeadline
{
    private const int ErrorExitCode = 1;
    private static readonly TimeSpan Backstop = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan LongestDue = TimeSpan.FromMilliseconds(int.MaxValue);
    private readonly TimeSpan grace;
    private readonly IShutdownTimer timer;
    private readonly IProcessExit process;
    private readonly ILogger logger;
    private int begun;

    public ShutdownDeadline(TimeSpan grace, IShutdownTimer timer, IProcessExit process, ILogger logger)
    {
        EnsureUsable(grace, nameof(grace));
        ArgumentNullException.ThrowIfNull(timer);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(logger);
        this.grace = grace;
        this.timer = timer;
        this.process = process;
        this.logger = logger;
    }

    public bool HasBegun => Volatile.Read(ref begun) != 0;

    public static void EnsureUsable(TimeSpan grace, string paramName)
    {
        if (grace <= TimeSpan.Zero || grace > LongestDue - Backstop)
        {
            throw new ArgumentOutOfRangeException(paramName, grace, "The shutdown grace period must be positive and leave room for the termination backstop.");
        }
    }

    public void Begin()
    {
        if (Interlocked.Exchange(ref begun, 1) != 0)
        {
            return;
        }

        // Arm both before any reporting or exit handler can block. Keep them armed through process exit.
        timer.Schedule(grace + Backstop, Terminate);
        timer.Schedule(grace, RequestExit);
    }

    private void RequestExit()
    {
        TryLog(() => logger.Fatal("Shutdown did not finish within {Grace}; exiting with code {ExitCode}", grace, ErrorExitCode));
        try
        {
            process.RequestExit(ErrorExitCode);
        }
        catch (Exception error)
        {
            TryLog(() => logger.Fatal(error, "Requesting process exit failed: {Reason}", error.Message));
        }
    }

    private void Terminate()
    {
        try
        {
            process.Terminate(ErrorExitCode);
        }
        catch (Exception error)
        {
            TryLog(() => logger.Fatal(error, "Terminating the process failed: {Reason}", error.Message));
        }
    }

    private static void TryLog(Action write)
    {
        try
        {
            write();
        }
        catch (Exception error)
        {
            Trace.TraceError("Shutdown diagnostics failed: {0}", error);
        }
    }
}
