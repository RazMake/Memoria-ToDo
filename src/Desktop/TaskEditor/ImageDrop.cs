using System.Windows;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Lets files be dropped on a field: the first dropped file is inserted as an image at the cursor. Other drops keep their usual meaning.</summary>
internal static class ImageDrop
{
    public static void AddTo(StyledTextEditor field, ImageInsertion images)
    {
        field.AllowDrop = true;
        field.PreviewDragOver += (_, e) =>
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        };
        field.PreviewDrop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } paths)
            {
                e.Handled = true;
                images.InsertFile(field, paths[0]);
            }
        };
    }
}
