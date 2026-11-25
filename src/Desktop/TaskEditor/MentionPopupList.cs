using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Rendering;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The contact list, only while open: a header that is the contact in the current template as a <see cref="MentionChip"/> and moves
/// to the next template when clicked, a hint line in the theme's tertiary text color, and a row of the contact's bold name and
/// italic alias for each contact. Nothing in it takes the keyboard focus. Rows and header come from the <see cref="ContactPicking"/>
/// that shows the list; a list with no header shows none. The header follows a switch between light and dark.
/// </summary>
internal sealed class MentionPopupList : ListBox
{
    private const string TextBrush = "TextFillColorPrimaryBrush";
    private const string HelpBrush = "TextFillColorTertiaryBrush";
    private const string HeaderName = "Header";
    private const string HintName = "Hint";

    private static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register("Header", typeof(string), typeof(MentionPopupList), new PropertyMetadata(""));

    // The header's chip; null while there's no header. A chip doesn't follow the theme, so a theme switch makes a new one.
    private static readonly DependencyProperty HeaderChipProperty =
        DependencyProperty.Register("HeaderChip", typeof(UIElement), typeof(MentionPopupList));

    private static readonly DependencyProperty HintProperty =
        DependencyProperty.Register("Hint", typeof(string), typeof(MentionPopupList), new PropertyMetadata(""));

    // The alias's italic color, from the theme; the follower keeps it current while loaded.
    private static readonly DependencyProperty AliasBrushProperty =
        DependencyProperty.Register("AliasBrush", typeof(Brush), typeof(MentionPopupList));

    public MentionPopupList(ChoiceList list, Func<ContactPicking?> shower, Func<bool> headerClicked, ITheme theme)
    {
        void ShowHeader() => ShowHeaderOf(shower()?.Header ?? "", theme);
        _ = new ThemeFollower(this, theme, () =>
        {
            ShowTheme(theme);
            ShowHeader();
        });
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
            ShowHeader();
            SetValue(HintProperty, shower()?.TemplateHint ?? "");
        };
    }

    private void ShowTheme(ITheme theme) =>
        SetValue(AliasBrushProperty, TagLook.Paint(TextLooks.Of(theme.IsDark).Italic));

    private void ShowHeaderOf(string header, ITheme theme)
    {
        SetValue(HeaderProperty, header);
        SetValue(HeaderChipProperty, header == "" ? null : new MentionChip(header, theme.IsDark));
    }

    private static ControlTemplate HeaderHintAndRows(Func<bool> headerClicked)
    {
        // A click on the chip moves to the next template. The host never takes the focus, since a field that loses it closes the list.
        var header = new FrameworkElementFactory(typeof(ContentControl), HeaderName);
        header.SetValue(FocusableProperty, false);
        header.SetValue(BackgroundProperty, Brushes.Transparent);
        header.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Left);
        header.SetValue(MarginProperty, new Thickness(0, 0, 0, 4));
        header.SetResourceReference(ForegroundProperty, TextBrush);
        header.SetValue(AutomationProperties.AutomationIdProperty, "MentionHeader");
        header.SetValue(ContentControl.ContentProperty, new TemplateBindingExtension(HeaderChipProperty));
        header.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler((_, e) =>
        {
            headerClicked();
            e.Handled = true;
        }));

        var hint = new FrameworkElementFactory(typeof(TextBlock), HintName);
        hint.SetValue(TextBlock.TextProperty, new TemplateBindingExtension(HintProperty));
        hint.SetValue(AutomationProperties.AutomationIdProperty, "MentionHint");
        hint.SetValue(MarginProperty, new Thickness(5, 0, 0, 4));
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
