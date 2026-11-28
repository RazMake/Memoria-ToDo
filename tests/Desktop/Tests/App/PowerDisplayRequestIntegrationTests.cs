using Memoria.ToDo.Desktop.KeepAwake;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>The real display request asks Windows' power API for the display and the system, with no admin rights and no screen.</summary>
[Integration]
public sealed class PowerDisplayRequestIntegrationTests
{
    [Fact]
    public void Setting_the_request_on_and_then_off_succeeds_in_the_real_Windows()
    {
        IDisplayRequest request = new PowerDisplayRequest();

        var failure = Record.Exception(() =>
        {
            request.Set(true);
            request.Set(false);
        });

        Assert.Null(failure);
    }

    [Fact]
    public void Setting_the_request_on_twice_in_a_row_succeeds()
    {
        IDisplayRequest request = new PowerDisplayRequest();

        var failure = Record.Exception(() =>
        {
            request.Set(true);
            request.Set(true);
        });
        request.Set(false);

        Assert.Null(failure);
    }

    [Fact]
    public void Clearing_a_request_that_was_never_set_succeeds()
    {
        IDisplayRequest request = new PowerDisplayRequest();

        var failure = Record.Exception(() =>
        {
            request.Set(false);
            request.Set(false);
        });

        Assert.Null(failure);
    }

    [Fact]
    public void Turning_the_request_on_and_off_again_and_again_succeeds()
    {
        IDisplayRequest request = new PowerDisplayRequest();

        var failure = Record.Exception(() =>
        {
            for (var i = 0; i < 5; i++)
            {
                request.Set(true);
                request.Set(false);
            }
        });
        request.Set(false);

        Assert.Null(failure);
    }

    [Fact]
    public void Two_display_requests_can_be_on_at_once()
    {
        IDisplayRequest first = new PowerDisplayRequest();
        IDisplayRequest second = new PowerDisplayRequest();

        var failure = Record.Exception(() =>
        {
            first.Set(true);
            second.Set(true);
        });
        first.Set(false);
        second.Set(false);

        Assert.Null(failure);
    }
}
