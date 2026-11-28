using System.Text;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Fakes;

namespace Memoria.ToDo.Desktop.KeepAwake.Tests;

/// <summary>
/// Keep awake built on the shared fakes of its two ports and the shared file system fake, as its tests use it: an unlocked
/// session, no setting file, and a request Windows accepts. Shared test setup, not a fake of a port.
/// </summary>
internal sealed class KeepAwakeHarness
{
    public const string DeviceFolder = @"C:\device";

    public KeepAwakeHarness()
    {
        App = Create();
    }

    public static string SettingFile { get; } = Path.Combine(DeviceFolder, "prevent-screen-saver.txt");

    public FakeDisplayRequest Display { get; } = new();

    public FakeSessionLock Session { get; } = new();

    public FakeFileSystem Files { get; } = new();

    /// <summary>The entries Keep awake logged, through the logger it was given.</summary>
    public InMemoryLogSink Log { get; } = new();

    public IKeepAwake App { get; }

    /// <summary>Calls <see cref="KeepAwake.Create"/> on these fakes, for a test that changes an argument.</summary>
    public IKeepAwake Create(string deviceFolder = DeviceFolder) =>
        KeepAwake.Create(Display, Session, Files, deviceFolder, Log.CreateLogger());

    /// <summary>Writes the setting file, as an earlier run would have.</summary>
    public void WriteSetting(string text) => Files.WriteFile(SettingFile, Encoding.UTF8.GetBytes(text));

    /// <summary>The text of the setting file, without whitespace around it.</summary>
    public string SavedSetting() => Encoding.UTF8.GetString(Files.ReadFile(SettingFile)).Trim();

    /// <summary>Starts with the setting on and the session unlocked, so the request is held.</summary>
    public void StartOn()
    {
        WriteSetting("on");
        App.Start();
    }

    /// <summary>Counts the Changed events <see cref="App"/> raises from now on.</summary>
    public Counter CountChanges()
    {
        var counter = new Counter();
        App.Changed += (_, _) => counter.Count++;
        return counter;
    }

    public sealed class Counter
    {
        public int Count { get; set; }
    }
}
