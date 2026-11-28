using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests.AddWindow;

/// <summary>
/// The task's one tag in the Add window: the menu, the New tag box and how both save. What the pill, the menu and the box
/// do on their own is the TagControl screen's; these check Add wires them to the draft, the saves and its Esc.
/// </summary>
[UX("AddWindow")]
[Collection(ScreenTests.Collection)]
public class AddWindowTagsTests
{
    [WpfFact]
    public void Path_7_the_task_gets_its_tags_from_the_menu_search_or_box_Esc_closes_the_open_parts_before_Add_and_the_box_text_saves()
    {
        using var screen = new AddScreen();
        screen.Services.UseTag("echo", 4);
        screen.Services.UseTag("hobby");
        screen.Show();
        screen.Fill("Buy milk");

        OpenWindows.Click(TagControls.AddPill(screen.Window));
        OpenWindows.Click(TagControls.MenuItem(screen.Window, "echo"));

        Assert.Equal(["echo"], screen.Editor.Value.Tags);
        Assert.Equal("echo", screen.ShownTag);

        OpenWindows.Click(TagControls.ClearButton(screen.Window));

        Assert.Empty(screen.Editor.Value.Tags);
        Assert.Null(screen.ShownTag);
        Assert.Equal(TagEditor.NoTagText, TagControls.PillText(screen.Window));

        OpenWindows.Click(TagControls.AddPill(screen.Window));
        TextFields.Type(TagControls.SearchBox(screen.Window), "hob");
        OpenWindows.Click(TagControls.SearchMatch(screen.Window, "hobby"));

        Assert.Equal(["hobby"], screen.Editor.Value.Tags);
        Assert.False(TagControls.MenuIsOpen(screen.Window));

        screen.TypeTag("ur");
        Assert.DoesNotContain(EditorFields.VisibleTexts(screen.Window), text => text == "urgent");
        screen.TypeTag("urgent");
        screen.Press(Key.Enter);

        Assert.Equal(["hobby", "urgent"], screen.Editor.Value.Tags);
        Assert.Equal(["hobby", "urgent"], screen.ShownTags);
        Assert.Equal("", screen.TypedTag);
        Assert.Empty(screen.Saved);

        screen.OpenCustom();
        screen.Focus(EditorFields.Tags);
        screen.Press(Key.Back);

        Assert.Equal(["hobby", "urgent"], screen.Editor.Value.Tags);
        Assert.True(TagControls.CustomIsOpen(screen.Window));

        screen.Press(Key.Escape);

        Assert.False(TagControls.CustomIsOpen(screen.Window));
        Assert.True(screen.IsShowing);

        OpenWindows.Click(TagControls.AddPill(screen.Window));
        screen.Press(Key.Escape);

        Assert.False(TagControls.MenuIsOpen(screen.Window));
        Assert.True(screen.IsShowing);

        OpenWindows.Click(TagControls.AddPill(screen.Window));
        screen.Focus(EditorFields.Title);
        screen.Press(Key.Escape);

        Assert.False(TagControls.MenuIsOpen(screen.Window));
        Assert.True(screen.IsShowing);

        screen.TypeTag("later");
        screen.Focus(EditorFields.Title);
        screen.Press(Key.Escape);

        Assert.False(TagControls.CustomIsOpen(screen.Window));
        Assert.True(screen.IsShowing);

        screen.TypeTag("later");
        screen.SaveButton.Focus();
        Keyboard.Focus(screen.SaveButton);
        screen.Press(Key.Escape);

        Assert.False(TagControls.CustomIsOpen(screen.Window));
        Assert.True(screen.IsShowing);

        screen.Press(Key.Escape);

        Assert.False(screen.IsShowing);
        Assert.Equal(["hobby", "urgent"], screen.KeptDraft?.Tags);

        screen.Show();

        Assert.Equal(["hobby", "urgent"], screen.ShownTags);

        screen.TypeTag("urgent");

        screen.ClickTagColor("red-2");

        Assert.Equal(["hobby", "urgent"], screen.Editor.Value.Tags);
        Assert.Contains(("urgent", "red-2"), screen.Services.TagColors.Kept);
        Assert.False(TagControls.CustomIsOpen(screen.Window));

        screen.Fill("First", "", "hobby", "urgent");
        screen.TypeTag("viaKey");
        screen.Save();

        Assert.Equal(["hobby", "urgent", "viakey"], screen.Saved[0].Tags);
        Assert.DoesNotContain(screen.Services.TagColors.Kept, kept => kept.Item1 == "viakey");

        screen.Show();
        screen.Fill("Second");
        screen.TypeTag("viaButton");
        screen.ClickSave();

        Assert.Equal(["viabutton"], screen.Saved[0].Tags);

        screen.Show();
        screen.Fill("Third");
        screen.TypeTag("viaClear");
        screen.SaveAndClear();

        Assert.Equal(["viaclear"], screen.Saved[0].Tags);
        Assert.Equal(3, screen.Saved.Count);

        screen.Editor.Value = new TaskDraft(screen.Editor.Value.TaskId, "Buy milk", "", ["first", "second", "third"]);

        Assert.Equal(["first", "second", "third"], screen.ShownTags);
        Assert.Equal(["first", "second", "third"], screen.Editor.Value.Tags);
        Assert.False(screen.Editor.HasChanges);

        screen.TypeTag("urgent");
        screen.Press(Key.Enter);

        Assert.True(screen.Editor.HasChanges);
        screen.Save();

        Assert.Equal(["first", "second", "third", "urgent"], screen.Saved[0].Tags);
    }
}
