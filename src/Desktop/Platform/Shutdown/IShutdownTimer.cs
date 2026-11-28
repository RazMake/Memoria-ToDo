namespace Memoria.ToDo.Desktop.Platform;

internal interface IShutdownTimer
{
    void Schedule(TimeSpan delay, Action callback);
}
