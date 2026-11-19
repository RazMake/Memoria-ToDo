using System.Windows;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>Asks the Contacts window's questions in the platform's question dialog, owned by the window.</summary>
internal sealed class WindowPrompts(Window owner) : IContactPrompts
{
    public LeaveAnswer Ask() => QuestionDialog.Ask(
        owner,
        "Contacts",
        "Save your changes to this contact?",
        [("Save", LeaveAnswer.Save), ("Discard", LeaveAnswer.Discard), ("Cancel", LeaveAnswer.Cancel)],
        LeaveAnswer.Save,
        LeaveAnswer.Cancel);

    public bool ConfirmDelete(string fullName) => QuestionDialog.Ask(
        owner,
        "Contacts",
        $"Delete {fullName}?",
        [("Delete", true), ("Cancel", false)],
        defaultAnswer: false,
        cancelAnswer: false);
}
