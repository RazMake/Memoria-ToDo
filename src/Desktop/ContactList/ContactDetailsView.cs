using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>
/// The right side, found on screen by its automation id "Details": the selected contact's details with Edit and Delete, the form
/// while one is open, or the text that asks the user to pick a contact.
/// </summary>
internal sealed class ContactDetailsView : ScrollViewer
{
    private readonly ContactsModel _model;
    private readonly Action _changed;
    private ContactFormView? _formView;

    public ContactDetailsView(ContactsModel model, Action changed)
    {
        _model = model;
        _changed = changed;
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        AutomationProperties.SetAutomationId(this, "Details");
    }

    /// <summary>Shows what the model holds. A form stays the same view while it's open, so typing isn't disturbed.</summary>
    public void Show()
    {
        if (_model.Form is { } form)
        {
            if (!ReferenceEquals(_formView?.Form, form))
            {
                var headings = _model.Groups.FirstOrDefault(group => group.Name == form.GroupName)?.Heading ?? form.GroupName;
                _formView = new ContactFormView(form, headings, Save);
                Content = _formView;
            }
            else
            {
                _formView.Follow();
            }

            return;
        }

        _formView = null;
        Content = _model.Details is { } details ? DetailsOf(details) : new TextBlock { Text = _model.DetailsText, Margin = new Thickness(16), TextWrapping = TextWrapping.Wrap };
    }

    /// <summary>Takes the dates typed into the form's date pickers that they haven't committed yet, so a save or a leave decision sees them. Does nothing with no form open.</summary>
    public void CommitTypedDates() => _formView?.CommitTypedDates();

    /// <summary>Shows the problems of every field of the open form, as a failed save does. Does nothing with no form open.</summary>
    public void ShowFormProblems() => _formView?.ShowProblems(saveTried: true);

    /// <summary>Saves the open form, showing every problem it has. Does nothing with no form open.</summary>
    public void Save()
    {
        CommitTypedDates();
        ShowFormProblems();
        _model.SaveForm();
        _changed();
    }

    private StackPanel DetailsOf(ContactDetails details)
    {
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(DetailBlocks.Heading(details.Heading));
        panel.Children.Add(DetailBlocks.GroupHeading(details.GroupHeading));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        if (_model.CanEdit)
        {
            buttons.Children.Add(ButtonOf("Edit", () => _model.BeginEdit()));
        }

        if (_model.CanDelete)
        {
            buttons.Children.Add(ButtonOf("Delete", () => _model.Delete()));
        }

        panel.Children.Add(buttons);
        panel.Children.Add(DetailBlocks.Fields(details.Fields, target =>
        {
            _model.Select(target.GroupName, target.Id);
            _changed();
        }));
        return panel;
    }

    private Button ButtonOf(string label, Func<bool> action)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 3, 12, 3) };
        button.Click += (_, _) =>
        {
            action();
            _changed();
        };
        return button;
    }
}
