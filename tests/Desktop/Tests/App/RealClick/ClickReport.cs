using System.Globalization;

namespace Memoria.ToDo.Desktop.App.Tests;

internal enum ClickStage { Press, Release, NativeMove, NativeDown, NativeUp, RoutedDown, RoutedUp, CaptureGained, CaptureLost }

internal sealed class ClickReport(string aim)
{
    private struct Stage
    {
        public long Count;
        public TimeSpan First;
        public TimeSpan Last;
        public string? Latest;
    }

    private readonly Stage[] _stages = new Stage[Enum.GetValues<ClickStage>().Length];
    private string? _nativeUnavailable;

    public string Aim { private get; set; } = aim;
    public string AfterPress { private get; set; } = "not recorded";
    public string AtTimeout { private get; set; } = "not recorded";
    public string PendingInput { private get; set; } = "not recorded";

    public void Record(ClickStage stage, TimeSpan elapsed, string details)
    {
        ref var summary = ref _stages[(int)stage];
        if (summary.Count++ == 0)
        {
            summary.First = elapsed;
        }

        summary.Last = elapsed;
        summary.Latest = details;
    }

    public void NativeMessagesUnavailable(string reason) => _nativeUnavailable = reason;

    public string Describe()
    {
        var lines = new List<string>
        {
            "Real click report:",
            $"aim: {Aim}",
            $"after press: {AfterPress}",
            $"at timeout: {AtTimeout}",
            $"pending input: {PendingInput}",
            "Native stages are this test thread's mouse-hook observations, not proof that a window handled them.",
        };
        foreach (var stage in Enum.GetValues<ClickStage>())
        {
            lines.Add(Describe(stage));
        }

        return string.Join('\n', lines);
    }

    private string Describe(ClickStage stage)
    {
        if (stage is >= ClickStage.NativeMove and <= ClickStage.NativeUp && _nativeUnavailable is not null)
        {
            return $"{stage}: unavailable ({_nativeUnavailable})";
        }

        var summary = _stages[(int)stage];
        return summary.Count == 0
            ? $"{stage}: none"
            : string.Create(CultureInfo.InvariantCulture,
                $"{stage}: count {summary.Count}, first {(long)summary.First.TotalMilliseconds} ms, last {(long)summary.Last.TotalMilliseconds} ms, latest {summary.Latest}");
    }
}
