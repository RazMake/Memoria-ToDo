using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using Memoria.ToDo.Desktop.TaskEditor.Tests;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>How a Task View test reads where the editor's parts sit on the right side during an edit: Save, the Description, and the Tags.</summary>
internal sealed partial class TaskListScreen
{
    /// <summary>Whether the right side shows a Save button.</summary>
    public bool ShowsSave => HasButton("Save", Details);

    /// <summary>The editor's Save button, which sits on the right side.</summary>
    public ButtonBase SaveButton => FindButton("Save", Details) ?? throw new InvalidOperationException("The right side shows no \"Save\" button.");

    /// <summary>The editor's Description field.</summary>
    public FrameworkElement EditedDescription => EditedField("Description");

    /// <summary>The editor's + button for tags, the top of the row under the Description.</summary>
    public ButtonBase TagsPlus
    {
        get
        {
            Refresh();
            return TagControls.Plus(Details);
        }
    }

    /// <summary>How far the right side scrolls as a whole, summed over every scroll viewer the editor's Title field sits in; the Description's own scrolling isn't counted.</summary>
    public double EditorOuterScrollRange
    {
        get
        {
            Refresh();
            var range = 0.0;
            for (DependencyObject? current = EditedField("Title"); current is not null; current = VisualTreeHelper.GetParent(current))
            {
                if (current is ScrollViewer viewer)
                {
                    range += viewer.ScrollableHeight;
                }
            }

            return range;
        }
    }

    /// <summary>Whether the Description scrolls inside itself: its text is taller than the field.</summary>
    public bool DescriptionScrollsItself => EditedDescription is TextEditor editor && editor.ExtentHeight > editor.ViewportHeight;

    /// <summary>Whether an outline frames the right side or a part of it after the keyboard tries to give the right side the focus, as Ctrl+Tab does.</summary>
    public bool OutlineFramesTheRightSideAfterTheKeyboardMovesTheFocus()
    {
        Refresh();
        OpenWindows.UseTheKeyboard();
        Keyboard.Focus(Details);
        Refresh();
        return OpenWindows.DrawsFocusOutline(Details)
            || OpenWindows.Descendants(Details).OfType<UIElement>().Any(OpenWindows.DrawsFocusOutline);
    }

    /// <summary>Makes the window this tall, in device-independent pixels, and lets it lay out.</summary>
    public void ResizeToHeight(double height)
    {
        Window.Height = height;
        Refresh();
    }
}
