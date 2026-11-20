using Xunit;
using Memoria.ToDo.Testing;
using Serilog;
using Serilog.Core;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Unit]
public class AppRunnerThreadCheckTests
{
    [Fact]
    public void Run_rejects_a_call_from_a_thread_that_is_not_an_STA_thread_before_building_the_lifetime()
    {
        var lifetimeBuilt = false;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                AppRunner.Run(() =>
                {
                    lifetimeBuilt = true;
                    throw new InvalidOperationException("The lifetime must not be built.");
                }, Logger.None);
            }
            catch (Exception e)
            {
                error = e;
            }
        });
        thread.SetApartmentState(ApartmentState.MTA);

        thread.Start();
        thread.Join();

        Assert.IsType<InvalidOperationException>(error);
        Assert.False(lifetimeBuilt);
    }

    [Fact]
    public void Run_rejects_a_missing_logger_before_building_the_lifetime()
    {
        var lifetimeBuilt = false;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                AppRunner.Run(() =>
                {
                    lifetimeBuilt = true;
                    throw new InvalidOperationException("The lifetime must not be built.");
                }, null!);
            }
            catch (Exception e)
            {
                error = e;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);

        thread.Start();
        thread.Join();

        Assert.Equal("logger", Assert.IsType<ArgumentNullException>(error).ParamName);
        Assert.False(lifetimeBuilt);
    }
}
