using Serilog.Events;

namespace Memoria.ToDo.Desktop.Logs;

/// <summary>One item of the minimum level list.</summary>
internal sealed record LevelChoice(string Label, LogEventLevel Level);
