using System.Windows;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Paste rule for one-line fields.</summary>
internal static class TextPaste
{
    /// <summary>A paste handler that joins the pasted lines with spaces, since the field holds one line.</summary>
    public static void JoinLines(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetData(DataFormats.UnicodeText) is string text)
        {
            var lines = text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
            e.DataObject = new DataObject(DataFormats.UnicodeText, string.Join(' ', lines.Select(line => line.Trim())).Trim());
        }
    }
}
