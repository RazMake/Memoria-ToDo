using System.Windows;
using System.Windows.Controls;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>The Behavior section's content: the app's switches as three rows with no cards, in order.</summary>
internal sealed class BehaviorSection : StackPanel
{
    public BehaviorSection(StartWithWindowsRow startWithWindows, KeepAwakeRow keepAwake, AiAssistantsRow aiAssistants)
    {
        keepAwake.Margin = new Thickness(0, 16, 0, 0);
        aiAssistants.Margin = new Thickness(0, 16, 0, 0);
        Children.Add(startWithWindows);
        Children.Add(keepAwake);
        Children.Add(aiAssistants);
    }
}
