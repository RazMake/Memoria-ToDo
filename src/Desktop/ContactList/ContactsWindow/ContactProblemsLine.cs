using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>The line above the list that says how many files were skipped and names shared; a click opens or closes the list under it.</summary>
internal sealed class ContactProblemsLine : StackPanel
{
    private readonly TextBlock _summary = new() { Cursor = Cursors.Hand, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _items = new() { Visibility = Visibility.Collapsed };

    public ContactProblemsLine()
    {
        Margin = new Thickness(8, 4, 8, 4);
        Visibility = Visibility.Collapsed;
        ContactHelpStyle.Apply(_summary);
        _summary.MouseLeftButtonUp += (_, _) =>
            _items.Visibility = _items.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        Children.Add(_summary);
        Children.Add(_items);
    }

    /// <summary>Shows the problems, or hides the line when there are none.</summary>
    public void Show(ContactProblems problems)
    {
        _summary.Text = problems.Summary ?? "";
        Visibility = problems.Summary is null ? Visibility.Collapsed : Visibility.Visible;
        _items.Children.Clear();
        foreach (var item in problems.Items)
        {
            var line = new TextBlock { Text = item, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(16, 2, 0, 0) };
            ContactHelpStyle.Apply(line);
            _items.Children.Add(line);
        }

        if (problems.Summary is null)
        {
            _items.Visibility = Visibility.Collapsed;
        }
    }
}
