using Microsoft.Data.SqlClient;
using System.Data;

namespace NativeSql.DataAccess;

public static partial class DbExtensions
{
    /// <summary>
    /// Starts a Fluent SQL Command
    /// </summary>
    public static SqlFluentBuilder Sql(this SqlConnection connection, string sqlText)
    {
        return new SqlFluentBuilder(connection, sqlText);
    }

    /// <summary>
    /// Performs a high-performance Bulk Insert.
    /// </summary>
    /// <param name="transaction">Optional: Pass an existing transaction if this is part of a larger unit of work.</param>
    public static async Task BulkInsertAsync<T>(
        this SqlConnection connection, 
        IEnumerable<T> data, 
        string destinationTableName, 
        SqlTransaction? transaction = null)
    {
        // 1. Convert data to format SQL understands
        // Note: For lists > 100k items, implementing IDataReader is better than DataTable (memory-wise),
        // but DataTable is perfect for batches of 1k-50k.
        using var dataTable = DataHelper.ToDataTable(data);

        // 2. Configure Bulk Copy
        // We pass the transaction (if any) into the constructor options
        var options = SqlBulkCopyOptions.CheckConstraints | SqlBulkCopyOptions.FireTriggers;
        
        using var bulkCopy = new SqlBulkCopy(connection, options, transaction);
        
        bulkCopy.DestinationTableName = destinationTableName;
        
        // Optional: Tuning settings
        bulkCopy.BatchSize = 5000; // Send to server in chunks of 5000
        bulkCopy.BulkCopyTimeout = 600; // 10 minutes

        // 3. Map Columns explicitly (Property Name -> Column Name)
        // This ensures that if your Class properties are in a different order than SQL columns, it still works.
        foreach (DataColumn column in dataTable.Columns)
        {
            bulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
        }

        // 4. Write to Server
        if (connection.State != ConnectionState.Open) await connection.OpenAsync();
        
        await bulkCopy.WriteToServerAsync(dataTable);
    }
}
