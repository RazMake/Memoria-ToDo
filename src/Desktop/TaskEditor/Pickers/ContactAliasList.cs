using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;
using Memoria.ToDo.Shared.Contacts;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Makes "@" in a plain text box offer the contacts by alias in a popup under it, so a box such as Task View's search can filter by contact.</summary>
public static class ContactAliasList
{
    private static readonly DependencyProperty KeysProperty =
        DependencyProperty.RegisterAttached("Keys", typeof(KeyMap), typeof(ContactAliasList));

    static ContactAliasList()
    {
        // While its list is open, a box takes Down, Up, Enter, and Esc before its owner's own preview handlers see them.
        EventManager.RegisterClassHandler(typeof(UIElement), Keyboard.PreviewKeyDownEvent, new KeyEventHandler(OnPreviewKeyDown));
    }

    /// <summary>
    /// Typing "@" in the box lists the contacts under it, each as its full name and alias. The list narrows as the user types;
    /// Down and Up move in it, Enter chooses the contact moved to and leaves "@" and its alias, or its ID when it has no alias,
    /// where "@" and what was typed were, and Esc closes the list. Tab closes the list and moves the focus as usual. The list
    /// closes when the box loses the focus or its window moves, changes size, or is deactivated. The list logs to <paramref name="logger"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static void Attach(TextBox box, IContacts contacts, ITheme theme, Serilog.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(box);
        ArgumentNullException.ThrowIfNull(contacts);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(logger);

        var choices = new ChoiceList();
        var popups = new PopupLists(choices);
        var field = new TextBoxField(box);
        var picking = new ContactAliasPicking(field, choices, popups, contacts, logger);
        _ = new MentionPopup(choices, () => picking.IsShowing ? picking : null, new Dictionary<ITextField, IPopupPlace> { [field] = field }, () => false, theme);
        PopupClosers.Attach(box, () => choices.Close());
        box.SetValue(KeysProperty, popups.AddKeysTo(new KeyMap())
            .Try(ModifierKeys.None, Key.Escape, popups.CloseOpen)
            .Try(ModifierKeys.None, Key.Tab, picking.Tab));
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!e.Handled
            && e.OriginalSource is TextBox box
            && box.GetValue(KeysProperty) is KeyMap keys
            && keys.TryRun(Keyboard.Modifiers, EventKey.Of(e)))
        {
            e.Handled = true;
        }
    }
}
