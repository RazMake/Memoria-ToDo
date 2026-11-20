using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The contact list as buttons, only while open: a header button that shows the contact in the current template and moves to the
/// next template when clicked, a hint line, and a row of the contact's bold name and italic alias for each contact.
/// Nothing in it takes the keyboard focus. Rows and header come from the <see cref="ContactPicking"/> that shows the list;
/// a list with no header shows none.
/// </summary>
internal sealed class MentionPopupList : ListBox
{
    private const string HelpBrush = "TextFillColorSecondaryBrush";
    private const string HeaderName = "Header";
    private const string HintName = "Hint";

    private static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register("Header", typeof(string), typeof(MentionPopupList), new PropertyMetadata(""));

    private static readonly DependencyProperty HintProperty =
        DependencyProperty.Register("Hint", typeof(string), typeof(MentionPopupList), new PropertyMetadata(""));

    // The header's chip background and the alias's italic color, from the theme; the follower keeps them current while loaded.
    private static readonly DependencyProperty ChipBackgroundProperty =
        DependencyProperty.Register("ChipBackground", typeof(Brush), typeof(MentionPopupList));

    private static readonly DependencyProperty AliasBrushProperty =
        DependencyProperty.Register("AliasBrush", typeof(Brush), typeof(MentionPopupList));

    public MentionPopupList(ChoiceList list, Func<ContactPicking?> shower, Func<bool> headerClicked, ITheme theme)
    {
        _ = new ThemeFollower(this, theme, () => ShowTheme(theme));
        ShowTheme(theme);
        Focusable = false;
        ScrollViewer.SetHorizontalScrollBarVisibility(this, ScrollBarVisibility.Disabled);
        AutomationProperties.SetAutomationId(this, "MentionList");
        AlternationCount = ContactPicking.MostShown; // A row's alternation index is its position, which finds its row.
        Template = HeaderHintAndRows(headerClicked);
        ClickableItems.AttachByPosition(this, Row(shower, this), list.Choose);
        ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(StackPanel)));
        this.ShowChoices(list);
        list.Changed += () =>
        {
            SetValue(HeaderProperty, shower()?.Header ?? "");
            SetValue(HintProperty, shower()?.TemplateHint ?? "");
        };
    }

    private void ShowTheme(ITheme theme)
    {
        var look = TextLooks.Of(theme.IsDark);
        SetValue(ChipBackgroundProperty, TagLook.Paint(look.MentionBackground));
        SetValue(AliasBrushProperty, TagLook.Paint(look.Italic));
    }

    private static ControlTemplate HeaderHintAndRows(Func<bool> headerClicked)
    {
        var header = new FrameworkElementFactory(typeof(Button), HeaderName);
        header.SetValue(FocusableProperty, false);
        header.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
        header.SetValue(HorizontalContentAlignmentProperty, HorizontalAlignment.Left);
        header.SetValue(MarginProperty, new Thickness(0, 0, 0, 4));
        header.SetValue(AutomationProperties.AutomationIdProperty, "MentionHeader");
        header.SetValue(ContentControl.ContentProperty, new TemplateBindingExtension(HeaderProperty));
        header.SetValue(BackgroundProperty, new TemplateBindingExtension(ChipBackgroundProperty));
        header.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((_, e) =>
        {
            headerClicked();
            e.Handled = true;
        }));

        var hint = new FrameworkElementFactory(typeof(TextBlock), HintName);
        hint.SetValue(TextBlock.TextProperty, new TemplateBindingExtension(HintProperty));
        hint.SetValue(AutomationProperties.AutomationIdProperty, "MentionHint");
        hint.SetValue(MarginProperty, new Thickness(0, 0, 0, 4));
        hint.SetResourceReference(TextBlock.ForegroundProperty, HelpBrush);

        var rows = new FrameworkElementFactory(typeof(ScrollViewer));
        rows.SetValue(FocusableProperty, false);
        rows.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        rows.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));

        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.AppendChild(header);
        panel.AppendChild(hint);
        panel.AppendChild(rows);

        var template = new ControlTemplate(typeof(MentionPopupList)) { VisualTree = panel };
        template.Triggers.Add(Collapsed(HeaderProperty, HeaderName));
        template.Triggers.Add(Collapsed(HintProperty, HintName));
        return template;
    }

    private static Trigger Collapsed(DependencyProperty property, string name) => new()
    {
        Property = property,
        Value = "",
        Setters = { new Setter(VisibilityProperty, Visibility.Collapsed, name) },
    };

    // A button that reads as the contact's name and alias, named "Full Name (alias)". It never takes the focus, since a field
    // that loses it closes the list.
    private static FrameworkElementFactory Row(Func<ContactPicking?> shower, MentionPopupList owner)
    {
        var row = ClickableItems.ItemButton();
        row.SetValue(FocusableProperty, false);
        row.SetValue(HorizontalContentAlignmentProperty, HorizontalAlignment.Left);
        row.SetBinding(AutomationProperties.NameProperty, new Binding("."));
        row.SetBinding(ContentControl.ContentProperty, new Binding
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(ListBoxItem), 1),
            Path = new PropertyPath(ItemsControl.AlternationIndexProperty),
            Converter = new RowAt(shower, owner),
        });
        return row;
    }

    private sealed class RowAt(Func<ContactPicking?> shower, MentionPopupList owner) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = new TextBlock();
            if (shower()?.Rows is { } rows && value is int position && position < rows.Count)
            {
                text.Inlines.Add(new Run(rows[position].FullName) { FontWeight = FontWeights.Bold });
                text.Inlines.Add(new Run(" ("));
                var alias = new Run(rows[position].Alias) { FontStyle = FontStyles.Italic };
                alias.SetBinding(TextElement.ForegroundProperty, new Binding { Source = owner, Path = new PropertyPath(AliasBrushProperty) });
                text.Inlines.Add(alias);
                text.Inlines.Add(new Run(")"));
            }

            return text;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
