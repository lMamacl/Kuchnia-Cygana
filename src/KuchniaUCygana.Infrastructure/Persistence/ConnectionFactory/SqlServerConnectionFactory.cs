using Microsoft.Data.SqlClient;
using System.Data;

namespace KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

public sealed class SqlServerConnectionFactory : IDbConnectionFactory
{
    private readonly string connectionString;

    /// <summary>
    /// Initializes a new instance of <see cref="SqlServerConnectionFactory"/> with the specified SQL Server connection string.
    /// </summary>
    /// <param name="connectionString">The SQL Server connection string used to create <see cref="System.Data.SqlClient.SqlConnection"/> instances.</param>
    public SqlServerConnectionFactory(string connectionString)
    {
        this.connectionString = connectionString;
    }

    /// <summary>
/// Creates a new SQL Server database connection initialized with the factory's connection string.
/// </summary>
/// <returns>A new <see cref="SqlConnection"/> instance initialized with the factory's connection string, returned as an <see cref="IDbConnection"/>.</returns>
public IDbConnection CreateConnection() => new SqlConnection(connectionString);
}
