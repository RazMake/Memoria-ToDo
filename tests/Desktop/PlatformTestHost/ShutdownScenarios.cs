namespace Memoria.ToDo.Desktop.PlatformTestHost;

internal static class ShutdownScenarios
{
    public static Task NeverStops() => new TaskCompletionSource().Task;

    public static Task Blocks()
    {
        using var never = new ManualResetEventSlim();
        if (!never.Wait(TimeSpan.FromSeconds(20)))
        {
            throw new TimeoutException("The app did not end its blocked shutdown.");
        }
        return Task.CompletedTask;
    }

    public static void EndWithBlockedExitHandler(Action endApp)
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            ScenarioLifetime.Say("exit-handler-entered");
            Blocks();
        };
        endApp();
    }

    public static void EndFromThreadWithBlockedUi(Action endApp)
    {
        new Thread(() =>
        {
            endApp();
            ScenarioLifetime.Say("background-end-returned");
        }) { IsBackground = true }.Start();
        Blocks();
    }

    public static void EndWithStarvedPoolAndBlockedExit(Action endApp)
    {
        new Thread(() =>
        {
            if (!ThreadPool.SetMinThreads(1, 1) || !ThreadPool.SetMaxThreads(1, 1))
            {
                throw new InvalidOperationException("Could not restrict the isolated host's thread pool.");
            }
            using var entered = new ManualResetEventSlim();
            ThreadPool.QueueUserWorkItem(_ =>
            {
                entered.Set();
                Blocks();
            });
            if (!entered.Wait(TimeSpan.FromSeconds(10)))
            {
                throw new TimeoutException("The isolated host did not occupy its worker.");
            }
            EndWithBlockedExitHandler(endApp);
        }) { IsBackground = true }.Start();
    }
}
