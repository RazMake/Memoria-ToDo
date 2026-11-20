using System.Globalization;
using Memoria.ToDo.Shared.Tasks;
using Serilog;

namespace Memoria.ToDo.Desktop.Configure;

/// <summary>
/// The deletion delay, in days, behind its box: the text to show, how typed text is checked and saved to the shared
/// settings, and the message to show when reading or saving fails.
/// </summary>
internal sealed class DeletionDelaySetting
{
    private readonly ISharedTaskSettings _settings;
    private readonly ILogger _logger;
    private bool _readFailed;

    /// <param name="logger">Where a failed read or save is logged.</param>
    public DeletionDelaySetting(ISharedTaskSettings settings, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(logger);
        _settings = settings;
        _logger = logger;
    }

    /// <summary>The delay to show: the last one read or saved, empty until a read succeeds.</summary>
    public string Shown { get; private set; } = "";

    /// <summary>What to tell the user, or null when there is nothing to say.</summary>
    public string? Message { get; private set; }

    /// <summary>
    /// Reads the saved value. When the settings can't be read, <see cref="Shown"/> keeps the last value and
    /// <see cref="Message"/> says why.
    /// </summary>
    public void Refresh()
    {
        try
        {
            Shown = _settings.DeletionDelayDays.ToString(CultureInfo.InvariantCulture);
            if (_readFailed)
            {
                _readFailed = false;
                Message = null;
            }
        }
        catch (Exception error)
        {
            _readFailed = true;
            _logger.Error(error, "The deletion delay couldn't be read");
            Message = $"The deletion delay couldn't be read: {error.Message}";
        }
    }

    /// <summary>Saves the typed text as the delay. A text that can't be saved leaves <see cref="Shown"/> as it was.</summary>
    public void Commit(string text)
    {
        text = text.Trim();
        string? problem = null;
        try
        {
            if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var days))
            {
                problem = $"The deletion delay wasn't changed: \"{text}\" isn't a whole number of days.";
            }
            else
            {
                _settings.SetDeletionDelayDays(days);
                Shown = days.ToString(CultureInfo.InvariantCulture);
            }
        }
        catch (ArgumentOutOfRangeException)
        {
            problem = $"The deletion delay wasn't changed: {text} is outside the allowed range.";
        }
        catch (Exception error)
        {
            _logger.Error(error, "The deletion delay couldn't be saved");
            problem = $"The deletion delay wasn't saved: {error.Message}";
        }

        Message = problem;
    }
}
