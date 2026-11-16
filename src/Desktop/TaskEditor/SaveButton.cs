using System.Windows;
using System.Windows.Controls;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>The Save button an editor shows at its bottom right: the theme's accent look, with a tooltip that names the Save keys.</summary>
internal static class SaveButton
{
    public static Button Create(SaveKeys saveKeys, Action save)
    {
        ArgumentNullException.ThrowIfNull(saveKeys);
        ArgumentNullException.ThrowIfNull(save);
        var button = new Button
        {
            Content = "Save",
            ToolTip = Tip(saveKeys),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        // The Save keys can change while the button lives, so the tip names them as they are when it opens.
        button.ToolTipOpening += (_, _) => button.ToolTip = Tip(saveKeys);
        button.SetResourceReference(FrameworkElement.StyleProperty, "AccentButtonStyle");
        button.Click += (_, _) => save();
        return button;
    }

    private static string Tip(SaveKeys saveKeys) =>
        saveKeys.Text.Length == 0 ? "Save the task" : $"Save the task ({saveKeys.Text})";
}
