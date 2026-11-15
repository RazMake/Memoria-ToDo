namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>How a user picks a tag in the "All tags" list.</summary>
public enum AllTagsInput
{
    /// <summary>Opens the list and left-clicks the tag.</summary>
    Mouse,

    /// <summary>Moves the highlight to the tag, then presses Enter.</summary>
    Enter,

    /// <summary>Moves the highlight to the tag, then presses F4, which closes the list and commits the highlight.</summary>
    F4,

    /// <summary>Moves the highlight to the tag, then presses Alt+Down, which closes the list and commits the highlight.</summary>
    AltDown,

    /// <summary>Moves the highlight to the tag, then presses Alt+Up, which closes the list and commits the highlight.</summary>
    AltUp,
}
