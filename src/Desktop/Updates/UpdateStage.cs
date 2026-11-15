namespace Memoria.ToDo.Desktop.Updates;

/// <summary>Which of the four things the app knows about updates.</summary>
public enum UpdateStage
{
    NotInstalled,
    NoUpdate,
    Available,
    Updating,
}
