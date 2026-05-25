using Microsoft.Data.SqlClient;
using System.Data;

namespace KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

public sealed class SqlServerConnectionFactory : IDbConnectionFactory
{
    private readonly string connectionString;

    public SqlServerConnectionFactory(string connectionString)
    {
        this.connectionString = connectionString;
    }

    public IDbConnection CreateConnection() => new SqlConnection(connectionString);
}
