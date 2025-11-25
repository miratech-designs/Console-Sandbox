Using .net10, I want to create a custom generic data access layer using only native libraries that will connect exclusively to MSSQL data servers.
the project will eventually be turned into a nuget package that will be the starting block of web and console applications.

looking to build a foundational, "bare-metal" library for .NET 10. Using .NET 10 (the latest Long Term Support release) with only native libraries (Microsoft.Data.SqlClient) is an excellent way to ensure high performance, low memory overhead, and zero third-party dependencies (like Entity Framework or Dapper).

Build a generic, reflection-based repository that maps SQL results to C# objects automatically.
- Core Library: A Generic Repository class that handles CRUD operations using Microsoft.Data.SqlClient.
- Mapping: A lightweight mapping system (using Reflection) to convert SqlDataReader rows into strong types (T).
- Dependency Injection: Extensions to easily add this to the DI container for Web/API apps.
- Packaging: Instructions on how to pack this as a generic NuGet library.
- extension methods to provide fluent syntax for executing all types of sql commands
- Implement IAsyncEnumerable<T> for performance
- implement a "Bulk Insert" feature next
- package that rivals Dapper in features but uses zero dependencies
- support and handle  "Output Parameters" (e.g., getting the ID back after an Insert) within your Fluent Syntax
- add "Global Exception Logging" (wrap these executions so that if any SQL error occurs, it is automatically logged with the exact SQL query and parameters that caused it).
