using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;
using static Memoria.ToDo.Desktop.FormattedView.Tests.FormattedDescription.DescriptionScreen;

namespace Memoria.ToDo.Desktop.FormattedView.Tests.FormattedDescription;

/// <summary>Which card a point on a description makes: the mention the web view finds there, by its number in the page, and none when the answer is stale or nothing is found. The description is laid out but never shown.</summary>
[Unit]
public class FormattedDescriptionCardTests
{
    [WpfFact]
    public async Task Asks_the_web_view_about_the_point_in_the_web_views_own_coordinates()
    {
        using var screen = new DescriptionScreen(inWindow: false);
        screen.Description.MentionCard = mention => CardOf(mention.Address);
        screen.Description.Markdown = $"Ask {AnaMention} now";
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);
        var point = new Point(45, 20);

        await screen.Description.PartAt(point);

        Assert.Equal([screen.InWebView(point)], screen.WebView.MentionQueries);
    }

    [WpfFact]
    public async Task Maps_each_mention_number_to_the_mention_counted_in_the_same_order_as_the_page()
    {
        using var screen = new DescriptionScreen(inWindow: false);
        screen.Description.MentionCard = mention => CardOf(mention.Address);
        screen.Description.Markdown = $"{AnaMention} and {BoMention} and {AnaMention}";

        screen.WebView.MentionUnderPointer = new MentionAt(1, MentionBox);
        var second = await screen.Description.PartAt(new Point(1, 1));
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);
        var first = await screen.Description.PartAt(new Point(1, 1));
        screen.WebView.MentionUnderPointer = new MentionAt(2, MentionBox);
        var third = await screen.Description.PartAt(new Point(1, 1));

        Assert.Equal("contact:b2", ((Border)second!.Card).Tag);
        Assert.Equal("contact:a1", ((Border)first!.Card).Tag);
        Assert.Equal("contact:a1", ((Border)third!.Card).Tag);
    }

    [WpfFact]
    public async Task Counts_a_mention_in_a_list_after_a_heading_by_its_place_in_the_document()
    {
        using var screen = new DescriptionScreen(inWindow: false);
        screen.Description.MentionCard = mention => CardOf(mention.Address);
        screen.Description.Markdown = $"# {AnaMention}\n\n- {BoMention}";
        screen.WebView.MentionUnderPointer = new MentionAt(1, MentionBox);

        var part = await screen.Description.PartAt(new Point(1, 1));

        Assert.Equal("contact:b2", ((Border)part!.Card).Tag);
    }

    [WpfFact]
    public async Task Does_not_count_a_contact_link_that_is_not_a_mention_when_it_maps_the_number()
    {
        using var screen = new DescriptionScreen(inWindow: false);
        screen.Description.MentionCard = mention => CardOf(mention.Address);
        screen.Description.Markdown = $"[Cy](contact:c3) `{AnaMention}` {BoMention}";
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);

        var part = await screen.Description.PartAt(new Point(1, 1));

        Assert.Equal("contact:b2", ((Border)part!.Card).Tag);
    }

    [WpfFact]
    public async Task Makes_no_card_where_the_web_view_finds_no_mention()
    {
        using var screen = new DescriptionScreen(inWindow: false);
        var asked = 0;
        screen.Description.MentionCard = _ => { asked++; return CardOf("any"); };
        screen.Description.Markdown = $"Ask {AnaMention} now";
        screen.WebView.MentionUnderPointer = null;

        Assert.Null(await screen.Description.PartAt(new Point(200, 100)));
        Assert.Equal(0, asked);
    }

    [WpfFact]
    public async Task Makes_no_card_for_a_mention_whose_user_makes_none()
    {
        using var screen = new DescriptionScreen(inWindow: false);
        screen.Description.MentionCard = _ => null;
        screen.Description.Markdown = $"Ask {AnaMention} now";
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);

        Assert.Null(await screen.Description.PartAt(new Point(45, 20)));
    }

    [WpfFact]
    public async Task Makes_no_card_before_a_card_function_is_set()
    {
        using var screen = new DescriptionScreen(inWindow: false);
        screen.Description.Markdown = $"Ask {AnaMention} now";
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);

        Assert.Null(screen.Description.MentionCard);
        Assert.Null(await screen.Description.PartAt(new Point(45, 20)));
    }

    [WpfFact]
    public async Task Makes_the_card_for_the_Markdown_now_shown_after_the_description_changes()
    {
        using var screen = new DescriptionScreen(inWindow: false);
        screen.Description.MentionCard = mention => CardOf(mention.Address);
        screen.Description.Markdown = $"Ask {AnaMention} now";
        screen.Description.Markdown = $"Tell {BoMention} now";
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);

        var part = await screen.Description.PartAt(new Point(45, 20));

        Assert.Equal("contact:b2", ((Border)part!.Card).Tag);
    }

    [WpfFact]
    public async Task Shows_nothing_for_an_answer_that_comes_after_another_Markdown_was_shown()
    {
        using var screen = new DescriptionScreen(inWindow: false);
        var asked = 0;
        screen.Description.MentionCard = mention => { asked++; return CardOf(mention.Address); };
        screen.Description.Markdown = $"Ask {AnaMention} now";
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);
        screen.WebView.HoldMentionAnswers();
        var answer = screen.Description.PartAt(new Point(45, 20));

        screen.Description.Markdown = $"Tell {BoMention} now";
        screen.WebView.ReleaseMentionAnswers();

        Assert.Null(await answer);
        Assert.Equal(0, asked);
    }

    [WpfFact]
    public async Task Shows_nothing_for_an_answer_that_comes_after_the_description_became_a_preview()
    {
        using var screen = new DescriptionScreen(inWindow: false);
        screen.Description.MentionCard = mention => CardOf(mention.Address);
        screen.Description.Markdown = $"Ask {AnaMention} now";
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);
        screen.WebView.HoldMentionAnswers();
        var answer = screen.Description.PartAt(new Point(45, 20));

        screen.Description.IsPreview = true;
        screen.WebView.ReleaseMentionAnswers();

        Assert.Null(await answer);
    }

    [WpfFact]
    public async Task Shows_the_card_of_an_answer_that_comes_while_the_same_page_is_still_shown()
    {
        using var screen = new DescriptionScreen(inWindow: false);
        screen.Description.MentionCard = mention => CardOf(mention.Address);
        screen.Description.Markdown = $"Ask {AnaMention} now";
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);
        screen.WebView.HoldMentionAnswers();
        var answer = screen.Description.PartAt(new Point(45, 20));

        screen.WebView.ReleaseMentionAnswers();

        Assert.Equal("contact:a1", ((Border)(await answer)!.Card).Tag);
    }

    [WpfFact]
    public async Task Makes_no_card_once_the_web_view_failed_and_the_Markdown_shows_as_source()
    {
        using var screen = new DescriptionScreen(inWindow: false);
        screen.Description.MentionCard = mention => CardOf(mention.Address);
        screen.Description.Markdown = $"Ask {AnaMention} now";
        screen.WebView.MentionUnderPointer = new MentionAt(0, MentionBox);
        screen.WebView.Fail("The WebView2 Runtime is missing.");

        Assert.Null(await screen.Description.PartAt(new Point(45, 20)));
    }

    [WpfFact]
    public async Task Keeps_showing_the_formatted_page_after_a_lookup_that_found_nothing()
    {
        using var screen = new DescriptionScreen(inWindow: false);
        screen.Description.MentionCard = mention => CardOf(mention.Address);
        screen.Description.Markdown = $"Ask {AnaMention} now";
        screen.WebView.MentionUnderPointer = null;

        var part = await screen.Description.PartAt(new Point(45, 20));

        Assert.Null(part);
        Assert.Same(screen.WebView.Element, screen.Description.Content);
        Assert.DoesNotContain(screen.Texts, text => text.Contains("Descriptions can't show formatted"));
        Assert.Empty(screen.OpenFailures);
    }
}
