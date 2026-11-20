using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>A modal question, owned by and centered on a window, that follows the theme.</summary>
public static class QuestionDialog
{
    /// <summary>
    /// Shows the text with one button per answer, in order, and returns the answer picked.
    /// Enter picks <paramref name="defaultAnswer"/>; Esc and the close button pick <paramref name="cancelAnswer"/>.
    /// </summary>
    public static T Ask<T>(
        Window owner, string title, string text,
        IReadOnlyList<(string Label, T Answer)> answers, T defaultAnswer, T cancelAnswer)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(answers);
        if (answers.Count == 0)
        {
            throw new ArgumentException("A question needs at least one answer.", nameof(answers));
        }

        var hasDefault = false;
        foreach (var (label, value) in answers)
        {
            if (string.IsNullOrEmpty(label))
            {
                throw new ArgumentException("Every answer needs a label.", nameof(answers));
            }

            hasDefault |= EqualityComparer<T>.Default.Equals(value, defaultAnswer);
        }

        if (!hasDefault)
        {
            throw new ArgumentException("The default answer must be one of the answers.", nameof(defaultAnswer));
        }

        var answer = cancelAnswer;
        var chosen = false;
        var dialog = new Window
        {
            Title = title,
            Owner = owner,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            ShowInTaskbar = false,
        };
        void Choose(T picked)
        {
            if (!chosen)
            {
                chosen = true;
                answer = picked;
            }

            dialog.Close();
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        foreach (var (label, value) in answers)
        {
            var button = new Button
            {
                Content = label,
                IsDefault = EqualityComparer<T>.Default.Equals(value, defaultAnswer),
                MinWidth = 80,
                Margin = new Thickness(8, 0, 0, 0),
            };
            button.Click += (_, _) => Choose(value);
            buttons.Children.Add(button);
        }

        var layout = new StackPanel { Margin = new Thickness(16) };
        layout.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 });
        layout.Children.Add(buttons);
        dialog.Content = layout;
        dialog.PreviewKeyDown += (_, args) =>
        {
            var key = EventKey.Of(args);
            if (!args.Handled && key is Key.Enter or Key.Escape)
            {
                args.Handled = true;
                Choose(key == Key.Enter ? defaultAnswer : cancelAnswer);
            }
        };
        ModalDialog.Show(dialog);
        return answer;
    }
}
