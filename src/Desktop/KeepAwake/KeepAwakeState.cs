namespace Memoria.ToDo.Desktop.KeepAwake;

/// <summary>
/// <paramref name="IsOn"/> is the setting. <paramref name="Paused"/> is true while the setting is on and the session is locked.
/// <paramref name="Failure"/> says why the setting can't be read or Windows refused the request; null when nothing failed.
/// </summary>
public sealed record KeepAwakeState(bool IsOn, bool Paused, string? Failure);
