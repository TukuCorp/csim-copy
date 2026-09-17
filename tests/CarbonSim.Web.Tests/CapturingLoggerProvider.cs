using Microsoft.Extensions.Logging;

namespace CarbonSim.Web.Tests;

/// <summary>
/// Keeps what the host logged, so a test can prove that a message which could not leave the
/// process still ended up somewhere a person can read.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<string> _messages = [];
    private readonly Lock _sync = new();

    public IReadOnlyList<string> Messages
    {
        get
        {
            lock (_sync)
            {
                return [.. _messages];
            }
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new CapturingLogger(this);
    }

    public void Dispose()
    {
    }

    private void Capture(string message)
    {
        lock (_sync)
        {
            _messages.Add(message);
        }
    }

    private sealed class CapturingLogger(CapturingLoggerProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Warning;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            provider.Capture($"{logLevel}: {formatter(state, exception)}");
        }
    }
}
