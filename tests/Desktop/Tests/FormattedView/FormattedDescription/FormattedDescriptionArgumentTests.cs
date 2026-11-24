using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;
using DescriptionControl = Memoria.ToDo.Desktop.FormattedView.FormattedDescription;

namespace Memoria.ToDo.Desktop.FormattedView.Tests.FormattedDescription;

[Unit]
public class FormattedDescriptionArgumentTests
{
    [WpfFact]
    public void The_description_rejects_a_null_logger()
    {
        var error = Assert.Throws<ArgumentNullException>(() =>
            new DescriptionControl(new FakeDescriptionWebView(), new FakeTheme(), new FakeDefaultBrowser(), new FakeDragPointer(), null!));

        Assert.Equal("logger", error.ParamName);
    }
}
