using Microsoft.Extensions.Logging;
using Xunit.Sdk;

namespace LumiRise.IntegrationTests;

/// <summary>
/// Logger that writes all output to <see cref="ITestOutputHelper"/> and throws
/// <see cref="XunitException"/> when a message at <see cref="LogLevel.Error"/>
/// or <see cref="LogLevel.Critical"/> is logged, causing the current test to fail.
/// Use <see cref="AllowErrors"/> to suppress the throw in tests that expect errors.
/// </summary>
public sealed class ErrorFailingLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _output;
    private bool _errorsAllowed;

    public ErrorFailingLogger(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Returns a scope that suppresses error-level throwing.
    /// Dispose the returned value to re-enable strict mode.
    /// </summary>
    public IDisposable AllowErrors()
    {
        _errorsAllowed = true;
        return new AllowErrorsScope(this);
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => NullDisposable.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        var formatted = $"[{logLevel}] {typeof(T).Name}: {message}";

        _output.WriteLine(formatted);
        if (exception != null)
            _output.WriteLine($"Exception: {exception}");

        if (logLevel >= LogLevel.Error && !_errorsAllowed)
        {
            var detail = exception != null
                ? $"{formatted}\nException: {exception}"
                : formatted;
            throw new XunitException($"Unexpected {logLevel} log during test:\n{detail}");
        }
    }

    private sealed class AllowErrorsScope(ErrorFailingLogger<T> logger) : IDisposable
    {
        public void Dispose() => logger._errorsAllowed = false;
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose() { }
    }
}
