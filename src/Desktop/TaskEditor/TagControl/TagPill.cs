using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>One tag as a pill in its color, with an x beside it that removes it. The first tag's pill is a little larger than the rest.</summary>
internal sealed class TagPill : StackPanel
{
    /// <summary>The first pill is this much of today's badge size, the others <see cref="OtherScale"/>.</summary>
    public const double FirstScale = 1.05;

    public const double OtherScale = 0.95;

    private const double HeldOpacity = 0.6;

    private static readonly ControlTemplate WithClear = TagBadgeVisuals.FlatButtonWith(new CornerRadius(10, 0, 0, 10));
    private static readonly ControlTemplate Clear = TagBadgeVisuals.FlatButtonWith(new CornerRadius(0, 10, 10, 0));

    public TagPill(string tag)
    {
        Text = tag;
        Orientation = Orientation.Horizontal;
        Margin = new Thickness(0, 0, 6, 6);
        VerticalAlignment = VerticalAlignment.Top;
        PillButton = new Button
        {
            Content = tag,
            Template = WithClear,
            Padding = new Thickness(8, 2, 8, 2),
            Cursor = Cursors.Hand,
            BorderThickness = new Thickness(0),
        };
        ClearButton = new Button
        {
            Content = "×",
            Template = Clear,
            Padding = new Thickness(2, 2, 8, 2),
            Cursor = Cursors.Hand,
            BorderThickness = new Thickness(0),
        };
        AutomationProperties.SetAutomationId(PillButton, "TagPill " + tag);
        AutomationProperties.SetAutomationId(ClearButton, "ClearTag " + tag);
        AutomationProperties.SetName(ClearButton, "Remove tag " + tag);
        Children.Add(PillButton);
        Children.Add(ClearButton);
    }

    /// <summary>The tag this pill shows.</summary>
    public string Text { get; }

    public Button PillButton { get; }

    public Button ClearButton { get; }

    /// <summary>Draws the pill in the fill, at the size of the place it has in the list, dimmed while it is being dragged.</summary>
    public void Show(TagFill fill, bool first, bool held)
    {
        var background = TagLook.Paint(fill.Background);
        var text = TagLook.Paint(fill.Text);
        foreach (var button in new[] { PillButton, ClearButton })
        {
            button.Background = background;
            button.Foreground = text;
        }

        var scale = first ? FirstScale : OtherScale;
        LayoutTransform = new ScaleTransform(scale, scale);
        Opacity = held ? HeldOpacity : 1;
    }
}
