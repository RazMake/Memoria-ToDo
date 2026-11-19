using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Desktop.TaskList.Tests;
using static Memoria.ToDo.Desktop.VisualTree;

namespace Memoria.ToDo.Desktop.ContactList.Tests;

/// <summary>
/// Drives a showing Contacts window the way a user does, through what the screen shows. Shared test setup. Found by accessible labels:
/// <list type="bullet">
/// <item>A contact's row on the left has automation id "Row", its full name as the name, and item status "Selected" while selected.</item>
/// <item>A group's heading has automation id "GroupHeading" and its heading ("Team") as the name; it holds an "Add" button. A row belongs to the nearest heading above it.</item>
/// <item>The right side has automation id "Details": the selected contact's details, the form, or the placeholder text.</item>
/// <item>The search box is a text box named "Search". A form's field has the field's file name as its automation id, such as "title".</item>
/// <item>Buttons are found by their label: Edit, Save, Delete, Add.</item>
/// <item>The question that asks before a delete is another showing window; its button is found by its label.</item>
/// </list>
/// </summary>
internal sealed class ContactsScreen : IDisposable
{
    private readonly OpenWindows _windows = new();
    private readonly ClosePrompt _question = new("Contacts");
    private readonly ContactServices _services;

    public ContactsScreen(ContactServices services)
    {
        _services = services;
        View = services.CreateWindow();
        View.Show();
        ScreenSizeLimit.Lift(Window);
    }

    public IContactsWindow View { get; }

    public Window Window => _windows.Visible().Single();

    /// <summary>The headings showing, top to bottom.</summary>
    public IReadOnlyList<string> GroupNames => [.. Headings().OrderBy(PositionFromTop).Select(AutomationProperties.GetName)];

    /// <summary>The full names of the rows showing, top to bottom.</summary>
    public IReadOnlyList<string> RowNames => [.. Rows().OrderBy(PositionFromTop).Select(AutomationProperties.GetName)];

    /// <summary>The full names of the rows under the group's heading, top to bottom.</summary>
    public IReadOnlyList<string> RowNamesIn(string heading) =>
        [.. RowsOf(Heading(heading)).OrderBy(PositionFromTop).Select(AutomationProperties.GetName)];

    public IReadOnlyList<string> SelectedNames => [.. Rows().Where(IsSelected).Select(AutomationProperties.GetName)];

    /// <summary>The texts the right side shows.</summary>
    public IReadOnlyList<string> DetailsTexts => TextsIn(Details);

    public FrameworkElement Details => Find(element => AutomationProperties.GetAutomationId(element) == "Details");

    public bool HasButton(string label) => Buttons(Window).Any(button => LabelOf(button) == label);

    /// <summary>Whether the group's heading holds a button with the label.</summary>
    public bool HeadingHasButton(string heading, string label) => Buttons(Heading(heading)).Any(button => LabelOf(button) == label);

    /// <summary>What the search box holds.</summary>
    public string SearchText => TextFields.TextOf(Find(element => AutomationProperties.GetName(element) == "Search"));

    /// <summary>Whether a text showing in the window contains the text.</summary>
    public bool ShowsText(string part) => TextsIn(Window).Any(text => text.Contains(part, StringComparison.Ordinal));

    /// <summary>The element of the form's field, such as a text box, a date picker, or a drop-down.</summary>
    public FrameworkElement Field(string field)
    {
        Refresh();
        return TextFields.Find(Window, field);
    }

    /// <summary>Clicks the text showing that starts with <paramref name="text"/>, as a link in the details or the problems line is clicked.</summary>
    public void ClickText(string text)
    {
        var block = TextBlockStartingWith(text);
        block.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        { RoutedEvent = UIElement.MouseLeftButtonUpEvent, Source = block });
        Refresh();
    }

    /// <summary>The one text block showing that starts with the text.</summary>
    public TextBlock TextBlockStartingWith(string text)
    {
        Refresh();
        return Visuals(Window).OfType<TextBlock>().Where(block => block.IsVisible).Single(block => OpenWindows.TextOf(block).StartsWith(text, StringComparison.Ordinal));
    }

    /// <summary>The text color of the text block, as a color of the window's theme.</summary>
    public static Color ColorOf(TextBlock block) => ((SolidColorBrush)block.Foreground).Color;

    /// <summary>Gives the window the Fluent theme, so the theme's brushes resolve.</summary>
    public void UseTheme() => OpenWindows.UseTheme(Window, dark: false);

    /// <summary>The color of a brush of the window's theme, such as TextFillColorSecondaryBrush.</summary>
    public Color ThemeColor(string brushKey) => ((SolidColorBrush)Window.FindResource(brushKey)).Color;

    /// <summary>The labels of the items showing on the right-click menu of the contact's row, opened as a right-click opens it; none when no menu opens.</summary>
    public IReadOnlyList<string> RowMenu(string fullName)
    {
        if (OpenRowMenu(fullName) is not { } menu)
        {
            return [];
        }

        var labels = ShowingItems(menu).Select(item => (string)item.Header).ToList();
        Close(menu);
        return labels;
    }

    /// <summary>Whether right-clicking the contact's row opens a menu.</summary>
    public bool OpensRowMenu(string fullName)
    {
        if (OpenRowMenu(fullName) is not { } menu)
        {
            return false;
        }

        Close(menu);
        return true;
    }

    /// <summary>Right-clicks the contact's row and chooses the showing item of its menu, as the automation tools do.</summary>
    public void ChooseFromRowMenu(string fullName, string label)
    {
        var menu = OpenRowMenu(fullName) ?? throw new InvalidOperationException($"Right-clicking {fullName} opened no menu.");
        var item = ShowingItems(menu).Single(candidate => (string)candidate.Header == label);
        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(item).GetPattern(PatternInterface.Invoke)).Invoke();
        Close(menu);
    }

    /// <summary>Types the date into the date picker's text box and leaves the focus there, so the picker hasn't taken it yet.</summary>
    public void TypeDate(string field, DateOnly date)
    {
        var picker = (DatePicker)Field(field);
        var box = Visuals(picker).OfType<TextBox>().Single();
        box.Focus();
        Keyboard.Focus(box);
        box.Text = date.ToDateTime(TimeOnly.MinValue).ToString("d", CultureInfo.CurrentCulture);
        Refresh();
    }

    public void Search(string text) => TextFields.Type(Find(element => AutomationProperties.GetName(element) == "Search"), text);

    public void ClickRow(string fullName)
    {
        var row = Row(fullName);
        row.Focus();
        Keyboard.Focus(row);
        MouseClick(row, MouseButton.Left);
        Refresh();
    }

    public void ClickButton(string label, string? inGroup = null)
    {
        var root = inGroup is null ? Window : Heading(inGroup);
        Activate(Buttons(root).First(button => LabelOf(button) == label));
        Refresh();
    }

    /// <summary>Types into the form's field, as typing does.</summary>
    public void Type(string field, string text)
    {
        TextFields.Type(TextFields.Find(Window, field), text);
        Refresh();
    }

    public string FieldText(string field) => TextFields.TextOf(TextFields.Find(Window, field));

    /// <summary>Presses the Save keys on the window, as a user does with a form open.</summary>
    public void PressSaveKeys()
    {
        SimulatedKey.Chord(Window, Key.LeftCtrl, Key.Enter);
        Refresh();
    }

    /// <summary>Clicks Delete and answers the question it asks by clicking the button with the label.</summary>
    public void DeleteAnsweringWith(string answer)
    {
        _question.Run(() =>
        {
            ClickButton("Delete");
            return 0;
        }, ClosePrompt.Click(answer));
        if (_question.Shown == 0)
        {
            throw new InvalidOperationException("Delete asked no question.");
        }
    }

    /// <summary>Drags the group's heading with the mouse past the drag distance and drops it on the other group's heading's upper half.</summary>
    /// <returns>Whether the drag started and the drop was taken.</returns>
    public bool DragGroup(string heading, string onHeading)
    {
        var source = Heading(heading);
        var far = SystemParameters.MinimumHorizontalDragDistance + 20;
        _services.Pointer.Position = new Point(5, 5);
        _services.Pointer.IsLeftButtonDown = true;
        source.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        { RoutedEvent = UIElement.PreviewMouseDownEvent, Source = source });
        _services.Pointer.Position = new Point(5 + far, 5);
        source.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.MouseMoveEvent, Source = source });
        _services.Pointer.IsLeftButtonDown = false;

        if (_services.Pointer.Drags.Count == 0)
        {
            return false;
        }

        var taken = SimulatedDrop.Data(Heading(onHeading), _services.Pointer.Drags[^1], new Point(1, 1));
        Refresh();
        return taken;
    }

    public void Dispose() => _windows.Dispose();

    private FrameworkElement Heading(string heading) =>
        Headings().Single(candidate => AutomationProperties.GetName(candidate) == heading);

    private FrameworkElement Row(string fullName)
    {
        Refresh();
        return Rows().Single(row => AutomationProperties.GetName(row) == fullName);
    }

    // A right-click selects the row, then WPF opens the row's menu, which a test can't trigger through raised mouse events,
    // so the menu is opened as WPF opens it, on the row as it shows after the selection.
    private ContextMenu? OpenRowMenu(string fullName)
    {
        MouseClick(Row(fullName), MouseButton.Right);
        var row = Row(fullName);
        if (row.ContextMenu is not { } menu)
        {
            return null;
        }

        menu.PlacementTarget = row;
        menu.IsOpen = true;
        Refresh();
        return menu.IsOpen ? menu : null;
    }

    private static IEnumerable<MenuItem> ShowingItems(ContextMenu menu) =>
        menu.Items.OfType<MenuItem>().Where(item => item.Visibility == Visibility.Visible);

    private void Close(ContextMenu menu)
    {
        menu.IsOpen = false;
        Refresh();
    }

    private IEnumerable<FrameworkElement> Rows() => WithId("Row");

    private IEnumerable<FrameworkElement> Headings() => WithId("GroupHeading");

    private IEnumerable<FrameworkElement> WithId(string id)
    {
        Refresh();
        return Visuals(Window).OfType<FrameworkElement>().Where(e => e.IsVisible && AutomationProperties.GetAutomationId(e) == id);
    }

    private FrameworkElement Find(Func<FrameworkElement, bool> match)
    {
        Refresh();
        return Visuals(Window).OfType<FrameworkElement>().Single(match);
    }

    private IEnumerable<FrameworkElement> RowsOf(FrameworkElement heading) =>
        Rows().Where(row => Headings().Where(h => PositionFromTop(h) <= PositionFromTop(row)).OrderByDescending(PositionFromTop).First() == heading);

    private static bool IsSelected(FrameworkElement row) => AutomationProperties.GetItemStatus(row) == "Selected";

    private double PositionFromTop(FrameworkElement element) => element.TranslatePoint(new Point(0, 0), Window).Y;

    private IEnumerable<ButtonBase> Buttons(DependencyObject root)
    {
        Refresh();
        return VisibleButtons(root);
    }

    private IReadOnlyList<string> TextsIn(DependencyObject root)
    {
        Refresh();
        return [.. Visuals(root).OfType<TextBlock>().Where(block => block.IsVisible).Select(OpenWindows.TextOf).Where(text => text.Length > 0)];
    }

    private void Refresh()
    {
        Window.UpdateLayout();
        Window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
        Window.UpdateLayout();
    }
}
