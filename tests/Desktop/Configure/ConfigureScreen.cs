using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The Configure window as a UX test uses it: built on the shared fake of the Start with Windows port, connected
/// to two shared fake shortcut actions that record each call, and driven as a user would. Keys go to a shortcut
/// field as key-down and key-up events, so a field must work out the modifiers from those events: a test can't set
/// the keyboard's real state. Shared test setup, not a fake of a port. Dispose it to close the window.
/// </summary>
internal sealed class ConfigureScreen : IDisposable
{
    private readonly OpenWindows windows = new();
    private readonly IConfigureWindow configure;

    /// <param name="startWithWindows">The port the window gets; a new fake, off, when null.</param>
    /// <param name="addNewTask">The Add new task action; a new fake that accepts every shortcut when null.</param>
    /// <param name="manageTasks">The Manage tasks action; a new fake that accepts every shortcut when null.</param>
    public ConfigureScreen(
        FakeStartWithWindows? startWithWindows = null,
        FakeShortcutAction? addNewTask = null,
        FakeShortcutAction? manageTasks = null,
        FakeRepositoryFolder? repositoryFolder = null)
    {
        StartWithWindows = startWithWindows ?? new FakeStartWithWindows();
        AddNewTask = addNewTask ?? new FakeShortcutAction();
        ManageTasks = manageTasks ?? new FakeShortcutAction();
        RepositoryFolder = repositoryFolder ?? new FakeRepositoryFolder();
        configure = ConfigureWindow.Create(StartWithWindows, RepositoryFolder);
        Connected = configure.ConnectShortcuts(AddNewTask, ManageTasks);
        AddNewTask.ClearCalls();
        ManageTasks.ClearCalls();
    }

    public FakeStartWithWindows StartWithWindows { get; }

    public FakeRepositoryFolder RepositoryFolder { get; }

    /// <summary>The window's collapsible sections, in order.</summary>
    public IReadOnlyList<Expander> Sections => [.. OpenWindows.Descendants(Window).OfType<Expander>()];

    /// <summary>Expands the section with the heading, as a click on its heading does.</summary>
    public void Expand(string heading) =>
        Sections.Single(section => ((TextBlock)section.Header).Text == heading).IsExpanded = true;

    /// <summary>The Add new task action. Its calls are those since the connection, which registered the default.</summary>
    public FakeShortcutAction AddNewTask { get; }

    /// <summary>The Manage tasks action. Its calls are those since the connection, which registered the default.</summary>
    public FakeShortcutAction ManageTasks { get; }

    /// <summary>What connecting the shortcuts answered.</summary>
    public bool Connected { get; }

    public Window Window => Assert.Single(windows.Visible());

    /// <summary>The Add new task row's shortcut box, the first of the two.</summary>
    public TextBox AddField => ShortcutFields[0];

    /// <summary>The Manage tasks row's shortcut box, the second of the two.</summary>
    public TextBox ManageField => ShortcutFields[1];

    public CheckBox StartWithWindowsBox => OpenWindows.Descendants(Window).OfType<CheckBox>().Single();

    /// <summary>The message under the Add new task row's box; empty when none.</summary>
    public string AddMessage => MessageUnder(AddField);

    /// <summary>The message under the Manage tasks row's box; empty when none.</summary>
    public string ManageMessage => MessageUnder(ManageField);

    /// <summary>The texts the window displays beside its controls, not the shortcut boxes'.</summary>
    public IReadOnlyList<string> Texts => OpenWindows.TextsIn(Window);

    /// <summary>The window's size once its layout has settled.</summary>
    public Size WindowSize
    {
        get
        {
            Settle();
            return new Size(Window.ActualWidth, Window.ActualHeight);
        }
    }

    private IReadOnlyList<TextBox> ShortcutFields => [.. OpenWindows.Descendants(Window).OfType<TextBox>().Take(2)];

    /// <summary>Opens the window, as the tray's Configure item does.</summary>
    public void Open() => configure.Show();

    /// <summary>Closes the window, as its close button does.</summary>
    public void CloseWindow() => Window.Close();

    /// <summary>Presses the keys in order and releases them in reverse order, on the shortcut box.</summary>
    public void Chord(TextBox field, params Key[] keys)
    {
        Reveal(field);
        SimulatedKey.Chord(field, keys);
    }

    /// <summary>Sends a key-down to the box. Returns whether the box took the key.</summary>
    public bool KeyDown(TextBox field, Key key) =>
        Reveal(field) && SimulatedKey.Raise(field, key, Keyboard.PreviewKeyDownEvent, Keyboard.KeyDownEvent);

    /// <summary>Sends a key-up to the box. Returns whether the box took the key.</summary>
    public bool KeyUp(TextBox field, Key key) =>
        Reveal(field) && SimulatedKey.Raise(field, key, Keyboard.PreviewKeyUpEvent, Keyboard.KeyUpEvent);

    /// <summary>Gives the keyboard focus to the element, as a click or Tab does.</summary>
    public static void Focus(UIElement element)
    {
        Reveal(element);
        Window.GetWindow(element)?.Activate();
        Keyboard.Focus(element);
        Assert.True(element.IsKeyboardFocused, "The window couldn't give the element the keyboard focus.");
    }

    /// <summary>Moves the keyboard focus to the Start with Windows check box, a focusable element that isn't a shortcut box.</summary>
    public void FocusStartWithWindowsBox() => Focus(StartWithWindowsBox);

    /// <summary>Switches the Start with Windows check box as a click does.</summary>
    public void SwitchStartWithWindows()
    {
        Reveal(StartWithWindowsBox);
        ((IToggleProvider)new CheckBoxAutomationPeer(StartWithWindowsBox).GetPattern(PatternInterface.Toggle)).Toggle();
    }

    /// <summary>Whether a displayed text contains the words, ignoring case.</summary>
    public bool ShowsText(string words) =>
        Texts.Any(text => text.Contains(words, StringComparison.OrdinalIgnoreCase));

    public void Dispose() => windows.Dispose();

    // A row's message is the text block that follows its box in the panel they share.
    private static string MessageUnder(TextBox field) =>
        ((Panel)field.Parent).Children.OfType<TextBlock>().First().Text;

    // Expands each section that holds the element, as a user does before using it. Always true, to chain with an action.
    private static bool Reveal(DependencyObject element)
    {
        for (var parent = LogicalTreeHelper.GetParent(element); parent is not null; parent = LogicalTreeHelper.GetParent(parent))
        {
            if (parent is Expander section)
            {
                section.IsExpanded = true;
                section.UpdateLayout();
            }
        }

        return true;
    }

    // Lets a pending layout and the window's resize to its content happen, so sizes and positions are current.
    private void Settle()
    {
        Window.UpdateLayout();
        Window.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
    }
}
