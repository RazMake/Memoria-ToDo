using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;
using TitleControl = Memoria.ToDo.Desktop.FormattedView.FormattedTitle;

namespace Memoria.ToDo.Desktop.FormattedView.Tests.FormattedTitle;

[Unit]
public class FormattedTitleArgumentTests
{
    [WpfFact]
    public void The_title_rejects_a_null_logger()
    {
        var error = Assert.Throws<ArgumentNullException>(() =>
            new TitleControl(new FakeTheme(), new FakeDefaultBrowser(), new FakeDragPointer(), null!));

        Assert.Equal("logger", error.ParamName);
    }
}
