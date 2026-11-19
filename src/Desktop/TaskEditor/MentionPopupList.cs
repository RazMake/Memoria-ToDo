using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The contact list shown as buttons under a field, only while open, like <see cref="PopupList"/>: each row is the contact's
/// text with its title after it, and the last line is the current template and the Tab hint, both in the help style.
/// It never takes the keyboard focus. Rows and last line come from the <see cref="MentionPicking"/> that shows the list.
/// </summary>
internal sealed class MentionPopupList : ListBox
{
    private const string HelpBrush = "TextFillColorSecondaryBrush";

    private static readonly DependencyProperty HintProperty =
        DependencyProperty.Register("Hint", typeof(string), typeof(MentionPopupList), new PropertyMetadata(""));

    public MentionPopupList(ChoiceList list, Func<MentionPicking?> shower)
    {
        Focusable = false;
        ScrollViewer.SetHorizontalScrollBarVisibility(this, ScrollBarVisibility.Disabled);
        AutomationProperties.SetAutomationId(this, "MentionList");
        AlternationCount = MentionPicking.MostShown; // A row's alternation index is its position, which finds its title.
        Template = ListWithHint();
        ClickableItems.AttachByPosition(this, Row(shower), list.Choose);
        this.ShowChoices(list);
        list.Changed += () => SetValue(HintProperty, shower()?.Hint ?? "");
    }

    // The items, then the hint as the last line.
    private static ControlTemplate ListWithHint()
    {
        var items = new FrameworkElementFactory(typeof(ScrollViewer));
        items.SetValue(FocusableProperty, false);
        items.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        items.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));

        var hint = new FrameworkElementFactory(typeof(TextBlock));
        hint.SetValue(TextBlock.TextProperty, new TemplateBindingExtension(HintProperty));
        hint.SetValue(MarginProperty, new Thickness(0, 0, 0, 4));
        hint.SetResourceReference(TextBlock.ForegroundProperty, HelpBrush);

        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.AppendChild(items);
        panel.AppendChild(hint);
        return new ControlTemplate(typeof(MentionPopupList)) { VisualTree = panel };
    }

    // A button with the contact's text, and the contact's title after it in the help style.
    private static FrameworkElementFactory Row(Func<MentionPicking?> shower)
    {
        var title = new FrameworkElementFactory(typeof(TextBlock));
        title.SetBinding(TextBlock.TextProperty, new Binding
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ListBoxItem), 1),
            Path = new PropertyPath(ItemsControl.AlternationIndexProperty),
            Converter = new TitleAt(shower),
        });
        title.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
        title.SetValue(MarginProperty, new Thickness(0, 0, 8, 4));
        title.SetResourceReference(TextBlock.ForegroundProperty, HelpBrush);

        var row = new FrameworkElementFactory(typeof(StackPanel));
        row.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        row.AppendChild(ClickableItems.ItemButton());
        row.AppendChild(title);
        return row;
    }

    private sealed class TitleAt(Func<MentionPicking?> shower) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            shower()?.Titles is { } titles && value is int position && position < titles.Count ? titles[position] ?? "" : "";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
