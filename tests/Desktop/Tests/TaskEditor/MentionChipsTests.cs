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
        public void It_shows_the_stored_text_with_no_repository()
        {
            var text = MentionChips.TextOf(_services.Contacts, Chip(0, stored: "Ana Old"));

            Assert.Equal("Ana Old", text);
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
