using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Platform.Tests;

[Unit]
public class AppRunnerArgumentTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-2)]
    [InlineData(4294967295)]
    [InlineData(double.MaxValue)]
    public void Run_rejects_an_unusable_shutdown_grace_before_any_side_effect(double milliseconds)
    {
        var grace = milliseconds == double.MaxValue ? TimeSpan.MaxValue : TimeSpan.FromMilliseconds(milliseconds);
        var built = false;
        var log = new InMemoryLogSink();

        var error = OnMta(() => AppRunner.Run(() =>
        {
            built = true;
            throw new InvalidOperationException("Must not build.");
        }, grace, log.CreateLogger()));

        Assert.Equal("shutdownGrace", Assert.IsType<ArgumentOutOfRangeException>(error).ParamName);
        Assert.False(built);
        Assert.Empty(log.Events);
    }

    [Fact]
    public void Run_rejects_a_missing_lifetime_factory_before_startup()
    {
        var log = new InMemoryLogSink();

        var error = OnMta(() => AppRunner.Run(null!, TimeSpan.FromSeconds(40), log.CreateLogger()));

        Assert.Equal("createLifetime", Assert.IsType<ArgumentNullException>(error).ParamName);
        Assert.Empty(log.Events);
    }

    private static Exception? OnMta(Action call)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { call(); }
            catch (Exception failure) { error = failure; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.MTA);
        thread.Start();
        Assert.True(thread.Join(TestWait.ForSignal), "Argument validation did not return.");
        return error;
    }
}
