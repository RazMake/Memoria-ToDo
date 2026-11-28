using Memoria.ToDo.Android.Platform;

namespace Memoria.ToDo.Android.Shell;

/// <summary>What the entry activity needs. Connect sets it and the activity reads it.</summary>
internal static class EntryServices
{
    public static ScreenServices<AppOpening> Current { get; } = new();
}
