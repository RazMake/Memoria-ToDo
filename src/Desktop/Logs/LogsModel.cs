using Serilog;
using Serilog.Events;

namespace Memoria.ToDo.Desktop.Logs;

/// <summary>
/// What the Logs window holds, apart from how it looks: the loaded entries, the filters, the rows that pass them, and the
/// status line. Every filter change filters again at once. Use it on one thread; <see cref="LoadAsync"/> reads off it and
/// comes back to the caller's context.
/// </summary>
internal sealed class LogsModel
{
    private static readonly IReadOnlyList<LevelChoice> LevelChoices =
    [
        new("Information and up", LogEventLevel.Information),
        new("Warning and up", LogEventLevel.Warning),
        new("Error and up", LogEventLevel.Error),
        new("Fatal only", LogEventLevel.Fatal),
    ];

    private readonly ILogSource _source;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private LogRow[] _all = [];
    private List<ComponentChoice> _components = [];
    private int _unreadableLines;
    private string? _writeProblem;
    private string? _readFailure;
    private bool _hasRead;
    private TimePreset _preset = TimePreset.All;
    private string _from = "";
    private string _to = "";
    private TimeRange _range;
    private LogEventLevel _minimumLevel = LogEventLevel.Information;
    private string _searchText = "";
    private Task? _running;

    public LogsModel(ILogSource source, TimeProvider time, ILogger logger)
    {
        _source = source;
        _time = time;
        _logger = logger;
        Refilter();
    }

    public bool IsLoading { get; private set; }

    /// <summary>The text that stands where the list is: "Loading…", or "No log entries yet.", or null while the list shows.</summary>
    public string? ListMessage { get; private set; }

    /// <summary>The rows that pass every filter, newest first.</summary>
    public IReadOnlyList<LogRow> Rows { get; private set; } = [];

    public string StatusText { get; private set; } = "";

    public TimePreset Preset
    {
        get => _preset;
        set
        {
            _preset = value;
            if (value != TimePreset.Custom)
            {
                FillBoxes();
            }

            Refilter();
        }
    }

    public string From
    {
        get => _from;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value != _from)
            {
                _from = value;
                EditedBox();
            }
        }
    }

    public string To
    {
        get => _to;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value != _to)
            {
                _to = value;
                EditedBox();
            }
        }
    }

    public bool FromIsInvalid => _range.FromIsInvalid;

    public bool ToIsInvalid => _range.ToIsInvalid;

    public IReadOnlyList<LevelChoice> Levels => LevelChoices;

    public LogEventLevel MinimumLevel
    {
        get => _minimumLevel;
        set
        {
            _minimumLevel = value;
            Refilter();
        }
    }

    /// <summary>The components of the loaded entries in name order, each with its check box.</summary>
    public IReadOnlyList<ComponentChoice> Components => _components;

    public bool AllComponentsChecked
    {
        get => _components.All(choice => choice.IsChecked);
        set
        {
            foreach (var choice in _components)
            {
                choice.Set(value);
            }

            Refilter();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _searchText = value;
            Refilter();
        }
    }

    /// <summary>Reads the log again, keeping the filters. Asked again while it reads, it waits for that read.</summary>
    public Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_running is { } running)
        {
            return running;
        }

        IsLoading = true;
        UpdateListMessage();
        var task = RunAsync(cancellationToken);
        _running = task.IsCompleted ? null : task;
        return task;
    }

    /// <summary>The rows as text to copy, in the order given.</summary>
    public string CopyText(IEnumerable<LogRow> rows) => string.Join(Environment.NewLine, rows.Select(row => row.CopyText));

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        var zone = _time.LocalTimeZone;
        try
        {
            var loaded = await Task.Run(async () => Prepare(await _source.ReadAsync(cancellationToken).ConfigureAwait(false), zone), cancellationToken);
            Show(loaded);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception failure)
        {
            _logger.Error(failure, "The log files could not be listed");
            Fail(failure);
        }
        finally
        {
            IsLoading = false;
            _running = null;
            UpdateListMessage();
        }
    }

    // Runs off the caller's thread: the rows and the sort are the slow part of a big log.
    private static Loaded Prepare(LogRead read, TimeZoneInfo zone)
    {
        var rows = new LogRow[read.Entries.Count];
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var at = 0; at < rows.Length; at++)
        {
            rows[at] = new LogRow(read.Entries[at], zone);
            names.Add(rows[at].Component);
        }

        Array.Sort(rows, LogRow.NewestFirst);
        return new Loaded(rows, [.. names.Order(StringComparer.OrdinalIgnoreCase)], read.UnreadableLines, read.WriteProblem);
    }

    private void Show(Loaded loaded)
    {
        var cleared = _components.Where(choice => !choice.IsChecked).Select(choice => choice.Name).ToHashSet(StringComparer.Ordinal);
        _all = loaded.Rows;
        _unreadableLines = loaded.UnreadableLines;
        _writeProblem = loaded.WriteProblem;
        _readFailure = null;
        _hasRead = true;
        _components = [.. loaded.Components.Select(name => new ComponentChoice(name, !cleared.Contains(name), Refilter))];
        if (_preset != TimePreset.Custom)
        {
            FillBoxes();
        }

        Refilter();
    }

    private void Fail(Exception failure)
    {
        _all = [];
        _unreadableLines = 0;
        _writeProblem = null;
        _readFailure = failure.Message;
        Refilter();
    }

    // A preset fills the boxes from the time now; All and the others without a start empty them.
    private void FillBoxes()
    {
        _from = TimePresets.Start(_preset, _time.GetUtcNow(), _time.LocalTimeZone);
        _to = "";
        _range = TimeRange.Of(_from, _to, _time.LocalTimeZone);
    }

    private void EditedBox()
    {
        _preset = TimePreset.Custom;
        Refilter();
    }

    private void Refilter()
    {
        _range = TimeRange.Of(_from, _to, _time.LocalTimeZone);
        var hidden = _components.Where(choice => !choice.IsChecked).Select(choice => choice.Name).ToHashSet(StringComparer.Ordinal);
        var rows = new List<LogRow>();
        foreach (var row in _all)
        {
            if (row.IsAtLeast(_minimumLevel)
                && _range.Contains(row.Entry.Time)
                && (hidden.Count == 0 || !hidden.Contains(row.Component))
                && (_searchText.Length == 0 || row.Contains(_searchText)))
            {
                rows.Add(row);
            }
        }

        Rows = rows;
        StatusText = Status(rows.Count);
        UpdateListMessage();
    }

    private string Status(int shown)
    {
        var parts = new List<string>
        {
            _readFailure is null ? $"Showing {shown} of {_all.Length} entries" : $"The log can't be read: {_readFailure}",
        };
        if (_unreadableLines > 0)
        {
            parts.Add(_unreadableLines == 1 ? "1 line couldn't be read" : $"{_unreadableLines} lines couldn't be read");
        }

        if (_writeProblem is not null)
        {
            parts.Add($"The log can't be written: {_writeProblem}");
        }

        return string.Join(" · ", parts);
    }

    private void UpdateListMessage() =>
        ListMessage = IsLoading ? "Loading…"
            : _readFailure is not null ? "The log couldn't be read."
            : _hasRead && _all.Length == 0 ? "No log entries yet."
            : null;

    private sealed record Loaded(LogRow[] Rows, string[] Components, int UnreadableLines, string? WriteProblem);
}
