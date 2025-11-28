using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ErrorHandler;

public static class GlobalErrorHandling
{
    /// <summary>
    /// Registers the global error handler service and wires process/task-level events.
    /// Call this early in your console app or Host setup.
    /// </summary>
    public static IServiceCollection AddGlobalErrorHandler(this IServiceCollection services, Action<GlobalErrorHandlerOptions>? configure = null)
    {
        var options = new GlobalErrorHandlerOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IGlobalErrorHandler, GlobalErrorHandler>();

        return services;
    }

    /// <summary>
    /// Convenience method for HostApplicationBuilder (top-level Program) to register the handler.
    /// </summary>
    public static HostApplicationBuilder UseGlobalErrorHandler(this HostApplicationBuilder builder, Action<GlobalErrorHandlerOptions>? configure = null)
    {
        builder.Services.AddGlobalErrorHandler(configure);
        return builder;
    }

    /// <summary>
    /// Wire process-level events (AppDomain/TaskScheduler) and returns a disposable that will unhook handlers when disposed.
    /// Call this after the DI container is built and the IGlobalErrorHandler is available.
    /// </summary>
    public static IDisposable RegisterProcessLevelHandlers(this IHost host)
    {
        var logger = host.Services.GetService<ILoggerFactory>()?.CreateLogger("GlobalErrorHandling") ?? throw new InvalidOperationException("ILoggerFactory not available");
        var handler = host.Services.GetService<IGlobalErrorHandler>() ?? throw new InvalidOperationException("IGlobalErrorHandler not registered");

        void HandleException(Exception ex)
        {
            try
            {
                handler.HandleExceptionAsync(ex).GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                try { logger.LogError(e, "Exception while processing unhandled exception"); } catch { }
            }
        }

        // AppDomain unhandled
        UnhandledExceptionEventHandler? domainHandler = (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                HandleException(ex);
            else
                logger.LogCritical("UnhandledExceptionEvent: non-Exception object: {Obj}", e.ExceptionObject);
        };
        AppDomain.CurrentDomain.UnhandledException += domainHandler;

        // TaskScheduler unobserved task exceptions
        EventHandler<UnobservedTaskExceptionEventArgs>? taskHandler = (s, e) =>
        {
            e.SetObserved();
            HandleException(e.Exception);
        };
        TaskScheduler.UnobservedTaskException += taskHandler;

        return new DisposableAction(() =>
        {
            try { AppDomain.CurrentDomain.UnhandledException -= domainHandler; } catch { }
            try { TaskScheduler.UnobservedTaskException -= taskHandler; } catch { }
        });
    }

    /// <summary>
    /// Simple wrapper to run an async main function with DI-provided global exception handling.
    /// Example: await GlobalErrorHandling.RunWithGlobalHandlerAsync(host.Services, async () => { ... });
    /// </summary>
    public static async Task RunWithGlobalHandlerAsync(IServiceProvider services, Func<Task> func)
    {
        var handler = services.GetService<IGlobalErrorHandler>();
        try
        {
            await func().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (handler != null)
            {
                await handler.HandleExceptionAsync(ex).ConfigureAwait(false);
            }
            else
            {
                // If no handler, rethrow to preserve behavior
                throw;
            }
        }
    }

    private sealed class DisposableAction : IDisposable
    {
        private readonly Action _cleanup;
        private bool _disposed;
        public DisposableAction(Action cleanup) => _cleanup = cleanup ?? throw new ArgumentNullException(nameof(cleanup));
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _cleanup(); } catch { }
        }
    }
}
