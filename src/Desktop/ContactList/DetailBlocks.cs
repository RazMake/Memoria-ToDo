using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>The text blocks of a contact's details, as the details pane and the contact card both draw them.</summary>
internal static class DetailBlocks
{
    /// <summary>The contact's full name, large.</summary>
    public static TextBlock Heading(string text) =>
        new() { Text = text, FontSize = 20, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };

    /// <summary>The group's heading, in the help style, under the full name.</summary>
    public static TextBlock GroupHeading(string text)
    {
        var block = new TextBlock { Text = text, Margin = new Thickness(0, 0, 0, 8) };
        ContactHelpStyle.Apply(block);
        return block;
    }

    private static readonly Thickness ValuePadding = new(6, 2, 6, 2);

    /// <summary>
    /// One row per field: its label in the help style, then its values, each on a subtle background a little larger than the text.
    /// With follow, a value that names a contact looks like a link, and a click on it calls follow; without it, nothing takes a click.
    /// </summary>
    public static Grid Fields(IEnumerable<DetailField> fields, Action<ContactRef>? follow)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        foreach (var field in fields)
        {
            var row = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock { Text = field.Label, Margin = new Thickness(0, 6 + ValuePadding.Top, 12, 0) };
            ContactHelpStyle.Apply(label);
            Grid.SetRow(label, row);
            grid.Children.Add(label);

            var values = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            foreach (var value in field.Values)
            {
                values.Children.Add(ValueOf(value, follow));
            }

            Grid.SetRow(values, row);
            Grid.SetColumn(values, 1);
            grid.Children.Add(values);
        }

        return grid;
    }

    private static Border ValueOf(DetailValue value, Action<ContactRef>? follow)
    {
        var block = new TextBlock { Text = value.Text, TextWrapping = TextWrapping.Wrap };
        var box = new Border
        {
            Child = block,
            Padding = ValuePadding,
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 2),
        };
        box.SetResourceReference(Border.BackgroundProperty, "SubtleFillColorSecondaryBrush");
        if (follow is not null && value.Target is { } target)
        {
            block.TextDecorations = TextDecorations.Underline;
            block.Cursor = Cursors.Hand;
            block.MouseLeftButtonUp += (_, _) => follow(target);
        }

        return box;
    }
}
