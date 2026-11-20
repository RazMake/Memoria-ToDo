using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.TaskList.Tests;
using Memoria.ToDo.Testing;
using Serilog.Core;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

/// <summary>
/// The question dialog Task View's close prompt and Configure's Forget key share: a modal window owned by and centered
/// on a window, with one button per answer, a default answer for Enter, and a cancel answer for Esc and the close button.
/// The prompt is modal, so <see cref="ClosePrompt"/> answers it while the call waits.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class QuestionDialogTests : IDisposable
{
    private const string Title = "Pick one";
    private const string Text = "Which do you want?";

    public enum Pick { First, Second, Cancel }

    private static readonly IReadOnlyList<(string Label, Pick Answer)> Answers =
        [("First", Pick.First), ("Second", Pick.Second), ("Cancel", Pick.Cancel)];

    private readonly OpenWindows _windows = new();
    private readonly ClosePrompt _prompt = new(Title);
    private readonly Window _owner = new() { Title = "Owner", Width = 500, Height = 400, Left = 120, Top = 90 };

    public QuestionDialogTests() => _owner.Show();

    public void Dispose() => _windows.Dispose();

    private Pick Ask(Action<Window> answer, Pick defaultAnswer = Pick.First, Pick cancelAnswer = Pick.Cancel) =>
        _prompt.Run(() => QuestionDialog.Ask(_owner, Title, Text, Answers, defaultAnswer, cancelAnswer, Logger.None), answer);

    [WpfFact]
    public void Shows_its_title_and_text_and_one_button_per_answer_in_order()
    {
        string[] buttons = [];
        string? title = null;

        Ask(prompt =>
        {
            title = prompt.Title;
            buttons = [.. OpenWindows.Descendants(prompt).OfType<Button>().Select(b => (string)b.Content)];
            OpenWindows.Click(prompt, "Cancel");
        });

        Assert.Equal(Title, title);
        Assert.Contains(Text, _prompt.Texts);
        Assert.Equal(["First", "Second", "Cancel"], buttons);
        Assert.Equal(1, _prompt.Shown);
    }

    [WpfFact]
    public void Is_owned_by_the_window_and_centered_on_it_and_stays_out_of_the_taskbar()
    {
        var showInTaskbar = true;
        var resizeMode = ResizeMode.CanResize;

        Ask(prompt =>
        {
            showInTaskbar = prompt.ShowInTaskbar;
            resizeMode = prompt.ResizeMode;
            OpenWindows.Click(prompt, "Cancel");
        });

        Assert.Same(_owner, _prompt.Owner);
        Assert.True(_prompt.Centered);
        Assert.False(showInTaskbar);
        Assert.Equal(ResizeMode.NoResize, resizeMode);
    }

    [WpfTheory]
    [InlineData("First", Pick.First)]
    [InlineData("Second", Pick.Second)]
    [InlineData("Cancel", Pick.Cancel)]
    public void Clicking_a_button_returns_its_answer(string label, Pick expected)
    {
        var picked = Ask(ClosePrompt.Click(label), defaultAnswer: Pick.Second, cancelAnswer: Pick.Cancel);

        Assert.Equal(expected, picked);
    }

    [WpfFact]
    public void Enter_picks_the_default_answer_even_when_it_is_not_the_first_button()
    {
        var picked = Ask(ClosePrompt.PressEnter, defaultAnswer: Pick.Second);

        Assert.Equal(Pick.Second, picked);
    }

    [WpfFact]
    public void Enter_picks_Cancel_when_Cancel_is_the_default_answer()
    {
        var picked = Ask(ClosePrompt.PressEnter, defaultAnswer: Pick.Cancel);

        Assert.Equal(Pick.Cancel, picked);
    }

    [WpfFact]
    public void Esc_picks_the_cancel_answer_not_the_default()
    {
        var picked = Ask(ClosePrompt.PressEscape, defaultAnswer: Pick.First, cancelAnswer: Pick.Cancel);

        Assert.Equal(Pick.Cancel, picked);
    }

    [WpfFact]
    public void The_close_button_picks_the_cancel_answer_not_the_default()
    {
        var picked = Ask(ClosePrompt.CloseIt, defaultAnswer: Pick.First, cancelAnswer: Pick.Cancel);

        Assert.Equal(Pick.Cancel, picked);
    }

    [WpfFact]
    public void Only_the_button_of_the_default_answer_is_the_default_button()
    {
        string[] defaults = [];

        Ask(prompt =>
        {
            defaults = [.. OpenWindows.Descendants(prompt).OfType<Button>().Where(b => b.IsDefault).Select(b => (string)b.Content)];
            OpenWindows.Click(prompt, "Cancel");
        }, defaultAnswer: Pick.Second);

        Assert.Equal(["Second"], defaults);
    }

    [WpfFact]
    public void Refuses_a_null_logger_before_showing_the_dialog()
    {
        Assert.Throws<ArgumentNullException>(() => _prompt.Run(
            () => QuestionDialog.Ask(_owner, Title, Text, Answers, Pick.First, Pick.Cancel, null!),
            ClosePrompt.Click("Cancel")));

        Assert.Equal(0, _prompt.Shown);
    }

    [WpfFact]
    public void Returns_after_one_answer_and_leaves_the_owner_open()
    {
        Ask(ClosePrompt.Click("Second"));

        Assert.True(_owner.IsVisible);
        Assert.DoesNotContain(_windows.Visible(), window => window.Title == Title);
    }
}
