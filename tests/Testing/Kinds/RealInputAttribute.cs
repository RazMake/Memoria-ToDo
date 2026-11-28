namespace Memoria.ToDo.Testing;

/// <summary>Marks native-input helpers usable only by integration tests or other marked helpers.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Constructor, AllowMultiple = false)]
public sealed class RealInputAttribute : Attribute;
