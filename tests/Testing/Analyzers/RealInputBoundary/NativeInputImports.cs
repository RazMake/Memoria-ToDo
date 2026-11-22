using System;
using Microsoft.CodeAnalysis;

namespace Memoria.ToDo.Testing.Analyzers;

internal static class NativeInputImports
{
    private const string LibraryImportTypeName = "System.Runtime.InteropServices.LibraryImportAttribute";
    private const string Library = "user32";
    private const string LibraryExtension = ".dll";

    private static readonly string[] EntryPoints =
    [
        "SetCursorPos",
        "SendInput",
        "mouse_event",
        "keybd_event",
        "SetForegroundWindow",
    ];

    public static bool IsNativeInput(IMethodSymbol method)
    {
        string? library;
        string? entryPoint;
        var dllImport = method.GetDllImportData();
        if (dllImport is not null)
        {
            library = dllImport.ModuleName;
            entryPoint = dllImport.EntryPointName;
        }
        else if (!TryReadLibraryImport(method, out library, out entryPoint))
        {
            return false;
        }

        return library is not null
            && IsUser32(library)
            && Array.IndexOf(EntryPoints, entryPoint ?? method.Name) >= 0;
    }

    private static bool TryReadLibraryImport(IMethodSymbol method, out string? library, out string? entryPoint)
    {
        library = null;
        entryPoint = null;
        foreach (var attribute in method.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != LibraryImportTypeName)
            {
                continue;
            }

            library = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value as string : null;
            foreach (var argument in attribute.NamedArguments)
            {
                if (argument.Key == "EntryPoint")
                {
                    entryPoint = argument.Value.Value as string;
                }
            }

            return true;
        }

        return false;
    }

    private static bool IsUser32(string library)
    {
        if (library.EndsWith(LibraryExtension, StringComparison.OrdinalIgnoreCase))
        {
            library = library.Substring(0, library.Length - LibraryExtension.Length);
        }

        return string.Equals(library, Library, StringComparison.OrdinalIgnoreCase);
    }
}
