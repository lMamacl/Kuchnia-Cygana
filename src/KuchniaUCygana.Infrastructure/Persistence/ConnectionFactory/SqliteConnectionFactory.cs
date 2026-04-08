using Microsoft.Extensions.Configuration;
using ServiceStack.OrmLite;
using ServiceStack.OrmLite.Sqlite;
using System.Data;

namespace KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

public sealed class SqliteConnectionFactory : IDbConnectionFactory
{
    private readonly string connectionString;

    public SqliteConnectionFactory(IConfiguration configuration)
    {
        connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    public IDbConnection CreateConnection()
    {
        var ormFactory = new OrmLiteConnectionFactory(connectionString, SqliteDialect.Provider);
        return ormFactory.OpenDbConnection();
    }
}
