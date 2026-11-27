using Memoria.ToDo.Android.Platform;

namespace Memoria.ToDo.Android.TaskEditor;

/// <summary>Add, shown by asking the opener for its activity.</summary>
internal sealed class OpeningAddScreen(IScreenOpener screens) : IAddScreen
{
    public void Show() => screens.Open(AddActivityName.Value);
}
