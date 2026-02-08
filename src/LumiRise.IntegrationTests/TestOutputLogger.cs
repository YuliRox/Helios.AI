using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace LumiRise.IntegrationTests;

/// <summary>
/// Logger implementation that writes to xUnit's ITestOutputHelper.
/// Allows real logging output to be captured in test results.
/// </summary>
public sealed class TestOutputLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _output;

    public TestOutputLogger(ITestOutputHelper output)
    {
        _output = output;
    }

    IDisposable ILogger.BeginScope<TState>(TState state)
        => NullDisposable.Instance;

    bool ILogger.IsEnabled(LogLevel logLevel)
        => true;

    void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        _output.WriteLine($"[{logLevel}] {typeof(T).Name}: {message}");
        if (exception != null)
        {
            _output.WriteLine($"Exception: {exception}");
        }
    }

    /// <summary>
    /// Null disposable for logger scope operations.
    /// </summary>
    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose() { }
    }
}
