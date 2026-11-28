namespace Memoria.ToDo.Android.Platform;

/// <summary>
/// Opens the app's screens. The composition root implements it with Android's application context, and tests
/// pass a fake. See docs/architecture/android/platform.md, "Screens open through one opener the composition root supplies".
/// </summary>
public interface IScreenOpener
{
    /// <summary>
    /// Opens the activity with this fixed name in front of the app's other screens, unless it's in front already.
    /// A name Android can't find throws.
    /// </summary>
    void Open(string activityName);
}
