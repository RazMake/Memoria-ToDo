using System.Runtime.CompilerServices;

namespace Memoria.ToDo.Desktop;

/// <summary>
/// Runs the class constructors of the given types on one STA thread, so that parallel desktop tests can't deadlock in WPF's
/// first-use setup (dotnet/wpf#6609).
/// </summary>
internal static class ClassConstructorWarmUp
{
    /// <summary>
    /// Runs the class constructor of every non-generic type in <paramref name="types"/> on one new STA thread and returns when
    /// they all ran. Throws <see cref="InvalidOperationException"/> naming the type and carrying the original exception when a
    /// class constructor throws, and <see cref="TimeoutException"/> naming the type in progress when it takes longer than <paramref name="bound"/>.
    /// </summary>
    internal static void Run(IEnumerable<Type> types, TimeSpan bound)
    {
        Type? inProgress = null;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            foreach (var type in types)
            {
                if (type.ContainsGenericParameters)
                {
                    continue;
                }

                Volatile.Write(ref inProgress, type);
                try
                {
                    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                }
                catch (Exception error)
                {
                    failure = new InvalidOperationException($"The class constructor of {type.FullName} failed during the WPF warm-up: {error.Message}", error);
                    return;
                }
            }

            Volatile.Write(ref inProgress, null);
        })
        {
            IsBackground = true,
            Name = "WPF warm-up",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        if (!thread.Join(bound))
        {
            throw new TimeoutException($"The WPF warm-up didn't finish within {bound}; it was running the class constructor of {Volatile.Read(ref inProgress)?.FullName}.");
        }

        if (failure is not null)
        {
            throw failure;
        }
    }
}
