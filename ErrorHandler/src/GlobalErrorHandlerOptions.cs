using System;

namespace ErrorHandler;

public class GlobalErrorHandlerOptions
{
    /// <summary>
    /// If true, the handler will rethrow exceptions after handling them.
    /// Useful for development and debugging. Default: false.
    /// </summary>
    public bool Rethrow { get; set; } = false;

    /// <summary>
    /// Exit code to use if the process should exit on an unhandled exception.
    /// Null means do not exit the process.
    /// </summary>
    public int? ExitCode { get; set; }

    /// <summary>
    /// Optional callback invoked when an unhandled exception occurs.
    /// </summary>
    public Func<Exception, System.Threading.CancellationToken, System.Threading.Tasks.Task>? OnExceptionAsync { get; set; }
}
