using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The row of a task's tag pills, wrapping onto more lines when they don't fit, followed by the add pill. The add pill says
/// "No tag" while the task has none and "+" after, and is off, dimmed and skipped by Tab, at the most tags. It draws what it is told
/// and reports clicks and presses by place; the places are the pills' places, and the add pill's is the number of pills.
/// </summary>
internal sealed class TagPills : WrapPanel
{
    private const double DimmedOpacity = 0.5;

    private readonly TagLook _look;
    private readonly Dictionary<string, TagPill> _pills = new(StringComparer.Ordinal);
    private readonly Button _add = new()
    {
        Padding = new Thickness(8, 2, 8, 2),
        Margin = new Thickness(0, 0, 6, 6),
        Cursor = Cursors.Hand,
        Template = TagBadgeVisuals.FlatButtonWith(new CornerRadius(10)),
        Background = Brushes.Transparent,
        BorderThickness = new Thickness(1),
        VerticalAlignment = VerticalAlignment.Top,
    };
    private IReadOnlyList<string> _shown = [];
    private string? _held;

    public TagPills(TagLook look)
    {
        _look = look;
        AutomationProperties.SetAutomationId(_add, "AddTagPill");
        _add.SetResourceReference(Control.BorderBrushProperty, "SurfaceStrokeColorDefaultBrush");
        _add.SetResourceReference(Control.ForegroundProperty, "TextFillColorPrimaryBrush");
        _add.Click += (_, _) => AddClicked?.Invoke();
        Children.Add(_add);
    }

    /// <summary>A pill was clicked, with its place.</summary>
    public event Action<int>? TagClicked;

    /// <summary>A pill's x was clicked, with the pill's place.</summary>
    public event Action<int>? TagRemoved;

    /// <summary>The left button went down on a pill, with its place.</summary>
    public event Action<int>? TagPressed;

    /// <summary>The add pill was clicked.</summary>
    public event Action? AddClicked;

    /// <summary>The add pill.</summary>
    public Button AddPill => _add;

    /// <summary>The number of pills shown, not counting the add pill.</summary>
    public int Count => _shown.Count;

    /// <summary>The pill's button at the place.</summary>
    public Button PillAt(int index) => _pills[_shown[index]].PillButton;

    /// <summary>
    /// Shows the tags as pills in that order, each in its color, and the add pill. The pill of <paramref name="held"/>, a tag, is
    /// dimmed. The cursor stays on the element it was on, even when that pill moved.
    /// </summary>
    public void Show(IReadOnlyList<string> tags, bool addEnabled, string? held)
    {
        var focused = Keyboard.FocusedElement as UIElement;
        var cursorHere = focused is not null && IsAncestorOf(focused);
        _shown = tags;
        _held = held;

        foreach (var gone in _pills.Keys.Where(tag => !tags.Contains(tag, StringComparer.Ordinal)).ToList())
        {
            Children.Remove(_pills[gone]);
            _pills.Remove(gone);
        }

        for (var i = 0; i < tags.Count; i++)
        {
            if (!_pills.TryGetValue(tags[i], out var pill))
            {
                pill = Create(tags[i]);
                _pills[tags[i]] = pill;
                Children.Insert(i, pill);
            }
            else if (!ReferenceEquals(Children[i], pill))
            {
                Children.Remove(pill);
                Children.Insert(i, pill);
            }
        }

        Paint();
        _add.Content = tags.Count == 0 ? TagEditor.NoTagText : "+";
        _add.IsEnabled = addEnabled;
        _add.Opacity = addEnabled ? 1 : DimmedOpacity;
        if (cursorHere && !focused!.IsKeyboardFocused)
        {
            UpdateLayout();
            KeyboardFocus.MoveTo(focused);
        }
    }

    /// <summary>Draws the pills again, after the theme or a tag's color changed.</summary>
    public void Repaint() => Paint();

    /// <summary>The place of the pill the element is in, the pill count for the add pill, or -1 for neither.</summary>
    public int IndexOfPill(object? source)
    {
        for (var element = source as DependencyObject; element is not null; element = ParentOf(element))
        {
            if (ReferenceEquals(element, _add))
            {
                return Count;
            }

            if (element is TagPill pill)
            {
                return IndexOf(pill.Text);
            }
        }

        return -1;
    }

    /// <summary>Puts the cursor in the first pill, or in the add pill when there are none.</summary>
    public void FocusFirst() => KeyboardFocus.MoveTo(Count > 0 ? PillAt(0) : _add);

    /// <summary>The place of the pill nearest the point, which is in the coordinates of <paramref name="owner"/>; 0 inside it.</summary>
    public int NearestTo(Point point, UIElement owner)
    {
        UpdateLayout();
        var nearest = 0;
        var nearestDistance = double.MaxValue;
        for (var i = 0; i < Count; i++)
        {
            var pill = _pills[_shown[i]];
            var bounds = pill.TransformToAncestor(owner).TransformBounds(new Rect(pill.RenderSize));
            var dx = Math.Max(Math.Max(bounds.Left - point.X, point.X - bounds.Right), 0);
            var dy = Math.Max(Math.Max(bounds.Top - point.Y, point.Y - bounds.Bottom), 0);
            var distance = dx * dx + dy * dy;
            if (distance < nearestDistance)
            {
                (nearest, nearestDistance) = (i, distance);
            }
        }

        return nearest;
    }

    private TagPill Create(string tag)
    {
        var pill = new TagPill(tag);
        pill.PillButton.Click += (_, _) => TagClicked?.Invoke(IndexOf(tag));
        pill.PillButton.PreviewMouseLeftButtonDown += (_, _) => TagPressed?.Invoke(IndexOf(tag));
        pill.ClearButton.Click += (_, _) => TagRemoved?.Invoke(IndexOf(tag));
        return pill;
    }

    private int IndexOf(string tag)
    {
        for (var i = 0; i < _shown.Count; i++)
        {
            if (string.Equals(_shown[i], tag, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private void Paint()
    {
        for (var i = 0; i < _shown.Count; i++)
        {
            var tag = _shown[i];
            _pills[tag].Show(_look.FillOf(tag), first: i == 0, held: string.Equals(tag, _held, StringComparison.Ordinal));
        }
    }

    private static DependencyObject? ParentOf(DependencyObject element) =>
        element is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
}
