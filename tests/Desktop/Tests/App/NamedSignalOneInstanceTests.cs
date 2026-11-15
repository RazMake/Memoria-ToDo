using Memoria.ToDo.Desktop.Shell;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>
/// The named-signal adapter, through <see cref="IOneInstance"/>. A second process isn't practical here, so the
/// "later instance" is a second adapter with the same signal name in the same process, which is how the adapter
/// tells instances apart: by the name, not by the process. Every test uses its own unique name, never the app's.
/// </summary>
[Integration]
public class NamedSignalOneInstanceTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    private static string UniqueName() => $"Memoria.ToDo.Tests.{Guid.NewGuid():N}";

    [WpfFact]
    public void The_first_claim_is_true_and_a_second_claim_with_the_same_name_is_false()
    {
        var name = UniqueName();
        IOneInstance first = new NamedSignalOneInstance(name);
        IOneInstance second = new NamedSignalOneInstance(name);
        try
        {
            Assert.True(first.Claim(() => { }));
            Assert.False(second.Claim(() => { }));
        }
        finally
        {
            second.Release();
            first.Release();
        }
    }

    [WpfFact]
    public async Task A_later_claim_runs_the_first_ones_action_on_the_UI_thread_and_not_its_own()
    {
        var name = UniqueName();
        var uiThread = Environment.CurrentManagedThreadId;
        var handedOver = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var laterRan = false;
        IOneInstance first = new NamedSignalOneInstance(name);
        IOneInstance second = new NamedSignalOneInstance(name);
        first.Claim(() => handedOver.TrySetResult(Environment.CurrentManagedThreadId));
        try
        {
            second.Claim(() => laterRan = true);

            var threadOfAction = await handedOver.Task.WaitAsync(Limit, TestContext.Current.CancellationToken);

            Assert.Equal(uiThread, threadOfAction);
            Assert.False(laterRan);
        }
        finally
        {
            second.Release();
            first.Release();
        }
    }

    [WpfFact]
    public async Task Later_claims_made_at_the_same_moment_each_run_the_first_ones_action()
    {
        const int laterCount = 20;
        var name = UniqueName();
        var runs = 0;
        var all = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IOneInstance first = new NamedSignalOneInstance(name);
        first.Claim(() =>
        {
            if (++runs == laterCount)
            {
                all.TrySetResult();
            }
        });
        try
        {
            // Dedicated threads released together, so the claims don't wait for the first instance to wake between them.
            using var together = new Barrier(laterCount);
            var claims = Enumerable.Range(0, laterCount).Select(_ => Task.Factory.StartNew(
                () =>
                {
                    Assert.True(together.SignalAndWait(Limit), "The claims were not released together.");
                    var later = new NamedSignalOneInstance(name);
                    try
                    {
                        later.Claim(() => { });
                    }
                    finally
                    {
                        later.Release();
                    }
                },
                TestContext.Current.CancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default)).ToArray();

            // No timeout of its own: the barrier's Limit bounds every worker, so all have ended before the first instance is released.
            await Task.WhenAll(claims);
            await all.Task.WaitAsync(Limit, TestContext.Current.CancellationToken);

            Assert.Equal(laterCount, runs);
        }
        finally
        {
            first.Release();
        }
    }

    [WpfFact]
    public void Release_frees_the_claim_for_the_next_instance()
    {
        var name = UniqueName();
        IOneInstance first = new NamedSignalOneInstance(name);
        IOneInstance next = new NamedSignalOneInstance(name);
        first.Claim(() => { });

        first.Release();

        try
        {
            Assert.True(next.Claim(() => { }));
        }
        finally
        {
            next.Release();
        }
    }

    [WpfFact]
    public void Release_by_an_instance_that_lost_the_claim_leaves_the_first_ones_claim()
    {
        var name = UniqueName();
        IOneInstance first = new NamedSignalOneInstance(name);
        IOneInstance second = new NamedSignalOneInstance(name);
        IOneInstance third = new NamedSignalOneInstance(name);
        first.Claim(() => { });
        second.Claim(() => { });
        try
        {
            second.Release();

            Assert.False(third.Claim(() => { }));
        }
        finally
        {
            third.Release();
            first.Release();
        }
    }
}
