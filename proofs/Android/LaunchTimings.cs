using System.Globalization;
using Android.Content;

namespace Memoria.Proofs;

/// <summary>One Add screen launch. Times are milliseconds after <see cref="MeasuredFrom"/>; null when not seen.</summary>
public sealed record Launch(
    DateTime Utc,
    bool Cold,
    string MeasuredFrom,
    double? CreatedMs,
    double? FirstFrameMs,
    double? WindowFocusMs,
    double? KeyboardVisibleMs,
    double? KeyboardShownMs,
    bool ShowCalled);

/// <summary>Keeps each Add screen launch's timings in the app's files, for the report.</summary>
public static class LaunchTimings
{
    private static bool activityCreated;

    /// <summary>True only for the first activity this process creates: that launch was a cold start.</summary>
    public static bool ClaimColdStart()
    {
        bool cold = !activityCreated;
        activityCreated = true;
        return cold;
    }

    public static void Append(Context context, Launch launch)
    {
        string[] fields =
        [
            launch.Utc.ToString("O", CultureInfo.InvariantCulture),
            launch.Cold ? "cold" : "warm",
            launch.MeasuredFrom,
            Format(launch.CreatedMs),
            Format(launch.FirstFrameMs),
            Format(launch.WindowFocusMs),
            Format(launch.KeyboardVisibleMs),
            Format(launch.KeyboardShownMs),
            launch.ShowCalled ? "yes" : "no",
        ];
        File.AppendAllText(FilePath(context), string.Join('\t', fields) + "\n");
    }

    public static IReadOnlyList<Launch> ReadAll(Context context)
    {
        string path = FilePath(context);
        return !File.Exists(path)
            ? []
            : File.ReadLines(path)
                .Select(line => line.Split('\t'))
                .Where(f => f.Length == 9)
                .Select(f => new Launch(
                    DateTime.Parse(f[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    f[1] == "cold",
                    f[2],
                    Parse(f[3]),
                    Parse(f[4]),
                    Parse(f[5]),
                    Parse(f[6]),
                    Parse(f[7]),
                    f[8] == "yes"))
                .ToList();
    }

    private static string FilePath(Context context) =>
        Path.Combine(context.FilesDir!.AbsolutePath, "add-launches.tsv");

    private static string Format(double? ms) => ms?.ToString("F0", CultureInfo.InvariantCulture) ?? "";

    private static double? Parse(string text) =>
        text.Length == 0 ? null : double.Parse(text, CultureInfo.InvariantCulture);
}
