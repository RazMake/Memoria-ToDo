namespace Memoria.ToDo.Shared.Tasks;

/// <summary>A tag in use and how many tasks have it.</summary>
public sealed record TagUse(string Tag, int Count);
