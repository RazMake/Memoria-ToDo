using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Memoria.ToDo.Desktop.Fakes;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using Memoria.ToDo.Desktop.TaskEditor.Tests;

namespace Memoria.ToDo.Desktop.TaskList.Tests;

/// <summary>How a Task View test reads where the editor's parts sit on the right side during an edit: Save and the Description.</summary>
internal sealed partial class TaskListScreen
{
    /// <summary>The Description editor's Save button, which sits on the right side.</summary>
    public ButtonBase SaveButton => FindButton("Save", Details) ?? throw new InvalidOperationException("The right side shows no \"Save\" button.");

    /// <summary>The editor's Description field.</summary>
    public FrameworkElement EditedDescription => EditedField("Description");
}
