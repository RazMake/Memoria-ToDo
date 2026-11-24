using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// The Application section's content: three cards, Update, Shortcuts, and AI assistants. The Update card has the theme's
/// caution background while a version is available.
/// </summary>
internal sealed class ApplicationSection : StackPanel
{
    public ApplicationSection(UpdateCard update, ShortcutSection shortcuts, AiAssistantsCard aiAssistants, ITheme theme)
    {
        var updateBox = ThemedBox.Card("Update", update, theme);
        void ShowCaution() => ThemedBox.ShowCaution(updateBox, update.Cautioned);
        update.CautionChanged += ShowCaution;
        ShowCaution();
        var shortcutsBox = ThemedBox.Card("Shortcuts", shortcuts, theme);
        shortcutsBox.Margin = new Thickness(0, 12, 0, 0);
        Children.Add(updateBox);
        var aiBox = ThemedBox.Card("AI assistants", aiAssistants, theme);
        aiBox.Margin = new Thickness(0, 12, 0, 0);
        Children.Add(shortcutsBox);
        Children.Add(aiBox);
    }
}
