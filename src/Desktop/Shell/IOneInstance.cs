namespace Memoria.ToDo.Desktop.Shell;

/// <summary>Port that lets only one instance of the app run per sign-in session.</summary>
public interface IOneInstance
{
    /// <summary>
    /// Returns whether this is the first instance in the sign-in session. When true, <paramref name="laterStart"/>
    /// runs on the UI thread at each later start. When false, the port has already told the running instance.
    /// </summary>
    bool Claim(Action laterStart);

    /// <summary>Gives up the claim. Does nothing when there is none.</summary>
    void Release();
}
