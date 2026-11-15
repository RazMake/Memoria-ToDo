using Memoria.ToDo.Desktop.Fakes;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// One shortcut setting wired to a <see cref="FakeShortcutAction"/> and an in-memory stored text, for unit tests.
/// Shared test setup, not a fake of a port. Set <see cref="Stored"/>, <see cref="ReadFailure"/>, or
/// <see cref="SaveFailure"/> at any time; <see cref="Saved"/> lists the texts the setting saved.
/// </summary>
internal sealed class TestShortcutSetting
{
    private TestShortcutSetting(string label, Shortcut defaultShortcut)
    {
        Setting = new ShortcutSetting(label, defaultShortcut, ReadStored, Save);
    }

    public ShortcutSetting Setting { get; }

    public FakeShortcutAction Action { get; } = new();

    /// <summary>The stored shortcut text; null when nothing is stored.</summary>
    public string? Stored { get; set; }

    /// <summary>When set, reading the stored text throws it.</summary>
    public Exception? ReadFailure { get; set; }

    /// <summary>When set, saving throws it and the text isn't stored.</summary>
    public Exception? SaveFailure { get; set; }

    /// <summary>The texts saved, in order.</summary>
    public List<string?> Saved { get; } = [];

    public static TestShortcutSetting AddNewTask() => new("Add new task", TestShortcut.AddNewTaskDefault);

    public static TestShortcutSetting ManageTasks() => new("Task View", TestShortcut.ManageTasksDefault);

    /// <summary>Connects the action, then forgets the registration the connection made.</summary>
    public TestShortcutSetting Connected()
    {
        Setting.Connect(Action);
        Action.ClearCalls();
        return this;
    }

    private string? ReadStored() => ReadFailure is null ? Stored : throw ReadFailure;

    private void Save(string? text)
    {
        if (SaveFailure is not null)
        {
            throw SaveFailure;
        }

        Saved.Add(text);
        Stored = text;
    }
}
