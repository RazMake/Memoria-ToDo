using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Desktop.FormattedView.Tests;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// What the open "@" list shows above its rows: the header, which is the Formatted view's mention chip of the contact moved to in the
/// current template, and the hint line under it, in the theme's tertiary text color and 5 pixels in from the list's left edge. The
/// list is laid out but never shown, so these need no desktop; a click on the header and the list's place on screen are the UX test's.
/// </summary>
[Unit]
public class MentionPopupListTests
{
    private const string Italic = "{fullName} (_{alias}_)";
    private const string Plain = "{fullName}";

    private readonly EditorServices _services = new();
    private readonly ChoiceList _list = new();
    private readonly FakeTextField _field = new();
    private readonly MentionPicking _picking;
    private readonly MentionPopupList _popup;

    public MentionPopupListTests()
    {
        _picking = new MentionPicking(_field, _list, new PopupLists(_list), _services.Contacts);
        _popup = new MentionPopupList(_list, () => _picking, () => _picking.NextTemplate(), _services.Theme);
    }

    private void Open(params string[] templates)
    {
        _services.UseContacts(templates, ContactFiles.Person("ana", "Ana Pop", ("alias", "anap")));
        _field.Type("@");
        Layout();
    }

    private void Layout()
    {
        _popup.Measure(new Size(300, 400));
        _popup.Arrange(new Rect(0, 0, 300, 400));
        _popup.UpdateLayout();
        _popup.Dispatcher.Invoke(() => { }, DispatcherPriority.Background); // Bindings fill the header and the rows.
        _popup.UpdateLayout();
    }

    private MentionChip Header => VisualTree.Visuals(_popup).OfType<MentionChip>().Single();

    private TextBlock Hint => VisualTree.Visuals(_popup).OfType<TextBlock>()
        .Single(block => AutomationProperties.GetAutomationId(block) == EditorFields.MentionHint);

    private bool HintShows => Hint.Visibility == Visibility.Visible && Hint.ActualHeight > 0;

    [WpfFact]
    public void The_hint_line_is_in_the_themes_tertiary_text_color_not_the_secondary_one()
    {
        var tertiary = new SolidColorBrush(Colors.Red);
        _popup.Resources["TextFillColorTertiaryBrush"] = tertiary;
        _popup.Resources["TextFillColorSecondaryBrush"] = new SolidColorBrush(Colors.Blue);

        Open(Italic, Plain);

        Assert.Same(tertiary, Hint.Foreground);
    }

    [WpfFact]
    public void The_hint_line_follows_a_change_of_the_themes_tertiary_color_because_it_is_a_resource_reference()
    {
        _popup.Resources["TextFillColorTertiaryBrush"] = new SolidColorBrush(Colors.Red);
        Open(Italic, Plain);
        var other = new SolidColorBrush(Colors.Green);

        _popup.Resources["TextFillColorTertiaryBrush"] = other;

        Assert.Same(other, Hint.Foreground);
    }

    [WpfFact]
    public void The_hint_line_sits_5_pixels_in_from_the_lists_left_edge()
    {
        Open(Italic, Plain);

        Assert.Equal(5, Hint.TranslatePoint(new Point(0, 0), _popup).X);
    }

    [WpfFact]
    public void The_hint_line_reads_Tab_or_click_next_template_with_two_or_more_templates()
    {
        Open(Italic, Plain);

        Assert.True(HintShows);
        Assert.Equal("Tab or click: next template", Hint.Text);
    }

    [WpfFact]
    public void The_hint_line_does_not_show_with_one_template()
    {
        Open(Italic);

        Assert.False(HintShows);
    }

    [WpfFact]
    public void The_header_is_a_mention_chip_that_shows_the_alias_in_italic_with_no_underscore_showing()
    {
        Open(Italic, Plain);

        Assert.Equal("Ana Pop (anap)", ChipRuns.ShownBy(Header));
        Assert.True(RunStyles.IsItalic(ChipRuns.Run(Header, "anap")), "'anap' isn't italic.");
        Assert.False(RunStyles.IsItalic(ChipRuns.Run(Header, "Ana Pop")), "'Ana Pop' is italic too.");
    }

    [WpfFact]
    public void The_headers_text_is_in_the_themes_primary_text_color()
    {
        var primary = new SolidColorBrush(Colors.Red);
        _popup.Resources["TextFillColorPrimaryBrush"] = primary;

        Open(Italic, Plain);

        Assert.Same(primary, ChipRuns.Run(Header, "Ana Pop").Foreground);
    }

    [WpfFact]
    public void The_headers_screen_reader_name_is_its_text_without_Markdown_characters()
    {
        Open(Italic, Plain);

        Assert.Equal("Ana Pop (anap)", AutomationProperties.GetName(Header));
    }

    [WpfTheory]
    [InlineData("**{fullName}**", "bold")]
    [InlineData("~~{fullName}~~", "strikethrough")]
    [InlineData("`{fullName}`", "code")]
    [InlineData(":red[{fullName}]", "color")]
    public void The_headers_text_is_styled_by_the_templates_bold_strikethrough_code_and_color(string template, string style)
    {
        Open(template);

        var run = ChipRuns.Run(Header, "Ana Pop");
        Assert.Equal("Ana Pop", ChipRuns.ShownBy(Header));
        Assert.True(style switch
        {
            "bold" => RunStyles.IsBold(run),
            "strikethrough" => RunStyles.IsStruckThrough(run),
            "code" => RunStyles.IsMonospaced(run),
            _ => RunStyles.HasOwnColor(run),
        }, $"The header isn't styled as {style}.");
    }

    [WpfFact]
    public void The_header_shows_the_next_templates_text_after_Tab()
    {
        Open(Italic, "{alias}");

        _picking.NextTemplate();
        Layout();

        Assert.Equal("anap", ChipRuns.ShownBy(Header));
    }

    [WpfFact]
    public void The_header_follows_a_switch_between_light_and_dark_while_the_list_is_open()
    {
        _popup.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        Open(Italic, Plain);
        var light = Assert.IsType<SolidColorBrush>(Header.Background).Color;

        _services.Theme.Switch(true);
        Layout();

        var dark = Assert.IsType<SolidColorBrush>(Header.Background).Color;
        Assert.Equal(Assert.IsType<SolidColorBrush>(new MentionChip("x", dark: true).Background).Color, dark);
        Assert.NotEqual(light, dark);
    }
}
