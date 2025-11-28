# ErrorHandler

Lightweight global error handling helpers for console and generic host apps.

## Usage

- Register in Host/HostApplicationBuilder: `builder.Services.AddGlobalErrorHandler(opts => { ... });`
- After building the `IHost` call `host.RegisterProcessLevelHandlers()` to wire AppDomain/TaskScheduler events.
- For simple console programs you can use `await GlobalErrorHandling.RunWithGlobalHandlerAsync(host.Services, async () => { /* your main */ });`

## Design

- Use DI and `ILogger<T>`; no direct telemetry dependencies.
- Configurable options include `Rethrow`, `ExitCode`, and an `OnExceptionAsync` callback.
- Wires AppDomain/TaskScheduler events to catch non-observed exceptions.
- No hard dependency on telemetry libraries; use `OnExceptionAsync` callback to plug telemetry.

## Reporting to OpenTelemetry & Serilog

The library intentionally avoids pulling in telemetry dependencies. Use the `OnExceptionAsync` callback to report exceptions to your telemetry and logging stacks. Below is a copy-paste example showing how to configure Serilog and OpenTelemetry in a host/console app and report unhandled exceptions from `OnExceptionAsync`.

Install the packages you need (example):

```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Exporter.Console   # or use OTLP/Jaeger/Zipkin exporters
```
# ErrorHandler

Lightweight global error handling helpers for console and generic host apps.

## Usage

- Register in Host/HostApplicationBuilder: `builder.Services.AddGlobalErrorHandler(opts => { ... });`
- After building the `IHost` call `host.RegisterProcessLevelHandlers()` to wire AppDomain/TaskScheduler events.
- For simple console programs you can use `await GlobalErrorHandling.RunWithGlobalHandlerAsync(host.Services, async () => { /* your main */ });`

## Design

- Use DI and `ILogger<T>`; no direct telemetry dependencies.
- Configurable options include `Rethrow`, `ExitCode`, and an `OnExceptionAsync` callback.
- Wires AppDomain/TaskScheduler events to catch non-observed exceptions.
- No hard dependency on telemetry libraries; use `OnExceptionAsync` callback to plug telemetry.

## Reporting to OpenTelemetry & Serilog

The library intentionally avoids pulling in telemetry dependencies. Use the `OnExceptionAsync` callback to report exceptions to your telemetry and logging stacks. Below is a copy-paste example showing how to configure Serilog and OpenTelemetry in a host/console app and report unhandled exceptions from `OnExceptionAsync`.

Install the packages you need (example):

```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Extensions.Hosting
dotnet add package OpenTelemetry.Exporter.Console   # or use OTLP/Jaeger/Zipkin exporters
```

Example wiring in `Program.cs`:

```csharp
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ErrorHandler;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
	.Enrich.FromLogContext()
	.WriteTo.Console()
	.CreateLogger();

var services = new ServiceCollection();
services.AddLogging(builder => builder.AddSerilog());

// Basic OpenTelemetry Tracing setup (adjust exporters/instrumentation as needed)
services.AddOpenTelemetryTracing(builder =>
{
	builder
		.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MyService"))
		.AddAspNetCoreInstrumentation()
		.AddConsoleExporter();
});

// Register the global error handler and capture exceptions
services.AddGlobalErrorHandler(opts =>
{
	opts.ExitCode = 1;
	opts.OnExceptionAsync = async (ex, ct) =>
	{
		// 1) Log with Serilog (or any ILogger provider)
		Log.Fatal(ex, "Unhandled exception captured by GlobalErrorHandler");

		// 2) Record exception on an OpenTelemetry activity
		//    Create an ActivitySource instance in your app and reuse it; this is illustrative.
		using var activitySource = new ActivitySource("MyCompany.MyProduct.ErrorHandler");
		using var activity = activitySource.StartActivity("UnhandledException", ActivityKind.Internal);
		if (activity != null)
		{
			activity.SetTag("exception.type", ex.GetType().FullName);
			activity.SetTag("exception.message", ex.Message);
			activity.SetStatus(ActivityStatusCode.Error);
			activity.RecordException(ex);
		}

		await Task.CompletedTask;
	};
});

using var provider = services.BuildServiceProvider();

// Run your app logic via the helper so any thrown exceptions are routed through the handler
await GlobalErrorHandling.RunWithGlobalHandlerAsync(provider, async () =>
{
	// Your app logic here
	throw new InvalidOperationException("demo exception");
});
```

## Advanced: Non-toplevel console example

If your application doesn't use C# top-level statements (for example you have an explicit `Main` or run a long-lived background process), you can wire the global handler manually and still capture process-level exceptions. This example shows how to:

- build a `ServiceProvider` manually,
- register the `IGlobalErrorHandler`,
- wire `AppDomain.CurrentDomain.UnhandledException` and `TaskScheduler.UnobservedTaskException`, and
- ensure handlers are removed and resources disposed on shutdown.

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ErrorHandler;

class Program
{
	static int Main(string[] args)
	{
		var services = new ServiceCollection();
		services.AddLogging(builder => builder.AddSimpleConsole());

		services.AddGlobalErrorHandler(opts =>
		{
			opts.ExitCode = 1;
			opts.Rethrow = false; // keep process alive for graceful shutdown
			opts.OnExceptionAsync = async (ex, ct) =>
			{
				// additional app-level telemetry/reporting
				await Task.CompletedTask;
			};
		});

		using var provider = services.BuildServiceProvider();

		var handler = provider.GetRequiredService<IGlobalErrorHandler>();

		// Wire process-level handlers manually (non-host scenario)
		UnhandledExceptionEventHandler domainHandler = (s, e) =>
		{
			if (e.ExceptionObject is Exception ex)
			{
				handler.HandleExceptionAsync(ex).GetAwaiter().GetResult();
			}
		};

		EventHandler<System.Threading.Tasks.UnobservedTaskExceptionEventArgs> taskHandler = (s, e) =>
		{
			e.SetObserved();
			handler.HandleExceptionAsync(e.Exception).GetAwaiter().GetResult();
		};

		AppDomain.CurrentDomain.UnhandledException += domainHandler;
		TaskScheduler.UnobservedTaskException += taskHandler;

		try
		{
			// Call into the main app logic (could be async but we synchronously wait here for clarity)
			RunAsync(provider).GetAwaiter().GetResult();
			return 0;
		}
		catch (Exception ex)
		{
			// Ensure exceptions during startup are handled
			handler.HandleExceptionAsync(ex).GetAwaiter().GetResult();
			return 1;
		}
		finally
		{
			// Unhook handlers and dispose provider
			try { AppDomain.CurrentDomain.UnhandledException -= domainHandler; } catch { }
			try { TaskScheduler.UnobservedTaskException -= taskHandler; } catch { }
		}
	}

	static async Task RunAsync(IServiceProvider services)
	{
		// Example application code that may throw
		await Task.Delay(100);
		throw new InvalidOperationException("example failure");
	}
}
```

Notes:

- Use `GlobalErrorHandling.RunWithGlobalHandlerAsync` when possible — it's a simple wrapper that handles try/catch semantics for you.
- The manual wiring above is useful when you cannot use top-level statements or when you need explicit control over when handlers are registered and removed.
- Always remove handlers and dispose DI containers to avoid leaks, especially in long-running processes.

## Notes

- Use a single shared `ActivitySource` for your app rather than creating one each time.
- Replace the `ConsoleExporter` with OTLP/Jaeger/Zipkin exporters in production.
- Serilog's sinks can be used to forward logs to structured logging backends; OpenTelemetry can provide traces and exception events.
