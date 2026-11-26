using System.Text;

namespace Memoria.ToDo.Desktop.Platform.Tests;

internal sealed record MarkedMoveSample(TimeSpan Entered, TimeSpan? Completed, nint? Result,
    Win32.Point Point, uint InjectionFlags);

internal enum HookLifecycle { Installed, NeverInstalled, Installing, RemovalFailed, Removed }

internal sealed record MarkedMoveReport(int Count, IReadOnlyList<MarkedMoveSample> Samples, Exception? Failure,
    HookLifecycle Lifecycle = HookLifecycle.Installed)
{
    public string Describe()
    {
        var text = new StringBuilder("low-level mouse hook: ");
        var listening = Lifecycle is not (HookLifecycle.NeverInstalled or HookLifecycle.Installing);
        text.Append(Count > 0 ? $"{Count} marked injected move(s) reached it"
            : listening ? "no marked injected move reached it" : "no move observed");
        if (Count > Samples.Count)
        {
            text.Append($", the first {Samples.Count} shown");
        }

        text.Append("; hook ").Append(Lifecycle switch
        {
            HookLifecycle.NeverInstalled => "never installed, so no input could be observed",
            HookLifecycle.Installing => "still installing, or its installation timed out",
            HookLifecycle.RemovalFailed => "installed, but its removal failed",
            HookLifecycle.Removed => "installed, then removed",
            _ => "installed",
        });
        text.Append("; times are milliseconds since the observer was created");
        for (var i = 0; i < Samples.Count; i++)
        {
            var move = Samples[i];
            text.Append($"\n    move {i + 1}: entered +{(long)move.Entered.TotalMilliseconds} ms, ");
            text.Append(move.Completed is { } done && move.Result is { } result
                ? $"next hook returned {result} at +{(long)done.TotalMilliseconds} ms"
                : "pending: the next hook had not returned a result");
            text.Append($", at ({move.Point.X}, {move.Point.Y}), injection flags 0x{move.InjectionFlags:X}");
        }

        if (Failure is not null)
        {
            text.Append($"; observer failure: {Failure.GetType().Name}: {Failure.Message}");
        }

        return text.ToString();
    }
}
