using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>What a group heading's drag carries: the group's file name.</summary>
internal sealed record DraggedContactGroup(string GroupName)
{
    public DataObject ToData() => new(this);

    public static DraggedContactGroup? From(IDataObject data) => data.GetData(typeof(DraggedContactGroup)) as DraggedContactGroup;
}

/// <summary>
/// A group's heading, with its Add button unless the group has none. Found on screen by its automation id "GroupHeading", named
/// by the heading. Dragging it, with the left button, starts the drag of the whole group while <c>canDrag</c> says so.
/// </summary>
internal sealed class ContactGroupHeading : Border
{
    private readonly PressDrag _press = new();
    private readonly Button? _add;

    public ContactGroupHeading(ListedGroup group, IDragPointer pointer, Func<bool> canDrag, Action<string> add)
    {
        Group = group.Name;
        Margin = new Thickness(0, 12, 0, 4);
        Padding = new Thickness(8, 2, 8, 2);
        Background = System.Windows.Media.Brushes.Transparent;
        AutomationProperties.SetAutomationId(this, "GroupHeading");
        AutomationProperties.SetName(this, group.Heading);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock { Text = group.Heading, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        if (group.CanAdd)
        {
            _add = new Button { Content = "Add", Padding = new Thickness(10, 2, 10, 2) };
            _add.Click += (_, _) => add(group.Name);
            Grid.SetColumn(_add, 1);
            grid.Children.Add(_add);
        }

        Child = grid;
        PreviewMouseDown += (_, e) =>
        {
            if (e.ChangedButton == MouseButton.Left && !IsInAdd(e.OriginalSource))
            {
                _press.Press(pointer.PositionIn(this));
            }
        };
        MouseMove += (_, e) =>
        {
            if (canDrag() && _press.Move(pointer.PositionIn(this), pointer.IsLeftButtonDown))
            {
                pointer.StartDrag(this, new DraggedContactGroup(group.Name).ToData());
                _press.Leave();
            }
        };
        MouseLeave += (_, _) => _press.Leave();
        PreviewMouseUp += (_, _) => _press.Release();
    }

    public string Group { get; }

    private bool IsInAdd(object source)
    {
        for (var element = source as DependencyObject; element is not null && !ReferenceEquals(element, this); element = System.Windows.Media.VisualTreeHelper.GetParent(element))
        {
            if (ReferenceEquals(element, _add))
            {
                return true;
            }
        }

        return false;
    }
}
