using System.Windows.Documents;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Shared.Rendering;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>
/// The contact list over the real editor of the Title and the Description, whose events come in the order AvalonEdit raises them,
/// not in a fake field's order. Erasing is done with the editor's own Backspace command, as the key does. The rules are in
/// <see cref="MentionPickingTests"/>, over a fake field.
/// </summary>
[Unit]
public class MentionPickingInEditorTests
{
    private readonly EditorServices _services = new();
    private readonly ChoiceList _list = new();
    private readonly StyledTextEditor _editor;

    public MentionPickingInEditorTests()
    {
        _services.UseContacts(["{fullName}"],
            ContactFiles.Person("ana", "Ana Pop"),
            ContactFiles.Person("bob", "Bob Rus"));
        _editor = new StyledTextEditor("Description", MarkdownField.Description, _services.Theme, _services.Contacts, _services.Pointer, Serilog.Core.Logger.None);
        _ = new MentionPicking(_editor, _list, new PopupLists(_list), _services.Contacts);
    }

    [WpfFact]
    public void Backspace_after_Esc_closed_the_list_back_into_a_partial_mention_opens_it_for_the_shortened_query()
    {
        foreach (var character in "@ana")
        {
            _editor.TextArea.PerformTextInput(character.ToString());
        }
        Assert.True(_list.IsOpen);
        Assert.True(_list.Close());
        Assert.False(_list.IsOpen);

        EditingCommands.Backspace.Execute(null, _editor.TextArea);

        Assert.Equal("@an", _editor.Text);
        Assert.Equal(["Ana Pop"], _list.Items.Select(item => item.Split(" (")[0]));
    }
}
