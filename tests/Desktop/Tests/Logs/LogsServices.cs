using System.Globalization;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Fakes;
using Serilog;
using Serilog.Events;

namespace Memoria.ToDo.Desktop.Logs.Tests;

/// <summary>
/// The ports a Logs test builds on: a <see cref="FakeLogSource"/>, a clock that reads 2026-03-14 14:30:45 in the PC's local time,
/// and a sink to read what Logs logged. The PC's local zone is fixed at UTC+2 for every test, so local times mean the same on any
/// PC; entries are handed over in UTC, as the real source does. Shared test setup, not a fake of a port.
/// </summary>
internal sealed class LogsServices
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(2);

    public LogsServices()
    {
        Time = new FakeTimeProvider(new DateTimeOffset(2026, 3, 14, 12, 30, 45, TimeSpan.Zero));
        Time.UseLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Test zone", Offset, "Test zone", "Test zone"));
        Logger = Sink.CreateLogger();
    }

    public FakeLogSource Source { get; } = new();

    public FakeTimeProvider Time { get; }

    /// <summary>What the window logged.</summary>
    public InMemoryLogSink Sink { get; } = new();

    public ILogger Logger { get; }

    public FakeTheme Theme { get; } = new();

    public CloseWindowKeys CloseWindowKeys { get; } = new();

    /// <summary>An entry at a local time such as "2026-03-14 14:00" or "2026-03-14 14:00:00.123", Information from Sync unless told otherwise.</summary>
    public static LogEntry Entry(
        string localTime, LogEventLevel level = LogEventLevel.Information, string component = "Sync", string message = "A message", string? error = null) =>
        new(Local(localTime), level, component, message, error);

    /// <summary>The instant of a local time such as "2026-03-14 14:00" in the test zone, in UTC.</summary>
    public static DateTimeOffset Local(string localTime) =>
        new DateTimeOffset(
            DateTime.ParseExact(
                localTime.Length switch { 16 => localTime + ":00.000", 19 => localTime + ".000", _ => localTime },
                "yyyy-MM-dd HH:mm:ss.fff",
                CultureInfo.InvariantCulture),
            Offset).ToUniversalTime();

    /// <summary>Puts the entries in the source, so the next read finds them.</summary>
    public LogsServices With(params LogEntry[] entries)
    {
        Source.Entries.AddRange(entries);
        return this;
    }

    public LogsModel Model() => new(Source, Time, Logger);

    /// <summary>A model that has read the source once.</summary>
    public async Task<LogsModel> LoadedAsync(CancellationToken cancellationToken)
    {
        var model = Model();
        await model.LoadAsync(cancellationToken);
        return model;
    }

    public ILogsWindow CreateWindow() => LogsWindow.Create(Source, Theme, CloseWindowKeys, Time, Logger);
}
