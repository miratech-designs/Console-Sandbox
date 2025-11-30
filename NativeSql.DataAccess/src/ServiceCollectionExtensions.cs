using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging; // Required for ILogger
using Microsoft.Data.SqlClient;

namespace NativeSql.DataAccess;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNativeSql(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton(new ConnectionStringWrapper(connectionString));
        
        services.AddTransient<IDataExecutor>(provider =>
        {
            var connStringWrapper = provider.GetRequiredService<ConnectionStringWrapper>();
            var logger = provider.GetRequiredService<ILogger<LoggingDataExecutor>>();
            
            // This factory creates a new executor for each call.
            // The SQL text is meant to be replaced by the user of the factory.
            var connection = new SqlConnection(connStringWrapper.Value);
            var builder = new SqlFluentBuilder(connection, "");
            return new LoggingDataExecutor(builder, logger);
        });

        return services;
    }
}
