using Microsoft.Data.SqlClient;
using System.Reflection;

namespace NativeSql.DataAccess;

internal static class EntityMapper
{
    public static T Map<T>(SqlDataReader reader) where T : new()
    {
        var item = new T();
        var properties = typeof(T).GetProperties();

        // Optimization: Cache column names in a HashSet for fast lookup
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < reader.FieldCount; i++)
        {
            columns.Add(reader.GetName(i));
        }

        foreach (var prop in properties)
        {
            if (columns.Contains(prop.Name))
            {
                var value = reader[prop.Name];
                if (value != DBNull.Value)
                {
                    prop.SetValue(item, value);
                }
            }
        }

        return item;
    }
}
