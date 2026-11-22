using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.FormattedView.Tests;

[Unit]
public class FormattedControlArgumentTests
{
    [WpfFact]
    public void The_title_rejects_a_null_logger()
    {
        var error = Assert.Throws<ArgumentNullException>(() =>
            new FormattedTitle(new FakeTheme(), new FakeDefaultBrowser(), new FakeDragPointer(), null!));

        Assert.Equal("logger", error.ParamName);
    }

    [WpfFact]
    public void The_description_rejects_a_null_logger()
    {
        var error = Assert.Throws<ArgumentNullException>(() =>
            new FormattedDescription(new FakeDescriptionWebView(), new FakeTheme(), new FakeDefaultBrowser(), new FakeDragPointer(), null!));

        Assert.Equal("logger", error.ParamName);
    }
}
