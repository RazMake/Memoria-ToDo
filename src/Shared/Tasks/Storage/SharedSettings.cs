namespace Memoria.ToDo.Shared.Tasks;

/// <summary>A setting's value and the UTC time it last changed.</summary>
internal readonly record struct Setting<T>(T Value, DateTimeOffset Updated);

/// <summary>The three settings every device shares, as <c>settings.json</c> holds them.</summary>
internal sealed record SharedSettings(
    Setting<int> DeletionDelayDays,
    Setting<string?> AddNewTaskShortcut,
    Setting<string?> ManageTasksShortcut)
{
    public const int DefaultDeletionDelayDays = 7;
    public const int MinDeletionDelayDays = 1;
    public const int MaxDeletionDelayDays = 365;

    /// <summary>The untouched defaults. Their time is the epoch, so they never overwrite a setting someone changed.</summary>
    public static readonly SharedSettings Defaults = new(
        new Setting<int>(DefaultDeletionDelayDays, DateTimeOffset.UnixEpoch),
        new Setting<string?>(null, DateTimeOffset.UnixEpoch),
        new Setting<string?>(null, DateTimeOffset.UnixEpoch));
}
