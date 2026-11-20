using System.Windows;
using System.Windows.Controls;

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

    /// <summary>A field's label, in the help style, above its values.</summary>
    public static TextBlock Label(string text)
    {
        var block = new TextBlock { Text = text, Margin = new Thickness(0, 8, 0, 0) };
        ContactHelpStyle.Apply(block);
        return block;
    }

    /// <summary>One value of a field.</summary>
    public static TextBlock Value(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap };
}
