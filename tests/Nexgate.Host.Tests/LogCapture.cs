using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Nexgate.Host.Tests;

public sealed record CapturedLog(string Category, LogLevel Level, string Message, IReadOnlyDictionary<string, string> Scope)
{
    /// <summary>Todo el texto que quedó registrado: mensaje y valores de los scopes.</summary>
    public string AllText => Message + " " + string.Join(" ", Scope.Select(kv => $"{kv.Key}={kv.Value}"));
}

/// <summary>Proveedor de logs de prueba: guarda cada entrada junto con los scopes activos al registrarla.</summary>
public sealed class LogCapture : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider? _scopes;

    public ConcurrentQueue<CapturedLog> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public void Dispose()
    {
    }

    private sealed class CaptureLogger(string category, LogCapture owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => owner._scopes?.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var scope = new Dictionary<string, string>();
            owner._scopes?.ForEachScope((s, dict) =>
            {
                if (s is IEnumerable<KeyValuePair<string, object>> pairs)
                {
                    foreach (var (key, value) in pairs)
                    {
                        dict[key] = value?.ToString() ?? string.Empty;
                    }
                }
            }, scope);

            owner.Entries.Enqueue(new CapturedLog(category, logLevel, formatter(state, exception), scope));
        }
    }
}
