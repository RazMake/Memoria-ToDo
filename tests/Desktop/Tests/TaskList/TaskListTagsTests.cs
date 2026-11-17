using System.IO;
using System.Windows.Input;
using Memoria.ToDo.Desktop.TaskEditor;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Tasks;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>
/// Tags in Task View: one group per task, the selected task's tag control, a choice or a clear saving at once with its Undo
/// bar, the control during an edit, and changes from Sync. The rules for what Tasks saves and what Undo restores are in the
/// unit tests of the tag changes and of Tasks.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TaskListTagsTests
{
    private static TaskItem Only(ListServices services, string title) => services.Stored(title);

    private static void ChooseTagElsewhere(ListServices services, string title, string? tag)
    {
        var started = Only(services, title);
        services.Tasks.SaveEdit(started, new TaskDraft(started.Id, started.Title, started.Description, tag is null ? [] : [tag]));
    }

    private static byte[] Png() => FakeImageEncoder.Image(ImageFormat.Png, 100, 80);

    private static Func<int> CountSaves(ListServices services) => services.CountSaves();

    // ---- Groups ----

    [WpfFact]
    public void A_task_with_one_tag_shows_in_exactly_one_group_and_one_with_none_in_the_No_tag_group()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        services.Add("Plain");
        using var screen = new TaskListScreen(services);

        Assert.Equal(["Buy milk"], screen.RowTitlesIn("home"));
        Assert.Equal(["Plain"], screen.RowTitlesIn(TagEditor.NoTagText));
        Assert.Equal(["Buy milk", "Plain"], screen.RowTitles);
    }

    [WpfFact]
    public void The_untagged_groups_heading_uses_the_words_the_Task_editor_publishes_and_comes_last()
    {
        var services = new ListServices();
        services.Add("Plain");
        services.Add("Buy milk", "", "home");
        using var screen = new TaskListScreen(services);

        Assert.Equal(["home", TagEditor.NoTagText], screen.GroupNames);
    }

    // ---- The pill ----

    [WpfFact]
    public void The_selected_task_shows_its_tag_in_the_pill_with_an_x()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        using var screen = new TaskListScreen(services);
        Assert.False(screen.ShowsTagControl("Buy milk"));

        screen.ClickRow("Buy milk");

        Assert.True(screen.ShowsTagControl("Buy milk"));
        Assert.Equal("home", screen.PillTag("Buy milk"));
        Assert.True(screen.PillHasClear("Buy milk"));
    }

    [WpfFact]
    public void A_task_with_no_tag_shows_the_No_tag_pill_with_no_x()
    {
        var services = new ListServices();
        services.Add("Buy milk");
        using var screen = new TaskListScreen(services);

        screen.ClickRow("Buy milk");

        Assert.Equal(TagEditor.NoTagText, screen.PillText("Buy milk"));
        Assert.False(screen.PillHasClear("Buy milk"));
    }

    [WpfFact]
    public void A_task_saved_with_several_tags_shows_its_first_in_the_pill()
    {
        var services = new ListServices();
        services.AddOlder("Buy milk", "", "home", "errand");
        using var screen = new TaskListScreen(services);

        screen.ClickRow("Buy milk", "home");

        Assert.Equal("home", screen.PillTag("Buy milk"));
    }

    [WpfFact]
    public void The_selected_resolved_task_shows_the_tag_control()
    {
        var services = new ListServices();
        services.AddResolved("Old job", "", "home");
        using var screen = new TaskListScreen(services);
        screen.Click(screen.ResolvedLabel!);

        screen.ClickRow("Old job");

        Assert.True(screen.ShowsTagControl("Old job"));
    }

    // ---- Choosing and clearing, without Edit ----

    [WpfFact]
    public void Choosing_a_tag_from_the_menu_saves_it_at_once_moves_the_task_to_its_group_and_shows_the_Tag_changed_bar()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");
        screen.OpenTagMenu("Buy milk");

        screen.ChooseMenuTag("work");

        Assert.Equal(["work"], Only(services, "Buy milk").Tags);
        Assert.True(screen.ShowsText("Tag changed"));
        Assert.Equal("work", screen.PillTag("Buy milk"));
        Assert.Equal(["work"], screen.GroupNames);
    }

    [WpfFact]
    public void Clearing_with_the_x_saves_at_once_moves_the_task_to_No_tag_and_shows_the_Tag_cleared_bar()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");

        screen.ClearTag("Buy milk");

        Assert.Empty(Only(services, "Buy milk").Tags);
        Assert.True(screen.ShowsText("Tag cleared"));
        Assert.Equal(TagEditor.NoTagText, screen.PillText("Buy milk"));
        Assert.Equal([TagEditor.NoTagText], screen.GroupNames);
    }

    [WpfFact]
    public void Undo_after_a_choice_restores_the_previous_tag_in_the_task_and_the_pill()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");
        screen.OpenTagMenu("Buy milk");
        screen.ChooseMenuTag("work");

        screen.Click("Undo");

        Assert.Equal(["home"], Only(services, "Buy milk").Tags);
        Assert.Equal("home", screen.PillTag("Buy milk"));
        Assert.False(screen.HasButton("Undo"));
    }

    [WpfFact]
    public void Undo_after_a_clear_puts_the_tag_back()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");
        screen.ClearTag("Buy milk");

        screen.Click("Undo");

        Assert.Equal(["home"], Only(services, "Buy milk").Tags);
        Assert.Equal("home", screen.PillTag("Buy milk"));
    }

    [WpfFact]
    public void Undo_for_a_task_saved_with_several_tags_brings_back_only_the_first()
    {
        var services = new ListServices();
        services.Add("Call Ana", "", "work");
        services.AddOlder("Buy milk", "", "home", "errand");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk", "home");
        screen.OpenTagMenu("Buy milk");
        screen.ChooseMenuTag("work");

        screen.Click("Undo");

        Assert.Equal(["home"], Only(services, "Buy milk").Tags);
    }

    [WpfFact]
    public void Enter_in_Custom_chooses_the_typed_tag_and_saves_it_at_once()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");
        screen.OpenTagMenu("Buy milk");
        screen.ChooseCustom();
        screen.TypeTags("pend");

        screen.Press(Key.Enter);

        Assert.Equal(["pend"], Only(services, "Buy milk").Tags);
        Assert.True(screen.ShowsText("Tag changed"));
    }

    [WpfFact]
    public void A_failed_tag_choice_reverts_the_pill_and_shows_no_Undo_bar_but_says_why()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");
        screen.OpenTagMenu("Buy milk");
        services.Files.FailWritesUnder(services.TaskFolder);

        screen.ChooseMenuTag("work");

        Assert.Equal("home", screen.PillTag("Buy milk"));
        Assert.False(screen.HasButton("Undo"));
        Assert.True(screen.ShowsText("Writing is not allowed here"));
        Assert.Equal(["home"], Only(services, "Buy milk").Tags);
    }

    [WpfFact]
    public void A_failed_clear_reverts_the_pill_and_shows_no_Undo_bar_but_says_why()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");
        services.Files.FailWritesUnder(services.TaskFolder);

        screen.ClearTag("Buy milk");

        Assert.Equal("home", screen.PillTag("Buy milk"));
        Assert.False(screen.HasButton("Undo"));
        Assert.True(screen.ShowsText("Writing is not allowed here"));
    }

    // ---- The control during an edit ----

    [WpfFact]
    public void During_a_description_edit_choosing_a_tag_only_changes_the_pill_saves_nothing_and_shows_no_Undo_bar()
    {
        var services = new ListServices();
        var started = services.Add("Buy milk", "Oat milk", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Oat milk, two cartons");
        var saves = CountSaves(services);

        screen.OpenTagMenu("Buy milk");
        screen.ChooseMenuTag("work");

        Assert.Equal("work", screen.PillTag("Buy milk"));
        Assert.Equal(started, Only(services, "Buy milk"));
        Assert.Equal(0, saves());
        Assert.False(screen.HasButton("Undo"));
        Assert.False(screen.ShowsText("Tag changed"));
        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal("Oat milk, two cartons", screen.EditedDescriptionText);
    }

    [WpfFact]
    public void During_a_description_edit_clearing_the_tag_only_changes_the_pill_saves_nothing_and_shows_no_Undo_bar()
    {
        var services = new ListServices();
        var started = services.Add("Buy milk", "Oat milk", "home");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        var saves = CountSaves(services);

        screen.ClearTag("Buy milk");

        Assert.Equal(TagEditor.NoTagText, screen.PillText("Buy milk"));
        Assert.Equal(started, Only(services, "Buy milk"));
        Assert.Equal(0, saves());
        Assert.False(screen.HasButton("Undo"));
        Assert.False(screen.ShowsText("Tag cleared"));
        Assert.True(screen.IsEditing("Buy milk"));
    }

    [WpfFact]
    public void The_Save_shortcut_makes_one_save_with_the_edited_description_and_the_tag_chosen_during_the_edit()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Oat milk, two cartons");
        screen.OpenTagMenu("Buy milk");
        screen.ChooseMenuTag("work");
        var saves = CountSaves(services);

        screen.Press(Key.LeftCtrl, Key.Enter);

        var saved = Only(services, "Buy milk");
        Assert.Equal("Oat milk, two cartons", saved.Description);
        Assert.Equal(["work"], saved.Tags);
        Assert.Equal(1, saves());
        Assert.False(screen.IsEditingAny);
        Assert.False(screen.HasButton("Undo"));
        Assert.Equal(["work"], screen.GroupNames);
    }

    [WpfFact]
    public void A_tag_cleared_during_a_description_edit_is_cleared_by_the_edits_one_save()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk", "home");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Oat milk, two cartons");
        screen.ClearTag("Buy milk");
        var saves = CountSaves(services);

        screen.Press(Key.LeftCtrl, Key.Enter);

        var saved = Only(services, "Buy milk");
        Assert.Equal("Oat milk, two cartons", saved.Description);
        Assert.Empty(saved.Tags);
        Assert.Equal(1, saves());
    }

    [WpfFact]
    public void Clicking_Save_makes_one_save_with_the_edited_description_and_the_tag_chosen_during_the_edit()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Oat milk, two cartons");
        screen.OpenTagMenu("Buy milk");
        screen.ChooseMenuTag("work");
        var saves = CountSaves(services);

        screen.Click("Save");

        var saved = Only(services, "Buy milk");
        Assert.Equal("Oat milk, two cartons", saved.Description);
        Assert.Equal(["work"], saved.Tags);
        Assert.Equal(1, saves());
    }

    [WpfFact]
    public void The_tag_control_works_under_the_Preview_tab_too_and_still_only_changes_the_pill()
    {
        var services = new ListServices();
        var started = services.Add("Buy milk", "Oat milk", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.ClickTab("Preview");
        var saves = CountSaves(services);

        screen.OpenTagMenu("Buy milk");
        screen.ChooseMenuTag("work");

        Assert.Equal("work", screen.PillTag("Buy milk"));
        Assert.Equal(started, Only(services, "Buy milk"));
        Assert.Equal(0, saves());
        Assert.False(screen.HasButton("Undo"));
        Assert.True(screen.PageIsPreview);
    }

    [WpfFact]
    public void A_tag_chosen_during_a_title_edit_only_changes_the_pill_and_the_title_save_carries_both_in_one_save()
    {
        var services = new ListServices();
        var started = services.Add("Buy milk", "", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");
        screen.PressOn("Buy milk", Key.LeftShift, Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");
        var saves = CountSaves(services);

        screen.OpenTagMenu("Buy milk");
        screen.ChooseMenuTag("work");

        Assert.Equal(started, Only(services, "Buy milk"));
        Assert.Equal(0, saves());
        Assert.False(screen.HasButton("Undo"));
        Assert.True(screen.IsEditingTitle("Buy milk"));

        screen.Press(Key.LeftCtrl, Key.Enter);

        var saved = Only(services, "Buy oat milk");
        Assert.Equal(["work"], saved.Tags);
        Assert.Equal(1, saves());
        Assert.False(screen.IsEditingAny);
    }

    [WpfFact]
    public void A_failed_edit_save_leaves_the_tag_unsaved_and_the_edit_open_with_its_text_and_the_pills_tag()
    {
        var services = new ListServices();
        var started = services.Add("Buy milk", "Oat milk", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Oat milk, two cartons");
        screen.OpenTagMenu("Buy milk");
        screen.ChooseMenuTag("work");
        services.Files.FailWritesUnder(services.TaskFolder);

        screen.Press(Key.LeftCtrl, Key.Enter);

        Assert.Equal(started, Only(services, "Buy milk"));
        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal("Oat milk, two cartons", screen.EditedDescriptionText);
        Assert.Equal("work", screen.PillTag("Buy milk"));
        Assert.True(screen.ShowsText("Writing is not allowed here"));
        Assert.False(screen.HasButton("Undo"));
    }

    [WpfFact]
    public void A_failed_title_save_leaves_the_tag_unsaved_and_the_title_edit_open_with_its_text_and_the_pills_tag()
    {
        var services = new ListServices();
        var started = services.Add("Buy milk", "", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.LeftShift, Key.F2);
        screen.TypeTitle("Buy milk", "Buy oat milk");
        screen.OpenTagMenu("Buy milk");
        screen.ChooseMenuTag("work");
        services.Files.FailWritesUnder(services.TaskFolder);

        screen.Press(Key.LeftCtrl, Key.Enter);

        Assert.Equal(started, Only(services, "Buy milk"));
        Assert.True(screen.ShowsText("Writing is not allowed here"));
        Assert.True(screen.IsEditingTitle("Buy milk"));
        Assert.Equal("Buy oat milk", screen.EditorTitleText("Buy milk"));
        Assert.Equal("work", screen.PillTag("Buy milk"));
    }

    [WpfFact]
    public void A_tag_change_during_an_edit_never_deletes_the_edits_waiting_images_and_the_edits_save_still_moves_them()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        var before = services.AllFiles;
        services.Clipboard.Image = Png();
        screen.PasteImageIntoDescription();
        var waiting = services.AllFiles.Except(before).ToList();
        Assert.NotEmpty(waiting);

        screen.OpenTagMenu("Buy milk");
        screen.ChooseMenuTag("work");

        Assert.All(waiting, image => Assert.True(services.Files.FileExists(image)));
        Assert.Empty(services.Files.AllFilesUnder(Path.Combine(services.Clone, "images")));

        screen.Press(Key.LeftCtrl, Key.Enter);

        Assert.NotEmpty(services.Files.AllFilesUnder(Path.Combine(services.Clone, "images")));
        Assert.Equal(["work"], Only(services, "Buy milk").Tags);
    }

    // ---- Text left in Custom when the edit saves ----

    private static TaskListScreen EditingWithTextInCustom(ListServices services)
    {
        services.Add("Buy milk", "Oat milk", "home");
        var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.TypeDescription("Oat milk, two cartons");
        screen.OpenTagMenu("Buy milk");
        screen.ChooseCustom();
        screen.TypeTags("pend");
        return screen;
    }

    [WpfFact]
    public void The_Save_shortcut_with_text_in_Custom_chooses_it_as_the_tag_first_and_makes_one_save_with_the_text_and_the_tag()
    {
        var services = new ListServices();
        using var screen = EditingWithTextInCustom(services);
        var saves = CountSaves(services);

        screen.Press(Key.LeftCtrl, Key.Enter);

        var saved = Only(services, "Buy milk");
        Assert.Equal(["pend"], saved.Tags);
        Assert.Equal("Oat milk, two cartons", saved.Description);
        Assert.Equal(1, saves());
        Assert.False(screen.IsEditingAny);
        Assert.False(screen.HasButton("Undo"));
    }

    [WpfFact]
    public void The_Save_button_with_text_in_Custom_chooses_it_as_the_tag_first_and_makes_one_save_with_the_text_and_the_tag()
    {
        var services = new ListServices();
        using var screen = EditingWithTextInCustom(services);
        var saves = CountSaves(services);

        screen.Click("Save");

        var saved = Only(services, "Buy milk");
        Assert.Equal(["pend"], saved.Tags);
        Assert.Equal("Oat milk, two cartons", saved.Description);
        Assert.Equal(1, saves());
    }

    // The check saves the edit, then resolves the task: two saves in all, and neither is a save of the typed tag alone.
    [WpfFact]
    public void Checking_the_task_with_text_in_Custom_chooses_it_as_the_tag_first_and_the_edit_save_carries_it()
    {
        var services = new ListServices();
        using var screen = EditingWithTextInCustom(services);
        var saves = CountSaves(services);

        screen.ClickCheckBox("Buy milk");

        var saved = Only(services, "Buy milk");
        Assert.Equal(["pend"], saved.Tags);
        Assert.Equal("Oat milk, two cartons", saved.Description);
        Assert.NotNull(saved.Resolved);
        Assert.Equal(2, saves());
    }

    [WpfFact]
    public void Text_in_Custom_that_the_edit_never_saves_is_dropped_with_the_edit_and_not_chosen()
    {
        var services = new ListServices();
        using var screen = EditingWithTextInCustom(services);
        screen.Press(Key.Escape);
        screen.Press(Key.Escape);

        Assert.False(screen.IsEditingAny);
        Assert.Equal(["home"], Only(services, "Buy milk").Tags);
        Assert.Equal("Oat milk", Only(services, "Buy milk").Description);
    }

    // ---- Leaving the edit without saving drops the tag choice ----

    private static TaskListScreen EditingWithTagChosen(ListServices services, string? text = "Oat milk, two cartons")
    {
        services.Add("Buy milk", "Oat milk", "home");
        services.Add("Call Ana", "", "work");
        var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        if (text is not null)
        {
            screen.TypeDescription(text);
        }

        screen.OpenTagMenu("Buy milk");
        screen.ChooseMenuTag("work");
        return screen;
    }

    [WpfFact]
    public void Esc_leaves_the_edit_and_drops_the_tag_choice_with_the_text()
    {
        var services = new ListServices();
        using var screen = EditingWithTagChosen(services);
        var started = Only(services, "Buy milk");
        var saves = CountSaves(services);

        screen.Press(Key.Escape);

        Assert.False(screen.IsEditingAny);
        Assert.Equal(started, Only(services, "Buy milk"));
        Assert.Equal(0, saves());
        Assert.Equal("home", screen.PillTag("Buy milk"));
        Assert.Equal(["home", "work"], screen.GroupNames);
    }

    [WpfFact]
    public void Selecting_another_task_leaves_the_edit_and_drops_the_tag_choice()
    {
        var services = new ListServices();
        using var screen = EditingWithTagChosen(services);
        var started = Only(services, "Buy milk");

        screen.ClickRow("Call Ana");
        screen.ClickRow("Buy milk");

        Assert.False(screen.IsEditingAny);
        Assert.Equal(started, Only(services, "Buy milk"));
        Assert.Equal("home", screen.PillTag("Buy milk"));
    }

    [WpfFact]
    public void Starting_another_edit_leaves_the_edit_and_drops_the_tag_choice()
    {
        var services = new ListServices();
        using var screen = EditingWithTagChosen(services);
        var started = Only(services, "Buy milk");

        screen.ChooseContextItem("Call Ana", "Edit Description");
        screen.ClickRow("Buy milk");

        Assert.Equal(started, Only(services, "Buy milk"));
        Assert.Equal("home", screen.PillTag("Buy milk"));
    }

    [WpfFact]
    public void A_description_edit_whose_only_change_is_the_tag_shows_the_Undo_bar_when_discarded_and_Undo_puts_the_tag_back()
    {
        var services = new ListServices();
        using var screen = EditingWithTagChosen(services, text: null);
        var started = Only(services, "Buy milk");

        screen.Press(Key.Escape);

        Assert.False(screen.IsEditingAny);
        Assert.True(screen.HasButton("Undo"));
        Assert.Equal(started, Only(services, "Buy milk"));

        screen.Click("Undo");

        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal("Oat milk", screen.EditedDescriptionText);
        Assert.Equal("work", screen.PillTag("Buy milk"));
        Assert.Equal(started, Only(services, "Buy milk"));
    }

    [WpfFact]
    public void A_description_edit_whose_only_change_is_a_cleared_tag_shows_the_Undo_bar_when_discarded()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk", "home");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.ClearTag("Buy milk");

        screen.Press(Key.Escape);

        Assert.True(screen.HasButton("Undo"));
        Assert.Equal(["home"], Only(services, "Buy milk").Tags);
    }

    [WpfFact]
    public void A_description_edit_with_the_tag_chosen_back_to_the_started_one_has_no_change_so_Esc_shows_no_Undo_bar()
    {
        var services = new ListServices();
        services.Add("Buy milk", "Oat milk", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.F2);
        screen.OpenTagMenu("Buy milk");
        screen.ChooseMenuTag("work");
        screen.OpenTagMenu("Buy milk");
        screen.ChooseMenuTag("home");

        screen.Press(Key.Escape);

        Assert.False(screen.IsEditingAny);
        Assert.False(screen.HasButton("Undo"));
    }

    [WpfFact]
    public void Undo_of_a_discarded_description_edit_puts_back_both_its_text_and_the_pills_tag()
    {
        var services = new ListServices();
        using var screen = EditingWithTagChosen(services);
        screen.Press(Key.Escape);

        screen.Click("Undo");

        Assert.True(screen.IsEditing("Buy milk"));
        Assert.Equal("Oat milk, two cartons", screen.EditedDescriptionText);
        Assert.Equal("work", screen.PillTag("Buy milk"));
        Assert.False(screen.HasButton("Undo"));
    }

    [WpfFact]
    public void A_title_edit_left_with_Esc_shows_no_Undo_bar_even_when_its_tag_changed_and_saves_nothing()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.PressOn("Buy milk", Key.LeftShift, Key.F2);
        screen.OpenTagMenu("Buy milk");
        screen.ChooseMenuTag("work");
        var started = Only(services, "Buy milk");

        screen.Press(Key.Escape);

        Assert.False(screen.IsEditingAny);
        Assert.False(screen.HasButton("Undo"));
        Assert.Equal(started, Only(services, "Buy milk"));
        Assert.Equal("home", screen.PillTag("Buy milk"));
    }

    // ---- Changes from Sync ----

    [WpfFact]
    public void A_sync_that_changes_the_tag_updates_the_pill()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");

        ChooseTagElsewhere(services, "Buy milk", "errand");

        Assert.Equal("errand", screen.PillTag("Buy milk"));
    }

    [WpfFact]
    public void A_sync_that_clears_the_tag_shows_the_No_tag_pill()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");

        ChooseTagElsewhere(services, "Buy milk", null);

        Assert.Equal(TagEditor.NoTagText, screen.PillText("Buy milk"));
    }

    [WpfFact]
    public void A_sync_that_changes_the_tag_keeps_an_open_menu_open()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");
        screen.OpenTagMenu("Buy milk");

        ChooseTagElsewhere(services, "Buy milk", "errand");
        screen.Settle();

        Assert.True(screen.TagMenuIsOpen);
        Assert.Equal("errand", screen.PillTag("Buy milk"));
    }

    [WpfFact]
    public void A_sync_that_changes_the_tag_keeps_Custom_and_its_text()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");
        screen.OpenTagMenu("Buy milk");
        screen.ChooseCustom();
        screen.TypeTags("pend");

        ChooseTagElsewhere(services, "Buy milk", "errand");
        screen.Settle();

        Assert.True(screen.CustomIsOpen);
        Assert.Equal("pend", screen.EditedTagsText);
    }

    [WpfFact]
    public void Selecting_another_task_drops_Custom_and_its_text()
    {
        var services = new ListServices();
        services.Add("Buy milk", "", "home");
        services.Add("Call Ana", "", "work");
        using var screen = new TaskListScreen(services);
        screen.ClickRow("Buy milk");
        screen.OpenTagMenu("Buy milk");
        screen.ChooseCustom();
        screen.TypeTags("pend");

        screen.ClickRow("Call Ana");

        Assert.False(screen.CustomIsOpen);
    }
}
