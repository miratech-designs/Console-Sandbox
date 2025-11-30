namespace NativeSql.DataAccess;

public interface IDataExecutor
{
    // Execution methods matching your current terminal methods
    Task<int> ExecuteNonQueryAsync();
    Task<T?> ExecuteScalarAsync<T>();
    Task<IEnumerable<T>> QueryAsync<T>() where T : new();
    IAsyncEnumerable<T> QueryStreamAsync<T>() where T : new();
    
    // Add methods to retrieve command details for logging
    string GetCommandText();
    IReadOnlyDictionary<string, object?> GetParameters();
}
