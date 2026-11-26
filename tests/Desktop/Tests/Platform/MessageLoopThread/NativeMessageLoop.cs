namespace Memoria.ToDo.Desktop.Platform.Tests;

internal sealed class NativeMessageLoop : IMessageLoop
{
    public void Prepare() => Win32.CreateMessageQueue();
    public uint CurrentThread() => Win32.CurrentThreadId();
    public void Run() => Win32.RunMessageLoop();
    public void Quit(uint thread) => Win32.StopMessageLoop(thread);
}
