using Xunit;

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

    /// <summary>
    /// Returns when the running test is in the screen collection, so its <paramref name="use"/> of the screen, such as "shows a window",
    /// can't disturb another class's. Throws <see cref="InvalidOperationException"/> naming the test class and the attribute it
    /// needs when the test is in another collection, or no test is running.
    /// </summary>
    public static void RequireScreenTest(string use)
    {
        if (RefusalOfNonScreenTest(use) is { } refusal)
        {
            throw new InvalidOperationException(refusal);
        }
    }

    /// <summary>
    /// The message <see cref="RequireScreenTest"/> throws, or null when the running test is in the screen collection.
    /// </summary>
    public static string? RefusalOfNonScreenTest(string use)
    {
        var context = TestContext.Current;
        if (context.TestCollection?.TestCollectionDisplayName == Collection)
        {
            return null;
        }

        var running = context.TestClass?.TestClassName ?? "no test class";
        return $"A test outside the screen collection {use}, which can take the activation, focus, or pointer from another class's test. "
            + $"Running test class: {running}. Add [Collection(ScreenTests.Collection)] to it.";
    }
}
