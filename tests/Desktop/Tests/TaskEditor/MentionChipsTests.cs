using System.Windows;
using Memoria.ToDo.Desktop.FormattedView;
using Memoria.ToDo.Desktop.FormattedView.Tests;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Editing;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The rules of a mention chip: the text it shows, and how Backspace and Delete remove it whole. Which text is a mention is Rendering's; the tests give the mentions literally.
/// </summary>
public class MentionChipsTests
{
    private const string Ana = "[Ana Pop](contact:ana \"{fullName}\")";

    private static readonly int AnaLength = Ana.Length;

    private static Mention Chip(int start, string address = "contact:ana", string template = "{fullName}", string stored = "Ana Pop") =>
        new(start, Ana.Length, address, template, stored);

    // "a [Ana Pop](...) b": the chip runs from 2 to 2 + AnaLength.
    private static readonly string Sentence = $"a {Ana} b";
    private static readonly Mention[] One = [Chip(2)];
    private static readonly int ChipEnd = 2 + AnaLength;

    [Unit]
    public class What_a_chip_shows
    {
        private readonly EditorServices _services = new();

        [Fact]
        public void It_shows_the_contacts_live_text_in_the_mentions_template_not_the_stored_text()
        {
            _services.UseContacts(["{fullName}"], ContactFiles.Person("ana", "Ana Pop", ("title", "Engineer")));

            var text = MentionChips.TextOf(_services.Contacts, Chip(0, template: "{fullName} ({title})", stored: "Old Name"));

            Assert.Equal("Ana Pop (Engineer)", text);
        }

        [Fact]
        public void It_shows_the_stored_text_when_no_contact_has_the_address()
        {
            _services.UseContacts(["{fullName}"], ContactFiles.Person("ana", "Ana Pop"));

            var text = MentionChips.TextOf(_services.Contacts, Chip(0, address: "contact:zed", stored: "Zed Old"));

            Assert.Equal("Zed Old", text);
        }

        [Fact]
        public void It_shows_the_stored_text_when_the_template_is_not_valid()
        {
            _services.UseContacts(["{fullName}"], ContactFiles.Person("ana", "Ana Pop"));

            var text = MentionChips.TextOf(_services.Contacts, Chip(0, template: "{nope}", stored: "Ana Old"));

            Assert.Equal("Ana Old", text);
        }

        [Fact]
        public void It_is_Markdown_with_each_value_escaped_when_live_and_the_stored_text_as_written_when_not()
        {
            _services.UseContacts(["{fullName}"], ContactFiles.Person("ana", "Ana *QA*", ("alias", "anap")));

            var live = MentionChips.TextOf(_services.Contacts, Chip(0, template: "{fullName} (_{alias}_)", stored: "Old"));
            var stored = MentionChips.TextOf(_services.Contacts, Chip(0, address: "contact:zed", stored: "Zed \\*QA\\* (_zz_)"));

            Assert.Equal("Ana \\*QA\\* (_anap_)", live);
            Assert.Equal("Zed \\*QA\\* (_zz_)", stored);
        }

        [Fact]
        public void It_shows_the_stored_text_with_no_repository()
        {
            var text = MentionChips.TextOf(_services.Contacts, Chip(0, stored: "Ana Old"));

            Assert.Equal("Ana Old", text);
        }
    }

    [Unit]
    public class What_an_editor_shows_for_a_mention
    {
        private const string Template = "{fullName} (_{alias}_)";

        private readonly EditorServices _services = new();

        public What_an_editor_shows_for_a_mention()
        {
            _services.UseRepository();
            _services.UseContacts(["{fullName}"], ContactFiles.Person("ana", "Ana Pop", ("alias", "anap")));
        }

        // An editor over the services, holding the text and laid out, never shown; its chips are the mention chips in its text view.
        private FrameworkElement Editor(string field, string text)
        {
            FrameworkElement editor;
            if (field == EditorFields.Title)
            {
                var title = _services.CreateTitleEditor();
                title.Value = text;
                editor = title;
            }
            else
            {
                var description = _services.CreateDescriptionEditor();
                description.TaskId = _services.Tasks.StartDraft().TaskId;
                description.Value = text;
                editor = description;
            }

            return editor;
        }

        private static IReadOnlyList<MentionChip> ChipsIn(FrameworkElement editor, string field)
        {
            editor.Measure(new Size(480, 300));
            editor.Arrange(new Rect(0, 0, 480, 300));
            editor.UpdateLayout();
            var view = ((StyledTextEditor)EditorFields.Field(editor, field)).TextArea.TextView;
            view.EnsureVisualLines();
            Assert.NotEmpty(view.VisualLines);
            return [.. VisualTree.Visuals(view).OfType<MentionChip>()];
        }

        [WpfTheory]
        [InlineData(EditorFields.Title)]
        [InlineData(EditorFields.Description)]
        public void A_mention_shows_as_a_chip_of_the_live_text_with_the_alias_in_italic_and_no_underscore(string field)
        {
            var editor = Editor(field, $"Call [Old](contact:ana \"{Template}\") today");

            var chip = Assert.Single(ChipsIn(editor, field));

            Assert.Equal("Ana Pop (anap)", ChipRuns.ShownBy(chip));
            Assert.True(RunStyles.IsItalic(ChipRuns.Run(chip, "anap")), "'anap' isn't italic.");
            Assert.False(RunStyles.IsItalic(ChipRuns.Run(chip, "Ana Pop")), "'Ana Pop' is italic too.");
        }

        [WpfTheory]
        [InlineData(EditorFields.Title)]
        [InlineData(EditorFields.Description)]
        public void A_mention_with_no_live_text_shows_its_stored_text_formatted_the_same_way(string field)
        {
            var editor = Editor(field, "Call [Ana \\*QA\\*](contact:zed \"{fullName}\") today");

            var chip = Assert.Single(ChipsIn(editor, field));

            Assert.Equal("Ana *QA*", ChipRuns.ShownBy(chip));
            Assert.False(RunStyles.IsItalic(ChipRuns.Run(chip, "QA")), "'QA' is italic.");
        }

        [WpfTheory]
        [InlineData(EditorFields.Title)]
        [InlineData(EditorFields.Description)]
        public void The_chip_shows_a_contacts_escaped_value_as_written_and_the_Markdown_stays_in_the_text(string field)
        {
            _services.UseContacts(["{fullName}"], ContactFiles.Person("ana", "Ana *QA*", ("alias", "anap")));
            var markdown = $"[Old](contact:ana \"{Template}\")";
            var editor = Editor(field, markdown);

            var chip = Assert.Single(ChipsIn(editor, field));

            Assert.Equal("Ana *QA* (anap)", ChipRuns.ShownBy(chip));
            Assert.Equal(markdown, EditorFields.TextOf(editor, field));
        }

        [WpfTheory]
        [InlineData(EditorFields.Title)]
        [InlineData(EditorFields.Description)]
        public void The_chip_shows_the_new_text_after_contacts_change_while_the_editor_is_loaded(string field)
        {
            var editor = Editor(field, $"[Old](contact:ana \"{Template}\")");
            Assert.Equal("Ana Pop (anap)", ChipRuns.ShownBy(Assert.Single(ChipsIn(editor, field))));
            EditorFields.Field(editor, field).RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));

            _services.ChangeContacts(ContactFiles.Person("ana", "Ana Maria Pop", ("alias", "anap")));
            editor.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);

            Assert.Equal("Ana Maria Pop (anap)", ChipRuns.ShownBy(Assert.Single(ChipsIn(editor, field))));
            Assert.Equal($"[Old](contact:ana \"{Template}\")", EditorFields.TextOf(editor, field));
        }
    }

    [Unit]
    public class Which_chip_a_place_is_on
    {
        [Fact]
        public void A_place_inside_a_chip_is_on_that_chip()
        {
            Assert.Equal(One[0], MentionChips.At(One, 2));
            Assert.Equal(One[0], MentionChips.At(One, 5));
            Assert.Equal(One[0], MentionChips.At(One, ChipEnd - 1));
        }

        [Fact]
        public void A_place_before_or_after_a_chip_is_on_none_and_the_end_is_exclusive()
        {
            Assert.Null(MentionChips.At(One, 1));
            Assert.Null(MentionChips.At(One, ChipEnd));
        }

        [Fact]
        public void A_place_is_on_the_chip_that_holds_it_when_there_are_two()
        {
            var two = new[] { Chip(0), Chip(AnaLength + 3, address: "contact:bob") };

            Assert.Equal("contact:bob", MentionChips.At(two, AnaLength + 4)!.Address);
            Assert.Equal("contact:ana", MentionChips.At(two, 1)!.Address);
            Assert.Null(MentionChips.At(two, AnaLength + 1));
        }

        [Fact]
        public void No_chips_means_no_chip()
        {
            Assert.Null(MentionChips.At([], 0));
        }

        [Fact]
        public void A_place_outside_the_text_is_on_no_chip()
        {
            Assert.Null(MentionChips.At(One, -1));
        }
    }

    [Unit]
    public class Backspace_and_Delete_at_a_chip
    {
        [Fact]
        public void Backspace_just_after_a_chip_removes_the_whole_mention_and_puts_the_cursor_where_it_was()
        {
            var result = MentionChips.Backspace(new TextAndSelection(Sentence, ChipEnd, 0), One);

            Assert.Equal(new TextAndSelection("a  b", 2, 0), result);
        }

        [Fact]
        public void Backspace_after_a_chip_at_the_start_of_the_text_leaves_the_rest()
        {
            var result = MentionChips.Backspace(new TextAndSelection(Ana + "!", AnaLength, 0), [Chip(0)]);

            Assert.Equal(new TextAndSelection("!", 0, 0), result);
        }

        [Fact]
        public void Backspace_elsewhere_is_left_to_the_editor()
        {
            Assert.Null(MentionChips.Backspace(new TextAndSelection(Sentence, ChipEnd + 1, 0), One));
            Assert.Null(MentionChips.Backspace(new TextAndSelection(Sentence, 2, 0), One));
        }

        [Fact]
        public void Backspace_with_a_selection_is_left_to_the_editor()
        {
            Assert.Null(MentionChips.Backspace(new TextAndSelection(Sentence, ChipEnd, 1), One));
        }

        [Fact]
        public void Delete_just_before_a_chip_removes_the_whole_mention_and_leaves_the_cursor_there()
        {
            var result = MentionChips.Delete(new TextAndSelection(Sentence, 2, 0), One);

            Assert.Equal(new TextAndSelection("a  b", 2, 0), result);
        }

        [Fact]
        public void Delete_elsewhere_is_left_to_the_editor()
        {
            Assert.Null(MentionChips.Delete(new TextAndSelection(Sentence, 1, 0), One));
            Assert.Null(MentionChips.Delete(new TextAndSelection(Sentence, ChipEnd, 0), One));
        }

        [Fact]
        public void Delete_with_a_selection_is_left_to_the_editor()
        {
            Assert.Null(MentionChips.Delete(new TextAndSelection(Sentence, 2, 1), One));
        }

        [Fact]
        public void The_result_is_a_change_the_editor_can_make_as_one_undo_step()
        {
            var field = new FakeTextField(Sentence, ChipEnd);

            Assert.True(field.Run(current => MentionChips.Backspace(current, One)));
            field.Undo();

            Assert.Equal(Sentence, field.Text);
        }
    }
}
