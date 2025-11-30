using Microsoft.Data.SqlClient;
using System.Data;
using System.Reflection;

namespace NativeSql.DataAccess;

public abstract class BaseRepository<T> where T : new()
{
    private readonly string _connectionString;

    protected BaseRepository(string connectionString)
    {
        _connectionString = connectionString;
    }
    
    protected BaseRepository(ConnectionStringWrapper connectionStringWrapper)
    {
        _connectionString = connectionStringWrapper.Value;
    }

    // Helper to get an open connection
    protected async Task<SqlConnection> CreateConnectionAsync()
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    // Generic Get All
    public async Task<IEnumerable<T>> GetAllAsync(string tableName)
    {
        var list = new List<T>();

        using var connection = await CreateConnectionAsync();
        using var command = new SqlCommand($"SELECT * FROM [{tableName}]", connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            list.Add(MapReaderToObject(reader));
        }

        return list;
    }

    // Generic Get By Id
    public async Task<T?> GetByIdAsync(string tableName, string pkColumnName, object id)
    {
        using var connection = await CreateConnectionAsync();
        // Parameterized query to prevent SQL Injection
        var query = $"SELECT TOP 1 * FROM [{tableName}] WHERE [{pkColumnName}] = @Id";
        
        using var command = new SqlCommand(query, connection);
        command.Parameters.AddWithValue("@Id", id);

        using var reader = await command.ExecuteReaderAsync();
        
        if (await reader.ReadAsync())
        {
            return MapReaderToObject(reader);
        }

        return default;
    }

    // Generic Execute (Insert/Update/Delete)
    protected async Task<int> ExecuteNonQueryAsync(string sql, Dictionary<string, object> parameters)
    {
        using var connection = await CreateConnectionAsync();
        using var command = new SqlCommand(sql, connection);

        foreach (var param in parameters)
        {
            // Handle nulls gracefully for SQL
            command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
        }

        return await command.ExecuteNonQueryAsync();
    }

    // --- The "Magic" Mapper (Native Reflection) ---
    // Maps a SqlDataReader row to a generic Object T
    private T MapReaderToObject(SqlDataReader reader)
    {
        var item = new T();
        var properties = typeof(T).GetProperties();

        foreach (var prop in properties)
        {
            // Only map if the column exists in the result set
            if (!ColumnExists(reader, prop.Name)) continue;

            var value = reader[prop.Name];

            if (value != DBNull.Value)
            {
                prop.SetValue(item, value);
            }
        }

        return item;
    }

    // Helper to check if column exists in reader
    private bool ColumnExists(SqlDataReader reader, string columnName)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
