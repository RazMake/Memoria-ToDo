using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.FormattedView.Tests.FormattedDescription;

/// <summary>When a description reports no image resize: after its web view failed and after it was released. The description is laid out but never shown.</summary>
[Unit]
public class FormattedDescriptionResizeTests
{
    [WpfFact]
    public void A_failed_web_view_reports_no_resize()
    {
        using var screen = new DescriptionScreen(inWindow: false);
        screen.Description.IsPreview = true;
        screen.WebView.Fail("The WebView2 Runtime is missing.");

        screen.WebView.ResizeImage(0, 200);

        Assert.Empty(screen.Resizes);
    }

    [WpfFact]
    public void A_released_description_reports_no_resize()
    {
        using var screen = new DescriptionScreen(inWindow: false);
        screen.Description.IsPreview = true;
        screen.Description.Dispose();

        screen.WebView.ResizeImage(0, 200);

        Assert.Empty(screen.Resizes);
    }
}
