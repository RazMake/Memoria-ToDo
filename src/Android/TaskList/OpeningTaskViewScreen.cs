using Memoria.ToDo.Android.Platform;

namespace Memoria.ToDo.Android.TaskList;

/// <summary>Task View, shown by asking the opener for its activity.</summary>
internal sealed class OpeningTaskViewScreen(IScreenOpener screens) : ITaskViewScreen
{
    public void Show() => screens.Open(TaskViewActivityName.Value);
}
