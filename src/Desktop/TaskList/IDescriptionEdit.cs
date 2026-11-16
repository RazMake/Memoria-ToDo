namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>The edit of a task's description, which has a draft the Preview tab shows and resizes the images of.</summary>
internal interface IDescriptionEdit : IFieldEdit
{
    /// <summary>The Markdown typed so far.</summary>
    string Draft { get; }

    /// <summary>Sets the width of the draft's image number <paramref name="image"/>, as a change the user can undo.</summary>
    void SetImageWidth(int image, int width);

    /// <summary>Gives the editor the keyboard focus, keeping its cursor and selection where they were.</summary>
    void FocusEditor();
}
