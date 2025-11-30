using NativeSql.DataAccess;

namespace NativeSql.ConsoleExample;

public class UserRepository : BaseRepository<User>
{
    public UserRepository(string connectionString) : base(connectionString) { }
    public UserRepository(ConnectionStringWrapper connectionString) : base(connectionString) { }

    public async Task<IEnumerable<User>> GetUsersAsync() 
    {
        return await GetAllAsync("Users");
    }

    public async Task<User?> GetUserByIdAsync(int id)
    {
        return await GetByIdAsync("Users", "Id", id);
    }

    public async Task AddUserAsync(User user)
    {
        var sql = "INSERT INTO Users (FirstName, LastName, CreatedDate) VALUES (@Fn, @Ln, @Dt)";
        var parameters = new Dictionary<string, object>
        {
            { "@Fn", user.FirstName },
            { "@Ln", user.LastName },
            { "@Dt", user.CreatedDate }
        };

        await ExecuteNonQueryAsync(sql, parameters);
    }
}
