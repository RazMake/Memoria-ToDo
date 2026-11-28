namespace Memoria.ToDo.Desktop.Platform;

/// <summary>A box that tells the user something and waits until they close it.</summary>
public interface IMessageBox
{
    /// <summary>Shows the text with one OK button, and returns when the user closes the box.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    void Show(string text);
}
