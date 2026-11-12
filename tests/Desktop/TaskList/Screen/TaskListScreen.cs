using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Fakes;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Drives a showing View Tasks window the way a user does, through what the screen shows. Shared test setup.
/// The screen is found by ordinary accessible labels, so tests depend on no private element:
/// <list type="bullet">
/// <item>A row has automation id "Row" and as its name the task's plain title. It holds the checkbox, and, while the task is open, the Edit button, the web view's element, or the editor.</item>
/// <item>Buttons are found by their label: the automation name, or the text of the content: Edit, Delete, Undo, Reopen, "N resolved".</item>
/// <item>The search box is a text box named "Search". The tag chips are buttons labelled with the tag, inside the element named "Tag filter".</item>
/// <item>The editor's Title field has automation id "Title".</item>
/// </list>
/// </summary>
internal sealed class TaskListScreen : IDisposable
{
    private static readonly Regex ResolvedCountLabel = new(@"^\d+ resolved$");

    private readonly OpenWindows windows = new();
    private readonly ListServices services;

    public TaskListScreen(ListServices services, bool show = true)
    {
        this.services = services;
        View = services.CreateWindow();
        if (show)
        {
            View.Show();
        }
    }

    public IViewTasksWindow View { get; }

    public Window Window => windows.Visible().Single();

    /// <summary>The web view the window made; fails unless exactly one was made.</summary>
    public FakeDescriptionWebView Web => services.WebViews.Single();

    /// <summary>The titles of the rows showing, top to bottom.</summary>
    public IReadOnlyList<string> RowTitles =>
        [.. Rows().OrderBy(PositionFromTop).Select(AutomationProperties.GetName)];

    /// <summary>The titles of the rows that show their description.</summary>
    public IReadOnlyList<string> ExpandedTitles =>
        [.. Rows().Where(IsExpanded).OrderBy(PositionFromTop).Select(AutomationProperties.GetName)];

    /// <summary>The texts the window displays, such as titles, tags, and its message line.</summary>
    public IReadOnlyList<string> Texts => TextsWithin(Window);

    public bool ShowsText(string words, DependencyObject? within = null) =>
        TextsWithin(within ?? Window).Any(text => text.Contains(words, StringComparison.OrdinalIgnoreCase));

    private IReadOnlyList<string> TextsWithin(DependencyObject root)
    {
        Refresh();
        return [.. Visuals(root).OfType<TextBlock>().Where(b => b.IsVisible)
            .Select(OpenWindows.TextOf).Where(text => text.Length > 0)];
    }

    /// <summary>The text of the "N resolved" button, or null while it is hidden.</summary>
    public string? ResolvedLabel => Buttons(Window).Select(LabelOf).FirstOrDefault(label => ResolvedCountLabel.IsMatch(label));

    public bool HasButton(string label, DependencyObject? within = null) => FindButton(label, within ?? Window) is not null;

    /// <summary>The words on the Sync button, which show whether a pass is running; throws when the button or its words are missing.</summary>
    public string SyncButtonText =>
        FindButton("Sync", Window)?.Content as string is { Length: > 0 } words
            ? words
            : throw new InvalidOperationException("No Sync button with words shows.");

    /// <summary>
    /// Completes when the Sync button shows the words, as soon as they appear; fails with a TimeoutException after the limit.
    /// Call it before the action that changes the words, then await it: it watches the button's content, so no sleep or polling is needed.
    /// </summary>
    public async Task SyncButtonShowsAsync(string words, TimeSpan limit)
    {
        var button = FindButton("Sync", Window) ?? throw new InvalidOperationException("No Sync button shows.");
        var shown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var content = DependencyPropertyDescriptor.FromProperty(ContentControl.ContentProperty, typeof(ButtonBase));
        void OnContentChanged(object? sender, EventArgs e)
        {
            if (button.Content as string == words)
            {
                shown.TrySetResult();
            }
        }

        content.AddValueChanged(button, OnContentChanged);
        try
        {
            OnContentChanged(button, EventArgs.Empty);
            await shown.Task.WaitAsync(limit);
        }
        finally
        {
            content.RemoveValueChanged(button, OnContentChanged);
        }
    }

    /// <summary>How many buttons with the label show.</summary>
    public int CountButtons(string label) => Buttons(Window).Count(b => LabelOf(b) == label);

    /// <summary>The tags the chips offer, in order.</summary>
    public IReadOnlyList<string> ChipTags => [.. Buttons(Named("Tag filter")).Select(LabelOf)];

    /// <summary>Gives the row the keyboard focus, as selecting it does.</summary>
    public void Select(string title)
    {
        var row = Row(title);
        row.Focus();
        Keyboard.Focus(row);
    }

    /// <summary>Whether the row with a title link of that text is itself, not something in it, the keyboard target: the row keyboard actions act on.</summary>
    public bool RowWithLinkIsKeyboardTarget(string linkText) => ReferenceEquals(FocusedElement, RowWithLink(linkText));

    /// <summary>Whether the row with that title is itself the keyboard target, as it is after <see cref="Select"/>.</summary>
    public bool RowIsKeyboardTarget(string title) => ReferenceEquals(FocusedElement, Row(title));

    /// <summary>Clicks the row with the left mouse button.</summary>
    public void ClickRow(string title)
    {
        var row = Row(title);
        Select(title);
        LeftClick(row);
        Refresh();
    }

    /// <summary>Picks the tag in the "All tags" list the way a user does, even when it is the tag already chosen.</summary>
    /// <param name="input">With the mouse: opens the list and left-clicks the tag. With the keyboard: focuses the list, opens it with F4, moves the highlight to the tag with Home and Down, and commits with the key or chord <paramref name="input"/> names.</param>
    public void ChooseFromAllTags(string tag, AllTagsInput input)
    {
        (Key Key, bool WithAlt) commit = default;
        if (input != AllTagsInput.Mouse && !KeyboardCommits.TryGetValue(input, out commit))
        {
            throw new ArgumentOutOfRangeException(nameof(input), input, "Not a way to pick from the All tags list.");
        }

        var all = Visuals(Window).OfType<ComboBox>().Single(c => c.IsVisible && AutomationProperties.GetName(c) == "All tags");
        if (input == AllTagsInput.Mouse)
        {
            all.IsDropDownOpen = true;
            Refresh();
            LeftClick((ComboBoxItem)all.ItemContainerGenerator.ContainerFromItem(tag));
        }
        else
        {
            all.Focus();
            Keyboard.Focus(all);
            SendKey(all, Key.F4, withAlt: false);
            Refresh(); // Lets the list open and lay out; the native list moves focus into it after the key.

            // The highlight moves and commits in one synchronous run, so no dispatcher work can run between the keys.
            // Each key is checked first, because a list that closed meanwhile turns Down into a direct selection.
            var target = all.ItemContainerGenerator.ContainerFromItem(tag) as ComboBoxItem
                ?? throw new InvalidOperationException($"The All tags list has no item for \"{tag}\" after it opened.");
            RequireOpen(all, "after F4 opened it");
            SendKey(all, Key.Home, withAlt: false);
            for (var step = all.Items.IndexOf(tag); step > 0; step--)
            {
                RequireOpen(all, "before Down");
                SendKey(all, Key.Down, withAlt: false);
            }

            RequireOpen(all, "before the commit key");
            if (!target.IsHighlighted)
            {
                throw new InvalidOperationException($"The highlight isn't on \"{tag}\" before the commit key.");
            }

            SendKey(all, commit.Key, commit.WithAlt);
        }

        Refresh();
    }

    private static void RequireOpen(ComboBox all, string when)
    {
        if (!all.IsDropDownOpen)
        {
            throw new InvalidOperationException($"The All tags list isn't open {when}.");
        }
    }

    // The key, and whether Alt is held, that commits the highlighted tag for each keyboard way of picking.
    private static readonly Dictionary<AllTagsInput, (Key Key, bool WithAlt)> KeyboardCommits = new()
    {
        [AllTagsInput.Enter] = (Key.Enter, false),
        [AllTagsInput.F4] = (Key.F4, false),
        [AllTagsInput.AltDown] = (Key.Down, true),
        [AllTagsInput.AltUp] = (Key.Up, true),
    };

    // Sends one key without pumping the dispatcher. Alt is put back on return, before any later pump can overwrite the held-Alt state.
    private static void SendKey(UIElement target, Key key, bool withAlt)
    {
        if (withAlt)
        {
            HeldAlt.While(() => SimulatedKey.Chord(target, key));
        }
        else
        {
            SimulatedKey.Chord(target, key);
        }
    }

    private static void LeftClick(UIElement target)
    {
        foreach (var (tunneling, bubbling) in new[]
        {
            (Mouse.PreviewMouseDownEvent, Mouse.MouseDownEvent),
            (Mouse.PreviewMouseUpEvent, Mouse.MouseUpEvent),
        })
        {
            foreach (var routed in new[] { tunneling, bubbling })
            {
                target.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
                {
                    RoutedEvent = routed,
                    Source = target,
                });
            }
        }
    }

    /// <summary>Selects the row and presses the keys on it, as the keyboard does.</summary>
    public void PressOn(string title, params Key[] keys)
    {
        Select(title);
        Press(keys);
    }

    /// <summary>Presses the keys on the element of the window that has the focus, never on another window's.</summary>
    public void Press(params Key[] keys)
    {
        SimulatedKey.Chord(FocusedElement as UIElement ?? Window, keys);
        Refresh();
    }

    // The window's own focused element. Keyboard.FocusedElement is the process-wide keyboard focus, which isn't this window's
    // while the operating system has another window active, so it counts only when it is in this window.
    private IInputElement? FocusedElement =>
        Keyboard.FocusedElement is DependencyObject keyboard && PresentationSource.FromDependencyObject(keyboard) == PresentationSource.FromVisual(Window)
            ? Keyboard.FocusedElement
            : FocusManager.GetFocusedElement(Window);

    /// <summary>
    /// Clicks the link in the row's title: the left mouse button goes down and up on the link and the events travel up through
    /// the row, then the link is invoked as assistive tools do. Native hover and pressed state aren't modeled, so the routed
    /// events show only what the row does with a click on a link and the invoke is what follows the link.
    /// </summary>
    public void ClickTitleLink(string linkText)
    {
        var link = LinkIn(RowWithLink(linkText), linkText);
        foreach (var routed in new[] { Mouse.PreviewMouseDownEvent, Mouse.MouseDownEvent, Mouse.PreviewMouseUpEvent, Mouse.MouseUpEvent })
        {
            link.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = routed,
                Source = link,
            });
        }

        HyperlinkActivation.Invoke(link);
        Refresh();
    }

    // The rows are found by the link they show, not by their automation name, which is the whole title text.
    private FrameworkElement RowWithLink(string linkText)
    {
        Refresh();
        var matches = Rows().Where(row => Visuals(row).OfType<TextBlock>().SelectMany(block => Links(block.Inlines))
            .Any(hyperlink => OpenWindows.TextOf(hyperlink) == linkText)).ToList();
        return matches.Count == 1
            ? matches[0]
            : throw new InvalidOperationException(
                $"Expected one row with the link \"{linkText}\", found {matches.Count}. Rows show: "
                + string.Join(", ", Rows().Select(row => $"\"{AutomationProperties.GetName(row)}\"")));
    }

    private static Hyperlink LinkIn(FrameworkElement row, string linkText) =>
        Visuals(row).OfType<TextBlock>().SelectMany(block => Links(block.Inlines)).Single(hyperlink => OpenWindows.TextOf(hyperlink) == linkText);

    private static IEnumerable<Hyperlink> Links(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is Hyperlink link)
            {
                yield return link;
            }

            if (inline is Span span)
            {
                foreach (var inner in Links(span.Inlines))
                {
                    yield return inner;
                }
            }
        }
    }

    /// <summary>Clicks the button with the label, below the row when one is given.</summary>
    public void Click(string label, string? inRowTitle = null)
    {
        var button = FindButton(label, inRowTitle is null ? Window : Row(inRowTitle))
            ?? throw new InvalidOperationException($"No \"{label}\" button shows.");
        Activate(button);
    }

    /// <summary>Clicks the tag's chip.</summary>
    public void ClickChip(string tag) => Activate(Buttons(Named("Tag filter")).Single(b => LabelOf(b) == tag));

    /// <summary>Clicks the row's checkbox, as the mouse does.</summary>
    public void ClickCheckBox(string title) => Activate(Visuals(Row(title)).OfType<CheckBox>().Single());

    /// <summary>The state of the row's checkbox.</summary>
    public bool IsChecked(string title) => Visuals(Row(title)).OfType<CheckBox>().Single().IsChecked == true;

    /// <summary>Puts the text into the search box, as typing does. Ctrl+F gives the box the focus.</summary>
    public void Search(string text)
    {
        Refresh();
        Visuals(Window).OfType<TextBox>().Single(b => AutomationProperties.GetName(b) == "Search").Text = text;
        Refresh();
    }

    /// <summary>Whether the search box holds the keyboard focus.</summary>
    public bool SearchHasFocus =>
        FocusedElement is TextBox box && AutomationProperties.GetName(box) == "Search";

    /// <summary>What the search box holds now.</summary>
    public string SearchText => Visuals(Window).OfType<TextBox>().Single(b => AutomationProperties.GetName(b) == "Search").Text;

    /// <summary>The editor's Title field of the row being edited.</summary>
    public FrameworkElement EditorTitle(string title) => TextFields.Find(Row(title), "Title");

    /// <summary>What the Title field of the row being edited holds.</summary>
    public string EditorTitleText(string title) => TextFields.TextOf(EditorTitle(title));

    /// <summary>Whether the row is being edited, so it shows the editor's Title field.</summary>
    public bool IsEditing(string title) => TextFields.FindShowing(Row(title), "Title") is not null;

    /// <summary>Whether any row is being edited.</summary>
    public bool IsEditingAny => TextFields.FindShowing(Window, "Title") is not null;

    /// <summary>Types the text into the editor's Title field and gives it the keyboard focus.</summary>
    public void TypeTitle(string rowTitle, string text)
    {
        var field = EditorTitle(rowTitle);
        TextFields.Type(field, text);
        field.Focus();
        Keyboard.Focus(field);
    }

    /// <summary>What the Title field of whichever row is being edited holds now.</summary>
    public string EditedTitleText => TextFields.TextOf(EditedField("Title"));

    /// <summary>What the Tags field of the row being edited holds: the tag text typed and not yet added.</summary>
    public string EditedTagsText => TextFields.TextOf(EditedField("Tags"));

    /// <summary>Types the text into the edited row's Tags field, without adding it as a tag.</summary>
    public void TypeTags(string text)
    {
        var field = EditedField("Tags");
        TextFields.Type(field, text);
        field.Focus();
        Keyboard.Focus(field);
    }

    /// <summary>What the Description field of the row being edited holds now.</summary>
    public string EditedDescriptionText => TextFields.TextOf(EditedField("Description"));

    /// <summary>The texts of the read-only fields showing, such as the description shown as source when the web view failed.</summary>
    public IReadOnlyList<string> ReadOnlyFieldTexts
    {
        get
        {
            Refresh();
            return
            [
                .. Visuals(Window).OfType<TextBox>().Where(b => b.IsVisible && b.IsReadOnly).Select(b => b.Text),
                .. Visuals(Window).OfType<ICSharpCode.AvalonEdit.TextEditor>().Where(e => e.IsVisible && e.IsReadOnly).Select(e => e.Text),
            ];
        }
    }

    /// <summary>Pastes the clipboard's image into the edited Description, as Ctrl+V does.</summary>
    public void PasteImageIntoDescription()
    {
        var field = EditedField("Description");
        field.Focus();
        Keyboard.Focus(field);
        Press(Key.LeftCtrl, Key.V);
    }

    /// <summary>
    /// Drops the task on the row as the mouse does: the data is an ordinary <see cref="DataObject"/> holding the
    /// <c>TaskItem</c>, and the pointer is in the row's upper half, or its lower half when <paramref name="lowerHalf"/>.
    /// </summary>
    /// <returns>Whether a handler took the drop.</returns>
    public bool Drop(Shared.Tasks.TaskItem task, string onRowTitle, bool lowerHalf = false)
    {
        var row = Row(onRowTitle);
        var taken = SimulatedDrop.Data(row, new DataObject(task), new Point(1, lowerHalf ? row.ActualHeight - 1 : 1));
        Refresh();
        return taken;
    }

    /// <summary>Drops something else, such as files, on the row.</summary>
    public bool DropFiles(string onRowTitle, params string[] paths)
    {
        var taken = SimulatedDrop.Files(Row(onRowTitle), paths);
        Refresh();
        return taken;
    }

    // The field of the editor showing in the window.
    private FrameworkElement EditedField(string id)
    {
        Refresh();
        return TextFields.FindShowing(Window, id) ?? throw new InvalidOperationException($"No {id} field shows.");
    }

    /// <summary>Makes the window this wide, in device-independent pixels, and lets it lay out.</summary>
    public void ResizeTo(double width)
    {
        Window.Width = width;
        Refresh();
    }

    /// <summary>
    /// The labels of the buttons and the search box outside the rows that don't lie wholly inside the window's
    /// client area, so the user can't reach them. Scroll bars are not counted.
    /// </summary>
    public IReadOnlyList<string> ControlsOutsideTheWindow()
    {
        Refresh();
        var client = new Rect(0, 0, ((FrameworkElement)Window.Content).ActualWidth, ((FrameworkElement)Window.Content).ActualHeight);
        var rows = Rows().ToList();
        return
        [
            .. Visuals(Window).OfType<FrameworkElement>()
                .Where(e => e.IsVisible && e is (ButtonBase and not CheckBox and not System.Windows.Controls.Primitives.RepeatButton) or TextBox
                    && !rows.Any(row => IsInside(e, row)))
                .Where(e => !client.Contains(new Rect(e.TranslatePoint(new Point(0, 0), (FrameworkElement)Window.Content), new Size(e.ActualWidth, e.ActualHeight))))
                .Select(e => e is ButtonBase button ? LabelOf(button) : AutomationProperties.GetName(e)),
        ];
    }

    /// <summary>The row with the plain title; fails unless exactly one shows.</summary>
    public FrameworkElement Row(string title)
    {
        Refresh();
        return Rows().Single(row => AutomationProperties.GetName(row) == title);
    }

    /// <summary>Whether a row with the title shows.</summary>
    public bool HasRow(string title)
    {
        Refresh();
        return Rows().Any(row => AutomationProperties.GetName(row) == title);
    }

    /// <summary>Waits until the window has armed a timer, then moves the fake clock forward and lets what fired finish.</summary>
    public async Task PassTimeAsync(TimeSpan by)
    {
        await services.Time.WaitForActiveTimerAsync();
        AdvanceTime(by);
    }

    /// <summary>Moves the fake clock forward and lets what fired finish, whether or not the window has a timer armed.</summary>
    public void AdvanceTime(TimeSpan by)
    {
        services.Time.Advance(by);
        Refresh();
    }

    public void Dispose() => windows.Dispose();

    private IEnumerable<FrameworkElement> Rows() =>
        Visuals(Window).OfType<FrameworkElement>()
            .Where(e => e.IsVisible && AutomationProperties.GetAutomationId(e) == "Row");

    // A row shows its description when the one web view sits in it, or, after the web view failed, when it offers Edit.
    private bool IsExpanded(FrameworkElement row) =>
        FindButton("Edit", row) is not null
        || (services.WebViews.Count == 1 && services.WebViews[0].Element.IsVisible && IsInside(services.WebViews[0].Element, row));

    private double PositionFromTop(FrameworkElement row) => row.TranslatePoint(new Point(0, 0), Window).Y;

    private FrameworkElement Named(string name) =>
        Visuals(Window).OfType<FrameworkElement>().Single(e => AutomationProperties.GetName(e) == name);

    private IEnumerable<ButtonBase> Buttons(DependencyObject root)
    {
        Refresh();
        return Visuals(root).OfType<ButtonBase>().Where(b => b.IsVisible && b is not CheckBox);
    }

    private ButtonBase? FindButton(string label, DependencyObject root) => Buttons(root).FirstOrDefault(b => LabelOf(b) == label);

    private static string LabelOf(ButtonBase button) =>
        AutomationProperties.GetName(button) is { Length: > 0 } name ? name : button.Content as string ?? "";

    // Clicks the way the automation tools do, then lets the click's work finish.
    private static void Activate(ButtonBase button)
    {
        var peer = UIElementAutomationPeer.CreatePeerForElement(button);
        if (peer.GetPattern(PatternInterface.Invoke) is IInvokeProvider invoke)
        {
            invoke.Invoke();
        }
        else
        {
            ((IToggleProvider)peer.GetPattern(PatternInterface.Toggle)).Toggle();
        }

        button.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void Refresh()
    {
        Window.UpdateLayout();
        Window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        Window.UpdateLayout();
    }

    private static bool IsInside(DependencyObject element, DependencyObject ancestor)
    {
        for (var current = element; current is not null;
            current = (current is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(current) : null)
                ?? LogicalTreeHelper.GetParent(current))
        {
            if (current == ancestor)
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<DependencyObject> Visuals(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            yield return child;
            foreach (var inner in Visuals(child))
            {
                yield return inner;
            }
        }
    }
}
