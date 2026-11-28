using Memoria.ToDo.Desktop.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.FormattedView.Tests;

/// <summary>What a click on a link does: web links open in the default browser, any other address is reported, and a browser that fails is reported, never thrown into the click.</summary>
[Unit]
public class LinkOpenerTests
{
    private readonly FakeDefaultBrowser _browser = new();
    private readonly List<string> _otherLinks = [];
    private readonly List<Exception> _failures = [];
    private readonly LinkOpener _opener;

    public LinkOpenerTests()
    {
        _opener = new LinkOpener(_browser, _otherLinks.Add, _failures.Add);
    }

    [Theory]
    [InlineData("https://example.com/docs")]
    [InlineData("http://example.com/docs")]
    public void Opens_a_web_link_in_the_default_browser(string address)
    {
        _opener.Click(address);

        Assert.Equal([address], _browser.Opened);
        Assert.Empty(_otherLinks);
        Assert.Empty(_failures);
    }

    [Theory]
    [InlineData("task:01900000-0000-7000-8000-000000000001")]
    [InlineData("ftp://example.com/file")]
    [InlineData("file:///C:/secret.txt")]
    [InlineData("javascript:alert(1)")]
    public void Opens_nothing_for_an_address_that_is_not_a_web_link_and_reports_it(string address)
    {
        _opener.Click(address);

        Assert.Empty(_browser.Opened);
        Assert.Equal([address], _otherLinks);
    }

    [Fact]
    public void Reports_a_browser_that_cannot_open_a_web_link_instead_of_throwing()
    {
        var failure = new InvalidOperationException("No browser.");
        _browser.Failure = failure;

        _opener.Click("https://example.com/docs");

        Assert.Same(failure, Assert.Single(_failures));
        Assert.Empty(_otherLinks);
    }
}
