namespace Memoria.ToDo.Desktop.KeepAwake;

/// <summary>Keep awake as the composition root holds it.</summary>
public interface IKeepAwake : IKeepAwakeStatus, IKeepAwakeLifetime
{
}
