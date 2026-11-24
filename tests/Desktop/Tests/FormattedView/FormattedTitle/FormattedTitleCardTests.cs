using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;
using static Memoria.ToDo.Desktop.FormattedView.Tests.FormattedTitle.TitleScreen;

namespace Memoria.ToDo.Desktop.FormattedView.Tests.FormattedTitle;

/// <summary>Which card a point on a title makes: the mention's chip under the point, boxed by that chip, and none anywhere else. The title is laid out but never shown.</summary>
[Unit]
public class FormattedTitleCardTests
{
    [WpfFact]
    public async Task Makes_the_card_of_a_mention_with_escaped_punctuation_for_any_point_on_its_one_chip_with_that_chip_as_the_box()
    {
        using var screen = new TitleScreen(inWindow: false);
        screen.Title.MentionCard = mention => CardOf(mention.Address);
        screen.Title.Markdown = $"Ask {EscapedMention} now";
        var chip = screen.ChipBox("Ana Pop (SDE)");
        var y = chip.Top + (chip.Height / 2);

        foreach (var x in new[] { chip.Left + 1, chip.Left + (chip.Width * 0.25), chip.Left + (chip.Width / 2), chip.Left + (chip.Width * 0.75), chip.Right - 1 })
        {
            var part = await screen.Title.PartAt(new Point(x, y));

            Assert.Equal("contact:a1", ((Border)part!.Card).Tag);
            Assert.InRange(part.Box.Left, chip.Left - 0.5, chip.Left + 0.5);
            Assert.InRange(part.Box.Top, chip.Top - 0.5, chip.Top + 0.5);
            Assert.InRange(part.Box.Right, chip.Right - 0.5, chip.Right + 0.5);
            Assert.InRange(part.Box.Bottom, chip.Bottom - 0.5, chip.Bottom + 0.5);
        }
    }

    [WpfFact]
    public async Task Makes_the_card_of_a_mention_with_a_bold_part_for_any_point_on_its_one_chip()
    {
        using var screen = new TitleScreen(inWindow: false);
        screen.Title.MentionCard = mention => CardOf(mention.Address);
        screen.Title.Markdown = "Ask [Ana **Pop** Li](contact:a1 \"{fullName}\") now";
        var chip = screen.ChipBox("Ana Pop Li");
        var y = chip.Top + (chip.Height / 2);

        foreach (var x in new[] { chip.Left + 1, chip.Left + (chip.Width / 2), chip.Right - 1 })
        {
            var part = await screen.Title.PartAt(new Point(x, y));

            Assert.Equal("contact:a1", ((Border)part!.Card).Tag);
            Assert.InRange(part.Box.Left, chip.Left - 0.5, chip.Left + 0.5);
            Assert.InRange(part.Box.Right, chip.Right - 0.5, chip.Right + 0.5);
        }
    }

    [WpfFact]
    public async Task Makes_the_card_of_the_mention_chip_under_the_point_from_its_address_and_template_and_returns_it_with_the_chip_background_as_the_box()
    {
        using var screen = new TitleScreen(inWindow: false);
        var asked = new List<Mention>();
        screen.Title.MentionCard = mention => { asked.Add(mention); return CardOf(mention.Address); };
        screen.Title.Markdown = $"Ask {AnaMention} now";
        var chip = screen.ChipBox("Ana");

        var part = await screen.Title.PartAt(Middle(chip));

        var only = Assert.Single(asked);
        Assert.Equal("contact:a1", only.Address);
        Assert.Equal("{firstName}", only.Template);
        Assert.NotNull(part);
        Assert.Equal("contact:a1", ((Border)part.Card).Tag);
        Assert.InRange(part.Box.Left, chip.Left - 0.5, chip.Left + 0.5);
        Assert.InRange(part.Box.Top, chip.Top - 0.5, chip.Top + 0.5);
        Assert.InRange(part.Box.Right, chip.Right - 0.5, chip.Right + 0.5);
        Assert.InRange(part.Box.Bottom, chip.Bottom - 0.5, chip.Bottom + 0.5);
    }

    [WpfFact]
    public async Task Makes_the_card_of_the_mention_for_a_point_on_the_chip_padding_beside_its_text()
    {
        using var screen = new TitleScreen(inWindow: false);
        screen.Title.MentionCard = mention => CardOf(mention.Address);
        screen.Title.Markdown = $"Ask {AnaMention} now";
        var chip = screen.ChipBox("Ana");

        var onLeftPadding = await screen.Title.PartAt(new Point(chip.Left + 1, chip.Top + (chip.Height / 2)));
        var onRightPadding = await screen.Title.PartAt(new Point(chip.Right - 1, chip.Top + (chip.Height / 2)));

        Assert.Equal("contact:a1", ((Border)onLeftPadding!.Card).Tag);
        Assert.Equal("contact:a1", ((Border)onRightPadding!.Card).Tag);
    }

    [WpfFact]
    public async Task Makes_the_card_of_the_mention_the_point_is_on_when_a_title_has_text_around_and_two_mentions()
    {
        using var screen = new TitleScreen(inWindow: false);
        screen.Title.MentionCard = mention => CardOf(mention.Address);
        screen.Title.Markdown = $"Hi {AnaMention} and {BoMention} bye";

        var second = await screen.Title.PartAt(Middle(screen.ChipBox("Bo Li")));
        var first = await screen.Title.PartAt(Middle(screen.ChipBox("Ana")));

        Assert.Equal("contact:b2", ((Border)second!.Card).Tag);
        Assert.Equal("contact:a1", ((Border)first!.Card).Tag);
        Assert.NotEqual(first.Box, second.Box);
    }

    [WpfFact]
    public async Task Makes_no_card_for_a_point_on_the_text_before_between_and_after_two_mentions()
    {
        using var screen = new TitleScreen(inWindow: false);
        screen.Title.MentionCard = mention => CardOf(mention.Address);
        screen.Title.Markdown = $"Hi {AnaMention} and {BoMention} bye";

        Assert.Null(await screen.Title.PartAt(screen.InsideStartOf("Hi")));
        Assert.Null(await screen.Title.PartAt(screen.InsideStartOf("and")));
        Assert.Null(await screen.Title.PartAt(screen.InsideStartOf("bye")));
    }

    [WpfFact]
    public async Task Makes_no_card_for_a_point_on_text_that_is_not_a_mention()
    {
        using var screen = new TitleScreen(inWindow: false);
        var asked = 0;
        screen.Title.MentionCard = _ => { asked++; return CardOf("any"); };
        screen.Title.Markdown = $"Ask {AnaMention} about [the docs](https://example.com/docs)";

        Assert.Null(await screen.Title.PartAt(screen.InsideStartOf("Ask")));
        Assert.Null(await screen.Title.PartAt(screen.InsideStartOf("the docs")));
        Assert.Equal(0, asked);
    }

    [WpfFact]
    public async Task Makes_no_card_for_a_point_below_the_text()
    {
        using var screen = new TitleScreen(inWindow: false);
        screen.Title.MentionCard = _ => CardOf("any");
        screen.Title.Markdown = $"Ask {AnaMention}";

        Assert.Null(await screen.Title.PartAt(new Point(Middle(screen.ChipBox("Ana")).X, screen.Title.ActualHeight + 50)));
    }

    [WpfFact]
    public async Task Makes_no_card_for_a_mention_whose_user_makes_none()
    {
        using var screen = new TitleScreen(inWindow: false);
        screen.Title.MentionCard = _ => null;
        screen.Title.Markdown = $"Ask {AnaMention} now";

        Assert.Null(await screen.Title.PartAt(Middle(screen.ChipBox("Ana"))));
    }

    [WpfFact]
    public async Task Makes_no_card_before_a_card_function_is_set()
    {
        using var screen = new TitleScreen(inWindow: false);
        screen.Title.Markdown = $"Ask {AnaMention} now";

        Assert.Null(screen.Title.MentionCard);
        Assert.Null(await screen.Title.PartAt(Middle(screen.ChipBox("Ana"))));
    }

    [WpfFact]
    public async Task Makes_no_card_for_the_name_of_a_task_reference_where_mentions_are_plain_text()
    {
        using var screen = new TitleScreen(inWindow: false);
        screen.Title.MentionCard = _ => CardOf("any");
        screen.Title.Markdown = "Ask Ana now";

        Assert.Null(await screen.Title.PartAt(screen.InsideStartOf("Ana")));
    }

    [WpfFact]
    public async Task Makes_the_card_for_the_Markdown_now_shown_after_the_title_changes()
    {
        using var screen = new TitleScreen(inWindow: false);
        screen.Title.MentionCard = mention => CardOf(mention.Address);
        screen.Title.Markdown = $"Ask {AnaMention} now";
        screen.Title.Markdown = $"Tell {BoMention} now";

        var part = await screen.Title.PartAt(Middle(screen.ChipBox("Bo Li")));

        Assert.Equal("contact:b2", ((Border)part!.Card).Tag);
    }
}
