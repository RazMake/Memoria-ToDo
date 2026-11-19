using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Memoria.ToDo.Shared.Contacts;

namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>
/// The form that edits or adds one contact: a label, an editor, and under it the problem the form's own check finds, for each
/// field the form shows. A problem shows once the user changed its field or tried to save. Each editor has the field's file name as its
/// automation id, such as "title".
/// </summary>
internal sealed class ContactFormView : StackPanel
{
    private const string NotChosen = "";

    private readonly Dictionary<string, TextBlock> _problems = [];
    private readonly Dictionary<string, (StackPanel Slot, string Shape)> _keySlots = [];
    private readonly HashSet<string> _touched = [];
    private Control? _first;
    private bool _saveTried;

    public ContactFormView(ContactForm form, string groupHeading, Action save)
    {
        Form = form;
        Margin = new Thickness(16);
        var title = form.IsNew ? $"New contact in {groupHeading}" : $"Edit {form["fullName"]}";
        Children.Add(new TextBlock { Text = title, FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12), TextWrapping = TextWrapping.Wrap });
        foreach (var name in form.FieldNames)
        {
            var field = form.FieldOf(name);
            var problem = new TextBlock { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
            problem.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
            _problems[name] = problem;
            Children.Add(new TextBlock { Text = FieldLabels.Of(name), Margin = new Thickness(0, 8, 0, 2) });
            if (field.Kind == ContactFieldKind.Key)
            {
                // The data type can stop loading or load again while the form is open, so a key field's editor is made again by Follow.
                var slot = new StackPanel();
                _keySlots[name] = (slot, ShapeOf(name));
                Fill(slot, field);
                Children.Add(slot);
            }
            else
            {
                Children.Add(EditorOf(field));
            }

            Children.Add(problem);
        }

        var saveButton = new Button { Content = "Save", Margin = new Thickness(0, 16, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(16, 4, 16, 4) };
        saveButton.Click += (_, _) => save();
        Children.Add(saveButton);
        if (_first is { } first)
        {
            first.Loaded += (_, _) => first.Focus();
        }
    }

    public ContactForm Form { get; }

    /// <summary>Makes again the editor of each key field whose data type loaded or stopped loading, keeping the typed values.</summary>
    public void Follow()
    {
        foreach (var (name, (slot, shape)) in _keySlots.ToList())
        {
            var now = ShapeOf(name);
            if (now != shape)
            {
                _keySlots[name] = (slot, now);
                Fill(slot, Form.FieldOf(name));
            }
        }
    }

    /// <summary>
    /// Takes the date a date picker's text box holds but hasn't committed yet, as the picker does when it loses focus.
    /// The picker keeps its Text equal to the box's while the user types, and reads it with the current culture,
    /// since no window sets its Language, so the text is read the same way here.
    /// </summary>
    public void CommitTypedDates()
    {
        foreach (var picker in Children.OfType<DatePicker>())
        {
            picker.ApplyTemplate();
            if (picker.Template.FindName("PART_TextBox", picker) is not TextBox box)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(box.Text))
            {
                picker.SelectedDate = null;
            }
            else if (DateTime.TryParse(box.Text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var date))
            {
                picker.SelectedDate = date.Date;
            }
        }
    }

    /// <summary>Shows the problems of the fields the user changed, and of every field once a save was tried.</summary>
    public void ShowProblems(bool saveTried = false)
    {
        _saveTried |= saveTried;
        foreach (var (name, block) in _problems)
        {
            var problem = _saveTried || _touched.Contains(name) ? Form.ProblemOf(name) : null;
            block.Text = problem ?? "";
            block.Visibility = problem is null ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    // What decides a key field's editor: whether it can be picked, which file is wrong, and the choices.
    private string ShapeOf(string name) =>
        string.Join('\n', [Form.IsReadOnly(name).ToString(), Form.DataTypeProblem(name) ?? "", .. Form.ChoicesOf(name).Select(choice => $"{choice.Key}|{choice.Text}|{choice.Mark}")]);

    // A key field's editor, and under it which file is wrong when its data type isn't loaded.
    private void Fill(StackPanel slot, ContactField field)
    {
        slot.Children.Clear();
        slot.Children.Add(EditorOf(field));
        if (Form.DataTypeProblem(field.Name) is { } unreadable)
        {
            slot.Children.Add(new TextBlock { Text = unreadable, TextWrapping = TextWrapping.Wrap });
        }
    }

    private Control EditorOf(ContactField field)
    {
        var editor = NewEditor(Form, field);
        AutomationProperties.SetAutomationId(editor, field.Name);
        AutomationProperties.SetName(editor, FieldLabels.Of(field.Name));
        _first ??= Form.IsReadOnly(field.Name) ? null : editor;
        return editor;
    }

    private Control NewEditor(ContactForm form, ContactField field)
    {
        var name = field.Name;
        switch (field.Kind)
        {
            case ContactFieldKind.Date:
                var picker = new DatePicker { SelectedDate = form.DateOf(name)?.ToDateTime(TimeOnly.MinValue) };
                picker.SelectedDateChanged += (_, _) =>
                {
                    form.SetDate(name, picker.SelectedDate is { } date ? DateOnly.FromDateTime(date) : null);
                    Touched(name);
                };
                return picker;
            case ContactFieldKind.Key when !form.IsReadOnly(name):
                return ChoiceBox(form, name);
            default:
                var box = new TextBox { Text = form[name], IsReadOnly = form.IsReadOnly(name), TextWrapping = TextWrapping.Wrap };
                if (!box.IsReadOnly)
                {
                    box.TextChanged += (_, _) =>
                    {
                        form[name] = box.Text;
                        Touched(name);
                    };
                }

                return box;
        }
    }

    // A key field's drop-down: no typing, a blank first entry to clear the key, then the choices.
    private ComboBox ChoiceBox(ContactForm form, string name)
    {
        var choices = form.ChoicesOf(name);
        var box = new ComboBox { IsEditable = false };
        box.Items.Add(NotChosen);
        foreach (var choice in choices)
        {
            box.Items.Add(choice.Mark is null ? choice.Text : $"{choice.Text} ({choice.Mark})");
        }

        var at = choices.ToList().FindIndex(choice => string.Equals(choice.Key, form[name], StringComparison.OrdinalIgnoreCase));
        box.SelectedIndex = at + 1;
        box.SelectionChanged += (_, _) =>
        {
            form[name] = box.SelectedIndex > 0 ? choices[box.SelectedIndex - 1].Key : "";
            Touched(name);
        };
        return box;
    }

    private void Touched(string name)
    {
        _touched.Add(name);
        ShowProblems();
    }
}
