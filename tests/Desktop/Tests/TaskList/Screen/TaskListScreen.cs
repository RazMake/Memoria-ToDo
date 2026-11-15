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
using Memoria.ToDo.Desktop.FormattedView;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Drives a showing Task View window the way a user does, through what the screen shows. Shared test setup.
/// The screen is found by ordinary accessible labels, so tests depend on no private element:
/// <list type="bullet">
/// <item>A row on the left side has automation id "Row" and as its name the task's plain title. It holds the checkbox and the title, and nothing else. A task in two groups has a row in each. The selected task's rows have automation item status "Selected".</item>
/// <item>A group's heading has automation id "GroupHeading" and as its name the tag, or "No tag". A row belongs to the nearest heading above it.</item>
/// <item>The right side is the element with automation id "Details": it holds the selected task's description, its tag control, the editor, or the placeholder text.</item>
/// <item>A row's right-click menu is its <c>ContextMenu</c>, with the items Edit Description and Delete, each with its key as the gesture text.</item>
/// <item>Buttons are found by their label: the automation name, or the text of the content: Undo, "N resolved".</item>
/// <item>The search box is a text box named "Search"; the magnifier button next to it has the tooltip "Search (Enter)". The status bar has automation id "StatusBar" and holds the Sync button (name "Sync"), the error text and the version.</item>
/// <item>The editor's Title field has automation id "Title".</item>
/// </list>
/// </summary>
internal sealed partial class TaskListScreen : IDisposable
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

    /// <summary>The titles of the rows showing, top to bottom: a task in two groups shows twice.</summary>
    public IReadOnlyList<string> RowTitles =>
        [.. Rows().OrderBy(PositionFromTop).Select(AutomationProperties.GetName)];

    /// <summary>The names of the group headings showing, top to bottom: each tag, then "No tag".</summary>
    public IReadOnlyList<string> GroupNames =>
        [.. Headings().OrderBy(PositionFromTop).Select(AutomationProperties.GetName)];

    /// <summary>The titles of the rows under the group's heading, top to bottom.</summary>
    public IReadOnlyList<string> RowTitlesIn(string group)
    {
        var heading = Headings().Single(h => AutomationProperties.GetName(h) == group);
        return [.. RowsOf(heading).OrderBy(PositionFromTop).Select(AutomationProperties.GetName)];
    }

    /// <summary>The distinct titles of the selected rows, top to bottom.</summary>
    public IReadOnlyList<string> SelectedTitles =>
        [.. Rows().Where(IsSelected).OrderBy(PositionFromTop).Select(AutomationProperties.GetName).Distinct()];

    /// <summary>How many rows show as selected: two for a task in two groups.</summary>
    public int SelectedRowCount => Rows().Count(IsSelected);

    /// <summary>The right side, which holds the description, the tag control, the editor, or the placeholder.</summary>
    public FrameworkElement Details
    {
        get
        {
            Refresh();
            return Visuals(Window).OfType<FrameworkElement>().Single(e => AutomationProperties.GetAutomationId(e) == "Details");
        }
    }

    /// <summary>Whether the window has a bar between the two sides that the user can drag.</summary>
    public bool HasSplitter
    {
        get
        {
            Refresh();
            return Visuals(Window).OfType<GridSplitter>().Any(s => s.IsVisible);
        }
    }

    /// <summary>The window's x of the bar, the rows, and the right side: the right side starts after the bar, which starts after every row's right edge.</summary>
    public (double RowsRight, double SplitterLeft, double SplitterRight, double DetailsLeft) SideEdges
    {
        get
        {
            Refresh();
            var splitter = Visuals(Window).OfType<GridSplitter>().Single(s => s.IsVisible);
            var left = splitter.TranslatePoint(new Point(0, 0), Window).X;
            return (
                Rows().Max(row => row.TranslatePoint(new Point(row.ActualWidth, 0), Window).X),
                left,
                left + splitter.ActualWidth,
                Details.TranslatePoint(new Point(0, 0), Window).X);
        }
    }

    /// <summary>Moves the bar between the sides by the distance, as dragging it does: the drag starts, moves, and completes. Then the window lays out.</summary>
    public void MoveSplitter(double distance)
    {
        Refresh();
        var splitter = Visuals(Window).OfType<GridSplitter>().Single(s => s.IsVisible);
        splitter.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
        splitter.RaiseEvent(new DragDeltaEventArgs(distance, 0) { RoutedEvent = Thumb.DragDeltaEvent });
        splitter.RaiseEvent(new DragCompletedEventArgs(distance, 0, canceled: false) { RoutedEvent = Thumb.DragCompletedEvent });
        Refresh();
    }

    /// <summary>The width of the rows' side.</summary>
    public double RowsSideWidth
    {
        get
        {
            Refresh();
            return Rows().First().ActualWidth;
        }
    }

    /// <summary>
    /// How far the checkbox's middle is below the middle of the title's first line, in device-independent pixels, so 0 is
    /// aligned. Also says how many lines the title shows.
    /// </summary>
    public (double Gap, int Lines) CheckBoxGap(string title, string? group = null)
    {
        var row = Row(title, group);
        var text = Visuals(row).OfType<FormattedTitle>().Single();
        var box = Visuals(row).OfType<CheckBox>().Single();
        var first = text.ContentStart.GetCharacterRect(LogicalDirection.Forward);
        var firstMiddle = text.TranslatePoint(new Point(0, first.Top + first.Height / 2), Window).Y;
        var boxMiddle = box.TranslatePoint(new Point(0, box.ActualHeight / 2), Window).Y;
        var lines = (int)Math.Round(text.ActualHeight / first.Height);
        return (boxMiddle - firstMiddle, lines);
    }

    /// <summary>Makes the text of the window this many points, as the user's text size does, and lets it lay out.</summary>
    public void SetTextSize(double points)
    {
        Window.FontSize = points;
        Refresh();
    }

    /// <summary>The cursor over the row's title.</summary>
    public Cursor? TitleCursor(string title, string? group = null) =>
        Visuals(Row(title, group)).OfType<FormattedTitle>().Single().Cursor;

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

    /// <summary>How many buttons with the label show.</summary>
    public int CountButtons(string label) => Buttons(Window).Count(b => LabelOf(b) == label);

    /// <summary>Gives the row the keyboard focus, without selecting its task.</summary>
    public void Select(string title, string? group = null)
    {
        var row = Row(title, group);
        row.Focus();
        Keyboard.Focus(row);
    }

    /// <summary>Whether the row with that title is itself the keyboard target, as it is after <see cref="Select"/>.</summary>
    public bool RowIsKeyboardTarget(string title, string? group = null) => ReferenceEquals(FocusedElement, Row(title, group));

    /// <summary>Gives the row the focus and clicks it with the left mouse button, which selects its task.</summary>
    public void ClickRow(string title, string? group = null)
    {
        var row = Row(title, group);
        Select(title, group);
        MouseClick(row, MouseButton.Left);
        Refresh();
    }

    /// <summary>Clicks the row's title with the left mouse button, as a user does to see the task's description.</summary>
    public void ClickTitle(string title, string? group = null)
    {
        var row = Row(title, group);
        Select(title, group);
        MouseClick(Visuals(row).OfType<FormattedTitle>().Single(), MouseButton.Left);
        Refresh();
    }

    /// <summary>Right-clicks the row, which selects its task and opens its menu.</summary>
    public void RightClick(string title, string? group = null)
    {
        var row = Row(title, group);
        Select(title, group);
        MouseClick(row, MouseButton.Right);
        Refresh();
    }

    /// <summary>The items of the row's right-click menu, each as its label and the key shown beside it, in order.</summary>
    public IReadOnlyList<(string Label, string Key)> ContextMenuItems(string title, string? group = null) =>
        [.. (Row(title, group).ContextMenu?.Items.OfType<MenuItem>()
            ?? throw new InvalidOperationException($"The row \"{title}\" has no right-click menu."))
            .Select(item => ((string)item.Header, item.InputGestureText))];

    /// <summary>Chooses the item of the row's right-click menu, as clicking it does.</summary>
    public void ChooseContextItem(string title, string label, string? group = null)
    {
        var item = (Row(title, group).ContextMenu ?? throw new InvalidOperationException($"The row \"{title}\" has no right-click menu."))
            .Items.OfType<MenuItem>().Single(i => (string)i.Header == label);
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Refresh();
    }

    private static void MouseClick(UIElement target, MouseButton button)
    {
        foreach (var (tunneling, bubbling) in new[]
        {
            (Mouse.PreviewMouseDownEvent, Mouse.MouseDownEvent),
            (Mouse.PreviewMouseUpEvent, Mouse.MouseUpEvent),
        })
        {
            foreach (var routed in new[] { tunneling, bubbling })
            {
                target.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, button)
                {
                    RoutedEvent = routed,
                    Source = target,
                });
            }
        }
    }

    /// <summary>Clicks the row, which selects its task and gives it the keyboard focus, and presses the keys on it.</summary>
    public void PressOn(string title, params Key[] keys)
    {
        ClickRow(title);
        Press(keys);
    }

    /// <summary>Presses the keys on the element of the window that has the focus, never on another window's.</summary>
    public void Press(params Key[] keys)
    {
        SimulatedKey.Chord(FocusedElement as UIElement ?? Window, keys);

        // Keys that close the window leave nothing to lay out.
        if (windows.Visible().Any())
        {
            Refresh();
        }
    }

    /// <summary>
    /// Presses the keys, the last of which closes Task View. Nothing is laid out afterwards, since no window is left.
    /// </summary>
    public void PressToClose(params Key[] keys)
    {
        SimulatedKey.Chord(FocusedElement as UIElement ?? Window, keys);
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
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

    /// <summary>Clicks the row's checkbox, as the mouse does.</summary>
    public void ClickCheckBox(string title, string? group = null) => Activate(Visuals(Row(title, group)).OfType<CheckBox>().Single());

    /// <summary>Whether the row's checkbox shows ticked.</summary>
    public bool CheckBoxIsTicked(string title, string? group = null) => Visuals(Row(title, group)).OfType<CheckBox>().Single().IsChecked == true;

    /// <summary>Types the text into the search box and presses Enter in it, which searches. Ctrl+F gives the box the focus.</summary>
    public void Search(string text)
    {
        TypeSearch(text);
        var box = SearchBox;
        box.Focus();
        Keyboard.Focus(box);
        Press(Key.Enter);
    }

    /// <summary>Puts the text into the search box, as typing does, without searching.</summary>
    public void TypeSearch(string text)
    {
        Refresh();
        SearchBox.Text = text;
        Refresh();
    }

    private TextBox SearchBox => Visuals(Window).OfType<TextBox>().Single(b => AutomationProperties.GetName(b) == "Search");

    /// <summary>Whether the search box holds the keyboard focus.</summary>
    public bool SearchHasFocus =>
        FocusedElement is TextBox box && AutomationProperties.GetName(box) == "Search";

    /// <summary>What the search box holds now.</summary>
    public string SearchText => SearchBox.Text;

    /// <summary>The row's Title field while its title is edited in the row; name the group when the task shows in two.</summary>
    public FrameworkElement EditorTitle(string title, string? group = null) => TextFields.Find(Row(title, group), "Title");

    /// <summary>What the Title field in the row holds.</summary>
    public string EditorTitleText(string title, string? group = null) => TextFields.TextOf(EditorTitle(title, group));

    /// <summary>Whether the task's description is being edited: the selected task, named by <paramref name="title"/>, shows the Description editor on the right side.</summary>
    public bool IsEditing(string title) => SelectedTitles.Contains(title) && TextFields.FindShowing(Details, "Description") is not null;

    /// <summary>Whether the task's title is being edited in its row.</summary>
    public bool IsEditingTitle(string title) => TextFields.FindShowing(Row(title), "Title") is not null;

    /// <summary>Whether any edit is open: a Description editor on the right side or a Title editor in a row.</summary>
    public bool IsEditingAny => TextFields.FindShowing(Window, "Title") is not null || TextFields.FindShowing(Window, "Description") is not null;

    private void RequireSelected(string title)
    {
        if (!SelectedTitles.Contains(title))
        {
            throw new InvalidOperationException($"\"{title}\" isn't the selected task. Selected: {string.Join(", ", SelectedTitles)}.");
        }
    }

    /// <summary>Types the text into the row's Title field and gives it the keyboard focus.</summary>
    public void TypeTitle(string rowTitle, string text, string? group = null)
    {
        var field = EditorTitle(rowTitle, group);
        TextFields.Type(field, text);
        field.Focus();
        Keyboard.Focus(field);
    }

    /// <summary>Sets the text of the edited Description field and gives it the keyboard focus.</summary>
    public void TypeDescription(string text)
    {
        var field = EditedField("Description");
        TextFields.Type(field, text);
        field.Focus();
        Keyboard.Focus(field);
    }

    /// <summary>What the Title field of whichever row is being edited holds now.</summary>
    public string EditedTitleText => TextFields.TextOf(EditedField("Title"));

    /// <summary>Whether the edited row's Title is marked as empty, for the eye and for assistive tools.</summary>
    public bool EditedTitleIsMarked => !string.IsNullOrEmpty(AutomationProperties.GetItemStatus(EditedField("Title")));

    /// <summary>Whether the keyboard focus is in one of the editor's fields.</summary>
    public bool FocusIsInEditor => TaskEditor.Tests.EditorFields.FocusedFieldName() is not null;

    /// <summary>The name of the editor field that has the keyboard focus, such as "Description", or null.</summary>
    public string? FocusedEditorField => TaskEditor.Tests.EditorFields.FocusedFieldName();

    /// <summary>Whether the editor's Description has its cursor at the end of its text, with nothing selected.</summary>
    public bool DescriptionCursorIsAtEnd
    {
        get
        {
            Refresh();
            var (start, length) = TaskEditor.Tests.EditorFields.SelectionOf(Window, "Description");
            return length == 0 && start == EditedDescriptionText.Length;
        }
    }

    /// <summary>Types the text into the edited row's Description, one character at a time as the keyboard does; "/" opens the snippet list.</summary>
    public void TypeInDescription(string text)
    {
        var field = EditedField("Description");
        field.Focus();
        Keyboard.Focus(field);
        TaskEditor.Tests.EditorFields.TypeOnFocused(text);
        Refresh();
    }

    /// <summary>The snippets the edited row's open snippet list offers; empty while it is closed.</summary>
    public IReadOnlyList<string> ShownSnippets => TaskEditor.Tests.EditorFields.ShownSnippets(Window);

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
    /// Drops the task on the row as the mouse does: the data is the payload a row drag carries, with the group of the row the
    /// drag started from (<paramref name="fromGroup"/>, or the first row showing the task), and the pointer is in the row's
    /// upper half, or its lower half when <paramref name="lowerHalf"/>.
    /// </summary>
    /// <returns>Whether a handler took the drop.</returns>
    public bool Drop(Shared.Tasks.TaskItem task, string onRowTitle, bool lowerHalf = false, string? onGroup = null, string? fromGroup = null)
    {
        var row = Row(onRowTitle, onGroup);
        var source = ((TaskRow)Row(task.Title, fromGroup)).Group;
        var taken = SimulatedDrop.Data(row, new DraggedTask(task, source).ToData(), new Point(1, lowerHalf ? row.ActualHeight - 1 : 1));
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

    /// <summary>The row with the plain title, in the group when one is named; a task in two groups needs the group, or its first row is taken.</summary>
    public FrameworkElement Row(string title, string? group = null)
    {
        Refresh();
        var named = Rows().Where(row => AutomationProperties.GetName(row) == title);
        if (group is not null)
        {
            var heading = Headings().Single(h => AutomationProperties.GetName(h) == group);
            named = named.Where(row => HeadingOf(row) == heading);
        }

        return named.OrderBy(PositionFromTop).First();
    }

    public void Dispose() => windows.Dispose();

    private IEnumerable<FrameworkElement> Rows() =>
        Visuals(Window).OfType<FrameworkElement>()
            .Where(e => e.IsVisible && AutomationProperties.GetAutomationId(e) == "Row");

    private IEnumerable<FrameworkElement> Headings() =>
        Visuals(Window).OfType<FrameworkElement>()
            .Where(e => e.IsVisible && AutomationProperties.GetAutomationId(e) == "GroupHeading");

    // The heading a row sits under: the nearest one above it.
    private FrameworkElement? HeadingOf(FrameworkElement row) =>
        Headings().Where(h => PositionFromTop(h) <= PositionFromTop(row)).OrderByDescending(PositionFromTop).FirstOrDefault();

    private IEnumerable<FrameworkElement> RowsOf(FrameworkElement heading) => Rows().Where(row => HeadingOf(row) == heading);

    private static bool IsSelected(FrameworkElement row) => AutomationProperties.GetItemStatus(row) == "Selected";
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
