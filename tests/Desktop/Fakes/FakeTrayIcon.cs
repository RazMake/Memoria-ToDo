using Memoria.ToDo.Desktop.Shell;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>
/// The shared fake of <see cref="ITrayIcon"/>. It remembers what was shown, lets a test click as a user would,
/// and can be told to fail when shown, as Windows does when it can't add the icon.
/// </summary>
public sealed class FakeTrayIcon : ITrayIcon
{
    private IReadOnlyList<TrayMenuItem> menu = [];
    private Action leftClick = () => { };

    /// <summary>Whether the icon is in the tray now.</summary>
    public bool IsShown { get; private set; }

    /// <summary>The tooltip of the icon last shown.</summary>
    public string? ToolTip { get; private set; }

    /// <summary>The texts of the menu last shown, in order.</summary>
    public IReadOnlyList<string> MenuTexts => [.. menu.Select(item => item.Text)];

    /// <summary>How many times the icon was added.</summary>
    public int ShowCount { get; private set; }

    /// <summary>When set, <see cref="Show"/> throws it and the icon isn't added.</summary>
    public Exception? ShowFailure { get; set; }

    /// <inheritdoc />
    public void Show(string toolTip, IReadOnlyList<TrayMenuItem> menu, Action leftClick)
    {
        if (ShowFailure is not null)
        {
            throw ShowFailure;
        }

        ToolTip = toolTip;
        this.menu = menu;
        this.leftClick = leftClick;
        IsShown = true;
        ShowCount++;
    }

    /// <inheritdoc />
    public void Remove() => IsShown = false;

    /// <summary>Chooses the menu item with this text.</summary>
    /// <exception cref="InvalidOperationException">The icon isn't shown, or has no such item.</exception>
    public void ChooseMenuItem(string text)
    {
        EnsureShown();
        var item = menu.FirstOrDefault(i => i.Text == text)
            ?? throw new InvalidOperationException($"The tray menu has no item '{text}'.");
        item.Invoke();
    }

    /// <summary>Clicks the icon with the left mouse button.</summary>
    /// <exception cref="InvalidOperationException">The icon isn't shown.</exception>
    public void LeftClick()
    {
        EnsureShown();
        leftClick();
    }

    private void EnsureShown()
    {
        if (!IsShown)
        {
            throw new InvalidOperationException("The tray icon isn't shown.");
        }
    }
}
