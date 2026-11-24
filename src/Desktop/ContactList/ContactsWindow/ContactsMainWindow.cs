using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Contacts;
using Serilog;

namespace Memoria.ToDo.Desktop.ContactList;

/// <summary>
/// The Contacts window: the search band, the message and problems lines on top, and below them the list on the left and the details
/// or the form on the right. It shows what its <see cref="ContactsModel"/> holds after every action, and after Contacts changes.
/// </summary>
internal sealed class ContactsMainWindow : Window
{
    private readonly IContacts _contacts;
    private readonly ContactsModel _model;
    private readonly SearchBand _search = new();
    private readonly MessageLine _message = new() { Margin = new Thickness(8, 4, 8, 0) };
    private readonly ContactProblemsLine _problems = new();
    private readonly ContactListView _list;
    private readonly ContactDetailsView _details;
    private readonly SaveKeys _saveKeys;
    private readonly HeldModifierKeys _held = new();
    private bool _closed;

    public ContactsMainWindow(IContacts contacts, TimeProvider time, SaveKeys saveKeys, IDragPointer pointer, ILogger logger)
    {
        _contacts = contacts;
        _saveKeys = saveKeys;
        _model = new ContactsModel(contacts, new WindowPrompts(this, logger), logger);
        _list = new ContactListView(_model, time, pointer, Render, () => _search.FocusText());
        _details = new ContactDetailsView(_model, Render);

        Title = "Contacts";
        Width = 1100;
        Height = 720;
        MinWidth = 680;

        var top = new StackPanel();
        top.Children.Add(_search);
        top.Children.Add(_message);
        top.Children.Add(_problems);
        var layout = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        layout.Children.Add(top);
        layout.Children.Add(new SplitPanes(_list, 40, 260, _details, 60, 340));
        Content = layout;

        _search.TextChanged += (_, _) =>
        {
            if (_model.SearchText != _search.Text)
            {
                _model.SearchText = _search.Text;
                Render();
            }
        };
        _search.EdgeRowRequested += (_, last) => _list.SelectEdgeRow(last);
        Loaded += (_, _) => _search.FocusText();
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewKeyUp += (_, e) => _held.Released(EventKey.Of(e));
        Deactivated += (_, _) => _held.Clear();
        // Esc leaves an open form first; the platform's Esc rule, added after it, closes the window when the form didn't take the key.
        KeyDown += OnKeyDown;
        EscCloses.Attach(this);
        contacts.Changed += OnContactsChanged;
        Closed += (_, _) =>
        {
            _closed = true;
            contacts.Changed -= OnContactsChanged;
        };
        Render();
    }

    /// <summary>Empties the search and puts the cursor there, as opening the window again does.</summary>
    public void StartSearch()
    {
        _search.Text = "";
        _search.FocusText();
    }

    /// <summary>Closes the window as its close button does. False when the user cancels, the save fails, or the question already shows.</summary>
    public bool TryClose()
    {
        _details.CommitTypedDates();
        var mayClose = _model.MayClose();

        // The application ending can close the window while the question shows; it is closed then, whatever the answer.
        if (_closed)
        {
            return true;
        }

        if (mayClose)
        {
            Close();
        }
        else
        {
            // The question's Save can fail: show why, on the message line or under the fields.
            _details.ShowFormProblems();
            Render();
        }

        return mayClose;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        CloseCommandHook.Attach(this, () => TryClose());
    }

    private void Render()
    {
        // A search the model emptied, such as by following a link the search hid, empties the band too, which filters through its one path.
        if (_search.Text != _model.SearchText)
        {
            _search.Text = _model.SearchText;
        }

        _list.Show();
        _details.Show();
        _message.Show(_model.Message);
        _problems.Show(_model.Problems);
    }

    // Contacts says it changed on the thread of the call that changed it.
    private void OnContactsChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(() =>
        {
            _model.Refresh();
            Render();
        });

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var key = EventKey.Of(e);
        if (ModifierKey.Of(key) != ModifierKeys.None)
        {
            _held.Pressed(key);
        }
        else if (_model.Form is not null && _saveKeys.Matches(_held.Modifiers, key))
        {
            e.Handled = true;
            _details.Save();
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (!e.Handled && EventKey.Of(e) == Key.Escape && !_held.AnyHeld && _model.Form is not null)
        {
            e.Handled = true;
            _details.CommitTypedDates();
            _model.LeaveForm();
            Render();
        }
    }
}
