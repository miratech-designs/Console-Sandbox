using Microsoft.Extensions.Logging;
using System.Text.Json; // Native library for easy object-to-string conversion

namespace NativeSql.DataAccess;

public class LoggingDataExecutor : IDataExecutor
{
    private readonly IDataExecutor _innerExecutor;
    private readonly ILogger<LoggingDataExecutor> _logger;
    
    // The decorator takes the core builder (IDataExecutor) and the logger
    public LoggingDataExecutor(IDataExecutor innerExecutor, ILogger<LoggingDataExecutor> logger)
    {
        _innerExecutor = innerExecutor;
        _logger = logger;
    }

    // --- Decorator Execution Methods ---

    public async Task<int> ExecuteNonQueryAsync() =>
        await ExecuteAndLog(async () => await _innerExecutor.ExecuteNonQueryAsync());

    public async Task<T?> ExecuteScalarAsync<T>() =>
        await ExecuteAndLog(async () => await _innerExecutor.ExecuteScalarAsync<T>());

    public async Task<IEnumerable<T>> QueryAsync<T>() where T : new() =>
        await ExecuteAndLog(async () => await _innerExecutor.QueryAsync<T>());

    // Note: Streaming needs special handling to yield control to the inner stream
    public IAsyncEnumerable<T> QueryStreamAsync<T>() where T : new()
    {
        // For streaming, we can't use a simple try/catch around the whole method; 
        // we wrap the inner enumerable for robust exception handling during iteration.
        return ExecuteAndLogStream<T>(() => _innerExecutor.QueryStreamAsync<T>());
    }

    // --- Core Logging Logic ---

    private async Task<T> ExecuteAndLog<T>(Func<Task<T>> func)
    {
        try
        {
            return await func();
        }
        catch (Exception ex)
        {
            LogExecutionError(ex);
            throw; // Rethrow the exception after logging
        }
    }

    private IAsyncEnumerable<T> ExecuteAndLogStream<T>(Func<IAsyncEnumerable<T>> func)
    {
        // Wrap the source IAsyncEnumerable with a custom enumerator that can catch
        // exceptions during MoveNextAsync without using 'yield' inside a try/catch
        return new LoggingAsyncEnumerable<T>(func(), LogExecutionError);
    }

    private sealed class LoggingAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        private readonly IAsyncEnumerable<T> _source;
        private readonly Action<Exception> _onError;

        public LoggingAsyncEnumerable(IAsyncEnumerable<T> source, Action<Exception> onError)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _onError = onError ?? throw new ArgumentNullException(nameof(onError));
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new LoggingAsyncEnumerator(_source.GetAsyncEnumerator(cancellationToken), _onError);
        }

        private sealed class LoggingAsyncEnumerator : IAsyncEnumerator<T>
        {
            private readonly IAsyncEnumerator<T> _inner;
            private readonly Action<Exception> _onError;

            public LoggingAsyncEnumerator(IAsyncEnumerator<T> inner, Action<Exception> onError)
            {
                _inner = inner ?? throw new ArgumentNullException(nameof(inner));
                _onError = onError ?? throw new ArgumentNullException(nameof(onError));
            }

            public T Current => _inner.Current;

            public async ValueTask<bool> MoveNextAsync()
            {
                try
                {
                    return await _inner.MoveNextAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _onError(ex);
                    throw;
                }
            }

            public async ValueTask DisposeAsync()
            {
                try
                {
                    await _inner.DisposeAsync().ConfigureAwait(false);
                }
                catch { }
            }
        }
    }

    private void LogExecutionError(Exception ex)
    {
        var sql = _innerExecutor.GetCommandText();
        var parameters = _innerExecutor.GetParameters();

        // Convert parameters to a readable JSON string using the native System.Text.Json
        var paramJson = JsonSerializer.Serialize(parameters);

        _logger.LogError(
            ex, 
            "SQL Execution Failed. Command: {SqlText}. Parameters: {Parameters}",
            sql, 
            paramJson);
    }
    
    // Required by the interface, but not used by the Decorator directly.
    public string GetCommandText() => _innerExecutor.GetCommandText();
    public IReadOnlyDictionary<string, object?> GetParameters() => _innerExecutor.GetParameters();
}
