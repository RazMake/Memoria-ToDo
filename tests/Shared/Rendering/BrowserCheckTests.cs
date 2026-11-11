using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Shared.Rendering.Tests;

/// <summary>Which links may open in the default browser: only absolute web addresses.</summary>
[Unit]
public class BrowserCheckTests
{
    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com/path?q=1#top")]
    [InlineData("HTTPS://example.com")]
    [InlineData("HtTp://example.com/a")]
    public void Lets_an_absolute_http_or_https_address_open(string address)
    {
        Assert.True(BrowserCheck.MayOpen(address));
    }

    [Theory]
    [InlineData("task:01900000-0000-7000-8000-000000000001")]
    [InlineData("ftp://example.com/file")]
    [InlineData("file:///C:/Windows/System32/calc.exe")]
    [InlineData("javascript:alert(1)")]
    [InlineData("ms-msdt:/id PCWDiagnostic")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("data:text/html,<b>x</b>")]
    public void Refuses_an_address_with_another_scheme(string address)
    {
        Assert.False(BrowserCheck.MayOpen(address));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("example.com")]
    [InlineData("/images/a.png")]
    [InlineData("//example.com/a")]
    [InlineData("#section")]
    [InlineData("http:")]
    public void Refuses_an_address_that_is_not_an_absolute_web_address(string address)
    {
        Assert.False(BrowserCheck.MayOpen(address));
    }
}
