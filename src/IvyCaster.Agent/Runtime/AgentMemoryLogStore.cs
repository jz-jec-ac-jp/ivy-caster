using System.Collections.Concurrent;
using IvyCaster.Core;
using Microsoft.Extensions.Logging;

namespace IvyCaster.Agent.Runtime;

public sealed class AgentMemoryLogStore : IAgentRuntimeLogStore, ILoggerProvider
{
    private readonly ConcurrentQueue<AgentLogEntry> _entries = new();
    private readonly int _maxEntries;

    public AgentMemoryLogStore(int maxEntries = 1000)
    {
        _maxEntries = maxEntries;
    }

    public void Add(string level, string message)
    {
        _entries.Enqueue(new AgentLogEntry(DateTimeOffset.UtcNow, level, message));

        while (_entries.Count > _maxEntries && _entries.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyList<AgentLogEntry> GetRecent(int take)
    {
        if (take <= 0)
        {
            take = 100;
        }

        return _entries.ToArray().TakeLast(take).ToArray();
    }

    public ILogger CreateLogger(string categoryName) => new AgentMemoryLogger(categoryName, this);

    public void Dispose()
    {
    }

    private sealed class AgentMemoryLogger(string categoryName, AgentMemoryLogStore store) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            if (exception is not null)
            {
                message = $"{message} | {exception.GetType().Name}: {exception.Message}";
            }

            store.Add(logLevel.ToString(), $"[{categoryName}] {message}");
        }
    }
}
