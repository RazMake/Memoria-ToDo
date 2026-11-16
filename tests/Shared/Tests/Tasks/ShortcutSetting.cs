using Xunit;

namespace Memoria.ToDo.Shared.Tasks.Tests;

/// <summary>A shortcut setting as it sits in settings.json, written as literal JSON.</summary>
public sealed record ShortcutEntry(string Name, string? Value, string Time);

/// <summary>
/// An optional shortcut setting that follows the same rules as every other one after the manage shortcut: kept, merged by its
/// own time, read as null when missing, written on every write, and stored as typed. Tests that state such a rule run once
/// per setting, so a new one is covered by adding it to <see cref="All"/>.
/// </summary>
public sealed class ShortcutSetting
{
    public const string DefaultTime = "2026-01-01T00:00:00.000Z";

    public static readonly ShortcutSetting CloseWindow = new("closeWindowShortcut",
        settings => settings.CloseWindowShortcut, (settings, shortcut) => settings.SetCloseWindowShortcut(shortcut));

    public static readonly ShortcutSetting Save = new("saveShortcut",
        settings => settings.SaveShortcut, (settings, shortcut) => settings.SetSaveShortcut(shortcut));

    public static readonly ShortcutSetting Configure = new("configureShortcut",
        settings => settings.ConfigureShortcut, (settings, shortcut) => settings.SetConfigureShortcut(shortcut));

    /// <summary>The settings in file order.</summary>
    public static readonly IReadOnlyList<ShortcutSetting> All = [CloseWindow, Save, Configure];

    /// <summary>Every setting as a file that has never changed it writes it.</summary>
    public static IEnumerable<ShortcutEntry> AllDefault => All.Select(s => s.Entry(null));

    /// <summary>Theory data: each setting.</summary>
    public static TheoryData<ShortcutSetting> Each() => new(All);

    /// <summary>Theory data: each setting with each value that is not a valid setting in a file.</summary>
    public static TheoryData<ShortcutSetting, string> EachInvalidValue()
    {
        var data = new TheoryData<ShortcutSetting, string>();
        foreach (var setting in All)
            foreach (var value in (string[])
            [
                """{ "value": 5, "updated": "2026-01-01T00:00:00.000Z" }""",
                """{ "value": "x", "updated": "yesterday" }""",
                """{ "value": "x" }""",
                """7""",
            ])
                data.Add(setting, value);
        return data;
    }

    private readonly Func<ISharedTaskSettings, string?> get;
    private readonly Action<ISharedTaskSettings, string?> set;

    private ShortcutSetting(string name, Func<ISharedTaskSettings, string?> get, Action<ISharedTaskSettings, string?> set)
    {
        Name = name;
        this.get = get;
        this.set = set;
    }

    /// <summary>The setting's property name in settings.json.</summary>
    public string Name { get; }

    public string? Get(ISharedTaskSettings settings) => get(settings);

    public void Set(ISharedTaskSettings settings, string? shortcut) => set(settings, shortcut);

    public ShortcutEntry Entry(string? value, string time = DefaultTime) => new(Name, value, time);

    /// <summary>This setting with the given value and time, and every other setting at its default.</summary>
    public IEnumerable<ShortcutEntry> Entries(string? value, string time = DefaultTime) =>
        All.Select(s => s == this ? s.Entry(value, time) : s.Entry(null));

    /// <summary>Every other setting, and none for this one, as a file written before this setting existed.</summary>
    public IEnumerable<ShortcutEntry> EntriesWithout() => All.Where(s => s != this).Select(s => s.Entry(null));

    /// <summary>A settings file whose entry for this setting is the literal JSON <paramref name="json"/>, valid or not.</summary>
    public string FileWith(string json) =>
        $$"""{ "version": 1, "deletionDelayDays": { "value": 7, "updated": "2026-01-01T00:00:00.000Z" }, "addNewTaskShortcut": { "value": null, "updated": "2026-01-01T00:00:00.000Z" }, "manageTasksShortcut": { "value": null, "updated": "2026-01-01T00:00:00.000Z" }, "{{Name}}": {{json}} }""";

    public override string ToString() => Name;
}
