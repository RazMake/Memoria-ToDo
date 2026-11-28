namespace Memoria.ToDo.Desktop.Updates;

/// <summary>The running version, the newer one when there is one, and why the last update or setting read failed, if it did.</summary>
public sealed record UpdateState(UpdateStage Stage, string CurrentVersion, string? NewVersion, string? Failure);
