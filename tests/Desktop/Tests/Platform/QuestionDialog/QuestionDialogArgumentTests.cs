using System.Windows;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests.QuestionDialog;

using QuestionDialog = Memoria.ToDo.Desktop.Platform.QuestionDialog;

/// <summary>The question dialog's argument checks, which fail before any dialog shows, so no window is needed.</summary>
[Unit]
public class QuestionDialogArgumentTests
{
    private enum Pick { First, Cancel }

    [WpfFact]
    public void Refuses_a_null_logger_before_showing_the_dialog()
    {
        IReadOnlyList<(string Label, Pick Answer)> answers = [("First", Pick.First), ("Cancel", Pick.Cancel)];

        Assert.Throws<ArgumentNullException>(() =>
            QuestionDialog.Ask(new Window(), "Pick one", "Which do you want?", answers, Pick.First, Pick.Cancel, null!));
    }
}
