namespace Memoria.ToDo.Desktop.Configure;

/// <summary>A shortcut row's name and keys, as another row sees them when it checks that its pick is different.</summary>
internal interface IUsedShortcut
{
    /// <summary>What the shortcut does, such as Add new task.</summary>
    string Label { get; }

    /// <summary>The keys the row uses, or null when it has none.</summary>
    Shortcut? Active { get; }
}
