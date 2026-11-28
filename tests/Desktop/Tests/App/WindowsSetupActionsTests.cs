using System.Windows;
using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// Configure's setup actions on Windows, through <c>WindowsSetupActions</c>: <c>CopyText</c> puts any text, such as an
/// AI assistant's connection lines, on the real clipboard. The clipboard is shared by the whole desktop, so these tests
/// run in the screen collection and put back every format they found, such as an image, not only text.
/// </summary>
[Integration]
[Collection(ScreenTests.Collection)]
public class WindowsSetupActionsTests
{
    private static DataObject Saved()
    {
        var copy = new DataObject();
        if (Clipboard.GetDataObject() is { } held)
        {
            foreach (var format in held.GetFormats(false))
            {
                if (held.GetData(format, false) is { } data)
                {
                    copy.SetData(format, data, false);
                }
            }
        }

        return copy;
    }

    private static void PutBack(DataObject saved)
    {
        if (saved.GetFormats(false).Length == 0)
        {
            Clipboard.Clear();
        }
        else
        {
            Clipboard.SetDataObject(saved, true);
        }
    }

    [WpfFact]
    public void CopyText_puts_the_text_on_the_clipboard_with_its_lines_and_quotes_unchanged()
    {
        const string text = "{\n  \"servers\": {\n    \"memoria\": { \"command\": \"C:\\\\Program Files\\\\App.exe\", \"args\": [\"--mcp\"] }\n  }\n}";
        var saved = Saved();
        try
        {
            new WindowsSetupActions(new FakeDefaultBrowser()).CopyText(text);

            Assert.Equal(text, Clipboard.GetText());
        }
        finally
        {
            PutBack(saved);
        }
    }

    [WpfFact]
    public void CopyText_replaces_what_the_clipboard_held()
    {
        var saved = Saved();
        try
        {
            Clipboard.SetText("old");

            new WindowsSetupActions(new FakeDefaultBrowser()).CopyText("new");

            Assert.Equal("new", Clipboard.GetText());
        }
        finally
        {
            PutBack(saved);
        }
    }

    [WpfFact]
    public void CopyPublicKey_still_puts_the_key_on_the_clipboard()
    {
        var saved = Saved();
        try
        {
            new WindowsSetupActions(new FakeDefaultBrowser()).CopyPublicKey("ssh-ed25519 AAAA test");

            Assert.Equal("ssh-ed25519 AAAA test", Clipboard.GetText());
        }
        finally
        {
            PutBack(saved);
        }
    }
}
