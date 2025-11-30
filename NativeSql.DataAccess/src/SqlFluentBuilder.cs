using Microsoft.Data.SqlClient;
using System.Data;

namespace NativeSql.DataAccess;

public class SqlFluentBuilder : IDataExecutor
{
    private readonly SqlConnection _connection;
    private readonly SqlCommand _command;
    private SqlTransaction? _transaction;
    
    // Store actions to run after execution to populate user variables
    private readonly List<Action> _postExecuteCallbacks = new();

    public SqlFluentBuilder(SqlConnection connection, string sqlText)
    {
        _connection = connection;
        _command = new SqlCommand(sqlText, connection);
    }

    // --- Configuration Methods (Chainable) ---

    public SqlFluentBuilder WithParameter(string name, object? value)
    {
        // Native handling of C# nulls to SQL DBNull
        _command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return this;
    }

    public SqlFluentBuilder WithParameters(Dictionary<string, object?> parameters)
    {
        foreach (var param in parameters)
        {
            WithParameter(param.Key, param.Value);
        }
        return this;
    }

    public SqlFluentBuilder AsStoredProcedure()
    {
        _command.CommandType = CommandType.StoredProcedure;
        return this;
    }

    public SqlFluentBuilder WithTimeout(int seconds)
    {
        _command.CommandTimeout = seconds;
        return this;
    }

    public SqlFluentBuilder WithTransaction(SqlTransaction transaction)
    {
        _transaction = transaction;
        _command.Transaction = transaction;
        return this;
    }
    
    /// <summary>
    /// Adds an OUTPUT parameter and defines a callback to receive the value.
    /// </summary>
    /// <typeparam name="T">The C# type to map to (e.g., int, string)</typeparam>
    /// <param name="name">Parameter name (e.g. @Id)</param>
    /// <param name="callback">Action to run with the result (e.g. val => myId = val)</param>
    /// <param name="size">Required for String/Varchar output parameters</param>
    public SqlFluentBuilder WithOutputParameter<T>(string name, Action<T> callback, int size = -1)
    {
        var param = new SqlParameter(name, default(T))
        {
            Direction = ParameterDirection.Output,
            Size = size // Important for strings!
        };
        _command.Parameters.Add(param);

        // Register the callback to run later
        _postExecuteCallbacks.Add(() => 
        {
            if (param.Value != DBNull.Value && param.Value != null)
            {
                callback((T)Convert.ChangeType(param.Value, typeof(T)));
            }
        });

        return this;
    }

    /// <summary>
    /// Captures the "RETURN" value from a Stored Procedure (usually status codes like 0 or 1).
    /// </summary>
    public SqlFluentBuilder WithReturnValue(Action<int> callback)
    {
        var param = new SqlParameter
        {
            Direction = ParameterDirection.ReturnValue
        };
        _command.Parameters.Add(param);

        _postExecuteCallbacks.Add(() => 
        {
            if (param.Value != DBNull.Value && param.Value != null)
            {
                callback((int)param.Value);
            }
        });

        return this;
    }

    // --- Execution Methods (Terminals) ---

    // 1. Execute NonQuery (INSERT, UPDATE, DELETE)
    public async Task<int> ExecuteNonQueryAsync()
    {
        await EnsureConnectionOpenAsync();
        var result = await _command.ExecuteNonQueryAsync();
        
        // Trigger all the output callbacks now that the command is done
        TriggerCallbacks();
        
        return result;
    }

    // 2. Execute Scalar (Count, Getting IDs)
    public async Task<T?> ExecuteScalarAsync<T>()
    {
        await EnsureConnectionOpenAsync();
        var result = await _command.ExecuteScalarAsync();

        if (result == null || result == DBNull.Value)
            return default;
        
        TriggerCallbacks();
        return (T)Convert.ChangeType(result, typeof(T));
    }

    // 3. Execute List (SELECT * FROM ...)
    public async Task<IEnumerable<T>> QueryAsync<T>() where T : new()
    {
        await EnsureConnectionOpenAsync();
        var list = new List<T>();

        using var reader = await _command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(EntityMapper.Map<T>(reader));
        }
        
        reader.Close(); 
        TriggerCallbacks();

        return list;
    }

    // 4. Execute Single (SELECT TOP 1 ...)
    public async Task<T?> QuerySingleAsync<T>() where T : new()
    {
        await EnsureConnectionOpenAsync();
        
        using var reader = await _command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var result = EntityMapper.Map<T>(reader);
            reader.Close();
            TriggerCallbacks();
            return result;
        }

        return default;
    }
    
    // 5. Execute Stream (IAsyncEnumerable) - The "Conveyor Belt"
    public async IAsyncEnumerable<T> QueryStreamAsync<T>() where T : new()
    {
        await EnsureConnectionOpenAsync();

        // SequentialAccess optimizes the read for data streams
        // Note: When using SequentialAccess, you technically must read columns in the order they appear in the SQL.
        // Our EntityMapper handles this fairly well, but for maximum speed, ensure your T properties 
        // roughly match the order of columns in your SELECT statement.
        using var reader = await _command.ExecuteReaderAsync(CommandBehavior.SequentialAccess);

        while (await reader.ReadAsync())
        {
            // Yield results one by one. 
            // The database connection remains open and "busy" until this loop finishes.
            yield return EntityMapper.Map<T>(reader);
        }
        reader.Close();
        TriggerCallbacks();
    }

    // Helper to ensure connection is open before execution
    private async Task EnsureConnectionOpenAsync()
    {
        if (_connection.State == ConnectionState.Closed)
        {
            await _connection.OpenAsync();
        }
    }
    
    private void TriggerCallbacks()
    {
        foreach (var action in _postExecuteCallbacks)
        {
            action();
        }
    }
    
    public string GetCommandText()
    {
        return _command.CommandText;
    }

    public IReadOnlyDictionary<string, object?> GetParameters()
    {
        var dict = new Dictionary<string, object?>();
        foreach(SqlParameter p in _command.Parameters)
        {
            // Don't log the direction/type; just the name and the value we sent.
            if (p.Direction != ParameterDirection.ReturnValue)
            {
                dict.Add(p.ParameterName, p.Value == DBNull.Value ? null : p.Value);
            }
        }
        return dict;
    }
}
