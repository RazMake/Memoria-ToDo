using Memoria.ToDo.Shared.Tasks;

namespace Memoria.ToDo.Shared.Fakes;

/// <summary>
/// In-memory <see cref="ITagColors"/>. Tags match with case ignored, as in the real one. Set <see cref="Failure"/> to
/// make <see cref="SetColor"/> throw it before keeping anything, as a missing repository or an unreadable tags.json
/// does. Like the real one, keeping the color a tag already has raises no <see cref="Changed"/>; use
/// <see cref="RaiseChanged"/> to model a reload.
/// </summary>
public sealed class FakeTagColors : ITagColors
{
    private readonly Dictionary<string, string> colors = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>When set, <see cref="SetColor"/> throws it and keeps nothing.</summary>
    public Exception? Failure { get; set; }

    /// <summary>Every color <see cref="SetColor"/> was asked to keep, in order, with the tag as it was spelled. Failed calls are not listed.</summary>
    public List<(string Tag, string Color)> Kept { get; } = [];

    /// <summary>Gives the tag a color without a call being recorded, as an earlier session or another device would.</summary>
    public void Seed(string tag, string colorName) => colors[tag] = colorName;

    /// <inheritdoc />
    public string? ColorOf(string tag) => colors.GetValueOrDefault(tag);

    /// <inheritdoc />
    public void SetColor(string tag, string colorName)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Kept.Add((tag, colorName));
        if (colors.TryGetValue(tag, out var kept) && kept == colorName)
        {
            return;
        }

        colors[tag] = colorName;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Raises <see cref="Changed"/> on the calling thread, as a reload does.</summary>
    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
