namespace Memoria.ToDo.Desktop.Platform;

/// <summary>The Windows messages that close a window: the user's close command, and the plain close of the application ending.</summary>
internal static class CloseMessages
{
    public const int WmClose = 0x0010;
    public const int WmSysCommand = 0x0112;
    public const int ScClose = 0xF060;

    /// <summary>The mask that clears the four low bits of a WM_SYSCOMMAND's wParam, which Windows uses for its own flags.</summary>
    public const int SysCommandMask = 0xFFF0;
}
