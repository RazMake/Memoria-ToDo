using System.Windows;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.TaskEditor.Tests;

/// <summary>The paste handler of the one-line fields: it swaps the pasted text for its one-line form.</summary>
[Unit]
public class TextPasteTests
{
    private static DataObjectPastingEventArgs Paste(DataObject data, string formatToApply) =>
        new(data, false, formatToApply);

    [Fact]
    public void Pasted_lines_are_replaced_by_one_line()
    {
        var args = Paste(new DataObject(DataFormats.UnicodeText, "first\r\nsecond"), DataFormats.UnicodeText);

        TextPaste.JoinLines(this, args);

        Assert.Equal("first second", args.DataObject.GetData(DataFormats.UnicodeText));
    }

    [Fact]
    public void A_paste_without_text_is_left_as_it_was()
    {
        var original = new DataObject(DataFormats.FileDrop, new[] { "a.txt" });
        var args = Paste(original, DataFormats.FileDrop);

        TextPaste.JoinLines(this, args);

        Assert.Same(original, args.DataObject);
    }
}
