using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;
using static Memoria.ToDo.Desktop.FormattedView.Tests.FormattedTitle.TitleScreen;

namespace Memoria.ToDo.Desktop.FormattedView.Tests;

/// <summary>
/// The chip a mention shows as: its text formatted as a title formats it, on the theme's mention background, with room around it.
/// The chip is measured but never shown, so these need no desktop.
/// </summary>
[Unit]
public class MentionChipTests
{
    [WpfFact]
    public void Shows_the_alias_in_italic_with_no_underscores_showing()
    {
        var chip = new MentionChip("Ana Pop (_anap_)", dark: false);

        Assert.Equal("Ana Pop (anap)", ChipRuns.ShownBy(chip));
        Assert.True(RunStyles.IsItalic(ChipRuns.Run(chip, "anap")), "'anap' isn't italic.");
        Assert.False(RunStyles.IsItalic(ChipRuns.Run(chip, "Ana Pop")), "'Ana Pop' is italic too.");
    }

    [WpfFact]
    public void Shows_bold_text_in_bold_with_no_asterisks_showing()
    {
        var chip = new MentionChip("Ana **Pop** Li", dark: false);

        Assert.Equal("Ana Pop Li", ChipRuns.ShownBy(chip));
        Assert.True(RunStyles.IsBold(ChipRuns.Run(chip, "Pop")), "'Pop' isn't bold.");
        Assert.False(RunStyles.IsBold(ChipRuns.Run(chip, "Ana")), "'Ana' is bold too.");
    }

    [WpfFact]
    public void Shows_struck_through_text_struck_through_with_no_tildes_showing()
    {
        var chip = new MentionChip("Ana ~~Pop~~ Li", dark: false);

        Assert.Equal("Ana Pop Li", ChipRuns.ShownBy(chip));
        Assert.True(RunStyles.IsStruckThrough(ChipRuns.Run(chip, "Pop")), "'Pop' isn't struck through.");
        Assert.False(RunStyles.IsStruckThrough(ChipRuns.Run(chip, "Ana")), "'Ana' is struck through too.");
    }

    [WpfFact]
    public void Shows_code_in_the_code_font_with_no_backticks_showing()
    {
        var chip = new MentionChip("Ana `Pop` Li", dark: false);

        Assert.Equal("Ana Pop Li", ChipRuns.ShownBy(chip));
        Assert.True(RunStyles.IsMonospaced(ChipRuns.Run(chip, "Pop")), "'Pop' isn't in a monospaced font.");
        Assert.Equal(TextLooks.CodeFont, ChipRuns.Run(chip, "Pop").FontFamily.Source);
        Assert.False(RunStyles.IsMonospaced(ChipRuns.Run(chip, "Ana")), "'Ana' is monospaced too.");
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shows_a_palette_color_in_that_color_for_the_theme_with_no_directive_showing(bool dark)
    {
        var chip = new MentionChip("Ana :red[Pop] Li", dark);

        Assert.Equal("Ana Pop Li", ChipRuns.ShownBy(chip));
        Assert.Equal(Parse(dark ? Red.Dark : Red.Light), RunStyles.ColorOf(ChipRuns.Run(chip, "Pop")));
        Assert.False(RunStyles.HasOwnColor(ChipRuns.Run(chip, "Ana")), "'Ana' has a color of its own.");
    }

    [WpfFact]
    public void Shows_escaped_punctuation_as_written_and_not_as_Markdown()
    {
        var chip = new MentionChip("Ana \\*QA\\*", dark: false);

        Assert.Equal("Ana *QA*", ChipRuns.ShownBy(chip));
        Assert.False(RunStyles.IsItalic(ChipRuns.Run(chip, "QA")), "'QA' is italic.");
    }

    [WpfFact]
    public void Shows_plain_text_in_the_normal_text_color()
    {
        var chip = new MentionChip("Ana Pop", dark: false);

        Assert.Equal("Ana Pop", ChipRuns.ShownBy(chip));
        Assert.False(RunStyles.HasOwnColor(ChipRuns.Run(chip, "Ana")), "The text has a color of its own.");
        Assert.Equal(DependencyProperty.UnsetValue, chip.ReadLocalValue(TextElement.ForegroundProperty));
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shows_italic_and_code_in_the_normal_text_color_as_a_mention_does_everywhere(bool dark)
    {
        var chip = new MentionChip("Ana (_anap_) `Pop`", dark);

        Assert.False(RunStyles.HasOwnColor(ChipRuns.Run(chip, "anap")), "The italic text has a color of its own.");
        Assert.False(RunStyles.HasOwnColor(ChipRuns.Run(chip, "Pop")), "The code has a color of its own.");
    }

    [WpfTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shows_on_the_text_look_mention_background_of_its_theme(bool dark)
    {
        var chip = new MentionChip("Ana Pop", dark);

        var expected = Parse(TextLooks.Of(dark).MentionBackground);
        var background = Assert.IsType<SolidColorBrush>(chip.Background);
        Assert.Equal(expected.R, background.Color.R);
        Assert.Equal(expected.G, background.Color.G);
        Assert.Equal(expected.B, background.Color.B);
        Assert.Equal(1.0, background.Opacity * background.Color.A / 255, 2);
    }

    [WpfFact]
    public void Shows_a_different_background_in_the_dark_theme_than_in_the_light_theme()
    {
        var light = Assert.IsType<SolidColorBrush>(new MentionChip("Ana Pop", dark: false).Background);
        var dark = Assert.IsType<SolidColorBrush>(new MentionChip("Ana Pop", dark: true).Background);

        Assert.NotEqual(light.Color, dark.Color);
    }

    [WpfFact]
    public void Leaves_the_text_look_room_around_the_text_and_rounds_its_corners()
    {
        var chip = new MentionChip("Ana Pop", dark: false);

        Assert.Equal(
            new Thickness(TextLooks.MentionPaddingSides, TextLooks.MentionPaddingTop, TextLooks.MentionPaddingSides, TextLooks.MentionPaddingBottom),
            chip.Padding);
        Assert.Equal(new CornerRadius(TextLooks.MentionCornerRadius), chip.CornerRadius);
    }

    [WpfFact]
    public void Takes_no_more_line_height_than_the_text_alone_and_the_room_beside_the_text_adds_to_its_width()
    {
        var chip = new MentionChip("Ana Pop", dark: false);
        var plain = new TextBlock { Text = "Ana Pop" };

        chip.Measure(new Size(1000, 1000));
        plain.Measure(new Size(1000, 1000));

        Assert.Equal(plain.DesiredSize.Height, chip.DesiredSize.Height, 2);
        Assert.Equal(plain.DesiredSize.Width + (2 * TextLooks.MentionPaddingSides), chip.DesiredSize.Width, 2);
    }

    [WpfFact]
    public void Sits_on_the_baseline_of_its_text()
    {
        var chip = new MentionChip("Ana Pop", dark: false);
        var plain = new TextBlock { Text = "Ana Pop" };

        chip.Measure(new Size(1000, 1000));

        Assert.Equal(plain.FontFamily.Baseline * plain.FontSize, TextBlock.GetBaselineOffset(chip), 2);
    }

    [WpfFact]
    public void Takes_its_font_from_where_it_sits_except_for_code()
    {
        var chip = new MentionChip("Ana `Pop`", dark: false);
        var parent = new Border { Child = chip };
        TextElement.SetFontSize(parent, 21);
        TextElement.SetFontFamily(parent, new FontFamily("Courier New"));

        var plain = ChipRuns.Run(chip, "Ana");
        Assert.Equal(21, plain.FontSize);
        Assert.Equal("Courier New", plain.FontFamily.Source);
        Assert.Equal(TextLooks.CodeFont, ChipRuns.Run(chip, "Pop").FontFamily.Source);
    }

    [WpfTheory]
    [InlineData("Ana Pop (_anap_)", "Ana Pop (anap)")]
    [InlineData("**Ana** `Pop` :red[Li]", "Ana Pop Li")]
    [InlineData("Ana \\*QA\\*", "Ana *QA*")]
    public void Names_itself_by_the_text_it_shows_with_no_Markdown_characters(string text, string name)
    {
        var chip = new MentionChip(text, dark: false);

        Assert.Equal(name, AutomationProperties.GetName(chip));
    }

    [WpfFact]
    public void Rejects_a_null_text()
    {
        Assert.Throws<ArgumentNullException>(() => new MentionChip(null!, dark: false));
    }
}
