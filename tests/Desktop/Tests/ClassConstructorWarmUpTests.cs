using Memoria.ToDo.Shared.Fakes;
using Memoria.ToDo.Testing;
using Xunit;

namespace Memoria.ToDo.Desktop.Tests;

/// <summary>
/// The WPF warm-up runs class constructors on one thread so that parallel desktop tests can't deadlock in WPF's first-use setup.
/// Each class constructor runs once per process, so every test gives the warm-up types of its own.
/// </summary>
[Unit]
public class ClassConstructorWarmUpTests
{
    private static IEnumerable<Exception> ChainOf(Exception error)
    {
        for (var current = error; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }

    // The apartment is read while the thread runs: the warm-up's thread has ended once Run returns.
    private static class RecordsFirst
    {
        internal static Thread? RanOn;
        internal static ApartmentState Apartment;
        static RecordsFirst() => (RanOn, Apartment) = (Thread.CurrentThread, Thread.CurrentThread.GetApartmentState());
    }

    private static class RecordsSecond
    {
        internal static Thread? RanOn;
        internal static ApartmentState Apartment;
        static RecordsSecond() => (RanOn, Apartment) = (Thread.CurrentThread, Thread.CurrentThread.GetApartmentState());
    }

    private static class RecordsThird
    {
        internal static Thread? RanOn;
        internal static ApartmentState Apartment;
        static RecordsThird() => (RanOn, Apartment) = (Thread.CurrentThread, Thread.CurrentThread.GetApartmentState());
    }

    [Fact]
    public void Runs_the_class_constructor_of_every_type_it_is_given_on_one_STA_thread_that_is_not_the_callers_and_returns_after_they_ran()
    {
        ClassConstructorWarmUp.Run([typeof(RecordsFirst), typeof(RecordsSecond), typeof(RecordsThird)], TestWait.ForSignal);

        var threads = new[] { RecordsFirst.RanOn, RecordsSecond.RanOn, RecordsThird.RanOn };
        Assert.All(threads, Assert.NotNull);
        Assert.Single(threads.Distinct());
        Assert.NotSame(Thread.CurrentThread, threads[0]);
        Assert.All(new[] { RecordsFirst.Apartment, RecordsSecond.Apartment, RecordsThird.Apartment }, apartment => Assert.Equal(ApartmentState.STA, apartment));
    }

    private static class OpenGeneric<T>
    {
        internal static int Ran;
        static OpenGeneric() => Ran++;
    }

    // Recorded outside the type: reading the type's own field would run its class constructor on the test's thread.
    private static Thread? _ranAfterGeneric;

    private static class RecordsAfterGeneric
    {
        static RecordsAfterGeneric() => _ranAfterGeneric = Thread.CurrentThread;
    }

    [Fact]
    public void Skips_an_open_generic_type_without_failing_and_still_runs_the_types_after_it()
    {
        ClassConstructorWarmUp.Run([typeof(OpenGeneric<>), typeof(RecordsAfterGeneric)], TestWait.ForSignal);

        Assert.NotNull(_ranAfterGeneric);
    }

    // Outside the failing type: any later touch of that type rethrows its TypeInitializationException.
    private static readonly InvalidOperationException ThrownByThrows = new("the class constructor failed");

    private static class Throws
    {
        static Throws() => throw ThrownByThrows;
    }

    [Fact]
    public void Fails_with_a_message_naming_the_type_and_carrying_the_original_exception_when_a_class_constructor_throws()
    {
        var error = Assert.Throws<InvalidOperationException>(() => ClassConstructorWarmUp.Run([typeof(Throws)], TestWait.ForSignal));

        Assert.Contains(typeof(Throws).FullName!, error.Message);
        Assert.Contains(ThrownByThrows, ChainOf(error).Skip(1).ToList());
    }

    // The signal lives outside the waiting type: touching a type whose class constructor is still running would block the test too.
    private static readonly ManualResetEventSlim Release = new(false);

    private static class WaitsForSignal
    {
        static WaitsForSignal() => Release.Wait(TestWait.ForSignal);
    }

    // The test waits this out, so it's short, yet far longer than the warm-up's thread takes to reach the class constructor.
    private static readonly TimeSpan ShortBound = TimeSpan.FromSeconds(1);

    [Fact]
    public void Fails_naming_the_type_in_progress_when_the_warm_up_does_not_finish_within_its_bound()
    {
        try
        {
            var error = Assert.Throws<TimeoutException>(() => ClassConstructorWarmUp.Run([typeof(WaitsForSignal)], ShortBound));

            Assert.Contains(typeof(WaitsForSignal).FullName!, error.Message);
        }
        finally
        {
            Release.Set(); // Lets the warm-up's thread end.
        }
    }

    [Fact]
    public void Covers_WPFs_four_assemblies_and_the_themed_AvalonEdit_assembly_of_the_desktop_test_project()
    {
        var names = WpfWarmUpScope.Assemblies(typeof(ClassConstructorWarmUpTests).Assembly).Select(a => a.GetName().Name).ToList();

        Assert.Contains("WindowsBase", names);
        Assert.Contains("PresentationCore", names);
        Assert.Contains("PresentationFramework", names);
        Assert.Contains("System.Xaml", names);
        Assert.Contains("ICSharpCode.AvalonEdit", names);
    }

    [Fact]
    public void Leaves_out_referenced_assemblies_without_ThemeInfo_such_as_Markdig_and_xunit()
    {
        var names = WpfWarmUpScope.Assemblies(typeof(ClassConstructorWarmUpTests).Assembly).Select(a => a.GetName().Name).ToList();

        Assert.DoesNotContain("Markdig", names);
        Assert.DoesNotContain(typeof(FactAttribute).Assembly.GetName().Name, names);
    }
}
