using System.Windows;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Paste rule for one-line fields.</summary>
internal static class TextPaste
{
    /// <summary>A paste handler that joins the pasted lines with spaces, since the field holds one line.</summary>
    public static void JoinLines(object sender, DataObjectPastingEventArgs e) => Rewrite(e, OneLine.Of);

    /// <summary>Swaps pasted text for what <paramref name="rewrite"/> makes of it. A paste with no text is left as it was.</summary>
    public static void Rewrite(DataObjectPastingEventArgs e, Func<string, string> rewrite)
    {
        if (e.DataObject.GetData(DataFormats.UnicodeText) is string text)
        {
            e.DataObject = new DataObject(DataFormats.UnicodeText, rewrite(text));
        }
    }
}
