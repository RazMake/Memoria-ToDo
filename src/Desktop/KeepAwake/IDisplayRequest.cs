using System.ComponentModel;

namespace Memoria.ToDo.Desktop.KeepAwake;

/// <summary>Windows' power request for the display, and the system while it's on. The composition root implements it.</summary>
public interface IDisplayRequest
{
    /// <summary>Sets or clears the request.</summary>
    /// <exception cref="Win32Exception">Windows refuses.</exception>
    void Set(bool on);
}
