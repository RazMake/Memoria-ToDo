using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Platform;

namespace Memoria.ToDo.Desktop.TaskList;

/// <summary>
/// The modal "Unsaved changes" prompt, owned by and centered on Task View. It takes the application's theme as every
/// window does. Save is the default button; Esc and the close button answer Cancel.
/// </summary>
internal sealed class CloseDialog(Window owner) : IClosePrompt
{
    public CloseAnswer Ask()
    {
        var answer = CloseAnswer.Cancel;
        var chosen = false;
        var dialog = new Window
        {
            Title = "Unsaved changes",
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            ShowInTaskbar = false,
        };
        void Choose(CloseAnswer picked)
        {
            if (!chosen)
            {
                chosen = true;
                answer = picked;
            }

            dialog.Close();
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(ButtonFor("Save", CloseAnswer.Save, isDefault: true, Choose));
        buttons.Children.Add(ButtonFor("Discard", CloseAnswer.Discard, isDefault: false, Choose));
        buttons.Children.Add(ButtonFor("Cancel", CloseAnswer.Cancel, isDefault: false, Choose));
        var layout = new StackPanel { Margin = new Thickness(16) };
        layout.Children.Add(new TextBlock { Text = "Save your changes to this task?" });
        layout.Children.Add(buttons);
        dialog.Content = layout;

        // Enter and Esc by key events too, so they work as they do with a user's key press on any focused element.
        dialog.KeyDown += (_, e) =>
        {
            var key = EventKey.Of(e);
            if (!e.Handled && key is Key.Enter or Key.Escape)
            {
                e.Handled = true;
                Choose(key == Key.Enter ? CloseAnswer.Save : CloseAnswer.Cancel);
            }
        };
        dialog.ShowDialog();
        return answer;
    }

    private static Button ButtonFor(string text, CloseAnswer answer, bool isDefault, Action<CloseAnswer> choose)
    {
        var button = new Button { Content = text, IsDefault = isDefault, MinWidth = 80, Margin = new Thickness(8, 0, 0, 0) };
        button.Click += (_, _) => choose(answer);
        return button;
    }
}
