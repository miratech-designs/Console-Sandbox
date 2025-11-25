Using .net10, I want to create a custom generic data access layer using only native libraries that will connect exclusively to MSSQL data servers.
the project will eventually be turned into a nuget package that will be the starting block of web and console applications.


Build a generic, reflection-based repository that maps SQL results to C# objects automatically.
- high performance, low memory overhead, and zero third-party dependencies.
- package that rivals Dapper in features but uses zero dependencies
- Core Library: A Generic Repository class that handles CRUD operations using Microsoft.Data.SqlClient.
- Mapping: A lightweight mapping system (using Reflection) to convert SqlDataReader rows into strong types (T).
- Dependency Injection: Extensions to easily add this to the DI container for Web/API apps.
- Packaging: Instructions on how to pack this as a generic NuGet library.
- extension methods to provide fluent syntax for executing all types of sql commands.
- Implement IAsyncEnumerable<T> for performance.
- implement a "Bulk Insert" feature.
- support and handle  "Output Parameters" (e.g., getting the ID back after an Insert) within your Fluent Syntax
- add "Global Exception Logging" (wrap these executions so that if any SQL error occurs, it is automatically logged with the exact SQL query and parameters that caused it).
