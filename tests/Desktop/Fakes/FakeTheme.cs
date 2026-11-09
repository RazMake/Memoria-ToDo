using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.Fakes;

/// <summary>The shared fake of <see cref="ITheme"/>. A test sets <see cref="IsDark"/> and raises <see cref="Changed"/> itself.</summary>
public sealed class FakeTheme : ITheme
{
    /// <summary>Starts light.</summary>
    public bool IsDark { get; set; }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Raises <see cref="Changed"/> on the calling thread, as Windows switching the theme does.</summary>
    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>Sets <see cref="IsDark"/>, then raises <see cref="Changed"/>.</summary>
    public void Switch(bool isDark)
    {
        IsDark = isDark;
        RaiseChanged();
    }
}
