namespace Memoria.ToDo.Desktop.Platform;

internal interface IProcessExit
{
    void RequestExit(int exitCode);
    void Terminate(int exitCode);
}
