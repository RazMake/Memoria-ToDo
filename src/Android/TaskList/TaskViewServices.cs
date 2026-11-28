using Memoria.ToDo.Android.Platform;

namespace Memoria.ToDo.Android.TaskList;

/// <summary>What Task View's activity needs. Create sets it and the activity reads it.</summary>
internal static class TaskViewServices
{
    public static ScreenServices<TaskViewActions> Current { get; } = new();
}
