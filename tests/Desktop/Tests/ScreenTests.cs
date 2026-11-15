namespace Memoria.ToDo.Desktop;

/// <summary>
/// The xUnit collection of every test class that shows a window, starts a process that does, registers a hot key, or
/// sends input: every UX class and those integration classes. Its classes run one after another. Side by side, one
/// class's window takes the activation, keyboard focus, or pointer from another's, and the Add window hides when it
/// loses activation.
/// </summary>
internal static class ScreenTests
{
    public const string Collection = "Desktop screens";
}
