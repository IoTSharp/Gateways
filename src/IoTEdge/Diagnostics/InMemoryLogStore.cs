using Microsoft.Extensions.Logging;

namespace IoTEdge.Diagnostics;

public sealed record InMemoryLogEntry(
    long Sequence,
    DateTime TimestampUtc,
    string Level,
    string Category,
    string Message,
    string? Exception);

public sealed class InMemoryLogStore
{
    private const int MaxEntries = 1000;
    private readonly object _lock = new();
    private readonly Queue<InMemoryLogEntry> _entries = new();
    private long _sequence;

    public void Add(LogLevel level, string category, string message, Exception? exception)
    {
        if (string.IsNullOrWhiteSpace(message) && exception is null)
        {
            return;
        }

        lock (_lock)
        {
            _entries.Enqueue(new InMemoryLogEntry(
                ++_sequence,
                DateTime.UtcNow,
                level.ToString(),
                category,
                message,
                exception?.ToString()));

            while (_entries.Count > MaxEntries)
            {
                _entries.Dequeue();
            }
        }
    }

    public IReadOnlyCollection<InMemoryLogEntry> GetRecent(int count, LogLevel minimumLevel)
    {
        count = Math.Clamp(count, 1, MaxEntries);
        lock (_lock)
        {
            return _entries
                .Where(entry => ParseLogLevel(entry.Level) >= minimumLevel)
                .OrderByDescending(entry => entry.Sequence)
                .Take(count)
                .OrderBy(entry => entry.Sequence)
                .ToArray();
        }
    }

    public static LogLevel ParseLogLevel(string? level)
        => Enum.TryParse<LogLevel>(level, true, out var parsed) ? parsed : LogLevel.Information;
}

public sealed class InMemoryLoggerProvider : ILoggerProvider
{
    private readonly InMemoryLogStore _store;

    public InMemoryLoggerProvider(InMemoryLogStore store)
    {
        _store = store;
    }

    public ILogger CreateLogger(string categoryName)
        => new InMemoryLogger(_store, categoryName);

    public void Dispose()
    {
    }

    private sealed class InMemoryLogger : ILogger
    {
        private readonly InMemoryLogStore _store;
        private readonly string _categoryName;

        public InMemoryLogger(InMemoryLogStore store, string categoryName)
        {
            _store = store;
            _categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
            => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel)
            => logLevel >= LogLevel.Information && logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            _store.Add(logLevel, _categoryName, formatter(state, exception), exception);
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
