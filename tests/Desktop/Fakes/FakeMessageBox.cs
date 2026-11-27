using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>The shared fake of <see cref="IMessageBox"/>. It shows nothing and records the texts it was asked to show, in order.</summary>
public sealed class FakeMessageBox : IMessageBox
{
    /// <summary>The texts shown so far, in order.</summary>
    public List<string> Texts { get; } = [];

    /// <summary>Runs at the moment a text is shown, before <see cref="Show"/> returns, so a test sees what the app looks like while the real box blocks. Null does nothing.</summary>
    public Action<string>? WhenShown { get; set; }

    /// <inheritdoc />
    public void Show(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Texts.Add(text);
        WhenShown?.Invoke(text);
    }
}
