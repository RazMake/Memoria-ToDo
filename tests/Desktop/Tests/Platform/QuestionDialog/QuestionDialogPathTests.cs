using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.TaskList.Tests;
using Memoria.ToDo.Testing;
using Serilog.Core;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests.QuestionDialog;

using QuestionDialog = Memoria.ToDo.Desktop.Platform.QuestionDialog;

/// <summary>
/// The question dialog Task View's close prompt and Configure's Forget key share: a modal window owned by and centered
/// on a window, with one button per answer, a default answer for Enter, and a cancel answer for Esc and the close button.
/// The prompt is modal, so <see cref="ClosePrompt"/> answers it while the call waits. Each test is one path a user takes.
/// </summary>
[UX("QuestionDialog")]
[Collection(ScreenTests.Collection)]
public class QuestionDialogPathTests : IDisposable
{
    private const string Title = "Pick one";
    private const string Text = "Which do you want?";

    private enum Pick { First, Second, Cancel }

    private static readonly IReadOnlyList<(string Label, Pick Answer)> Answers =
        [("First", Pick.First), ("Second", Pick.Second), ("Cancel", Pick.Cancel)];

    private readonly OpenWindows _windows = new();
    private readonly ClosePrompt _prompt = new(Title);
    private readonly Window _owner = new() { Title = "Owner", Width = 500, Height = 400, Left = 120, Top = 90 };

    public QuestionDialogPathTests() => _owner.Show();

    public void Dispose() => _windows.Dispose();

    private Pick Ask(Action<Window> answer, Pick defaultAnswer = Pick.First, Pick cancelAnswer = Pick.Cancel) =>
        _prompt.Run(() => QuestionDialog.Ask(_owner, Title, Text, Answers, defaultAnswer, cancelAnswer, Logger.None), answer);

    [WpfFact]
    public void Asking_shows_the_question_with_a_button_per_answer_and_clicking_an_answer_returns_it()
    {
        string? title = null;
        string[] buttons = [];
        string[] defaults = [];
        var showInTaskbar = true;
        var resizeMode = ResizeMode.CanResize;

        var picked = Ask(prompt =>
        {
            title = prompt.Title;
            showInTaskbar = prompt.ShowInTaskbar;
            resizeMode = prompt.ResizeMode;
            var shownButtons = OpenWindows.Descendants(prompt).OfType<Button>().ToList();
            buttons = [.. shownButtons.Select(b => (string)b.Content)];
            defaults = [.. shownButtons.Where(b => b.IsDefault).Select(b => (string)b.Content)];
            OpenWindows.Click(prompt, "Second");
        }, defaultAnswer: Pick.Second);

        Assert.Equal(Pick.Second, picked);
        Assert.Equal(Title, title);
        Assert.Contains(Text, _prompt.Texts);
        Assert.Equal(["First", "Second", "Cancel"], buttons);
        Assert.Equal(["Second"], defaults);
        Assert.Same(_owner, _prompt.Owner);
        Assert.True(_prompt.Centered);
        Assert.False(showInTaskbar);
        Assert.Equal(ResizeMode.NoResize, resizeMode);
        Assert.Equal(1, _prompt.Shown);
        Assert.True(_owner.IsVisible);
        Assert.DoesNotContain(_windows.Visible(), window => window.Title == Title);

        Assert.Equal(Pick.First, Ask(ClosePrompt.Click("First")));
        Assert.Equal(Pick.Cancel, Ask(ClosePrompt.Click("Cancel")));
        Assert.True(_owner.IsVisible);
    }

    [WpfFact]
    public void Enter_picks_the_default_answer_even_when_it_is_not_the_first_button_and_Cancel_when_that_is_the_default()
    {
        Assert.Equal(Pick.Second, Ask(ClosePrompt.PressEnter, defaultAnswer: Pick.Second));
        Assert.Equal(Pick.Cancel, Ask(ClosePrompt.PressEnter, defaultAnswer: Pick.Cancel));
    }

    [WpfFact]
    public void Esc_and_the_close_button_pick_the_cancel_answer_not_the_default()
    {
        Assert.Equal(Pick.Cancel, Ask(ClosePrompt.PressEscape, defaultAnswer: Pick.First, cancelAnswer: Pick.Cancel));
        Assert.Equal(Pick.Cancel, Ask(ClosePrompt.CloseIt, defaultAnswer: Pick.First, cancelAnswer: Pick.Cancel));
    }
}
