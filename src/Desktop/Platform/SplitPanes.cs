using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>
/// Two sides beside each other, and a bar between them the user drags to move the split. The bar takes no focus and is no
/// keyboard stop.
/// </summary>
public sealed class SplitPanes : Grid
{
    /// <summary>The sides share the width in the ratio <paramref name="leftShare"/> to <paramref name="rightShare"/>, each at least its minimum width.</summary>
    public SplitPanes(UIElement left, double leftShare, double leftMinWidth, UIElement right, double rightShare, double rightMinWidth)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        RequirePositiveFinite(leftShare);
        RequirePositiveFinite(rightShare);
        RequireNonNegativeFinite(leftMinWidth);
        RequireNonNegativeFinite(rightMinWidth);

        var splitter = new GridSplitter
        {
            Width = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ResizeDirection = GridResizeDirection.Columns,
            ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            Focusable = false,
            IsTabStop = false,
        };
        splitter.SetResourceReference(BackgroundProperty, "CardStrokeColorDefaultBrush");
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(leftShare, GridUnitType.Star), MinWidth = leftMinWidth });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(rightShare, GridUnitType.Star), MinWidth = rightMinWidth });
        SetColumn(left, 0);
        SetColumn(splitter, 1);
        SetColumn(right, 2);
        Children.Add(left);
        Children.Add(splitter);
        Children.Add(right);
    }

    private static void RequirePositiveFinite(double share, [CallerArgumentExpression(nameof(share))] string? name = null)
    {
        if (!(share > 0 && double.IsFinite(share)))
        {
            throw new ArgumentOutOfRangeException(name, share, "A share must be a positive, finite number.");
        }
    }

    private static void RequireNonNegativeFinite(double width, [CallerArgumentExpression(nameof(width))] string? name = null)
    {
        if (!(width >= 0 && double.IsFinite(width)))
        {
            throw new ArgumentOutOfRangeException(name, width, "A minimum width must be zero or more, and finite.");
        }
    }
}
