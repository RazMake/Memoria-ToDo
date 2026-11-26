using Memoria.ToDo.Desktop.KeepAwake;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.App.Tests;

/// <summary>The real lock adapter asks Windows about this session. A test can't lock the PC, so the Changed events are checked by hand.</summary>
[Integration]
public sealed class WtsSessionLockIntegrationTests
{
    [Fact]
    public void Reports_the_interactive_session_the_test_runs_in_as_unlocked()
    {
        ISessionLock session = new WtsSessionLock();

        Assert.False(session.IsLocked);
    }

    [Fact]
    public void Gives_the_same_answer_each_time_it_is_asked()
    {
        ISessionLock session = new WtsSessionLock();

        var answers = Enumerable.Range(0, 5).Select(_ => session.IsLocked).ToArray();

        Assert.All(answers, answer => Assert.False(answer));
    }

    [Fact]
    public void Following_and_unfollowing_changes_raises_nothing_while_the_session_stays_as_it_is()
    {
        ISessionLock session = new WtsSessionLock();
        var raised = 0;
        EventHandler handler = (_, _) => raised++;

        session.Changed += handler;
        var unlocked = !session.IsLocked;
        session.Changed -= handler;

        Assert.True(unlocked);
        Assert.Equal(0, raised);
    }
}
