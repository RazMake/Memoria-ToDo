using Memoria.ToDo.Desktop.TaskEditor;
using Microsoft.Win32;

namespace Memoria.ToDo.Desktop.App;

/// <summary>The Windows open-file dialog for images. Thin: the Task editor's tests use a fake, so no test shows a dialog.</summary>
internal sealed class WpfImageFileChooser : IImageFileChooser
{
    public string? Choose()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose an image",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|All files|*.*",
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
