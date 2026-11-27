using System.IO;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Contacts;
using Memoria.ToDo.Shared.Platform;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>
/// The "@" list of a field that holds mentions: its header shows the contact moved to in the current mention template, Tab or a
/// click on the header moves to the next template, and choosing a contact inserts its mention in that template, then moves that
/// template first in the templates file. A move that fails keeps the mention, is logged, and is passed to <paramref name="report"/>.
/// </summary>
internal sealed class MentionPicking(ITextField field, ChoiceList list, PopupLists popups, IContacts contacts, Serilog.ILogger? logger = null, Action<string>? report = null)
    : ContactPicking(field, list, popups, contacts, logger)
{
    private readonly Serilog.ILogger _log = logger ?? Serilog.Core.Logger.None;
    private int _template;
    private string _usedTemplate = "";

    /// <summary>The contact moved to in the current template, or the first row's contact while none is moved to.</summary>
    public override string Header => IsShowing ? Contacts.TextOf(Highlighted, CurrentTemplate) : "";

    /// <summary>"Tab or click: next template" while there are two or more templates.</summary>
    public override string TemplateHint => IsShowing && Contacts.MentionTemplates.Count > 1 ? "Tab or click: next template" : "";

    /// <summary>Moves to the next template. Returns true when the list is open, so the key stays in the field.</summary>
    public bool NextTemplate()
    {
        if (!IsShowing)
        {
            return false;
        }

        _template++;
        List.Refresh();
        return true;
    }

    protected override void Opening() => _template = 0;

    // The mention is already in; a failed move leaves it there, and the list keeps its order. A move kept before a notice failed
    // is a move that worked, which Run reports with the shared message.
    protected override void Chosen(Contact contact)
    {
        var template = _usedTemplate;
        StoreFailures.Run(
            () =>
            {
                Contacts.MoveTemplateFirst(template);
                _log.Debug("Asked Contacts to move the mention template {Template} first after choosing {ContactId}", template, contact.Id);
            },
            failure => failure is NoRepositoryFolderException or InvalidDataException or IOException or UnauthorizedAccessException
                ? "The mention format wasn't moved first: " + failure.Message
                : null,
            message => report?.Invoke(message),
            _log);
    }

    // Read once per choice: Contacts may reload while the text is inserted, so Chosen moves the template TextFor used.
    protected override string TextFor(Contact contact)
    {
        _usedTemplate = CurrentTemplate;
        return Contacts.MentionOf(contact, _usedTemplate);
    }

    private string CurrentTemplate
    {
        get
        {
            var templates = Contacts.MentionTemplates;
            return templates[_template % templates.Count];
        }
    }
}
