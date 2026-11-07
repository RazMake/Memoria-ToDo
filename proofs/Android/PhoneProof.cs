using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using LibGit2Sharp;

namespace Memoria.Proofs;

/// <summary>Runs the proof on the phone, then writes a report and pushes it into the repository.</summary>
public static class PhoneProof
{
    public const string DefaultAddress = "git@github.com:RazMake/memoria-proof.git";

    public sealed record Result(bool Passed, string Report, string? PushedAs);

    public static string KeyCreationFile(Context context) =>
        Path.Combine(context.FilesDir!.AbsolutePath, "key-created.txt");

    public static Result Run(Context context, KeystoreRsaKey key, string addressText, Action<string> show)
    {
        var lines = new List<string>();
        void Log(string line)
        {
            lock (lines)
            {
                lines.Add(line);
            }

            show(line);
        }

        SshTransport.Log = Log;
        DateTime startedUtc = DateTime.UtcNow;
        string stamp = startedUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string work = Path.Combine(context.FilesDir!.AbsolutePath, "runs", stamp);
        var environment = new List<string>();
        string? failure = null;
        try
        {
            var address = SshAddress.Parse(addressText);
            Describe(context, key, environment);
            environment.ForEach(Log);
            IsolateGitConfiguration(context, Log);
            SshTransport.Use(key, PublishedHostKeys.Pins);
            GitScenario.Run(address.ToUrlForm(), address.ToScpForm(), work, Log);
            GitScenario.RefuseUnpinnedHostKey(address.ToUrlForm(), address.Host, key, Log);
            Log("All phone checks passed.");
        }
        catch (Exception ex)
        {
            failure = ex.ToString();
            Log($"FAILED: {ex.Message}");
        }

        string report;
        lock (lines)
        {
            report = Report(context, startedUtc, environment, lines, failure);
        }

        string? pushedAs = null;
        string firstClone = GitScenario.FirstClone(work);
        if (Directory.Exists(Path.Combine(firstClone, ".git")))
        {
            try
            {
                SshTransport.Use(key, PublishedHostKeys.Pins);
                string model = (Build.Model ?? "phone").Replace(' ', '-');
                pushedAs = GitScenario.CommitAndPush(firstClone, $"phone-reports/{stamp}-{model}.md", report, "Phone proof report").File;
                Log($"Pushed the report as {pushedAs}");
            }
            catch (Exception ex)
            {
                Log($"Pushing the report failed: {ex.Message}");
            }
        }

        return new Result(failure is null, report, pushedAs);
    }

    private static void Describe(Context context, KeystoreRsaKey key, List<string> to)
    {
        to.Add($"Device: {Build.Manufacturer} {Build.Model}, Android {Build.VERSION.Release} (API {(int)Build.VERSION.SdkInt}), ABIs {string.Join(", ", Build.SupportedAbis ?? [])}");
        to.Add($"Runtime: {RuntimeInformation.FrameworkDescription}, {RuntimeInformation.ProcessArchitecture}, {RuntimeInformation.RuntimeIdentifier}");
        to.Add($"Runtime libraries mapped: {string.Join(", ", MappedLibraries("libcoreclr", "libmonosgen", "libmonodroid", "libnet-android", "libgit2"))}");
        to.Add($"HOME={System.Environment.GetEnvironmentVariable("HOME")}, TMPDIR={System.Environment.GetEnvironmentVariable("TMPDIR")}");

        var app = context.PackageManager!.GetApplicationInfo(context.PackageName!, 0);
        long apkBytes = new FileInfo(app.SourceDir!).Length;
        long splitBytes = (app.SplitSourceDirs ?? []).Sum(path => new FileInfo(path).Length);
        to.Add($"Installed APK: {apkBytes / 1024.0 / 1024.0:F2} MB ({apkBytes:N0} bytes), splits {splitBytes:N0} bytes, native library folder {app.NativeLibraryDir} holds [{string.Join(", ", NativeLibraryFiles(app.NativeLibraryDir))}]");

        string created = File.Exists(KeyCreationFile(context)) ? File.ReadAllText(KeyCreationFile(context)) : "unknown";
        to.Add($"Key: RSA {key.KeySize}, held in {key.SecurityLevel}, private key readable by the app: {(key.PrivateKeyReadable ? "YES" : "no")}, generated in {created} ms, SHA256:{key.Fingerprint}");
        to.Add($"Key algorithms offered: {string.Join(", ", key.HostKeyAlgorithms.Select(a => a.Name))}");

        // The first LibGit2Sharp call loads libgit2. The app never sets GlobalSettings.NativeLibraryPath.
        long started = Stopwatch.GetTimestamp();
        var version = GlobalSettings.Version;
        to.Add($"LibGit2Sharp {version.InformationalVersion}, libgit2 {version.LibGit2CommitSha}, features: {version.Features}, loaded in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F0} ms");
        to.Add($"Libraries mapped after loading libgit2: {string.Join(", ", MappedLibraries("libgit2"))}");
    }

    // The app reads no git configuration outside its own folder.
    private static void IsolateGitConfiguration(Context context, Action<string> log)
    {
        string folder = Path.Combine(context.FilesDir!.AbsolutePath, "git-config");
        Directory.CreateDirectory(folder);
        foreach (var level in new[] { ConfigurationLevel.System, ConfigurationLevel.Xdg, ConfigurationLevel.Global })
        {
            GlobalSettings.SetConfigSearchPaths(level, folder);
        }

        log($"Git configuration search paths set to {folder}");
    }

    private static IEnumerable<string> MappedLibraries(params string[] names)
    {
        try
        {
            return File.ReadLines("/proc/self/maps")
                .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .Where(fields => fields.Length >= 6)
                .Select(fields => fields[^1])
                .Where(path => names.Any(name => path.Contains(name, StringComparison.Ordinal)))
                .Distinct()
                .DefaultIfEmpty("none by name (libraries may be mapped straight from the APK)")
                .ToList();
        }
        catch (Exception ex)
        {
            return [$"unreadable: {ex.Message}"];
        }
    }

    private static IEnumerable<string> NativeLibraryFiles(string? folder) =>
        folder is not null && Directory.Exists(folder)
            ? Directory.GetFiles(folder).Select(f => $"{Path.GetFileName(f)} {new FileInfo(f).Length:N0}")
            : ["none"];

    private static string Report(Context context, DateTime startedUtc, List<string> environment, List<string> log, string? failure)
    {
        var report = new StringBuilder();
        report.AppendLine(CultureInfo.InvariantCulture, $"# Phone proof, {startedUtc:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine();
        report.AppendLine(failure is null ? "**Result:** passed." : "**Result:** FAILED. The failure is at the end.");
        report.AppendLine();
        report.AppendLine("## Environment");
        report.AppendLine();
        environment.ForEach(line => report.AppendLine($"- {line}"));
        report.AppendLine();
        AppendLaunches(report, LaunchTimings.ReadAll(context));
        AppendSystemStartRecords(report, context);
        report.AppendLine("## Log");
        report.AppendLine();
        report.AppendLine("```text");
        log.ForEach(line => report.AppendLine(line));
        report.AppendLine("```");
        if (failure is not null)
        {
            report.AppendLine();
            report.AppendLine("## Failure");
            report.AppendLine();
            report.AppendLine("```text");
            report.AppendLine(failure);
            report.AppendLine("```");
        }

        return report.ToString();
    }

    private static void AppendLaunches(StringBuilder report, IReadOnlyList<Launch> launches)
    {
        report.AppendLine("## Add screen launches");
        report.AppendLine();
        if (launches.Count == 0)
        {
            report.AppendLine("No launches were recorded.");
            report.AppendLine();
            return;
        }

        report.AppendLine("Milliseconds after the start the second column names. \"Keyboard visible\" is the first layout with the keyboard's space; \"keyboard shown\" is the end of its animation.");
        report.AppendLine();
        report.AppendLine("| UTC | Start | Measured from | onCreate | First frame | Window focus | Keyboard visible | Keyboard shown | show() called |");
        report.AppendLine("| --- | --- | --- | ---: | ---: | ---: | ---: | ---: | --- |");
        foreach (var l in launches)
        {
            report.AppendLine(CultureInfo.InvariantCulture, $"| {l.Utc:HH:mm:ss} | {(l.Cold ? "cold" : "warm")} | {l.MeasuredFrom} | {F(l.CreatedMs)} | {F(l.FirstFrameMs)} | {F(l.WindowFocusMs)} | {F(l.KeyboardVisibleMs)} | {F(l.KeyboardShownMs)} | {(l.ShowCalled ? "yes" : "no")} |");
        }

        report.AppendLine();
        foreach (bool cold in new[] { true, false })
        {
            var group = launches.Where(l => l.Cold == cold).ToList();
            report.AppendLine(CultureInfo.InvariantCulture, $"- {(cold ? "Cold" : "Warm")} starts: {group.Count}; median first frame {F(Median(group.Select(l => l.FirstFrameMs)))} ms, median keyboard visible {F(Median(group.Select(l => l.KeyboardVisibleMs)))} ms, median keyboard shown {F(Median(group.Select(l => l.KeyboardShownMs)))} ms.");
        }

        report.AppendLine();
    }

    // Android 15 and later record each start with the system's own timestamps, from the launcher's tap on.
    private static void AppendSystemStartRecords(StringBuilder report, Context context)
    {
        report.AppendLine("## System start records");
        report.AppendLine();
        if (!OperatingSystem.IsAndroidVersionAtLeast(35))
        {
            report.AppendLine("Not available: they need Android 15 (API 35).");
            report.AppendLine();
            return;
        }

        var names = new Dictionary<int, string>
        {
            [(int)ApplicationStartInfoStartTimestamp.Launch] = "launch",
            [(int)ApplicationStartInfoStartTimestamp.Fork] = "fork",
            [(int)ApplicationStartInfoStartTimestamp.BindApplication] = "bind application",
            [(int)ApplicationStartInfoStartTimestamp.ApplicationOncreate] = "Application.onCreate",
            [(int)ApplicationStartInfoStartTimestamp.FirstFrame] = "first frame",
            [(int)ApplicationStartInfoStartTimestamp.FullyDrawn] = "fully drawn",
            [(int)ApplicationStartInfoStartTimestamp.InitialRenderthreadFrame] = "first render thread frame",
            [(int)ApplicationStartInfoStartTimestamp.SurfaceFlingerCompositionComplete] = "first composition",
        };
        long nowNs = SystemClock.ElapsedRealtimeNanos();
        DateTime nowUtc = DateTime.UtcNow;
        var manager = (Android.App.ActivityManager)context.GetSystemService(Context.ActivityService)!;
        foreach (var info in manager.GetHistoricalProcessStartReasons(30))
        {
            var stamps = info.StartupTimestamps.ToDictionary(p => (int)p.Key, p => (long)p.Value);
            long launch = stamps.TryGetValue((int)ApplicationStartInfoStartTimestamp.Launch, out long l) ? l : stamps.Values.DefaultIfEmpty(0).Min();
            DateTime when = nowUtc - TimeSpan.FromTicks((nowNs - launch) / 100);
            string times = string.Join(", ", stamps.OrderBy(p => p.Value).Select(p =>
                $"{names.GetValueOrDefault(p.Key, p.Key.ToString(CultureInfo.InvariantCulture))} {(p.Value - launch) / 1e6:F0}"));
            report.AppendLine(CultureInfo.InvariantCulture, $"- {when:HH:mm:ss} UTC, start type {info.StartType}, reason {info.Reason}, state {info.StartupState}: {times} ms");
        }

        report.AppendLine();
    }

    private static double? Median(IEnumerable<double?> values)
    {
        var sorted = values.OfType<double>().Order().ToList();
        return sorted.Count == 0
            ? null
            : sorted.Count % 2 == 1 ? sorted[sorted.Count / 2] : (sorted[(sorted.Count / 2) - 1] + sorted[sorted.Count / 2]) / 2;
    }

    private static string F(double? ms) => ms?.ToString("F0", CultureInfo.InvariantCulture) ?? "-";
}
