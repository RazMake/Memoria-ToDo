using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;

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
        panel.Children.Add(FieldsOf(details));
        return panel;
    }

    // One row per field: its label, then its values, each on a subtle background a little larger than the text.
    private Grid FieldsOf(ContactDetails details)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        foreach (var field in details.Fields)
        {
            var row = grid.RowDefinitions.Count;
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock { Text = field.Label, Margin = new Thickness(0, 6 + ValuePadding.Top, 12, 0) };
            ContactHelpStyle.Apply(label);
            Grid.SetRow(label, row);
            grid.Children.Add(label);

            var values = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            foreach (var value in field.Values)
            {
                values.Children.Add(ValueOf(value));
            }

            Grid.SetRow(values, row);
            Grid.SetColumn(values, 1);
            grid.Children.Add(values);
        }

        return grid;
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

    private static readonly Thickness ValuePadding = new(6, 2, 6, 2);

    // A value a click goes from, to the contact it names, looks like a link.
    private Border ValueOf(DetailValue value)
    {
        var block = DetailBlocks.Value(value.Text);
        var box = new Border
        {
            Child = block,
            Padding = ValuePadding,
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 2),
        };
        box.SetResourceReference(Border.BackgroundProperty, "SubtleFillColorSecondaryBrush");
        if (value.Target is { } target)
        {
            block.TextDecorations = TextDecorations.Underline;
            block.Cursor = Cursors.Hand;
            block.MouseLeftButtonUp += (_, _) =>
            {
                _model.Select(target.GroupName, target.Id);
                _changed();
            };
        }

        return box;
    }
}
