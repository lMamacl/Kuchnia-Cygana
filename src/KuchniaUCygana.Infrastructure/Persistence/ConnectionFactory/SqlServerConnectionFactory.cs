using ServiceStack.OrmLite;
using System.Data;

namespace KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

public sealed class SqlServerConnectionFactory : IDbConnectionFactory
{
    private readonly OrmLiteConnectionFactory ormLiteConnectionFactory;

    public SqlServerConnectionFactory(OrmLiteConnectionFactory ormLiteConnectionFactory)
    {
        this.ormLiteConnectionFactory = ormLiteConnectionFactory;
    }

    public IDbConnection CreateConnection() => ormLiteConnectionFactory.OpenDbConnection();
}
