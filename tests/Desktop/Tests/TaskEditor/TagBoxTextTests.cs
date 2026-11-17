using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// What the tag box does with the letters the user types or pastes: tags are lowercase and have no spaces, and a tag that
/// already exists keeps the spelling it was stored with. The rules for the text itself are in the tag text's unit tests.
/// </summary>
[UX]
[Collection(ScreenTests.Collection)]
public class TagBoxTextTests
{
    [WpfFact]
    public void Typed_capital_letters_show_as_lowercase_in_the_tag_box()
    {
        using var window = new TagEditorWindow();
        window.OpenCustom();
        window.Focus(EditorFields.Tags);

        EditorFields.TypeOnFocused("Home");

        Assert.Equal("home", window.Typed);
    }

    [WpfFact]
    public void A_typed_space_does_nothing()
    {
        using var window = new TagEditorWindow();
        window.OpenCustom();
        window.Focus(EditorFields.Tags);

        EditorFields.TypeOnFocused("a b");

        Assert.Equal("ab", window.Typed);
    }

    [WpfTheory]
    [InlineData(" ")]
    [InlineData("\t")]
    public void Typing_only_whitespace_over_selected_text_changes_nothing(string whitespace)
    {
        using var window = new TagEditorWindow();
        window.OpenCustom();
        window.Focus(EditorFields.Tags);
        window.Type("home");
        var box = (TextBox)EditorFields.Field(window.Editor, EditorFields.Tags);
        box.Select(1, 2);

        EditorFields.TypeOnFocused(whitespace);

        Assert.Equal("home", window.Typed);
        Assert.Equal((1, 2), (box.SelectionStart, box.SelectionLength));
    }

    [WpfFact]
    public void A_pasted_text_becomes_lowercase_with_its_whitespace_removed_so_words_join()
    {
        using var window = new TagEditorWindow();
        window.OpenCustom();
        window.Focus(EditorFields.Tags);
        var box = (TextBox)EditorFields.Field(window.Editor, EditorFields.Tags);
        var args = new DataObjectPastingEventArgs(new DataObject(DataFormats.UnicodeText, "Big \r\n Errand"), false, DataFormats.UnicodeText)
        {
            RoutedEvent = DataObject.PastingEvent,
        };

        box.RaiseEvent(args);

        Assert.Equal("bigerrand", args.DataObject.GetData(DataFormats.UnicodeText));
    }

    [WpfFact]
    public void A_typed_tag_matching_an_existing_one_in_other_letters_is_added_with_the_stored_spelling()
    {
        using var window = new TagEditorWindow();
        window.Services.UseTag("Work");
        window.OpenCustom();
        window.Focus(EditorFields.Tags);
        EditorFields.TypeOnFocused("WORK");

        window.Press(Key.Enter);

        Assert.Equal("Work", window.Editor.TaskTag);
    }

    [WpfFact]
    public void An_existing_tag_keeps_its_spelling_in_the_pill_and_the_menu()
    {
        using var window = new TagEditorWindow("Work");
        window.Services.UseTag("Errand");
        window.ClickPill();

        Assert.Equal("Work", window.PillText);
        Assert.Equal(["Errand"], TagControls.MenuTags(window.Editor));
    }
}
