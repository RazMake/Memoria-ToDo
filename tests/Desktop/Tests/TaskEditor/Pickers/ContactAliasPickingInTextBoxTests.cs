using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests.Pickers;

/// <summary>
/// The "@" list over a real WPF text box, whose events come in the order the box raises them, not in a fake field's order. Erasing
/// is done with the box's own Backspace command, as the key does. The rules are in <see cref="ContactAliasPickingTests"/>, over a fake field.
/// </summary>
[Unit]
public class ContactAliasPickingInTextBoxTests
{
    private readonly EditorServices _services = new();
    private readonly ChoiceList _list = new();

    [WpfFact]
    public void Backspace_after_Esc_closed_the_list_back_into_a_partial_word_opens_it_for_the_shortened_query()
    {
        _services.UseContacts(["{fullName}"],
            ContactFiles.Person("ana", "Ana Pop", ("alias", "anap")),
            ContactFiles.Person("bob", "Bob Rus"));
        var box = new TextBox();
        _ = new ContactAliasPicking(new TextBoxField(box), _list, new PopupLists(_list), _services.Contacts);
        foreach (var character in "@ana")
        {
            box.RaiseEvent(new TextCompositionEventArgs(Keyboard.PrimaryDevice, new TextComposition(InputManager.Current, box, character.ToString()))
            {
                RoutedEvent = TextCompositionManager.TextInputEvent,
            });
        }

        Assert.Equal("@ana", box.Text);
        Assert.True(_list.IsOpen);
        Assert.True(_list.Close());

        EditingCommands.Backspace.Execute(null, box);

        Assert.Equal("@an", box.Text);
        Assert.Equal(["Ana Pop (anap)"], _list.Items);
    }
}
