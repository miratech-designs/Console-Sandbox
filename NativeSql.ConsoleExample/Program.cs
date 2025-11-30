using NativeSql.DataAccess;
using NativeSql.ConsoleExample;
using Microsoft.Data.SqlClient;

// Manual instantiation
var connString = "Server=.;Database=MyDb;Trusted_Connection=True;TrustServerCertificate=True;";
var connStringWrapper = new ConnectionStringWrapper(connString);
var userRepo = new UserRepository(connStringWrapper);

Console.WriteLine("Fetching users...");
// This will fail as there is no database.
// var users = await userRepo.GetUsersAsync();

// foreach(var u in users)
// {
//     Console.WriteLine($"{u.FirstName} {u.LastName}");
// }


// Fluent examples
using var conn = new SqlConnection(connString);

// Fluent Insert
Console.WriteLine("Running fluent insert example...");
// await conn.Sql("INSERT INTO Users (FirstName, LastName) VALUES (@fn, @ln)")
//           .WithParameter("@fn", "John")
//           .WithParameter("@ln", "Doe")
//           .ExecuteAsync();
Console.WriteLine("Fluent insert example completed.");

// Reading a List (Select)
Console.WriteLine("Running fluent select list example...");
// IEnumerable<User> users = await conn.Sql("SELECT * FROM Users WHERE CreatedDate > @date")
//                                     .WithParameter("@date", DateTime.Now.AddDays(-30))
//                                     .QueryAsync<User>();
Console.WriteLine("Fluent select list example completed.");

// Reading a Single Value (Scalar)
Console.WriteLine("Running fluent select scalar example...");
// int count = await conn.Sql("SELECT COUNT(*) FROM Users")
//                       .ExecuteScalarAsync<int>();
Console.WriteLine("Fluent select scalar example completed.");

// Using Stored Procedures
Console.WriteLine("Running stored procedure example...");
// var user = await conn.Sql("sp_GetUserById")
//                      .AsStoredProcedure()
//                      .WithParameter("@Id", 5)
//                      .QuerySingleAsync<User>();
Console.WriteLine("Stored procedure example completed.");


// Using Transactions
Console.WriteLine("Running transaction example...");
// await conn.OpenAsync();
// using var transaction = conn.BeginTransaction();
// 
// try 
// {
//     await conn.Sql("INSERT INTO Logs (Message) VALUES (@msg)")
//               .WithTransaction(transaction)
//               .WithParameter("@msg", "Step 1 Complete")
//               .ExecuteAsync();
// 
//     await conn.Sql("DELETE FROM TempTable")
//               .WithTransaction(transaction)
//               .ExecuteAsync();
// 
//     transaction.Commit();
// }
// catch
// {
//     transaction.Rollback();
// }
Console.WriteLine("Transaction example completed.");

// Streaming example
Console.WriteLine("Running streaming example...");
// var builder = conn.Sql("SELECT * FROM LargeTransactionTable");
// var dataStream = builder.QueryStreamAsync<Transaction>();
// 
// using var writer = new StreamWriter("output.csv");
// 
// await foreach (var t in dataStream)
// {
//     var line = $"{t.Id},{t.Amount},{t.Date}";
//     await writer.WriteLineAsync(line);
// }
Console.WriteLine("Streaming example completed.");


// Bulk insert example
Console.WriteLine("Running bulk insert example...");
var newUsers = new List<User>();
for(int i = 0; i < 10000; i++)
{
    newUsers.Add(new User 
    { 
        FirstName = $"User{i}", 
        LastName = "Test", 
        CreatedDate = DateTime.Now 
    });
}
// await conn.BulkInsertAsync(newUsers, "Users");
Console.WriteLine("Bulk insert example completed.");


// Output parameter example
Console.WriteLine("Running output parameter example...");
// int newUserId = 0;
// await conn.Sql("INSERT INTO Users (Name) VALUES (@n); SET @newId = SCOPE_IDENTITY();")
//           .WithParameter("@n", "Alice")
//           .WithOutputParameter<int>("@newId", val => newUserId = val)
//           .ExecuteAsync();
// Console.WriteLine($"Created User with ID: {newUserId}");
Console.WriteLine("Output parameter example completed.");

public class Transaction
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
}
