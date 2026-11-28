using System.Reflection;
using Memoria.ToDo.Shared.Contacts;

namespace Memoria.ToDo.Desktop.MCPServer.Tests;

/// <summary>
/// Wraps Contacts so another writer's save lands just before the first save a caller makes, whichever form it uses: the moment
/// a caller that read a contact earlier, then saves it whole, would overwrite the other writer's change.
/// Everything else goes straight to the wrapped Contacts. Shared test setup, not a fake of a port.
/// </summary>
public class WriterBeforeSave : DispatchProxy
{
    private IContacts inner = null!;
    private Action<IContacts> otherWriter = null!;
    private bool done;

    public static IContacts Around(IContacts inner, Action<IContacts> otherWriter)
    {
        var proxy = Create<IContacts, WriterBeforeSave>();
        var self = (WriterBeforeSave)(object)proxy;
        self.inner = inner;
        self.otherWriter = otherWriter;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (!done && targetMethod!.Name == nameof(IContacts.Save))
        {
            done = true;
            otherWriter(inner);
        }

        try
        {
            return targetMethod!.Invoke(inner, args);
        }
        catch (TargetInvocationException e) when (e.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            throw;
        }
    }
}
