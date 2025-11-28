using System;
using System.Threading;
using System.Threading.Tasks;

namespace ErrorHandler;

public interface IGlobalErrorHandler
{
    Task HandleExceptionAsync(Exception exception, CancellationToken cancellationToken = default);
}
