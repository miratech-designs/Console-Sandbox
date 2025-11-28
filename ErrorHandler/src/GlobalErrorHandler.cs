using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorHandler;

public class GlobalErrorHandler : IGlobalErrorHandler, IDisposable
{
    private readonly ILogger<GlobalErrorHandler> _logger;
    private readonly GlobalErrorHandlerOptions _options;

    public GlobalErrorHandler(ILogger<GlobalErrorHandler> logger, GlobalErrorHandlerOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? new GlobalErrorHandlerOptions();
    }

    public async Task HandleExceptionAsync(Exception exception, CancellationToken cancellationToken = default)
    {
        if (exception == null) return;

        try
        {
            _logger.LogCritical(exception, "Unhandled exception caught by GlobalErrorHandler");

            if (_options.OnExceptionAsync != null)
            {
                await _options.OnExceptionAsync(exception, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            // Swallow any errors raised while handling the original exception, but log them.
            try
            {
                _logger.LogError(ex, "Error while handling an unhandled exception");
            }
            catch { }
        }

        if (_options.ExitCode.HasValue)
        {
            try
            {
                Environment.Exit(_options.ExitCode.Value);
            }
            catch { }
        }

        if (_options.Rethrow)
        {
            throw exception;
        }
    }

    public void Dispose()
    {
        // Nothing to dispose for now - placeholder for future telemetry clients
    }
}
