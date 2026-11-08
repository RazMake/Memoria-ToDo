namespace Memoria.ToDo.Desktop.App;

/// <summary>The command-line argument that tells a start at sign-in from a start by hand.</summary>
internal static class StartArguments
{
    /// <summary>The argument the <c>Run</c> key's entry adds.</summary>
    public const string AtSignIn = "--at-sign-in";

    public static bool IsStartAtSignIn(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return Array.Exists(args, argument => argument == AtSignIn);
    }
}
