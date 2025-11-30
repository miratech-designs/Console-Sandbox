using System.Data;
using System.ComponentModel;

namespace NativeSql.DataAccess;

internal static class DataHelper
{
    public static DataTable ToDataTable<T>(IEnumerable<T> data)
    {
        var table = new DataTable();
        
        // 1. Get Properties of T to create columns
        // We ignore properties that can't be mapped (like Lists or complex objects)
        var properties = TypeDescriptor.GetProperties(typeof(T));

        foreach (PropertyDescriptor prop in properties)
        {
            // Use Nullable type if necessary
            table.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
        }

        // 2. Populate Rows
        foreach (T item in data)
        {
            var row = table.NewRow();
            foreach (PropertyDescriptor prop in properties)
            {
                // Handle DBNull safely
                row[prop.Name] = prop.GetValue(item) ?? DBNull.Value;
            }
            table.Rows.Add(row);
        }

        return table;
    }
}
