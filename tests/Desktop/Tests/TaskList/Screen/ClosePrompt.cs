using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Drives the prompt that asks what to do with a changed edit when Task View closes, the way a user does. The prompt is
/// modal, so a call that closes the window doesn't return until the prompt is answered: <see cref="Run{T}"/> queues the
/// answer first, to run once the prompt shows, then makes the call. The prompt is found as the showing window with its
/// title, "Unsaved changes" unless given another, and its buttons by their text. Shared test setup.
/// </summary>
internal sealed class ClosePrompt(string title = ClosePrompt.Title)
{
    public const string Title = "Unsaved changes";

    private readonly OpenWindows windows = new();
    private int shown;

    /// <summary>How many times the prompt showed during the calls <see cref="Run{T}"/> made.</summary>
    public int Shown => shown;

    /// <summary>The window that owns the prompt, as it was when the prompt showed; null while it never showed.</summary>
    public Window? Owner { get; private set; }

    /// <summary>The texts the prompt displayed when it showed, buttons included.</summary>
    public IReadOnlyList<string> Texts { get; private set; } = [];

    /// <summary>Whether the prompt was centered on its owner when it showed.</summary>
    public bool Centered { get; private set; }

    /// <summary>
    /// Calls <paramref name="close"/>, answers the prompt it shows with <paramref name="answer"/>, and returns what
    /// <paramref name="close"/> returned. When <paramref name="close"/> shows no prompt, <paramref name="answer"/> never runs.
    /// </summary>
    public T Run<T>(Func<T> close, Action<Window> answer)
    {
        var finished = false;
        Exception? failure = null;
        void AnswerWhenShown()
        {
            if (finished)
            {
                return;
            }

            var prompt = windows.Visible().SingleOrDefault(w => w.Title == title);
            if (prompt is null)
            {
                // Not showing yet. A call that shows no prompt returns, and then this stops; one that shows it is waited for.
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, AnswerWhenShown);
                return;
            }

            try
            {
                shown++;
                Owner = prompt.Owner;
                Texts = OpenWindows.TextsIn(prompt);
                Centered = IsCentered(prompt);
                answer(prompt);
            }
            catch (Exception e)
            {
                failure = e;
                prompt.Close(); // So that the call returns and the test reports why.
            }
        }

        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, AnswerWhenShown);
        try
        {
            return close();
        }
        finally
        {
            finished = true;
            if (failure is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
        }
    }

    /// <summary>Answers by clicking the prompt's button with the text.</summary>
    public static Action<Window> Click(string text) => prompt => OpenWindows.Click(prompt, text);

    /// <summary>Answers by pressing Enter, which picks the prompt's default button.</summary>
    public static void PressEnter(Window prompt) =>
        SimulatedKey.Raise(prompt, Key.Enter, Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent);

    public static Action<Window> PressEnterOn(string text) => prompt =>
    {
        var button = OpenWindows.Descendants(prompt).OfType<Button>().Single(candidate => candidate.Content as string == text);
        button.Focus();
        Keyboard.Focus(button);
        if (!ReferenceEquals(Keyboard.FocusedElement, button))
        {
            throw new InvalidOperationException($"The \"{text}\" button didn't take the keyboard focus.");
        }

        SimulatedKey.Raise(button, Key.Enter, Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent);
        if (prompt.IsVisible)
        {
            throw new InvalidOperationException("Enter on the focused button didn't answer the prompt.");
        }
    };

    /// <summary>Answers by pressing Esc.</summary>
    public static void PressEscape(Window prompt) => OpenWindows.PressEscape(prompt);

    /// <summary>Answers by closing the prompt with its own close button.</summary>
    public static void CloseIt(Window prompt) => prompt.Close();

    /// <summary>Closes a window the way its close button and Alt+F4 do: the system's Close command.</summary>
    public static void PressCloseButton(Window window) =>
        SendMessage(new WindowInteropHelper(window).Handle, WmSysCommand, ScClose, 0);

    /// <summary>Closes a window the way an app exit or a sign-out does: a plain WM_CLOSE, not the system's Close command.</summary>
    public static void SendPlainClose(Window window) =>
        SendMessage(new WindowInteropHelper(window).Handle, WmClose, 0, 0);

    private static bool IsCentered(Window prompt)
    {
        var owner = prompt.Owner;
        return owner is not null
            && Math.Abs(prompt.Left + prompt.ActualWidth / 2 - (owner.Left + owner.ActualWidth / 2)) < 2
            && Math.Abs(prompt.Top + prompt.ActualHeight / 2 - (owner.Top + owner.ActualHeight / 2)) < 2;
    }

    private const int WmClose = 0x0010;
    private const int WmSysCommand = 0x0112;
    private const nint ScClose = 0xF060;

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);
}
