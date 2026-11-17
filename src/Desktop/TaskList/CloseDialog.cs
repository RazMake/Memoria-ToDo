using System.Windows;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The modal "Unsaved changes" prompt, owned by and centered on Task View. It takes the application's theme as every
/// window does. Save is the default button; Esc and the close button answer Cancel.
/// </summary>
internal sealed class CloseDialog(Window owner) : IClosePrompt
{
    public CloseAnswer Ask()
    {
        return QuestionDialog.Ask(
            owner, "Unsaved changes", "Save your changes to this task?",
            [("Save", CloseAnswer.Save), ("Discard", CloseAnswer.Discard), ("Cancel", CloseAnswer.Cancel)],
            CloseAnswer.Save, CloseAnswer.Cancel);
    }
}
