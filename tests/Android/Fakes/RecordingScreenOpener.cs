using Memoria.ToDo.Android.Platform;

namespace Memoria.ToDo.Android.Fakes;

/// <summary>An <see cref="IScreenOpener"/> that records the name of each activity it was asked to open, in order.</summary>
public sealed class RecordingScreenOpener : IScreenOpener
{
    private readonly List<string> opened = [];

    /// <summary>The activity names opened so far, oldest first.</summary>
    public IReadOnlyList<string> Opened => opened;

    /// <inheritdoc />
    public void Open(string activityName) => opened.Add(activityName);
}
