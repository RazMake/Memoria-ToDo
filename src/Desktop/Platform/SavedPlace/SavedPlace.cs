namespace Memoria.ToDo.Desktop.Platform;

/// <summary>A window's restored rectangle in the screen pixels Windows gives, and whether it's maximized.</summary>
internal readonly record struct SavedPlace(int Left, int Top, int Width, int Height, bool Maximized);
