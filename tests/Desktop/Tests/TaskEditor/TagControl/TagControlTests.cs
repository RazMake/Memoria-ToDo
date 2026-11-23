using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Platform;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests.TagControl;

/// <summary>
/// The tag control and the read-only tag badges, as a window that hosts them shows them. Each test follows one path a user takes:
/// the pill, the menu by mouse, Search, Custom with its colors, Custom's Enter, the keys, the colors' keys, the calls a window
/// around the control makes, the tag box's text rules, and the badges. Which tags the menu lists, what Search matches and
/// what each key does to the menu are in the unit tests of the menu model and the tags model.
/// </summary>
[UX("TagControl")]
[Collection(ScreenTests.Collection)]
public class TagControlTests
{
    [WpfFact]
    public void The_pill_shows_No_tag_or_the_tag_in_its_color_and_the_x_clears_it()
    {
        using var host = new TagEditorWindow();
        Assert.Equal(TagEditor.NoTagText, host.PillText);
        Assert.False(TagControls.HasClearButton(host.Editor));
        Assert.Null(host.Editor.TaskTag);

        host.ClickPill();
        Assert.True(TagControls.MenuIsOpen(host.Editor));
        host.Editor.TaskTag = "x";
        Assert.True(TagControls.MenuIsOpen(host.Editor));
        host.ClickPill();
        Assert.False(TagControls.MenuIsOpen(host.Editor));

        host.Editor.TaskTag = "home";
        Assert.Equal("home", host.PillText);
        Assert.True(TagControls.HasClearButton(host.Editor));
        host.Editor.TaskTag = null;
        Assert.Equal(TagEditor.NoTagText, host.PillText);
        Assert.False(TagControls.HasClearButton(host.Editor));
        Assert.Empty(host.Changes);

        host.Services.TagColors.Seed("work", "teal-3");
        host.Editor.TaskTag = "work";
        var teal = TagColors.Find("teal-3")!;
        Assert.Equal(TagControls.Expected(teal, dark: false), TagControls.PillFill(host.Editor));
        host.Services.Theme.Switch(true);
        Assert.Equal(TagControls.Expected(teal, dark: true), TagControls.PillFill(host.Editor));

        TagEditorWindow.Focus(TagControls.PillButton(host.Editor));
        host.ClickClear();
        Assert.Equal(TagEditor.NoTagText, host.PillText);
        Assert.False(TagControls.HasClearButton(host.Editor));
        Assert.Null(host.Editor.TaskTag);
        var (tag, tagThen, kept) = Assert.Single(host.Changes);
        Assert.Null(tag);
        Assert.Null(tagThen);
        Assert.Empty(kept);
        Assert.False(TagControls.MenuIsOpen(host.Editor));
        Assert.Equal(TagControls.Pill, TagControls.FocusedId());

        host.ClickPill();
        Assert.True(TagControls.MenuIsOpen(host.Editor));
        host.ClickPill();
        Assert.False(TagControls.MenuIsOpen(host.Editor));
        Assert.Single(host.Changes);
    }

    [WpfFact]
    public void A_click_on_a_top_tag_in_the_menu_replaces_the_tag()
    {
        using var host = new TagEditorWindow("b");
        host.Services.UseTag("a", 3);
        host.Services.UseTag("b", 2);
        host.Services.UseTag("c");
        host.Services.TagColors.Seed("a", "teal-3");

        host.ClickPill();
        Assert.Equal(["a", "c"], TagControls.MenuTags(host.Editor));
        Assert.Equal(TagControls.Expected(TagColors.Find("teal-3")!, dark: false), TagControls.MenuFillOf(host.Editor, "a"));
        Assert.True(TagControls.SearchBox(host.Editor).IsVisible);
        Assert.True(TagControls.CustomItem(host.Editor).IsVisible);

        OpenWindows.Click(TagControls.MenuItem(host.Editor, "a"));
        Assert.Equal("a", host.Editor.TaskTag);
        Assert.Equal("a", host.PillText);
        var (tag, tagThen, kept) = Assert.Single(host.Changes);
        Assert.Equal("a", tag);
        Assert.Equal("a", tagThen);
        Assert.Empty(kept);
        Assert.False(TagControls.MenuIsOpen(host.Editor));
        Assert.Equal(TagControls.Pill, TagControls.FocusedId());

        host.ClickPill();
        host.Services.UseTag("d", 5);
        Assert.Equal(["d", "b", "c"], TagControls.MenuTags(host.Editor));
        host.Press(Key.Escape);

        host.ClickClear();
        host.ClickPill();
        OpenWindows.Click(TagControls.MenuItem(host.Editor, "d"));
        Assert.Equal("d", host.PillText);
        Assert.True(TagControls.HasClearButton(host.Editor));
        Assert.Equal("d", host.Changes[^1].Tag);
    }

    [WpfFact]
    public void Search_in_the_menu_lists_the_matches_and_a_click_on_one_replaces_the_tag()
    {
        using var host = new TagEditorWindow("errand");
        host.Services.UseTag("errand", 2);
        host.Services.UseTag("echo");
        host.Services.UseTag("work");
        host.ClickPill();

        TextFields.Type(TagControls.SearchBox(host.Editor), "e");
        Assert.Equal(["echo"], TagControls.SearchMatches(host.Editor));
        OpenWindows.Click(TagControls.SearchMatch(host.Editor, "echo"));
        Assert.Equal("echo", host.Editor.TaskTag);
        Assert.Equal("echo", Assert.Single(host.Changes).Tag);
        Assert.False(TagControls.MenuIsOpen(host.Editor));
        Assert.False(TagControls.SearchMatchesAreOpen(host.Editor));
        Assert.Equal(TagControls.Pill, TagControls.FocusedId());

        host.ClickPill();
        TextFields.Type(TagControls.SearchBox(host.Editor), "ech");
        Assert.Empty(TagControls.SearchMatches(host.Editor));
        Assert.NotNull(TagControls.NoMatchLine(host.Editor));

        TextFields.Type(TagControls.SearchBox(host.Editor), "zzz");
        Assert.Equal("No matching tags", OpenWindows.TextOf(TagControls.NoMatchLine(host.Editor)!));
        Assert.Empty(TagControls.SearchMatches(host.Editor));

        TextFields.Type(TagControls.SearchBox(host.Editor), "Big Errand");
        host.Press(Key.Escape);
        host.ClickPill();
        Assert.Equal("echo", host.Editor.TaskTag);
        Assert.Single(host.Changes);
        Assert.Equal("", TagControls.SearchBox(host.Editor).Text);
    }

    [WpfFact]
    public void Custom_shows_the_colors_while_the_box_holds_text_and_a_click_on_a_color_keeps_it_and_replaces_the_tag()
    {
        using var host = new TagEditorWindow("mine");
        host.Services.UseTag("home");
        host.Services.TagColors.Seed("home", "teal-2");

        host.OpenCustom();
        Assert.False(TagControls.MenuIsOpen(host.Editor));
        Assert.True(TagControls.CustomIsOpen(host.Editor));
        Assert.Equal(EditorFields.Tags, EditorFields.FocusedFieldName());
        Assert.False(TagControls.ColorsAreShowing(host.Editor));

        host.Type("errand");
        Assert.True(TagControls.ColorsAreShowing(host.Editor));
        Assert.Equal(TagColors.All.Select(color => color.Name), TagControls.ColorButtons(host.Editor).Select(color => color.Name));
        var names = TagColors.All.Select(color => color.Name).ToList();
        var positions = names.Select(name => TagControls.PositionOf(host.Editor, name)).ToList();
        var columns = positions.Select(p => Math.Round(p.X)).Distinct().Order().ToList();
        var rows = positions.Select(p => Math.Round(p.Y)).Distinct().Order().ToList();
        Assert.Equal(10, columns.Count);
        Assert.Equal(3, rows.Count);
        for (var k = 0; k < names.Count; k++)
        {
            Assert.Equal(columns[k / 3], Math.Round(positions[k].X));
            Assert.Equal(rows[k % 3], Math.Round(positions[k].Y));
        }

        var red = TagColors.Find("red-2")!;
        Assert.Equal(red.Light.Background, TagControls.SwatchOf(host.Editor, "red-2"));
        host.Services.Theme.Switch(true);
        Assert.Equal(red.Dark.Background, TagControls.SwatchOf(host.Editor, "red-2"));

        host.Type("");
        Assert.False(TagControls.ColorsAreShowing(host.Editor));
        host.Type("home");
        Assert.Equal("teal-2", TagControls.MarkedColor(host.Editor));

        host.Type("errand");
        OpenWindows.Click(TagControls.ColorButton(host.Editor, "red-2"));
        var (tag, tagThen, keptThen) = Assert.Single(host.Changes);
        Assert.Equal("errand", tag);
        Assert.Equal("errand", tagThen);
        Assert.Contains(("errand", "red-2"), keptThen);
        Assert.False(TagControls.CustomIsOpen(host.Editor));
        Assert.Equal("errand", host.PillText);
        Assert.Equal(TagControls.Pill, TagControls.FocusedId());
    }

    [WpfFact]
    public void Enter_in_Custom_chooses_the_typed_tag_and_a_failed_color_leaves_Custom_open_with_what_was_typed()
    {
        using var host = new TagEditorWindow("mine");
        host.Services.UseTag("Work");
        host.OpenCustom();
        host.Type("WORK");
        host.Focus(EditorFields.Tags);
        host.Press(Key.Enter);
        Assert.Equal("Work", host.Editor.TaskTag);
        Assert.Equal("Work", Assert.Single(host.Changes).Tag);
        Assert.Empty(host.Services.TagColors.Kept);
        Assert.False(TagControls.CustomIsOpen(host.Editor));

        host.OpenCustom();
        host.Type("work");
        host.Focus(EditorFields.Tags);
        host.Press(Key.Enter);
        Assert.Equal("Work", host.Editor.TaskTag);
        Assert.Single(host.Changes);
        Assert.False(TagControls.CustomIsOpen(host.Editor));
        Assert.Equal("Work", host.PillText);

        host.OpenCustom();
        host.Type("");
        host.Focus(EditorFields.Tags);
        host.Press(Key.Back);
        Assert.Equal("Work", host.Editor.TaskTag);
        Assert.Single(host.Changes);
        Assert.True(TagControls.CustomIsOpen(host.Editor));

        host.Editor.TaskTag = "x";
        Assert.True(TagControls.CustomIsOpen(host.Editor));

        host.Services.TagColors.Failure = new NoRepositoryFolderException();
        host.Type("errand");
        OpenWindows.Click(TagControls.ColorButton(host.Editor, "red-2"));
        Assert.Single(host.Changes);
        Assert.Equal("x", host.Editor.TaskTag);
        Assert.True(TagControls.CustomIsOpen(host.Editor));
        Assert.Equal("errand", host.Typed);
        Assert.Contains(host.Texts, text => text.Contains("Configure", StringComparison.Ordinal));

        host.Services.UseTag("echo", 2);
        host.ClickPill();
        OpenWindows.Click(TagControls.MenuItem(host.Editor, "echo"));
        Assert.Equal("echo", host.Editor.TaskTag);
        Assert.True(TagControls.CustomIsOpen(host.Editor));
        Assert.Equal("errand", host.Typed);
    }

    [WpfFact]
    public void The_keys_open_the_menu_move_through_it_and_choose()
    {
        using var host = new TagEditorWindow();
        host.Services.UseTag("a", 3);
        host.Services.UseTag("b", 2);
        host.Services.UseTag("c");
        TagEditorWindow.Focus(TagControls.PillButton(host.Editor));
        Assert.Equal(TagControls.Pill, TagControls.FocusedId());

        host.Editor.TaskTag = "w";
        TagEditorWindow.Focus(TagControls.PillButton(host.Editor));
        ((UIElement)Keyboard.FocusedElement).MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        Assert.Equal(TagControls.ClearTag, TagControls.FocusedId());
        host.Press(Key.Enter);
        Assert.Null(host.Editor.TaskTag);
        Assert.Null(host.Changes[^1].Tag);
        Assert.False(TagControls.MenuIsOpen(host.Editor));
        host.Editor.TaskTag = "w";
        TagEditorWindow.Focus(TagControls.ClearButton(host.Editor));
        host.Press(Key.Space);
        Assert.Null(host.Editor.TaskTag);
        Assert.False(TagControls.MenuIsOpen(host.Editor));

        foreach (var key in new[] { Key.Enter, Key.Space, Key.Down })
        {
            TagEditorWindow.Focus(TagControls.PillButton(host.Editor));
            host.Press(key);
            Assert.True(TagControls.MenuIsOpen(host.Editor));
            host.Press(Key.Escape);
            Assert.False(TagControls.MenuIsOpen(host.Editor));
        }

        TagEditorWindow.Focus(TagControls.PillButton(host.Editor));
        host.Press(Key.Enter);
        host.Press(Key.Down);
        host.Press(Key.Down);
        host.Press(Key.Enter);
        Assert.Equal("b", host.Editor.TaskTag);
        Assert.False(TagControls.MenuIsOpen(host.Editor));

        host.Editor.TaskTag = null;
        TagEditorWindow.Focus(TagControls.PillButton(host.Editor));
        host.Press(Key.Enter);
        host.Press(Key.Down);
        host.Press(Key.Down);
        host.Press(Key.Up);
        host.Press(Key.Enter);
        Assert.Equal("a", host.Editor.TaskTag);

        host.Editor.TaskTag = null;
        TagEditorWindow.Focus(TagControls.PillButton(host.Editor));
        host.Press(Key.Enter);
        for (var i = 0; i < 4; i++)
        {
            host.Press(Key.Down);
        }

        Assert.True(TagControls.SearchBox(host.Editor).IsKeyboardFocused);
        TextFields.Type(TagControls.SearchBox(host.Editor), "b");
        host.Press(Key.Enter);
        Assert.Equal("b", host.Editor.TaskTag);
        Assert.False(TagControls.MenuIsOpen(host.Editor));

        host.Editor.TaskTag = null;
        TagEditorWindow.Focus(TagControls.PillButton(host.Editor));
        host.Press(Key.Enter);
        host.Press(Key.Down);
        EditorFields.TypeOnFocused("c");
        Assert.Equal("c", TagControls.SearchBox(host.Editor).Text);
        Assert.True(TagControls.SearchBox(host.Editor).IsKeyboardFocused);
        host.Press(Key.Escape);

        host.Editor.TaskTag = "b";
        TagEditorWindow.Focus(TagControls.PillButton(host.Editor));
        host.Press(Key.Enter);
        for (var i = 0; i < 5; i++)
        {
            host.Press(Key.Down);
        }

        host.Press(Key.Enter);
        Assert.True(TagControls.CustomIsOpen(host.Editor));
        Assert.Equal(EditorFields.Tags, EditorFields.FocusedFieldName());
        Assert.Equal("b", host.Editor.TaskTag);
    }

    [WpfFact]
    public void In_the_colors_Tab_arrows_Space_and_Enter_move_and_choose()
    {
        using var host = new TagEditorWindow("mine");
        host.OpenCustom();
        host.Type("errand");
        host.Focus(EditorFields.Tags);
        host.Press(Key.Tab);
        Assert.StartsWith("Color ", TagControls.FocusedId());

        TagEditorWindow.Focus(TagControls.ColorButton(host.Editor, "red-1"));
        host.Press(Key.Right);
        Assert.Equal("Color orange-1", TagControls.FocusedId());
        host.Press(Key.Down);
        Assert.Equal("Color orange-2", TagControls.FocusedId());
        host.Press(Key.Left);
        Assert.Equal("Color red-2", TagControls.FocusedId());
        host.Press(Key.Up);
        Assert.Equal("Color red-1", TagControls.FocusedId());
        host.Press(Key.Right);
        host.Press(Key.Down);
        host.Press(Key.Space);
        Assert.Equal("errand", host.Editor.TaskTag);
        Assert.Equal([("errand", "orange-2")], host.Services.TagColors.Kept);

        host.OpenCustom();
        host.Type("chore");
        TagEditorWindow.Focus(TagControls.ColorButton(host.Editor, "blue-3"));
        host.Press(Key.Enter);
        Assert.Equal("chore", host.Editor.TaskTag);
        Assert.Contains(("chore", "blue-3"), host.Services.TagColors.Kept);
    }

    [WpfFact]
    public void A_window_around_the_control_closes_its_parts_with_CloseEdit_chooses_the_typed_text_and_takes_the_control_out_and_back()
    {
        using var host = new TagEditorWindow("home");
        Assert.False(host.Editor.CloseEdit());
        Assert.Equal("home", host.Editor.TaskTag);
        Assert.Empty(host.Changes);

        host.ClickPill();
        Assert.True(host.Editor.CloseEdit());
        Assert.False(TagControls.MenuIsOpen(host.Editor));

        host.OpenCustom();
        host.Type("half");
        host.ClickPill();
        Assert.True(TagControls.MenuIsOpen(host.Editor));
        Assert.True(TagControls.CustomIsOpen(host.Editor));
        Assert.True(host.Editor.CloseEdit());
        Assert.False(TagControls.MenuIsOpen(host.Editor));
        Assert.True(TagControls.CustomIsOpen(host.Editor));
        Assert.True(host.Editor.CloseEdit());
        Assert.False(TagControls.CustomIsOpen(host.Editor));
        Assert.False(host.Editor.CloseEdit());
        Assert.Empty(host.Changes);

        host.Editor.ChooseTypedText();
        Assert.Empty(host.Changes);
        host.OpenCustom();
        Assert.Equal("", host.Typed);
        host.Editor.ChooseTypedText();
        Assert.Equal("home", host.Editor.TaskTag);
        Assert.Empty(host.Changes);

        host.Type("errand");
        host.Editor.ChooseTypedText();
        var (tag, tagThen, _) = Assert.Single(host.Changes);
        Assert.Equal("errand", tag);
        Assert.Equal("errand", tagThen);
        Assert.Equal("errand", host.PillText);
        Assert.False(TagControls.CustomIsOpen(host.Editor));

        host.Services.UseTag("home");
        host.ClickPill();
        TextFields.Type(TagControls.SearchBox(host.Editor), "ho");
        host.Remove();
        host.Put();
        Assert.False(TagControls.MenuPopupIsOpen(host.Editor));
        host.ClickPill();
        Assert.True(TagControls.MenuPopupIsOpen(host.Editor));
        Assert.Equal(["home"], TagControls.MenuTags(host.Editor));
        Assert.Equal("", TagControls.SearchBox(host.Editor).Text);
        Assert.False(TagControls.SearchMatchesAreOpen(host.Editor));
        Assert.False(TagControls.CustomIsOpen(host.Editor));
        host.Press(Key.Escape);

        var before = TagControls.PillFill(host.Editor);
        host.Remove();
        host.Services.TagColors.Seed("errand", "blue-1");
        host.Services.TagColors.RaiseChanged();
        host.Services.Theme.Switch(true);
        Assert.Equal(before, TagControls.PillFill(host.Editor));
        host.Put();
        Assert.Equal(TagControls.Expected(TagColors.Find("blue-1")!, dark: true), TagControls.PillFill(host.Editor));
    }

    [WpfFact]
    public void The_tag_box_makes_typed_and_pasted_text_lowercase_without_spaces_and_keeps_an_existing_tags_spelling()
    {
        using var window = new TagEditorWindow();
        window.Services.UseTag("Work");
        window.OpenCustom();
        window.Focus(EditorFields.Tags);
        EditorFields.TypeOnFocused("Home");
        Assert.Equal("home", window.Typed);

        window.Type("");
        EditorFields.TypeOnFocused("a b");
        Assert.Equal("ab", window.Typed);

        window.Type("home");
        var box = (TextBox)EditorFields.Field(window.Editor, EditorFields.Tags);
        foreach (var whitespace in new[] { " ", "\t" })
        {
            box.Select(1, 2);
            EditorFields.TypeOnFocused(whitespace);
            Assert.Equal("home", window.Typed);
            Assert.Equal((1, 2), (box.SelectionStart, box.SelectionLength));
        }

        var args = new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText, "Big \r\n Errand"), false, DataFormats.UnicodeText)
        {
            RoutedEvent = DataObject.PastingEvent,
        };
        box.RaiseEvent(args);
        Assert.Equal("bigerrand", args.DataObject.GetData(DataFormats.UnicodeText));

        window.Type("");
        EditorFields.TypeOnFocused("WORK");
        window.Press(Key.Enter);
        Assert.Equal("Work", window.Editor.TaskTag);

        window.Services.UseTag("Errand");
        window.ClickPill();
        Assert.Equal("Work", window.PillText);
        Assert.Equal(["Errand"], TagControls.MenuTags(window.Editor));
    }

    [WpfFact]
    public async Task The_badges_show_the_tags_in_order_in_their_colors_follow_the_theme_and_the_tag_colors_and_catch_up_when_put_back()
    {
        using var host = new BadgesHost("work", "home", "errand");
        Assert.Equal(["work", "home", "errand"], TagControls.BadgeTags(host.Badges));
        Assert.Empty(TagControls.RemovableTags(host.Badges));
        host.Badges.Tags = ["home"];
        Assert.Equal(["home"], TagControls.BadgeTags(host.Badges));
        Assert.Equal(TagControls.Expected(TagColors.DefaultFor("home"), dark: false), TagControls.FillOf(host.Badges, "home"));

        host.Services.TagColors.Seed("home", "mauve-9");
        host.Badges.Tags = ["home"];
        Assert.Equal(TagControls.Expected(TagColors.DefaultFor("home"), dark: false), TagControls.FillOf(host.Badges, "home"));

        host.Services.TagColors.Seed("home", "blue-2");
        host.Badges.Tags = ["home"];
        Assert.Equal(TagControls.Expected(TagColors.Find("blue-2")!, dark: false), TagControls.FillOf(host.Badges, "home"));
        host.Services.Theme.Switch(true);
        Assert.Equal(TagControls.Expected(TagColors.Find("blue-2")!, dark: true), TagControls.FillOf(host.Badges, "home"));

        host.Services.TagColors.SetColor("home", "green-1");
        Assert.Equal(TagControls.Expected(TagColors.Find("green-1")!, dark: true), TagControls.FillOf(host.Badges, "home"));
        host.Services.TagColors.Seed("home", "red-1");
        await Task.Run(host.Services.TagColors.RaiseChanged);
        await RenderedField.Settled();
        Assert.Equal(TagControls.Expected(TagColors.Find("red-1")!, dark: true), TagControls.FillOf(host.Badges, "home"));

        host.Remove();
        host.Services.Theme.Switch(false);
        host.Put();
        Assert.Equal(TagControls.Expected(TagColors.Find("red-1")!, dark: false), TagControls.FillOf(host.Badges, "home"));

        using var dark = new BadgesHost(dark: true, "home");
        dark.Services.TagColors.Seed("home", "red-3");
        dark.Badges.Tags = ["home"];
        Assert.Equal(TagControls.Expected(TagColors.Find("red-3")!, dark: true), TagControls.FillOf(dark.Badges, "home"));
    }
}

/// <summary>Hosts a <see cref="TagBadges"/> in a showing window over the fakes, with the tags shown. Shared test setup.</summary>
internal sealed class BadgesHost : IDisposable
{
    private readonly Window window;

    public BadgesHost(params string[] tags)
        : this(false, tags)
    {
    }

    public BadgesHost(bool dark, params string[] tags)
    {
        Services = new EditorServices();
        Services.Theme.IsDark = dark;
        Badges = Services.CreateTagBadges();
        Badges.Tags = tags;
        window = new Window { Content = Badges, Width = 480, Height = 200 };
        window.Show();
    }

    public EditorServices Services { get; }

    public TagBadges Badges { get; }

    /// <summary>Takes the badges out of the window and waits for them to unload.</summary>
    public void Remove() => LoadSync.Remove(window, Badges);

    /// <summary>Puts the badges back in the window and waits for them to load.</summary>
    public void Put() => LoadSync.Put(window, Badges);

    public void Dispose() => window.Close();
}
