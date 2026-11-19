using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Xunit;

namespace Memoria.ToDo.Desktop.Configure.Tests;

/// <summary>
/// The Configure window as a UX test uses it: built on the shared fake of the Start with Windows port, connected
/// to three shared fake shortcut actions that record each call, and driven as a user would. Keys go to a shortcut
/// field as key-down and key-up events, so a field must work out the modifiers from those events: a test can't set
/// the keyboard's real state. Shared test setup, not a fake of a port. Dispose it to close the window.
/// </summary>
internal sealed partial class ConfigureScreen : IDisposable
{
    private readonly OpenWindows windows = new();
    private readonly IConfigureWindow configure;
    private readonly ConfigureServices services;

    /// <param name="startWithWindows">The port the window gets; a new fake, off, when null.</param>
    /// <param name="addNewTask">The Add new task action; a new fake that accepts every shortcut when null.</param>
    /// <param name="manageTasks">The Task View action; a new fake that accepts every shortcut when null.</param>
    /// <param name="configure">The Configure action; a new fake that accepts every shortcut when null.</param>
    /// <param name="contacts">The Contacts action; a new fake that accepts every shortcut when null.</param>
    public ConfigureScreen(
        FakeStartWithWindows? startWithWindows = null,
        FakeShortcutAction? addNewTask = null,
        FakeShortcutAction? manageTasks = null,
        FakeSyncService? sync = null,
        Action<ISharedTaskSettings>? initialSettings = null,
        FakeShortcutAction? configure = null,
        FakeShortcutAction? contacts = null)
    {
        services = new ConfigureServices(startWithWindows, sync);
        AddNewTask = addNewTask ?? new FakeShortcutAction();
        ManageTasks = manageTasks ?? new FakeShortcutAction();
        ConfigureAction = configure ?? new FakeShortcutAction();
        ContactsAction = contacts ?? new FakeShortcutAction();
        initialSettings?.Invoke(Store.Settings);
        this.configure = services.Create();
        Connected = this.configure.ConnectShortcuts(AddNewTask, ManageTasks, ConfigureAction, ContactsAction);
        ClearActionCalls();
    }

    /// <summary>The theme the window follows; starts light.</summary>
    public FakeTheme Theme => services.Theme;

    public FakeSyncService Sync => services.Sync;
    public FakeSetupActions SetupActions => services.SetupActions;
    public ITaskStore Store => services.Store;

    /// <summary>The headings of Configure's three sections, in order.</summary>
    public static readonly string[] SectionHeadings = ["Application", "Startup", "Sync settings"];

    /// <summary>
    /// The section titles, in order. A title is a content control (such as a button) whose content is the heading's
    /// text, so a test finds it without knowing which control the window uses.
    /// </summary>
    public IReadOnlyList<ContentControl> Titles =>
        [.. OpenWindows.Descendants(Window).OfType<ContentControl>().Where(IsTitle)];

    /// <summary>The title of the section with the heading.</summary>
    public ContentControl Title(string heading) => Titles.Single(title => TitleText(title) == heading);

    /// <summary>Clicks the title of the section with the heading, as the mouse does.</summary>
    public void ClickTitle(string heading)
    {
        Invoke(Title(heading));
        Settle();
    }

    /// <summary>The headings of the sections whose content shows, in order.</summary>
    public IReadOnlyList<string> OpenSections
    {
        get
        {
            // A section opened since the last layout has no visual children yet, so it isn't visible yet.
            Settle();
            return [.. SectionHeadings.Where(heading => ContentMarker(heading).IsVisible)];
        }
    }

    /// <summary>The Add new task action. Its calls are those since the connection, which registered the default.</summary>
    public FakeShortcutAction AddNewTask { get; }

    /// <summary>The Task View action. Its calls are those since the connection, which registered the default.</summary>
    public FakeShortcutAction ManageTasks { get; }

    /// <summary>The Configure action. Its calls are those since the connection, which registered the default.</summary>
    public FakeShortcutAction ConfigureAction { get; }

    /// <summary>The Contacts action. Its calls are those since the connection, which registered the default.</summary>
    public FakeShortcutAction ContactsAction { get; }

    /// <summary>What connecting the shortcuts answered.</summary>
    public bool Connected { get; }

    public Window Window => Assert.Single(windows.Visible());

    /// <summary>Whether the window is showing.</summary>
    public bool IsShowing => windows.Visible().Count > 0;

    /// <summary>Opens the Configure button's menu, as a click on the button does, and returns it.</summary>
    public ContextMenu OpenRepositoryMenu()
    {
        ClickTitle("Sync settings");
        Window.UpdateLayout();
        var button = OpenWindows.Descendants(Window).OfType<Button>().Single(b => b.Content as string == "Configure");
        OpenWindows.Click(button);
        Assert.True(button.ContextMenu.IsOpen, "The Configure button didn't open its menu.");
        return button.ContextMenu;
    }

    /// <summary>The Add new task row's shortcut box, the first of the six.</summary>
    public TextBox AddField => ShortcutFields[0];

    /// <summary>The Task View row's shortcut box, the second of the six.</summary>
    public TextBox ManageField => ShortcutFields[1];

    /// <summary>The Contacts row's shortcut box, the third of the six: its row comes after Task View's.</summary>
    public TextBox ContactsField => ShortcutFields[2];

    /// <summary>The Configure row's shortcut box, the fourth of the six.</summary>
    public TextBox ConfigureField => ShortcutFields[3];

    /// <summary>The Close window row's shortcut box, the fifth of the six.</summary>
    public TextBox CloseField => ShortcutFields[4];

    /// <summary>The Save row's shortcut box, the sixth of the six.</summary>
    public TextBox SaveField => ShortcutFields[5];

    /// <summary>The Close window keys the window was given.</summary>
    public CloseWindowKeys CloseWindowKeys => services.CloseWindowKeys;

    /// <summary>The Save keys the window was given.</summary>
    public SaveKeys SaveKeys => services.SaveKeys;

    public CheckBox StartWithWindowsBox =>
        OpenWindows.Descendants(Window).OfType<CheckBox>().Single(box => box.Content as string == "Start with Windows");

    /// <summary>The message under the Add new task row's box; empty when none.</summary>
    public string AddMessage => MessageUnder(AddField);

    /// <summary>The message under the Task View row's box; empty when none.</summary>
    public string ManageMessage => MessageUnder(ManageField);

    /// <summary>The message under the Configure row's box; empty when none.</summary>
    public string ConfigureMessage => MessageUnder(ConfigureField);

    /// <summary>The message under the Contacts row's box; empty when none.</summary>
    public string ContactsMessage => MessageUnder(ContactsField);

    /// <summary>The message under the Close window row's box; empty when none.</summary>
    public string CloseMessage => MessageUnder(CloseField);

    /// <summary>The message under the Save row's box; empty when none.</summary>
    public string SaveMessage => MessageUnder(SaveField);

    /// <summary>The description under the row's box: the first text block after the box, before the message.</summary>
    public static TextBlock DescriptionUnder(TextBox field) => ((Panel)field.Parent).Children.OfType<TextBlock>().First();

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

    private IReadOnlyList<TextBox> ShortcutFields => [.. OpenWindows.Descendants(Window).OfType<TextBox>().Take(6)];

    /// <summary>
    /// Opens the window, as the tray's Configure item does, then forgets the calls the actions got until now, the
    /// registrations that showing makes included, so a test sees what it does next. <see cref="Show"/> keeps them.
    /// </summary>
    public void Open()
    {
        configure.Show();
        ClearActionCalls();
    }

    /// <summary>Shows the window as the tray's Configure item does, and keeps the calls the actions got, so a test sees what showing registers.</summary>
    public void Show() => configure.Show();
    private void ClearActionCalls()
    {
        AddNewTask.ClearCalls();
        ManageTasks.ClearCalls();
        ConfigureAction.ClearCalls();
        ContactsAction.ClearCalls();
    }

    /// <summary>Closes the window, as its close button does.</summary>
    public void CloseWindow() => Window.Close();

    /// <summary>Presses the keys in order and releases them in reverse order, on the shortcut box.</summary>
    public void Chord(TextBox field, params Key[] keys)
    {
        Reveal(field);
        SimulatedKey.Chord(field, keys);
    }

    /// <summary>Presses and releases Esc on the element, as the keyboard does.</summary>
    public void PressEscape(UIElement element)
    {
        Reveal(element);
        SimulatedKey.Chord(element, Key.Escape);
    }

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

    public void Dispose()
    {
        configure.Dispose();
        windows.Dispose();
    }

    public TextBox Box(string name) =>
        OpenWindows.Descendants(Window).OfType<TextBox>()
            .Single(box => System.Windows.Automation.AutomationProperties.GetName(box) == name);

    public void Click(string text)
    {
        OpenWindows.Click(Window, text);
        Settle();
    }

    public void ChooseRepositoryAction(string choice)
    {
        var button = OpenWindows.Descendants(Window).OfType<Button>().Single(b => b.Content as string == "Configure");
        ((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();
        var item = button.ContextMenu.Items.OfType<MenuItem>().Single(i => (string)i.Header == choice);
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        button.ContextMenu.IsOpen = false;
        Settle();
    }

    // A row's message is the last text block under its box: the description comes first.
    private static string MessageUnder(TextBox field) =>
        ((Panel)field.Parent).Children.OfType<TextBlock>().Last().Text;

    private static string? TitleText(ContentControl control) => control.Content switch
    {
        string text => text,
        TextBlock block => OpenWindows.TextOf(block),
        _ => null,
    };

    private static bool IsTitle(ContentControl control) => SectionHeadings.Contains(TitleText(control));

    // A control that shows only while its section is open.
    private UIElement ContentMarker(string heading) => heading switch
    {
        "Application" => AddField,
        "Startup" => StartWithWindowsBox,
        _ => Box("Device name"),
    };

    // Clicks the title through its automation peer, whatever kind of control it is.
    private static void Invoke(ContentControl title)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(title);
        if (peer.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke)
        {
            invoke.Invoke();
        }
        else if (peer.GetPattern(PatternInterface.SelectionItem) is ISelectionItemProvider item)
        {
            item.Select();
        }
        else if (peer.GetPattern(PatternInterface.Toggle) is IToggleProvider toggle)
        {
            toggle.Toggle();
        }
        else
        {
            throw new InvalidOperationException("The section title can't be clicked.");
        }

        title.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
    }

    // Opens the section that holds the element, as a user does before using it. Always true, to chain with an action.
    private static bool Reveal(DependencyObject element)
    {
        if (element is UIElement { IsVisible: true })
        {
            return true;
        }

        var window = Window.GetWindow(element);
        foreach (var title in OpenWindows.Descendants(window).OfType<ContentControl>().Where(IsTitle))
        {
            Invoke(title);
            window.UpdateLayout();
            if (element is UIElement { IsVisible: true })
            {
                break;
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
