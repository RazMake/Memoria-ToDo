using System.Runtime.CompilerServices;

namespace Memoria.ToDo.Desktop.TaskEditor;

/// <summary>Checks a constructor's argument inside the arguments of the <c>this(...)</c> call, before any control exists, which a test without a UI thread can reach.</summary>
internal static class Argument
{
    /// <summary>The value, or an <see cref="ArgumentNullException"/> named for the argument's expression when it's null.</summary>
    public static T Required<T>(T? value, [CallerArgumentExpression(nameof(value))] string? name = null) where T : class =>
        value ?? throw new ArgumentNullException(name);
}
